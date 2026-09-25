using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

/// <summary>
/// Hover information (Visual Studio's "Quick Info") for the symbol at an offset of a script: its signature, where it's
/// declared and its XML documentation. The script is analysed with the namespaces it runs with, on a background thread.
/// </summary>
public sealed class CSharpQuickInfoService
{
    // The analysed text is this prefix followed by the script, so script offsets shift by its length only.
    private const string AnalysisPrefix = RoslynCompilerService.DefaultScriptUsings + "\n";

    private static readonly CSharpParseOptions ParseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp13);

    private static readonly CSharpCompilationOptions CompilationOptions = new(
        OutputKind.ConsoleApplication,
        optimizationLevel: OptimizationLevel.Debug,
        allowUnsafe: false);

    private readonly IReadOnlyList<MetadataReference> _references;
    private readonly object _cacheLock = new();
    private (string Code, SyntaxTree Tree, SemanticModel Model)? _lastAnalysis;

    public CSharpQuickInfoService(RoslynCompilerService compilerService)
        : this(compilerService.DefaultReferences)
    {
    }

    public CSharpQuickInfoService(IEnumerable<MetadataReference> references)
    {
        _references = XmlDocumentationLookup.WithDocumentation(references);
    }

    /// <summary>The hover for the token containing <paramref name="offset"/> of <paramref name="code"/>, or null when it isn't a symbol.</summary>
    public Task<CSharpQuickInfo?> GetQuickInfoAsync(string code, int offset, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(code) || offset < 0 || offset > code.Length)
        {
            return Task.FromResult<CSharpQuickInfo?>(null);
        }

        return Task.Run(() => Compute(code, offset, ct), ct);
    }

    private CSharpQuickInfo? Compute(string code, int offset, CancellationToken ct)
    {
        var (tree, model) = Analyze(code, ct);
        var position = AnalysisPrefix.Length + offset;
        var token = tree.GetRoot(ct).FindToken(position);
        if (!token.Span.Contains(position)) return null;

        var symbol = ResolveSymbol(model, token, ct);
        if (symbol == null) return null;

        var signature = QuickInfoSignatureBuilder.Signature(symbol, RangeVariableType(model, token, symbol, ct));
        if (signature == null) return null;

        ct.ThrowIfCancellationRequested();
        return new CSharpQuickInfo
        {
            SpanStart = token.SpanStart - AnalysisPrefix.Length,
            SpanLength = token.Span.Length,
            Signature = signature,
            Container = QuickInfoSignatureBuilder.Container(symbol),
            TypeArguments = QuickInfoSignatureBuilder.TypeArguments(symbol),
            Documentation = Documentation(symbol, model.Compilation, ct)
        };
    }

    // Hovering word after word without typing analyses the same text, so the last compilation is kept.
    private (SyntaxTree Tree, SemanticModel Model) Analyze(string code, CancellationToken ct)
    {
        lock (_cacheLock)
        {
            if (_lastAnalysis is { } last && string.Equals(last.Code, code, StringComparison.Ordinal))
            {
                return (last.Tree, last.Model);
            }
        }

        var tree = CSharpSyntaxTree.ParseText(AnalysisPrefix + code, ParseOptions, cancellationToken: ct);
        var compilation = CSharpCompilation.Create("QuickInfoAnalysis", [tree], _references, CompilationOptions);
        var model = compilation.GetSemanticModel(tree);

        lock (_cacheLock)
        {
            _lastAnalysis = (code, tree, model);
        }
        return (tree, model);
    }

    private static ISymbol? ResolveSymbol(SemanticModel model, SyntaxToken token, CancellationToken ct)
    {
        var node = token.Parent;
        if (node == null) return null;

        ISymbol? symbol = token.Kind() switch
        {
            SyntaxKind.IdentifierToken => ResolveIdentifier(model, node, ct),
            // `new List<int>()` and `new()`: the constructor that runs.
            SyntaxKind.NewKeyword when node is BaseObjectCreationExpressionSyntax creation => Best(model.GetSymbolInfo(creation, ct)),
            SyntaxKind.ThisKeyword or SyntaxKind.BaseKeyword when node is ConstructorInitializerSyntax initializer => Best(model.GetSymbolInfo(initializer, ct)),
            SyntaxKind.ThisKeyword or SyntaxKind.BaseKeyword when node is ExpressionSyntax expression => model.GetTypeInfo(expression, ct).Type,
            // int, string, bool, ...
            _ when node is PredefinedTypeSyntax predefined => model.GetTypeInfo(predefined, ct).Type,
            _ => null
        };

        return symbol switch
        {
            null or IErrorTypeSymbol => null,
            IAliasSymbol alias => alias.Target,
            _ => symbol
        };
    }

    private static ISymbol? ResolveIdentifier(SemanticModel model, SyntaxNode node, CancellationToken ct)
    {
        if (node is not SimpleNameSyntax name)
        {
            // An identifier owned directly by a declaration is the name being declared: a variable, method, type,
            // parameter, foreach or pattern variable, range variable, label...
            return model.GetDeclaredSymbol(node, ct);
        }

        // `new Point(1, 2)`: like Visual Studio, show the constructor it calls rather than the type.
        if (TypeOfObjectCreation(name) is { } creation && Best(model.GetSymbolInfo(creation, ct)) is IMethodSymbol constructor)
        {
            return constructor;
        }

        return Best(model.GetSymbolInfo(name, ct)) ??
               // `var` names no symbol of its own, but its type is the one it stands for.
               model.GetTypeInfo(name, ct).Type;
    }

    private static ObjectCreationExpressionSyntax? TypeOfObjectCreation(SimpleNameSyntax name)
    {
        SyntaxNode type = name.Parent is QualifiedNameSyntax qualified && qualified.Right == name ? qualified : name;
        return type.Parent is ObjectCreationExpressionSyntax creation && creation.Type == type ? creation : null;
    }

    private static ISymbol? Best(SymbolInfo info) => info.Symbol ?? info.CandidateSymbols.FirstOrDefault();

    private static ITypeSymbol? RangeVariableType(SemanticModel model, SyntaxToken token, ISymbol symbol, CancellationToken ct)
    {
        if (symbol is not IRangeVariableSymbol) return null;

        return token.Parent switch
        {
            IdentifierNameSyntax usage => model.GetTypeInfo(usage, ct).Type,
            FromClauseSyntax { Type: { } explicitType } => model.GetTypeInfo(explicitType, ct).Type,
            FromClauseSyntax from => ElementType(model.GetTypeInfo(from.Expression, ct).Type),
            JoinClauseSyntax join => ElementType(model.GetTypeInfo(join.InExpression, ct).Type),
            LetClauseSyntax let => model.GetTypeInfo(let.Expression, ct).Type,
            _ => null
        };
    }

    // The T of an IEnumerable<T> (or the element type of an array) a query ranges over.
    private static ITypeSymbol? ElementType(ITypeSymbol? source) => source switch
    {
        IArrayTypeSymbol array => array.ElementType,
        INamedTypeSymbol named => new[] { named }.Concat(named.AllInterfaces)
            .FirstOrDefault(t => t.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
            ?.TypeArguments[0],
        _ => null
    };

    private static QuickInfoDocumentation Documentation(ISymbol symbol, Compilation compilation, CancellationToken ct)
    {
        switch (symbol)
        {
            // Parameters and type parameters are documented on the member that declares them.
            case IParameterSymbol { ContainingSymbol: { } owner } parameter:
                return Described(Parse(owner, compilation, ct)?.Parameters, parameter.Name);
            case ITypeParameterSymbol typeParameter when ((ISymbol?)typeParameter.DeclaringMethod ?? typeParameter.DeclaringType) is { } declaring:
                return Described(Parse(declaring, compilation, ct)?.TypeParameters, typeParameter.Name);
        }

        var documentation = Parse(symbol, compilation, ct);

        // A constructor nobody documented still has a documented type (a record's comment covers both).
        if ((documentation == null || documentation.IsEmpty) && symbol is IMethodSymbol { MethodKind: MethodKind.Constructor } constructor &&
            Parse(constructor.ContainingType, compilation, ct) is { Summary.Count: > 0 } typeDocumentation)
        {
            documentation = new QuickInfoDocumentation
            {
                Summary = typeDocumentation.Summary,
                Parameters = documentation is { Parameters.Count: > 0 } ? documentation.Parameters : typeDocumentation.Parameters
            };
        }

        return documentation ?? QuickInfoDocumentation.Empty;
    }

    private static QuickInfoDocumentation Described(IReadOnlyList<QuickInfoNamedSection>? sections, string name) =>
        sections?.FirstOrDefault(s => s.Name.Text == name) is { Text.Count: > 0 } section
            ? new QuickInfoDocumentation { Summary = section.Text }
            : QuickInfoDocumentation.Empty;

    private static QuickInfoDocumentation? Parse(ISymbol symbol, Compilation compilation, CancellationToken ct, int depth = 0)
    {
        // Documentation belongs to the definition: List<T>.Add, not List<int>.Add; Enumerable.Where, not its reduced form.
        var definition = (symbol is IMethodSymbol { ReducedFrom: { } unreduced } ? unreduced : symbol).OriginalDefinition;

        string? xml;
        try
        {
            xml = definition.GetDocumentationCommentXml(cancellationToken: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }

        var documentation = XmlDocumentationParser.Parse(xml, cref => ResolveCref(cref, compilation));
        if (documentation?.InheritDocCref is not { } cref || depth >= 4) return documentation;

        var inheritedFrom = InheritedFrom(definition, cref, compilation);
        return inheritedFrom != null && Parse(inheritedFrom, compilation, ct, depth + 1) is { } inherited
            ? XmlDocumentationParser.Merge(documentation, inherited)
            : documentation;
    }

    private static QuickInfoTextRun? ResolveCref(string cref, Compilation compilation)
    {
        if (cref.StartsWith("!:", StringComparison.Ordinal)) return null;
        return DocumentationCommentId.GetFirstSymbolForDeclarationId(cref, compilation) is { } symbol
            ? QuickInfoSignatureBuilder.ShortName(symbol)
            : null;
    }

    // <inheritdoc/> without a cref inherits from the overridden member, the implemented interface member or the base type.
    private static ISymbol? InheritedFrom(ISymbol symbol, string cref, Compilation compilation)
    {
        if (cref.Length > 0) return DocumentationCommentId.GetFirstSymbolForDeclarationId(cref, compilation);

        switch (symbol)
        {
            case IMethodSymbol { OverriddenMethod: { } overridden }: return overridden;
            case IPropertySymbol { OverriddenProperty: { } overridden }: return overridden;
            case IEventSymbol { OverriddenEvent: { } overridden }: return overridden;
            case INamedTypeSymbol type: return type.BaseType is { SpecialType: not SpecialType.System_Object } baseType ? baseType : type.Interfaces.FirstOrDefault();
        }

        if (symbol.ContainingType is not { } containingType) return null;
        return containingType.AllInterfaces
            .SelectMany(i => i.GetMembers(symbol.Name))
            .FirstOrDefault(member => SymbolEqualityComparer.Default.Equals(containingType.FindImplementationForInterfaceMember(member), symbol));
    }
}
