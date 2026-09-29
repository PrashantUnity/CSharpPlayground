using System.Text.RegularExpressions;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;

/// <summary>
/// Provides code completion suggestions for Go files and notebook cells:
/// package members after dot (fmt., strings., time., os.), keywords, built-ins, and local declarations.
/// </summary>
public sealed partial class GoCompletionService : ILanguageCompletionService
{
    [GeneratedRegex(@"\bfunc\s+(?:\([^)]+\)\s+)?(?<name>[a-zA-Z_][a-zA-Z0-9_]*)\s*\(", RegexOptions.Multiline)]
    private static partial Regex FunctionRegex();

    [GeneratedRegex(@"\btype\s+(?<name>[a-zA-Z_][a-zA-Z0-9_]*)\s+(?:struct|interface)", RegexOptions.Multiline)]
    private static partial Regex TypeRegex();

    [GeneratedRegex(@"(?<name>[a-zA-Z_][a-zA-Z0-9_]*)\s*:=\s*", RegexOptions.Multiline)]
    private static partial Regex ShortVarRegex();

    [GeneratedRegex(@"\bvar\s+(?<name>[a-zA-Z_][a-zA-Z0-9_]*)", RegexOptions.Multiline)]
    private static partial Regex VarRegex();

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

        int identStart = dotOffset;
        while (identStart > 0 && (char.IsLetterOrDigit(code[identStart - 1]) || code[identStart - 1] == '_'))
        {
            identStart--;
        }

        var qualifier = code.Substring(identStart, dotOffset - identStart).Trim();
        var items = new List<CSharpCompletionItem>();

        switch (qualifier)
        {
            case "fmt":
                Add(items, "Println", "Println(a ...any)", "Formats and writes to stdout with newline", CompletionItemKind.Method);
                Add(items, "Printf", "Printf(format string, a ...any)", "Formats according to specifier to stdout", CompletionItemKind.Method);
                Add(items, "Print", "Print(a ...any)", "Formats and writes to stdout", CompletionItemKind.Method);
                Add(items, "Sprintf", "Sprintf(format string, a ...any) string", "Formats according to specifier returning string", CompletionItemKind.Method);
                Add(items, "Errorf", "Errorf(format string, a ...any) error", "Creates formatted error value", CompletionItemKind.Method);
                break;

            case "strings":
                Add(items, "Contains", "Contains(s, substr string) bool", "Reports whether substr is within s", CompletionItemKind.Method);
                Add(items, "Count", "Count(s, substr string) int", "Counts non-overlapping instances of substr in s", CompletionItemKind.Method);
                Add(items, "Split", "Split(s, sep string) []string", "Splits s by separator into slices", CompletionItemKind.Method);
                Add(items, "Join", "Join(elems []string, sep string) string", "Concatenates slice elements with separator", CompletionItemKind.Method);
                Add(items, "ReplaceAll", "ReplaceAll(s, old, new string) string", "Replaces all occurrences of old with new", CompletionItemKind.Method);
                Add(items, "HasPrefix", "HasPrefix(s, prefix string) bool", "Tests whether s begins with prefix", CompletionItemKind.Method);
                Add(items, "HasSuffix", "HasSuffix(s, suffix string) bool", "Tests whether s ends with suffix", CompletionItemKind.Method);
                Add(items, "ToLower", "ToLower(s string) string", "Converts s to lowercase", CompletionItemKind.Method);
                Add(items, "ToUpper", "ToUpper(s string) string", "Converts s to uppercase", CompletionItemKind.Method);
                break;

            case "time":
                Add(items, "Sleep", "Sleep(d Duration)", "Pauses current goroutine", CompletionItemKind.Method);
                Add(items, "Now", "Now() Time", "Returns current local time", CompletionItemKind.Method);
                Add(items, "Since", "Since(t Time) Duration", "Returns elapsed time since t", CompletionItemKind.Method);
                Add(items, "Second", "Second", "One second duration constant", CompletionItemKind.Field);
                Add(items, "Millisecond", "Millisecond", "One millisecond duration constant", CompletionItemKind.Field);
                Add(items, "Minute", "Minute", "One minute duration constant", CompletionItemKind.Field);
                break;

            case "os":
                Add(items, "Open", "Open(name string) (*File, error)", "Opens named file for reading", CompletionItemKind.Method);
                Add(items, "Create", "Create(name string) (*File, error)", "Creates or truncates named file", CompletionItemKind.Method);
                Add(items, "ReadFile", "ReadFile(name string) ([]byte, error)", "Reads named file into bytes", CompletionItemKind.Method);
                Add(items, "WriteFile", "WriteFile(name string, data []byte, perm FileMode) error", "Writes bytes into named file", CompletionItemKind.Method);
                Add(items, "Getenv", "Getenv(key string) string", "Reads environment variable", CompletionItemKind.Method);
                Add(items, "Exit", "Exit(code int)", "Causes current program to exit", CompletionItemKind.Method);
                Add(items, "Stdout", "Stdout *File", "Standard output stream", CompletionItemKind.Field);
                Add(items, "Stderr", "Stderr *File", "Standard error stream", CompletionItemKind.Field);
                break;

            case "json":
                Add(items, "Marshal", "Marshal(v any) ([]byte, error)", "Encodes value into JSON bytes", CompletionItemKind.Method);
                Add(items, "Unmarshal", "Unmarshal(data []byte, v any) error", "Parses JSON bytes into target pointer", CompletionItemKind.Method);
                break;

            case "sync":
                Add(items, "WaitGroup", "type WaitGroup struct", "Waits for a collection of goroutines to finish", CompletionItemKind.Class);
                Add(items, "Mutex", "type Mutex struct", "Mutual exclusion lock", CompletionItemKind.Class);
                Add(items, "RWMutex", "type RWMutex struct", "Reader/writer mutual exclusion lock", CompletionItemKind.Class);
                break;

            case "math":
                Add(items, "Max", "Max(x, y float64) float64", "Returns the larger of x or y", CompletionItemKind.Method);
                Add(items, "Min", "Min(x, y float64) float64", "Returns the smaller of x or y", CompletionItemKind.Method);
                Add(items, "Sqrt", "Sqrt(x float64) float64", "Returns square root of x", CompletionItemKind.Method);
                Add(items, "Abs", "Abs(x float64) float64", "Returns absolute value of x", CompletionItemKind.Method);
                break;
        }

