using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

/// <summary>
/// Writes a symbol's hover header as coloured runs: the declaration (<c>public static void WriteLine(string? value)</c>),
/// where it's declared (<c>in class System.Console</c>) and, for constructed generics, what each type parameter is.
/// </summary>
public static class QuickInfoSignatureBuilder
{
    private const SymbolDisplayMiscellaneousOptions CommonMiscellaneous =
        SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
        SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
        SymbolDisplayMiscellaneousOptions.AllowDefaultLiteral;

    private static readonly SymbolDisplayFormat MemberFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeVariance,
        memberOptions: SymbolDisplayMemberOptions.IncludeAccessibility | SymbolDisplayMemberOptions.IncludeModifiers |
                       SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeParameters |
                       SymbolDisplayMemberOptions.IncludeConstantValue | SymbolDisplayMemberOptions.IncludeRef |
                       SymbolDisplayMemberOptions.IncludeExplicitInterface,
        delegateStyle: SymbolDisplayDelegateStyle.NameAndSignature,
        extensionMethodStyle: SymbolDisplayExtensionMethodStyle.StaticMethod,
        parameterOptions: SymbolDisplayParameterOptions.IncludeParamsRefOut | SymbolDisplayParameterOptions.IncludeType |
                          SymbolDisplayParameterOptions.IncludeName | SymbolDisplayParameterOptions.IncludeDefaultValue |
                          SymbolDisplayParameterOptions.IncludeExtensionThis,
        propertyStyle: SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
        localOptions: SymbolDisplayLocalOptions.IncludeType | SymbolDisplayLocalOptions.IncludeConstantValue | SymbolDisplayLocalOptions.IncludeRef,
        kindOptions: SymbolDisplayKindOptions.IncludeMemberKeyword,
        miscellaneousOptions: CommonMiscellaneous | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    // "class List<T>", "readonly struct Int32", "delegate TResult Func<in T, out TResult>(T arg)".
    private static readonly SymbolDisplayFormat TypeFormat = MemberFormat
        .WithMemberOptions(SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeRef)
        .WithKindOptions(SymbolDisplayKindOptions.IncludeTypeKeyword);

    // Keeps "Int32" rather than "int" when the hovered type is itself a special type.
    private static readonly SymbolDisplayFormat SpecialTypeFormat = TypeFormat.WithMiscellaneousOptions(CommonMiscellaneous);

