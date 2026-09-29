using System.Text.RegularExpressions;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;

/// <summary>
/// Completion for Rust files and notebook cells without a language server: after <c>.</c> the everyday methods, after
/// <c>::</c> the module's or type's items (the standard library's, and the file's own <c>enum</c> variants and <c>impl</c>
/// functions), inside <c>#[…]</c> attributes and <c>derive(…)</c>, and otherwise keywords, types, macros, snippets and the
/// names the file declares.
/// </summary>
public sealed partial class RustCompletionService : ILanguageCompletionService
{
    [GeneratedRegex(@"\bfn\s+(?<name>[A-Za-z_]\w*)")]
    private static partial Regex FunctionRegex();

    [GeneratedRegex(@"\b(?:struct|enum|trait|type|union)\s+(?<name>[A-Za-z_]\w*)")]
    private static partial Regex TypeRegex();

    [GeneratedRegex(@"\b(?:const|static)\s+(?:mut\s+)?(?<name>[A-Za-z_]\w*)\s*:")]
    private static partial Regex ConstantRegex();

    [GeneratedRegex(@"\bmod\s+(?<name>[A-Za-z_]\w*)")]
    private static partial Regex ModuleRegex();

    [GeneratedRegex(@"\blet\s+(?:mut\s+)?(?<name>[A-Za-z_]\w*)")]
    private static partial Regex LetRegex();

    [GeneratedRegex(@"\bfor\s+(?:mut\s+)?(?<name>[A-Za-z_]\w*)\s+in\b")]
    private static partial Regex ForRegex();

    [GeneratedRegex(@"\bfn\s+\w+\s*(?:<[^>]*>)?\s*\((?<params>[^)]*)\)")]
    private static partial Regex SignatureRegex();

    [GeneratedRegex(@"(?:mut\s+)?(?<name>[A-Za-z_]\w*)\s*:")]
    private static partial Regex ParameterRegex();

    [GeneratedRegex(@"#!?\[[^\]\r\n]*$")]
    private static partial Regex OpenAttributeRegex();

    [GeneratedRegex(@"#!?\[\s*derive\s*\([^)\r\n]*$")]
    private static partial Regex OpenDeriveRegex();

    private static readonly string[] Attributes =
    [
        "derive", "allow", "warn", "deny", "cfg", "test", "inline", "must_use", "repr", "cfg_attr", "non_exhaustive", "ignore", "should_panic", "deprecated", "doc"
    ];

