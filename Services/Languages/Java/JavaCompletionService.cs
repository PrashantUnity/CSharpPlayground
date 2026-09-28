using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;

public sealed partial class JavaCompletionService : ILanguageCompletionService
{
    [GeneratedRegex(@"\b(?:int|long|double|float|boolean|char|String|var|[A-Z][a-zA-Z0-9_<>, ]*)\s+(?<name>[a-zA-Z_][a-zA-Z0-9_]*)\s*(?:=|;|,|\))", RegexOptions.Multiline)]
    private static partial Regex LocalVariableRegex();

    [GeneratedRegex(@"(?:public|private|protected|static|\s)+[a-zA-Z0-9_<>,\[\]]+\s+(?<name>[a-zA-Z_][a-zA-Z0-9_]*)\s*\(", RegexOptions.Multiline)]
    private static partial Regex MethodRegex();

    [GeneratedRegex(@"\b(?:class|record|interface|enum)\s+(?<name>[a-zA-Z_][a-zA-Z0-9_]*)", RegexOptions.Multiline)]
    private static partial Regex TypeRegex();

    public Task<IReadOnlyList<CSharpCompletionItem>> GetCompletionsAsync(
        string code,
        int caretOffset,
        EditorAssistantContext context,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(code) || caretOffset < 0 || caretOffset > code.Length)
        {
            return Task.FromResult<IReadOnlyList<CSharpCompletionItem>>(Array.Empty<CSharpCompletionItem>());
        }

        // Check if cursor is after a dot
        int dotOffset = -1;
        int wordStart = caretOffset;
        while (wordStart > 0 && (char.IsLetterOrDigit(code[wordStart - 1]) || code[wordStart - 1] == '_'))
        {
            wordStart--;
        }

        if (wordStart > 0 && code[wordStart - 1] == '.')
        {
            dotOffset = wordStart - 1;
        }

        if (dotOffset >= 0)
        {
            return Task.FromResult(GetMemberCompletions(code, dotOffset, wordStart, caretOffset));
        }

        return Task.FromResult(GetScopeCompletions(code, wordStart, caretOffset));
    }

    private static IReadOnlyList<CSharpCompletionItem> GetMemberCompletions(
        string code,
        int dotOffset,
        int wordStart,
        int caretOffset)
    {
        var prefix = caretOffset > wordStart ? code.Substring(wordStart, caretOffset - wordStart) : string.Empty;

        // Find expression before dot
        int exprEnd = dotOffset;
        while (exprEnd > 0 && char.IsWhiteSpace(code[exprEnd - 1])) exprEnd--;

        int exprStart = exprEnd;
        while (exprStart > 0 && (char.IsLetterOrDigit(code[exprStart - 1]) || code[exprStart - 1] == '_' || code[exprStart - 1] == '.'))
        {
            exprStart--;
        }

        var qualifier = exprStart < exprEnd ? code.Substring(exprStart, exprEnd - exprStart).Trim() : string.Empty;

        IReadOnlyList<CSharpCompletionItem> sourceList = qualifier switch
        {
            "System.out" or "out" => JavaJdkIndex.SystemOutMembers,
            "System.err" or "err" => JavaJdkIndex.SystemOutMembers,
            "Math" => JavaJdkIndex.MathMembers,
            "Arrays" => JavaJdkIndex.ArraysMembers,
            "Collections" => JavaJdkIndex.CollectionsMembers,
            _ => JavaJdkIndex.CommonInstanceMembers
        };

        if (string.IsNullOrEmpty(prefix))
        {
            return sourceList;
        }

        return sourceList
            .Where(item => item.DisplayText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static IReadOnlyList<CSharpCompletionItem> GetScopeCompletions(
        string code,
        int wordStart,
        int caretOffset)
    {
        var prefix = caretOffset > wordStart ? code.Substring(wordStart, caretOffset - wordStart) : string.Empty;
        var results = new List<CSharpCompletionItem>();

        // 1. Snippets
        foreach (var s in JavaJdkIndex.Snippets)
        {
            if (string.IsNullOrEmpty(prefix) || s.DisplayText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(s);
            }
        }

        // 2. Keywords
        foreach (var k in JavaJdkIndex.Keywords)
        {
            if (string.IsNullOrEmpty(prefix) || k.DisplayText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(k);
            }
        }

        // 3. Local variables, methods, and types declared in the document
        var localSymbols = ExtractLocalSymbols(code, prefix);
        results.AddRange(localSymbols);

        // 4. Standard Library types
        foreach (var t in JavaJdkIndex.StandardLibraryTypes)
        {
            if (string.IsNullOrEmpty(prefix) || t.DisplayText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(t);
            }
        }

        return results;
    }

    private static List<CSharpCompletionItem> ExtractLocalSymbols(string code, string prefix)
    {
        var items = new List<CSharpCompletionItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match m in LocalVariableRegex().Matches(code))
        {
            var name = m.Groups["name"].Value;
            if (name.Length > 0 && seen.Add(name))
            {
                if (string.IsNullOrEmpty(prefix) || name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    items.Add(new CSharpCompletionItem
                    {
                        DisplayText = name,
                        InsertionText = name,
                        Kind = CompletionItemKind.Variable,
                        Priority = 880,
                        Documentation = "Locally declared variable or parameter."
                    });
                }
            }
        }

        foreach (Match m in MethodRegex().Matches(code))
        {
            var name = m.Groups["name"].Value;
            if (name.Length > 0 && seen.Add(name))
            {
                if (string.IsNullOrEmpty(prefix) || name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    items.Add(new CSharpCompletionItem
                    {
                        DisplayText = name,
                        InsertionText = $"{name}($0)",
                        Kind = CompletionItemKind.Method,
                        Priority = 875,
                        Documentation = "Locally declared method."
                    });
                }
            }
        }

        foreach (Match m in TypeRegex().Matches(code))
        {
            var name = m.Groups["name"].Value;
            if (name.Length > 0 && seen.Add(name))
            {
                if (string.IsNullOrEmpty(prefix) || name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    items.Add(new CSharpCompletionItem
                    {
                        DisplayText = name,
                        InsertionText = name,
                        Kind = CompletionItemKind.Class,
                        Priority = 870,
                        Documentation = "Locally declared class or type."
                    });
                }
            }
        }

        return items;
    }
}
