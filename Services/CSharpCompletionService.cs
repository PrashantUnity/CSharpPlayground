using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public class CSharpCompletionService
{
    private readonly RoslynCompilerService _compilerService;

    private const string DefaultUsings = @"using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Diagnostics;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Models;
";

    // Common C# keywords with high base priority
    private static readonly Lazy<List<CSharpCompletionItem>> Keywords = new(() =>
    [
        new() { DisplayText = "var", InsertionText = "var ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Declares an implicitly-typed local variable." },
        new() { DisplayText = "int", InsertionText = "int ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Represents a 32-bit signed integer." },
        new() { DisplayText = "string", InsertionText = "string ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Represents a sequence of UTF-16 code units." },
        new() { DisplayText = "bool", InsertionText = "bool ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Represents a boolean value (true or false)." },
        new() { DisplayText = "double", InsertionText = "double ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Represents a double-precision 64-bit floating-point number." },
        new() { DisplayText = "decimal", InsertionText = "decimal ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Represents a precise 128-bit decimal value for financial operations." },
        new() { DisplayText = "long", InsertionText = "long ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Represents a 64-bit signed integer." },
        new() { DisplayText = "object", InsertionText = "object ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "The root type of the .NET type hierarchy." },
        new() { DisplayText = "void", InsertionText = "void ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Specifies that a method does not return a value." },
        new() { DisplayText = "if", InsertionText = "if (", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Executes a block based on a boolean condition." },
        new() { DisplayText = "else", InsertionText = "else\n{\n    $0\n}", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Executes when the corresponding if condition is false." },
        new() { DisplayText = "for", InsertionText = "for (int i = 0; i < length; i++)\n{\n    $0\n}", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Executes its statement while a specified condition is true." },
        new() { DisplayText = "foreach", InsertionText = "foreach (var item in collection)\n{\n    $0\n}", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Iterates through each element in an IEnumerable collection." },
        new() { DisplayText = "while", InsertionText = "while (", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Executes a statement while a condition evaluates to true." },
        new() { DisplayText = "return", InsertionText = "return ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Returns control and optionally a value to the caller." },
        new() { DisplayText = "new", InsertionText = "new ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Creates an instance of a type or invokes an object constructor." },
        new() { DisplayText = "await", InsertionText = "await ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Suspends execution until the awaited asynchronous task finishes." },
        new() { DisplayText = "async", InsertionText = "async ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Indicates that a method, lambda, or anonymous method is asynchronous." },
        new() { DisplayText = "public", InsertionText = "public ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Declares that a type or member can be accessed from any code." },
        new() { DisplayText = "private", InsertionText = "private ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Declares that a member can only be accessed within the same class/struct." },
        new() { DisplayText = "protected", InsertionText = "protected ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Declares that a member is accessible only within its class or derived classes." },
        new() { DisplayText = "static", InsertionText = "static ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Declares a member that belongs to the type itself rather than an instance." },
        new() { DisplayText = "class", InsertionText = "class ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Declares a reference type." },
        new() { DisplayText = "record", InsertionText = "record ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Declares a reference or value type with built-in value-based equality." },
        new() { DisplayText = "struct", InsertionText = "struct ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Declares a value type." },
        new() { DisplayText = "interface", InsertionText = "interface ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Defines a contract containing definitions for a group of related functionalities." },
        new() { DisplayText = "enum", InsertionText = "enum ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Declares an enumeration value type consisting of a set of named constants." },
        new() { DisplayText = "try", InsertionText = "try\n{\n    $0\n}\ncatch (Exception ex)\n{\n    \n}", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Executes code within an exception-handled guard block." },
        new() { DisplayText = "throw", InsertionText = "throw ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Signals the occurrence of an exception during program execution." },
        new() { DisplayText = "using", InsertionText = "using ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Defines a scope for disposable objects or imports namespaces." },
        new() { DisplayText = "null", InsertionText = "null", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "A literal representing a null reference." },
        new() { DisplayText = "true", InsertionText = "true", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Boolean true value." },
        new() { DisplayText = "false", InsertionText = "false", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Boolean false value." },
        new() { DisplayText = "switch", InsertionText = "switch (", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Matches a match expression against a list of candidate cases." }
    ]);

    // Common developer snippets
    private static readonly Lazy<List<CSharpCompletionItem>> Snippets = new(() =>
    [
        new()
        {
            DisplayText = "cw",
            InsertionText = "Console.WriteLine($0);",
            Kind = CompletionItemKind.Snippet,
            Signature = "Console.WriteLine();",
            Documentation = "Prints output to the standard console and adds a newline.",
            Priority = 900,
            CaretOffsetDelta = -2
        },
        new()
        {
            DisplayText = "dump",
            InsertionText = ".Dump();$0",
            Kind = CompletionItemKind.Snippet,
            Signature = ".Dump();",
            Documentation = "FryPDF Dump Extension: Renders any variable, array, collection, or object in an interactive visual table.",
            Priority = 920,
            CaretOffsetDelta = 0
        },
        new()
        {
            DisplayText = "prop",
            InsertionText = "public int MyProperty { get; set; }",
            Kind = CompletionItemKind.Snippet,
            Signature = "public Property { get; set; }",
            Documentation = "Auto-implemented property declaration.",
            Priority = 880
        },
        new()
        {
            DisplayText = "disp_html",
            InsertionText = "Display.Html(\"$0\");",
            Kind = CompletionItemKind.Snippet,
            Signature = "Display.Html(html)",
            Documentation = "FryPDF Interactive Display: Renders styled HTML output to the visual deck.",
            Priority = 890,
            CaretOffsetDelta = -3
        },
        new()
        {
            DisplayText = "disp_md",
            InsertionText = "Display.Markdown(\"$0\");",
            Kind = CompletionItemKind.Snippet,
            Signature = "Display.Markdown(markdown)",
            Documentation = "FryPDF Interactive Display: Renders formatted Markdown to the visual deck.",
            Priority = 890,
            CaretOffsetDelta = -3
        }
    ]);

    private static readonly HashSet<string> CommonTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Console", "Math", "List", "Dictionary", "Enumerable", "File", "Directory",
        "Path", "JsonSerializer", "DateTime", "TimeSpan", "Task", "Stopwatch",
        "Regex", "Display", "Environment", "String", "Int32", "Boolean", "Double"
    };

    public CSharpCompletionService(RoslynCompilerService compilerService)
    {
        _compilerService = compilerService;
    }

    public async Task<IReadOnlyList<CSharpCompletionItem>> GetCompletionsAsync(
        string code,
        int caretOffset,
        ExecutionLanguageMode mode = ExecutionLanguageMode.Statements,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(code) || caretOffset < 0)
        {
            return GetGlobalKeywordsAndSnippets();
        }

        caretOffset = Math.Min(caretOffset, code.Length);

        return await Task.Run(() =>
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                return ComputeCompletions(code, caretOffset, mode, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Resilient fallback on compilation or AST traversal failure
                return GetFallbackCompletions(code, caretOffset);
            }
        }, ct);
    }

    private IReadOnlyList<CSharpCompletionItem> ComputeCompletions(
        string code,
        int caretOffset,
        ExecutionLanguageMode mode,
        CancellationToken ct)
    {
        // 1. Determine context: Is it member access (preceded by '.')?
        var (isMemberAccess, wordStart, prefix) = AnalyzeContext(code, caretOffset);

        // 2. Wrap source code with usings using exact offset preservation (zero drift!)
        var wrappedCode = DefaultUsings + code;
        var targetOffset = DefaultUsings.Length + (isMemberAccess ? wordStart : caretOffset);

        // 3. Parse AST and SemanticModel
        var syntaxTree = CSharpSyntaxTree.ParseText(
            wrappedCode,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp13),
            cancellationToken: ct);

        var compilation = CSharpCompilation.Create(
            $"CompletionAnalysis_{Guid.NewGuid():N}",
            syntaxTrees: [syntaxTree],
            references: _compilerService.DefaultReferences,
            options: new CSharpCompilationOptions(
                OutputKind.ConsoleApplication,
                optimizationLevel: OptimizationLevel.Debug,
                allowUnsafe: false));

        var semanticModel = compilation.GetSemanticModel(syntaxTree);

        ct.ThrowIfCancellationRequested();

        if (isMemberAccess)
        {
            return ComputeMemberAccessCompletions(syntaxTree, semanticModel, targetOffset, prefix, ct);
        }

        return ComputeScopeCompletions(semanticModel, targetOffset, code, caretOffset, prefix, ct);
    }

    private static (bool IsMemberAccess, int WordStart, string Prefix) AnalyzeContext(string code, int caretOffset)
    {
        if (caretOffset == 0)
        {
            return (false, 0, string.Empty);
        }

        // Find the current identifier word start
        int i = caretOffset;
        while (i > 0 && (char.IsLetterOrDigit(code[i - 1]) || code[i - 1] == '_'))
        {
            i--;
        }

        var wordStart = i;
        var prefix = code.Substring(wordStart, caretOffset - wordStart);

        // Check if preceding non-whitespace character before wordStart is a dot '.'
        int dotCheck = wordStart - 1;
        while (dotCheck >= 0 && (code[dotCheck] == ' ' || code[dotCheck] == '\t'))
        {
            dotCheck--;
        }

        var isMemberAccess = dotCheck >= 0 && code[dotCheck] == '.';
        return (isMemberAccess, wordStart, prefix);
    }

    private IReadOnlyList<CSharpCompletionItem> ComputeMemberAccessCompletions(
        SyntaxTree syntaxTree,
        SemanticModel semanticModel,
        int targetOffset,
        string prefix,
        CancellationToken ct)
    {
        var root = syntaxTree.GetRoot(ct);
        int lookupPos = Math.Max(0, targetOffset - 1);
        var token = root.FindToken(lookupPos);

        // Walk up to find MemberAccessExpressionSyntax, ConditionalAccessExpressionSyntax, or QualifiedNameSyntax
        ExpressionSyntax? targetExpression = null;
        var current = token.Parent;

        while (current != null)
        {
            if (current is MemberAccessExpressionSyntax memberAccess)
            {
                targetExpression = memberAccess.Expression;
                break;
            }
            if (current is ConditionalAccessExpressionSyntax conditionalAccess)
            {
                targetExpression = conditionalAccess.Expression;
                break;
            }
            if (current is MemberBindingExpressionSyntax binding && binding.Parent is ConditionalAccessExpressionSyntax parentCond)
            {
                targetExpression = parentCond.Expression;
                break;
            }
            if (current is QualifiedNameSyntax qualifiedName)
            {
                targetExpression = qualifiedName.Left;
                break;
            }
            current = current.Parent;
        }

        if (targetExpression == null)
        {
            // If tree is incomplete right after dot, check previous token
            var prevToken = token.GetPreviousToken();
            if (prevToken.Parent is ExpressionSyntax expr)
            {
                targetExpression = expr;
            }
        }

        if (targetExpression == null)
        {
            return Array.Empty<CSharpCompletionItem>();
        }

        ITypeSymbol? typeSymbol = null;
        INamespaceOrTypeSymbol? containerSymbol = null;

        var typeInfo = semanticModel.GetTypeInfo(targetExpression, ct);
        typeSymbol = typeInfo.Type;
        if (typeSymbol is IErrorTypeSymbol || typeSymbol?.TypeKind == TypeKind.Error)
        {
            typeSymbol = null;
        }

        if (typeSymbol == null)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(targetExpression, ct);
            var sym = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
            if (sym != null && sym.Kind != SymbolKind.ErrorType)
            {
                if (sym is INamedTypeSymbol namedType)
                {
                    typeSymbol = namedType;
                    containerSymbol = namedType;
                }
                else if (sym is INamespaceSymbol ns)
                {
                    containerSymbol = ns;
                }
                else if (sym is ILocalSymbol local)
                {
                    typeSymbol = local.Type;
                }
            }
        }

        containerSymbol ??= typeSymbol;
        if (containerSymbol is IErrorTypeSymbol || (containerSymbol is ITypeSymbol errType && errType.TypeKind == TypeKind.Error))
        {
            containerSymbol = null;
        }

        // Typo-tolerant fallback: if expression is an identifier that had a typo (e.g. "enviroment" vs "environment"),
        // match to the closest local variable/parameter in scope!
        if (containerSymbol == null)
        {
            var exprText = targetExpression.ToString().Trim();
            if (!string.IsNullOrEmpty(exprText))
            {
                var scopeSymbols = semanticModel.LookupSymbols(targetExpression.SpanStart);
                var candidate = scopeSymbols
                    .Where(s => s is ILocalSymbol or IParameterSymbol or IPropertySymbol or IFieldSymbol)
                    .Select(s => new { Symbol = s, Dist = LevenshteinDistance(s.Name.ToLowerInvariant(), exprText.ToLowerInvariant()) })
                    .Where(x => x.Dist <= 2)
                    .OrderBy(x => x.Dist)
                    .FirstOrDefault();

                if (candidate != null)
                {
                    containerSymbol = candidate.Symbol switch
                    {
                        ILocalSymbol l => l.Type,
                        IParameterSymbol p => p.Type,
                        IPropertySymbol prop => prop.Type,
                        IFieldSymbol f => f.Type,
                        _ => null
                    };
                }
            }
        }

        if (containerSymbol == null)
        {
            return Array.Empty<CSharpCompletionItem>();
        }

        // Query accessible symbols in container (with reduced extension methods such as .Dump() and LINQ)
        var symbols = semanticModel.LookupSymbols(
            targetOffset,
            container: containerSymbol,
            includeReducedExtensionMethods: true);

        return FormatAndRankSymbols(symbols, isMember: true, prefix);
    }

    private IReadOnlyList<CSharpCompletionItem> ComputeScopeCompletions(
        SemanticModel semanticModel,
        int targetOffset,
        string code,
        int caretOffset,
        string prefix,
        CancellationToken ct)
    {
        var results = new List<CSharpCompletionItem>();

        // 1. Symbols in current scope
        var symbols = semanticModel.LookupSymbols(targetOffset);
        results.AddRange(FormatAndRankSymbols(symbols, isMember: false, prefix));

        // 2. C# Keywords (ranked by prefix match)
        foreach (var kw in Keywords.Value)
        {
            var score = CalculateMatchScore(kw.DisplayText, prefix);
            if (string.IsNullOrEmpty(prefix) || score > 0)
            {
                results.Add(new CSharpCompletionItem
                {
                    DisplayText = kw.DisplayText,
                    InsertionText = kw.InsertionText,
                    Kind = kw.Kind,
                    Documentation = kw.Documentation,
                    Priority = kw.Priority + score
                });
            }
        }

        // 3. Developer Snippets (ranked by prefix match)
        foreach (var snip in Snippets.Value)
        {
            var score = CalculateMatchScore(snip.DisplayText, prefix);
            if (string.IsNullOrEmpty(prefix) || score > 0)
            {
                results.Add(new CSharpCompletionItem
                {
                    DisplayText = snip.DisplayText,
                    InsertionText = snip.InsertionText,
                    Kind = snip.Kind,
                    Signature = snip.Signature,
                    Documentation = snip.Documentation,
                    Priority = snip.Priority + score,
                    CaretOffsetDelta = snip.CaretOffsetDelta
                });
            }
        }

        // 4. Scan document for identifiers that might not yet be in semantic model
        // Strip the current word at the caret so the word/typo currently being typed doesn't suggest itself!
        var codeWithoutCurrentWord = (!string.IsNullOrEmpty(prefix) && caretOffset >= prefix.Length && caretOffset <= code.Length)
            ? code.Remove(caretOffset - prefix.Length, prefix.Length)
            : code;
        var docWords = ExtractDocumentIdentifiers(codeWithoutCurrentWord);
        var existingNames = new HashSet<string>(results.Select(r => r.DisplayText), StringComparer.Ordinal);
        foreach (var word in docWords)
        {
            if (!existingNames.Contains(word) && word.Length > 2 && !string.Equals(word, prefix, StringComparison.OrdinalIgnoreCase))
            {
                var score = CalculateMatchScore(word, prefix);
                if (string.IsNullOrEmpty(prefix) || score > 0)
                {
                    results.Add(new CSharpCompletionItem
                    {
                        DisplayText = word,
                        InsertionText = word,
                        Kind = CompletionItemKind.Variable,
                        Documentation = "Identifier from current document",
                        Priority = 800 + score
                    });
                }
            }
        }

        return results
            .OrderByDescending(i => i.Priority)
            .ThenBy(i => i.DisplayText.Length)
            .ThenBy(i => i.DisplayText)
            .ToList();
    }

    private static IReadOnlyList<CSharpCompletionItem> FormatAndRankSymbols(
        IEnumerable<ISymbol> rawSymbols,
        bool isMember,
        string prefix)
    {
        var items = new List<CSharpCompletionItem>();
        var grouped = rawSymbols
            .Where(s => !s.IsImplicitlyDeclared && !s.Name.StartsWith("<") && !string.IsNullOrEmpty(s.Name))
            .GroupBy(s => s.Name);

        foreach (var group in grouped)
        {
            var symbolList = group.ToList();
            var first = symbolList[0];

            var kind = MapSymbolKind(first);
            var (returnType, signature, doc) = ExtractSymbolDetails(symbolList, first);

            var basePriority = GetSymbolBasePriority(first, isMember);
            var matchScore = CalculateMatchScore(first.Name, prefix);

            // If user typed a non-empty prefix, exclude items with 0 match score to eliminate noise!
            if (!string.IsNullOrEmpty(prefix) && matchScore <= 0)
            {
                continue;
            }

            var insertionText = first switch
            {
                IMethodSymbol => $"{first.Name}()",
                _ => first.Name
            };

            var caretDelta = first is IMethodSymbol ? -1 : 0;

            items.Add(new CSharpCompletionItem
            {
                DisplayText = first.Name,
                InsertionText = insertionText,
                Kind = kind,
                ReturnType = returnType,
                Signature = signature,
                Documentation = doc,
                Priority = basePriority + matchScore,
                CaretOffsetDelta = caretDelta
            });
        }

        return items
            .OrderByDescending(i => i.Priority)
            .ThenBy(i => i.DisplayText.Length)
            .ThenBy(i => i.DisplayText)
            .ToList();
    }

    private static double GetSymbolBasePriority(ISymbol symbol, bool isMember)
    {
        if (isMember)
        {
            // Direct properties and methods on the object should rank HIGHEST!
            if (symbol is IPropertySymbol) return 500;
            if (symbol is IMethodSymbol m)
            {
                if (m.IsExtensionMethod) return 400;
                if (m.ContainingType?.SpecialType == SpecialType.System_Object) return 200;
                return 450;
            }
            if (symbol is IFieldSymbol) return 480;
            return 300;
        }

        // Scope completion:
        // 1. Local variables and parameters are top priority!
        if (symbol is ILocalSymbol) return 1000;
        if (symbol is IParameterSymbol) return 950;

        // 2. Common studio and runtime types
        if (CommonTypes.Contains(symbol.Name)) return 700;

        // 3. Methods/properties of current script context
        if (symbol is IMethodSymbol or IPropertySymbol or IFieldSymbol) return 600;

        // 4. Classes, structs, records, interfaces
        if (symbol is INamedTypeSymbol type)
        {
            // Push *Exception types down so they never crowd out variables or common types!
            if (type.Name.EndsWith("Exception", StringComparison.Ordinal)) return 20;
            if (type.TypeKind == TypeKind.Interface) return 400;
            if (type.IsRecord) return 450;
            return 350;
        }

        if (symbol is INamespaceSymbol) return 100;

        return 100;
    }

    private static double CalculateMatchScore(string candidate, string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return 0;

        // Exact match
        if (candidate.Equals(prefix, StringComparison.Ordinal)) return 5000;
        if (candidate.Equals(prefix, StringComparison.OrdinalIgnoreCase)) return 4500;

        // Prefix match
        if (candidate.StartsWith(prefix, StringComparison.Ordinal)) return 3000;
        if (candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return 2500;

        // CamelCase match (e.g. 'cw' matches 'Console.WriteLine', 'toL' matches 'ToList')
        if (CamelCaseMatch(candidate, prefix)) return 2000;

        // Substring match
        if (candidate.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) >= 0) return 1500;

        // Typo tolerance (Levenshtein distance <= 2 for queries >= 4 chars, like 'enviroment' vs 'environment')
        if (prefix.Length >= 4)
        {
            int dist = LevenshteinDistance(candidate.ToLowerInvariant(), prefix.ToLowerInvariant());
            if (dist <= 1) return 1200;
            if (dist <= 2) return 800;
        }

        return 0; // No match
    }

    private static bool CamelCaseMatch(string candidate, string prefix)
    {
        if (string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(candidate)) return false;
        int pIdx = 0;
        for (int i = 0; i < candidate.Length && pIdx < prefix.Length; i++)
        {
            if (char.ToUpperInvariant(candidate[i]) == char.ToUpperInvariant(prefix[pIdx]))
            {
                pIdx++;
            }
        }
        return pIdx == prefix.Length;
    }

    public static int LevenshteinDistance(string s, string t)
    {
        int n = s.Length;
        int m = t.Length;
        if (n == 0) return m;
        if (m == 0) return n;

        var d = new int[n + 1, m + 1];
        for (int i = 0; i <= n; d[i, 0] = i++) ;
        for (int j = 0; j <= m; d[0, j] = j++) ;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }

    private static CompletionItemKind MapSymbolKind(ISymbol symbol) => symbol switch
    {
        IMethodSymbol m when m.IsExtensionMethod => CompletionItemKind.ExtensionMethod,
        IMethodSymbol => CompletionItemKind.Method,
        IPropertySymbol => CompletionItemKind.Property,
        IFieldSymbol => CompletionItemKind.Field,
        INamedTypeSymbol t when t.TypeKind == TypeKind.Interface => CompletionItemKind.Interface,
        INamedTypeSymbol t when t.TypeKind == TypeKind.Enum => CompletionItemKind.Enum,
        INamedTypeSymbol t when t.TypeKind == TypeKind.Struct => CompletionItemKind.Struct,
        INamedTypeSymbol t when t.IsRecord => CompletionItemKind.Record,
        INamedTypeSymbol => CompletionItemKind.Class,
        ILocalSymbol => CompletionItemKind.Variable,
        IParameterSymbol => CompletionItemKind.Variable,
        INamespaceSymbol => CompletionItemKind.Namespace,
        _ => CompletionItemKind.Method
    };

    private static (string? ReturnType, string Signature, string? Documentation) ExtractSymbolDetails(
        List<ISymbol> overloads,
        ISymbol primary)
    {
        string? returnType = null;
        string signature;
        string? doc = null;

        try
        {
            var xml = primary.GetDocumentationCommentXml();
            if (!string.IsNullOrWhiteSpace(xml))
            {
                doc = ExtractXmlSummary(xml);
            }
        }
        catch { }

        if (primary is IMethodSymbol method)
        {
            returnType = method.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            var paramList = string.Join(", ", method.Parameters.Select(p =>
                $"{p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {p.Name}"));

            signature = $"{returnType} {method.Name}({paramList})";
            if (overloads.Count > 1)
            {
                signature += $" (+{overloads.Count - 1} overload{(overloads.Count > 2 ? "s" : "")})";
            }

            if (string.IsNullOrEmpty(doc))
            {
                doc = method.ContainingType != null
                    ? $"Belongs to {method.ContainingType.ToDisplayString()}"
                    : "Method";
            }
        }
        else if (primary is IPropertySymbol prop)
        {
            returnType = prop.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            signature = $"{returnType} {prop.Name} {{ get; set; }}";
            doc ??= prop.ContainingType != null ? $"Belongs to {prop.ContainingType.ToDisplayString()}" : "Property";
        }
        else if (primary is IFieldSymbol field)
        {
            returnType = field.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            signature = $"{returnType} {field.Name}";
            doc ??= field.ContainingType != null ? $"Belongs to {field.ContainingType.ToDisplayString()}" : "Field";
        }
        else if (primary is INamedTypeSymbol type)
        {
            returnType = type.TypeKind.ToString().ToLowerInvariant();
            signature = $"{returnType} {type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}";
            doc ??= $"Namespace: {type.ContainingNamespace?.ToDisplayString() ?? "global"}";
        }
        else
        {
            signature = primary.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        }

        return (returnType, signature, doc);
    }

    private static string? ExtractXmlSummary(string xml)
    {
        try
        {
            var doc = XDocument.Parse($"<root>{xml}</root>");
            var summary = doc.Descendants("summary").FirstOrDefault();
            if (summary != null)
            {
                var text = summary.Value.Trim();
                return Regex.Replace(text, @"\s+", " ");
            }
        }
        catch { }
        return null;
    }

    private static IReadOnlyList<CSharpCompletionItem> GetGlobalKeywordsAndSnippets()
    {
        var list = new List<CSharpCompletionItem>();
        list.AddRange(Keywords.Value);
        list.AddRange(Snippets.Value);
        return list.OrderByDescending(i => i.Priority).ToList();
    }

    private static IReadOnlyList<CSharpCompletionItem> GetFallbackCompletions(string code, int caretOffset)
    {
        var items = new List<CSharpCompletionItem>();

        var standardTypes = new[]
        {
            "Console", "Math", "List", "Dictionary", "Enumerable", "File", "Directory",
            "Path", "JsonSerializer", "DateTime", "TimeSpan", "Task", "Stopwatch",
            "Regex", "Display", "Environment"
        };

        foreach (var t in standardTypes)
        {
            items.Add(new CSharpCompletionItem
            {
                DisplayText = t,
                InsertionText = t,
                Kind = CompletionItemKind.Class,
                ReturnType = "class",
                Signature = $"class {t}",
                Documentation = $"Standard runtime type {t}",
                Priority = 700
            });
        }

        items.AddRange(Keywords.Value);
        items.AddRange(Snippets.Value);

        var docWords = ExtractDocumentIdentifiers(code);
        foreach (var word in docWords)
        {
            if (!standardTypes.Contains(word))
            {
                items.Add(new CSharpCompletionItem
                {
                    DisplayText = word,
                    InsertionText = word,
                    Kind = CompletionItemKind.Variable,
                    Documentation = "Identifier from current document",
                    Priority = 800
                });
            }
        }

        return items.OrderByDescending(i => i.Priority).ToList();
    }

    private static HashSet<string> ExtractDocumentIdentifiers(string code)
    {
        var set = new HashSet<string>();
        if (string.IsNullOrWhiteSpace(code)) return set;

        var matches = Regex.Matches(code, @"\b[a-zA-Z_][a-zA-Z0-9_]{2,}\b");
        foreach (Match match in matches)
        {
            set.Add(match.Value);
        }

        return set;
    }
}