    // "System.Collections.Generic.List<int>", "namespace System.Text".
    private static readonly SymbolDisplayFormat QualifiedFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeContainingType,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType,
        kindOptions: SymbolDisplayKindOptions.IncludeNamespaceKeyword,
        miscellaneousOptions: CommonMiscellaneous | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static readonly SymbolDisplayFormat ShortTypeFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: CommonMiscellaneous | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    /// <summary>The declaration line for <paramref name="symbol"/>, or null when there's nothing worth showing.</summary>
    /// <param name="symbol">The hovered symbol.</param>
    /// <param name="rangeVariableType">A LINQ range variable's element type, which the symbol itself doesn't carry.</param>
    public static IReadOnlyList<QuickInfoTextRun>? Signature(ISymbol symbol, ITypeSymbol? rangeVariableType = null)
    {
        var runs = new List<QuickInfoTextRun>();
        switch (symbol)
        {
            case ILocalSymbol local:
                AddLabel(runs, local.IsConst ? "(local constant) " : "(local variable) ");
                AddParts(runs, local.ToDisplayParts(MemberFormat));
                break;
            case IParameterSymbol parameter:
                AddLabel(runs, "(parameter) ");
                AddParts(runs, parameter.ToDisplayParts(MemberFormat));
                break;
            case IRangeVariableSymbol rangeVariable:
                AddLabel(runs, "(range variable) ");
                if (rangeVariableType is { TypeKind: not TypeKind.Error })
                {
                    AddParts(runs, rangeVariableType.ToDisplayParts(MemberFormat));
                    Add(runs, new QuickInfoTextRun(" ", QuickInfoTextKind.Text));
                }
                Add(runs, new QuickInfoTextRun(rangeVariable.Name, QuickInfoTextKind.Variable));
                break;
            case ILabelSymbol label:
                AddLabel(runs, "(label) ");
                runs.Add(new QuickInfoTextRun(label.Name, QuickInfoTextKind.Variable));
                break;
            case IDiscardSymbol discard:
                AddLabel(runs, "(discard) ");
                AddParts(runs, discard.Type.ToDisplayParts(MemberFormat));
                runs.Add(new QuickInfoTextRun(" _", QuickInfoTextKind.Variable));
                break;
            case ITypeParameterSymbol typeParameter:
                AddLabel(runs, "(type parameter) ");
                AddParts(runs, typeParameter.ToDisplayParts(MemberFormat));
                break;
            case INamespaceSymbol { IsGlobalNamespace: false } ns:
                AddParts(runs, ns.ToDisplayParts(QualifiedFormat));
                break;
            case INamedTypeSymbol { TypeKind: not TypeKind.Error } type:
                AddTypeHeader(runs, type.OriginalDefinition);
                break;
            case ITypeSymbol { TypeKind: not TypeKind.Error } type:
                // Arrays, pointers and dynamic: `var` can stand for any of them.
                AddParts(runs, type.ToDisplayParts(MemberFormat));
                break;
            case IMethodSymbol { MethodKind: MethodKind.LocalFunction } localFunction:
                AddLabel(runs, "(local function) ");
                AddParts(runs, localFunction.ToDisplayParts(MemberFormat));
                break;
            case IMethodSymbol method:
                if (method.ReducedFrom != null) AddLabel(runs, "(extension) ");
                AddParts(runs, method.ToDisplayParts(MemberFormat));
                var overloads = OverloadCount(method);
                if (overloads > 0) AddLabel(runs, overloads == 1 ? " (+ 1 overload)" : $" (+ {overloads} overloads)");
                break;
            case IPropertySymbol or IFieldSymbol or IEventSymbol:
                AddParts(runs, symbol.ToDisplayParts(MemberFormat));
                break;
            default:
                return null;
        }
        return runs.Count > 0 ? runs : null;
    }

    /// <summary><c>in class System.Console</c>; empty for locals, script-level types and anonymous or tuple types.</summary>
    public static IReadOnlyList<QuickInfoTextRun> Container(ISymbol symbol)
    {
        ISymbol? container = symbol switch
        {
            ILocalSymbol or IParameterSymbol or IRangeVariableSymbol or ILabelSymbol or IDiscardSymbol or INamespaceSymbol => null,
            IMethodSymbol { MethodKind: MethodKind.LocalFunction or MethodKind.AnonymousFunction } => null,
            ITypeParameterSymbol typeParameter => (ISymbol?)typeParameter.DeclaringMethod ?? typeParameter.DeclaringType,
            _ => (ISymbol?)symbol.ContainingType ?? symbol.ContainingNamespace
        };

        var runs = new List<QuickInfoTextRun>();
        switch (container)
        {
            case INamedTypeSymbol type when !type.IsAnonymousType && !type.IsTupleType && !type.IsImplicitlyDeclared:
                AddLabel(runs, $"in {KindKeyword(type)} ");
                AddParts(runs, type.ToDisplayParts(QualifiedFormat));
                break;
            case INamespaceSymbol { IsGlobalNamespace: false } ns:
                AddLabel(runs, "in namespace ");
                AddParts(runs, ns.ToDisplayParts(QualifiedFormat.WithKindOptions(SymbolDisplayKindOptions.None)));
                break;
            case IMethodSymbol method:
                AddLabel(runs, "in method ");
                AddParts(runs, method.ToDisplayParts(QualifiedFormat));
                break;
        }
        return runs;
    }