        if (!string.IsNullOrEmpty(prefix))
        {
            items = items.Where(i => i.DisplayText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return items;
    }

    private static IReadOnlyList<CSharpCompletionItem> GetScopeCompletions(
        string code,
        int wordStart,
        int caretOffset)
    {
        var prefix = caretOffset > wordStart ? code.Substring(wordStart, caretOffset - wordStart) : string.Empty;
        var items = new List<CSharpCompletionItem>();

        // Keywords
        string[] keywords = ["break", "case", "chan", "const", "continue", "default", "defer", "else", "fallthrough",
            "for", "func", "go", "goto", "if", "import", "interface", "map", "package", "range", "return", "select", "struct", "switch", "type", "var"];
        foreach (var kw in keywords) Add(items, kw, kw, "Go keyword", CompletionItemKind.Keyword);

        // Built-ins
        string[] builtins = ["append", "cap", "clear", "close", "complex", "copy", "delete", "imag", "len", "make", "max", "min", "new", "panic", "print", "println", "real", "recover", "true", "false", "iota", "nil"];
        foreach (var b in builtins) Add(items, b, b, "Built-in Go identifier", CompletionItemKind.Method);

        // Built-in Types
        string[] types = ["any", "bool", "byte", "comparable", "error", "float32", "float64", "int", "int8", "int16", "int32", "int64", "rune", "string", "uint", "uint8", "uint16", "uint32", "uint64", "uintptr"];
        foreach (var t in types) Add(items, t, t, "Go type", CompletionItemKind.Class);

        // Standard Packages
        string[] packages = ["fmt", "strings", "time", "os", "sync", "json", "math", "io", "net", "http", "sort", "path", "bytes", "errors", "context"];
        foreach (var pkg in packages) Add(items, pkg, pkg, $"Go standard package '{pkg}'", CompletionItemKind.Namespace);

        // Snippets
        items.Add(new CSharpCompletionItem { DisplayText = "func main()", InsertionText = "func main() {\n\t\n}", Documentation = "Main entrypoint function", Kind = CompletionItemKind.Snippet });
        items.Add(new CSharpCompletionItem { DisplayText = "for range", InsertionText = "for i, val := range collection {\n\t\n}", Documentation = "Iterate over elements", Kind = CompletionItemKind.Snippet });
        items.Add(new CSharpCompletionItem { DisplayText = "if err != nil", InsertionText = "if err != nil {\n\treturn err\n}", Documentation = "Standard error check", Kind = CompletionItemKind.Snippet });
        items.Add(new CSharpCompletionItem { DisplayText = "go func()", InsertionText = "go func() {\n\t\n}()", Documentation = "Anonymous concurrent goroutine", Kind = CompletionItemKind.Snippet });

        // Scan local declarations
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in FunctionRegex().Matches(code))
        {
            var name = m.Groups["name"].Value;
            if (seen.Add(name)) Add(items, name, name, "Declared function", CompletionItemKind.Method);
        }
        foreach (Match m in TypeRegex().Matches(code))
        {
            var name = m.Groups["name"].Value;
            if (seen.Add(name)) Add(items, name, name, "Declared type", CompletionItemKind.Class);
        }
        foreach (Match m in ShortVarRegex().Matches(code))
        {
            var name = m.Groups["name"].Value;
            if (seen.Add(name)) Add(items, name, name, "Local variable", CompletionItemKind.Variable);
        }
        foreach (Match m in VarRegex().Matches(code))
        {
            var name = m.Groups["name"].Value;
            if (seen.Add(name)) Add(items, name, name, "Declared variable", CompletionItemKind.Variable);
        }

        if (!string.IsNullOrEmpty(prefix))
        {
            items = items.Where(i => i.DisplayText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return items;
    }

    private static void Add(List<CSharpCompletionItem> list, string display, string insert, string desc, CompletionItemKind kind) =>
        list.Add(new CSharpCompletionItem
        {
            DisplayText = display,
            InsertionText = insert,
            Documentation = desc,
            Kind = kind
        });
}