    public Task<IReadOnlyList<CSharpCompletionItem>> GetCompletionsAsync(
        string code,
        int caretOffset,
        EditorAssistantContext context,
        CancellationToken ct = default)
    {
        if (code == null || caretOffset < 0 || caretOffset > code.Length)
        {
            return Task.FromResult<IReadOnlyList<CSharpCompletionItem>>(Array.Empty<CSharpCompletionItem>());
        }

        var wordStart = caretOffset;
        while (wordStart > 0 && IsIdentChar(code[wordStart - 1])) wordStart--;
        var prefix = code[wordStart..caretOffset];

        var lineStart = code.LastIndexOf('\n', Math.Max(wordStart - 1, 0)) + 1;
        var beforeOnLine = code[lineStart..wordStart];

        IReadOnlyList<CSharpCompletionItem> items;
        if (OpenDeriveRegex().IsMatch(beforeOnLine))
        {
            items = RustStandardLibrary.Derivable.Select(name => Item(name, name, $"#[derive({name})]", CompletionItemKind.Interface)).ToList();
        }
        else if (OpenAttributeRegex().IsMatch(beforeOnLine))
        {
            items = Attributes.Select(AttributeItem).ToList();
        }
        else if (wordStart >= 1 && code[wordStart - 1] == '.' && !(wordStart >= 2 && code[wordStart - 2] == '.'))
        {
            items = MemberItems();
        }
        else if (wordStart >= 2 && code[wordStart - 1] == ':' && code[wordStart - 2] == ':')
        {
            items = PathItems(code, QualifierBefore(code, wordStart - 2));
        }
        else
        {
            items = ScopeItems(code);
        }

        if (prefix.Length > 0)
        {
            items = items.Where(i => i.DisplayText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return Task.FromResult(items);
    }

    private static List<CSharpCompletionItem> MemberItems()
    {
        var items = new List<CSharpCompletionItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var method in RustStandardLibrary.Methods)
        {
            if (!seen.Add(method.Name)) continue;
            items.Add(new CSharpCompletionItem
            {
                DisplayText = method.Name,
                InsertionText = method.Name,
                Kind = CompletionItemKind.Method,
                Signature = method.Signature,
                Documentation = $"{method.Summary} ({method.Owner})"
            });
        }

        items.Add(Item("await", "await", "Waits for a future to finish", CompletionItemKind.Keyword));
        return items;
    }

    private static IReadOnlyList<CSharpCompletionItem> PathItems(string code, string qualifier)
    {
        var items = new List<CSharpCompletionItem>();

        if (RustStandardLibrary.Paths.TryGetValue(qualifier, out var known))
        {
            items.AddRange(known.Select(FromSymbol));
        }

        var local = LocalMembers(code, qualifier.Split("::").Last());
        var seen = new HashSet<string>(items.Select(i => i.DisplayText), StringComparer.Ordinal);
        items.AddRange(local.Where(i => seen.Add(i.DisplayText)));

        // Self:: and crate:: reach the file's own functions.
        if (qualifier is "Self" or "self" or "crate" or "super" && items.Count == 0)
        {
            foreach (Match match in FunctionRegex().Matches(code))
            {
                var name = match.Groups["name"].Value;
                if (seen.Add(name)) items.Add(Item(name, name, "Declared function", CompletionItemKind.Method));
            }
        }

        return items;
    }

    // enum Shape { Circle, Square(f64) } gives Circle, Square; impl Shape { fn new() … const ORIGIN … } gives new, ORIGIN.
    private static List<CSharpCompletionItem> LocalMembers(string code, string typeName)
    {
        var items = new List<CSharpCompletionItem>();
        if (typeName.Length == 0) return items;

        var escaped = Regex.Escape(typeName);
        foreach (Match enumMatch in Regex.Matches(code, $@"\benum\s+{escaped}\b[^{{]*\{{"))
        {
            var body = BodyAt(code, enumMatch.Index + enumMatch.Length - 1);
            foreach (var variant in TopLevelEntries(body))
            {
                var name = LeadingIdentifier(variant);
                if (name.Length > 0) items.Add(Item(name, name, $"Variant of {typeName}", CompletionItemKind.Enum));
            }
        }

        foreach (Match implMatch in Regex.Matches(code, $@"\bimpl\b[^{{;]*?\b{escaped}\b[^{{;]*\{{"))
        {
            var body = BodyAt(code, implMatch.Index + implMatch.Length - 1);
            foreach (Match function in FunctionRegex().Matches(body))
            {
                var name = function.Groups["name"].Value;
                items.Add(Item(name, name, $"Function of {typeName}", CompletionItemKind.Method));
            }

            foreach (Match constant in ConstantRegex().Matches(body))
            {
                var name = constant.Groups["name"].Value;
                items.Add(Item(name, name, $"Constant of {typeName}", CompletionItemKind.Field));
            }
        }

        return items;
    }

    private static List<CSharpCompletionItem> ScopeItems(string code)
    {
        var items = new List<CSharpCompletionItem>();

        items.AddRange(RustStandardLibrary.Keywords.Select(k => Item(k.Name, k.Name, k.Summary, CompletionItemKind.Keyword)));
        items.AddRange(RustStandardLibrary.Types.Select(FromSymbol));
        items.AddRange(RustStandardLibrary.Traits.Select(FromSymbol));
        items.Add(Item("std", "std", "The standard library", CompletionItemKind.Namespace));
        items.AddRange(RustStandardLibrary.Macros.Select(MacroItem));
        items.AddRange(Snippets());

        // What the file declares comes first.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void Declared(Regex regex, string what, CompletionItemKind kind, string group = "name")
        {
            foreach (Match match in regex.Matches(code))
            {
                var name = match.Groups[group].Value;
                if (seen.Add(name)) items.Add(new CSharpCompletionItem { DisplayText = name, InsertionText = name, Documentation = what, Kind = kind, Priority = 2 });
            }
        }

        Declared(FunctionRegex(), "Declared function", CompletionItemKind.Method);
        Declared(TypeRegex(), "Declared type", CompletionItemKind.Struct);
        Declared(ConstantRegex(), "Declared constant", CompletionItemKind.Field);
        Declared(ModuleRegex(), "Declared module", CompletionItemKind.Namespace);
        Declared(LetRegex(), "Local variable", CompletionItemKind.Variable);
        Declared(ForRegex(), "Loop variable", CompletionItemKind.Variable);

        foreach (Match signature in SignatureRegex().Matches(code))
        {
            foreach (Match parameter in ParameterRegex().Matches(signature.Groups["params"].Value))
            {
                var name = parameter.Groups["name"].Value;
                if (name != "self" && seen.Add(name))
                {
                    items.Add(new CSharpCompletionItem { DisplayText = name, InsertionText = name, Documentation = "Parameter", Kind = CompletionItemKind.Variable, Priority = 2 });
                }
            }
        }

        return items;
    }

    private static IEnumerable<CSharpCompletionItem> Snippets()
    {
        CSharpCompletionItem Snippet(string display, string insertion, int caretFromEnd, string what) => new()
        {
            DisplayText = display,
            InsertionText = insertion,
            Kind = CompletionItemKind.Snippet,
            Documentation = what,
            CaretOffsetDelta = -caretFromEnd
        };

        yield return Snippet("fn main()", "fn main() {\n    \n}", 2, "The program's entry point");
        yield return Snippet("fn", "fn name() {\n    \n}", 2, "A function");
        yield return Snippet("struct", "struct Name {\n    \n}", 2, "A struct with named fields");
        yield return Snippet("enum", "enum Name {\n    \n}", 2, "An enum");
        yield return Snippet("impl", "impl Name {\n    \n}", 2, "Methods for a type");
        yield return Snippet("impl Trait for", "impl Trait for Name {\n    \n}", 2, "A trait implementation");
        yield return Snippet("trait", "trait Name {\n    \n}", 2, "A trait");
        yield return Snippet("match", "match value {\n    _ => {}\n}", 2, "A match expression");
        yield return Snippet("if let", "if let Some(value) = option {\n    \n}", 2, "Run a block when a pattern matches");
        yield return Snippet("while let", "while let Some(value) = iter.next() {\n    \n}", 2, "Loop while a pattern matches");
        yield return Snippet("for", "for item in items {\n    \n}", 2, "Loop over an iterator");
        yield return Snippet("loop", "loop {\n    \n}", 2, "Loop forever");
        yield return Snippet("mod tests", "#[cfg(test)]\nmod tests {\n    use super::*;\n\n    #[test]\n    fn it_works() {\n        \n    }\n}", 8, "A unit-test module");
        yield return Snippet("#[derive(Debug)]", "#[derive(Debug)]", 0, "Derive Debug");
    }

    private static CSharpCompletionItem MacroItem(RustSymbol macro)
    {
        // println!("|"), vec![|], assert_eq!(|, ): the caret lands where you type next.
        var (insertion, caret) = macro.Name switch
        {
            "vec!" => ("vec![]", 1),
            "assert_eq!" or "assert_ne!" => (macro.Name + "(, )", 3),
            "println!" or "print!" or "eprintln!" or "eprint!" or "format!" or "panic!" => (macro.Name + "(\"\")", 2),
            "todo!" or "unimplemented!" or "unreachable!" => (macro.Name + "()", 0),
            "macro_rules!" => ("macro_rules! name {\n    () => {};\n}", 0),
            _ => (macro.Name + "()", 1)
        };

        return new CSharpCompletionItem
        {
            DisplayText = macro.Name,
            InsertionText = insertion,
            Kind = CompletionItemKind.Snippet,
            Signature = macro.Signature,
            Documentation = macro.Summary,
            CaretOffsetDelta = -caret
        };
    }

    private static CSharpCompletionItem AttributeItem(string name)
    {
        var (insertion, caret) = name switch
        {
            "derive" or "allow" or "warn" or "deny" or "cfg" or "repr" or "cfg_attr" => (name + "()", 1),
            _ => (name, 0)
        };

        return new CSharpCompletionItem { DisplayText = name, InsertionText = insertion, Kind = CompletionItemKind.Keyword, Documentation = $"#[{name}]", CaretOffsetDelta = -caret };
    }

    private static CSharpCompletionItem FromSymbol(RustSymbol symbol) => new()
    {
        DisplayText = symbol.Name,
        InsertionText = symbol.Name,
        Kind = symbol.CompletionKind,
        Signature = symbol.Signature,
        Documentation = symbol.Owner == null ? symbol.Summary : $"{symbol.Summary} ({symbol.Owner})"
    };

    private static CSharpCompletionItem Item(string display, string insertion, string documentation, CompletionItemKind kind) =>
        new() { DisplayText = display, InsertionText = insertion, Documentation = documentation, Kind = kind };

    // The path typed before a "::": std::collections in "std::collections::", Vec in "Vec::".
    private static string QualifierBefore(string code, int colonsStart)
    {
        var start = colonsStart;
        while (start > 0 && (IsIdentChar(code[start - 1]) || (code[start - 1] == ':' && start >= 2 && code[start - 2] == ':')))
        {
            start -= code[start - 1] == ':' ? 2 : 1;
        }

        return start < colonsStart ? code[start..colonsStart] : string.Empty;
    }

    // The text between the { at openBrace and its matching }, skipping strings and comments.
    private static string BodyAt(string code, int openBrace)
    {
        var depth = 0;
        for (var i = openBrace; i < code.Length; i++)
        {
            switch (code[i])
            {
                case '"':
                    for (i++; i < code.Length && code[i] != '"'; i++) if (code[i] == '\\') i++;
                    break;
                case '/' when i + 1 < code.Length && code[i + 1] == '/':
                    while (i < code.Length && code[i] != '\n') i++;
                    break;
                case '{':
                    depth++;
                    break;
                case '}':
                    depth--;
                    if (depth == 0) return code[(openBrace + 1)..i];
                    break;
            }
        }

        return code[(openBrace + 1)..];
    }

    // The comma-separated entries of an enum body, ignoring commas inside (), {} and [].
    private static IEnumerable<string> TopLevelEntries(string body)
    {
        var depth = 0;
        var start = 0;
        for (var i = 0; i < body.Length; i++)
        {
            switch (body[i])
            {
                case '(' or '{' or '[':
                    depth++;
                    break;
                case ')' or '}' or ']':
                    depth--;
                    break;
                case ',' when depth == 0:
                    yield return body[start..i];
                    start = i + 1;
                    break;
            }
        }

        if (start < body.Length) yield return body[start..];
    }

    private static string LeadingIdentifier(string entry)
    {
        var text = Regex.Replace(entry, @"(?m)^\s*(?://.*|#\[[^\]]*\])\s*", string.Empty).TrimStart();
        var end = 0;
        while (end < text.Length && IsIdentChar(text[end])) end++;
        return end > 0 && !char.IsDigit(text[0]) ? text[..end] : string.Empty;
    }

    private static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}