    /// <summary>One <c>T is int</c> line per type argument when <paramref name="symbol"/> is a constructed generic type.</summary>
    public static IReadOnlyList<IReadOnlyList<QuickInfoTextRun>> TypeArguments(ISymbol symbol)
    {
        if (symbol is not INamedTypeSymbol { IsGenericType: true } type || SymbolEqualityComparer.Default.Equals(type, type.OriginalDefinition))
        {
            return [];
        }

        var lines = new List<IReadOnlyList<QuickInfoTextRun>>();
        var parameters = type.OriginalDefinition.TypeParameters;
        for (var i = 0; i < parameters.Length && i < type.TypeArguments.Length; i++)
        {
            if (type.TypeArguments[i] is ITypeParameterSymbol argument && argument.Name == parameters[i].Name) continue;

            var line = new List<QuickInfoTextRun> { new(parameters[i].Name, QuickInfoTextKind.TypeParameter) };
            AddLabel(line, " is ");
            AddParts(line, type.TypeArguments[i].ToDisplayParts(MemberFormat));
            lines.Add(line);
        }
        return lines;
    }

    /// <summary>
    /// How a <c>&lt;see cref&gt;</c> reads in running text: <c>List&lt;T&gt;</c> for types, <c>String.Split</c> for members,
    /// coloured like the symbol.
    /// </summary>
    public static QuickInfoTextRun ShortName(ISymbol symbol) => symbol switch
    {
        ITypeSymbol type => new QuickInfoTextRun(type.ToDisplayString(ShortTypeFormat), KindOf(type)),
        IMethodSymbol { MethodKind: MethodKind.Constructor, ContainingType: { } type } =>
            new QuickInfoTextRun(type.ToDisplayString(ShortTypeFormat), KindOf(type)),
        INamespaceSymbol ns => new QuickInfoTextRun(ns.ToDisplayString(), QuickInfoTextKind.Namespace),
        IParameterSymbol or ILocalSymbol => new QuickInfoTextRun(symbol.Name, QuickInfoTextKind.Variable),
        { ContainingType: { } type } => new QuickInfoTextRun(
            $"{type.ToDisplayString(ShortTypeFormat)}.{symbol.Name}",
            symbol is IMethodSymbol ? QuickInfoTextKind.Method : QuickInfoTextKind.Member),
        _ => new QuickInfoTextRun(symbol.Name, QuickInfoTextKind.Text)
    };

    private static void AddTypeHeader(List<QuickInfoTextRun> runs, INamedTypeSymbol type)
    {
        var accessibility = AccessibilityKeyword(type.DeclaredAccessibility);
        if (accessibility != null) AddKeyword(runs, accessibility);

        // Roslyn prints "readonly" and "ref" for structs itself, but not a class's modifiers.
        if (type.TypeKind == TypeKind.Class)
        {
            if (type.IsStatic) AddKeyword(runs, "static");
            else if (type.IsAbstract) AddKeyword(runs, "abstract");
            else if (type.IsSealed) AddKeyword(runs, "sealed");
        }

        AddParts(runs, type.ToDisplayParts(type.SpecialType == SpecialType.None ? TypeFormat : SpecialTypeFormat));
    }

    private static int OverloadCount(IMethodSymbol method)
    {
        var definition = method.ReducedFrom ?? method;
        if (definition.ContainingType is not { } type) return 0;

        var count = definition.MethodKind == MethodKind.Constructor
            ? type.InstanceConstructors.Length
            : type.GetMembers(definition.Name).OfType<IMethodSymbol>().Count(m => m.MethodKind == definition.MethodKind);
        return count - 1;
    }

    private static string KindKeyword(INamedTypeSymbol type) => type switch
    {
        { IsRecord: true, TypeKind: TypeKind.Struct } => "record struct",
        { IsRecord: true } => "record",
        { TypeKind: TypeKind.Interface } => "interface",
        { TypeKind: TypeKind.Struct } => "struct",
        { TypeKind: TypeKind.Enum } => "enum",
        { TypeKind: TypeKind.Delegate } => "delegate",
        _ => "class"
    };

    private static string? AccessibilityKeyword(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Public => "public",
        Accessibility.Internal => "internal",
        Accessibility.Private => "private",
        Accessibility.Protected => "protected",
        Accessibility.ProtectedOrInternal => "protected internal",
        Accessibility.ProtectedAndInternal => "private protected",
        _ => null
    };

    private static QuickInfoTextKind KindOf(ITypeSymbol type) => type switch
    {
        { TypeKind: TypeKind.Interface } => QuickInfoTextKind.Interface,
        { TypeKind: TypeKind.Struct or TypeKind.Enum } => QuickInfoTextKind.Struct,
        { TypeKind: TypeKind.TypeParameter } => QuickInfoTextKind.TypeParameter,
        { SpecialType: not SpecialType.None } => QuickInfoTextKind.Keyword,
        _ => QuickInfoTextKind.Type
    };

    private static QuickInfoTextKind KindOf(SymbolDisplayPartKind kind) => kind switch
    {
        SymbolDisplayPartKind.Keyword => QuickInfoTextKind.Keyword,
        SymbolDisplayPartKind.ClassName or SymbolDisplayPartKind.RecordClassName or SymbolDisplayPartKind.DelegateName or
            SymbolDisplayPartKind.ErrorTypeName or SymbolDisplayPartKind.ModuleName => QuickInfoTextKind.Type,
        SymbolDisplayPartKind.StructName or SymbolDisplayPartKind.RecordStructName or SymbolDisplayPartKind.EnumName => QuickInfoTextKind.Struct,
        SymbolDisplayPartKind.InterfaceName => QuickInfoTextKind.Interface,
        SymbolDisplayPartKind.TypeParameterName => QuickInfoTextKind.TypeParameter,
        SymbolDisplayPartKind.NamespaceName => QuickInfoTextKind.Namespace,
        SymbolDisplayPartKind.MethodName or SymbolDisplayPartKind.ExtensionMethodName => QuickInfoTextKind.Method,
        SymbolDisplayPartKind.PropertyName or SymbolDisplayPartKind.FieldName or SymbolDisplayPartKind.EventName => QuickInfoTextKind.Member,
        SymbolDisplayPartKind.EnumMemberName or SymbolDisplayPartKind.ConstantName => QuickInfoTextKind.Constant,
        SymbolDisplayPartKind.ParameterName or SymbolDisplayPartKind.LocalName or SymbolDisplayPartKind.RangeVariableName or
            SymbolDisplayPartKind.LabelName => QuickInfoTextKind.Variable,
        SymbolDisplayPartKind.StringLiteral => QuickInfoTextKind.StringLiteral,
        SymbolDisplayPartKind.NumericLiteral => QuickInfoTextKind.NumericLiteral,
        SymbolDisplayPartKind.Punctuation or SymbolDisplayPartKind.Operator => QuickInfoTextKind.Punctuation,
        _ => QuickInfoTextKind.Text
    };

    private static void AddParts(List<QuickInfoTextRun> runs, ImmutableArray<SymbolDisplayPart> parts)
    {
        foreach (var part in parts)
        {
            Add(runs, new QuickInfoTextRun(part.ToString(), KindOf(part.Kind)));
        }
    }

    private static void AddKeyword(List<QuickInfoTextRun> runs, string keyword)
    {
        Add(runs, new QuickInfoTextRun(keyword, QuickInfoTextKind.Keyword));
        Add(runs, new QuickInfoTextRun(" ", QuickInfoTextKind.Text));
    }

    private static void AddLabel(List<QuickInfoTextRun> runs, string text) => Add(runs, new QuickInfoTextRun(text, QuickInfoTextKind.Label));

    // Neighbouring runs of the same kind merge, so "public static void" isn't five separate runs.
    private static void Add(List<QuickInfoTextRun> runs, QuickInfoTextRun run)
    {
        if (run.Text.Length == 0) return;
        if (runs.Count > 0 && runs[^1].Kind == run.Kind)
        {
            runs[^1] = runs[^1] with { Text = runs[^1].Text + run.Text };
            return;
        }
        runs.Add(run);
    }
}
