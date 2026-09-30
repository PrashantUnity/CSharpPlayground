using System.Collections.Frozen;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;

/// <summary>
/// Produces hover (Quick Info) documentation for Go symbols and standard library functions
/// without requiring a language server.
/// </summary>
public static class GoQuickInfoProvider
{
    public sealed record QuickInfo(string Signature, string Summary, string? Package = null, string? Since = null);

    private static readonly FrozenDictionary<string, QuickInfo> Entries = BuildEntries().ToFrozenDictionary(StringComparer.Ordinal);

    public static QuickInfo? Lookup(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol)) return null;

        if (Entries.TryGetValue(symbol, out var info)) return info;

        var lower = symbol.ToLowerInvariant();
        return Entries.TryGetValue(lower, out info) ? info : null;
    }

    public static string? ExtractSymbol(string code, int position)
    {
        if (string.IsNullOrEmpty(code) || position < 0 || position > code.Length) return null;
        if (position == code.Length) position--;

        int start = position;
        while (start > 0 && IsIdentChar(code[start - 1])) start--;

        int end = position;
        while (end < code.Length && IsIdentChar(code[end])) end++;

        if (start >= end) return null;

        // Check if there is a package qualifier before, e.g. fmt.Println
        if (start > 1 && code[start - 1] == '.')
        {
            int pkgStart = start - 1;
            while (pkgStart > 0 && IsIdentChar(code[pkgStart - 1])) pkgStart--;
            if (pkgStart < start - 1)
            {
                var qualified = code[pkgStart..end];
                if (Entries.ContainsKey(qualified)) return qualified;
            }
        }

        return code[start..end];
    }

    private static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static Dictionary<string, QuickInfo> BuildEntries() => new(StringComparer.Ordinal)
    {
        // Built-in functions
        ["append"] = new("func append(slice []Type, elems ...Type) []Type", "Appends elements to the end of a slice and returns the updated slice.", "builtin"),
        ["cap"] = new("func cap(v Type) int", "Returns the capacity of v, according to its type (slice, array, or channel).", "builtin"),
        ["len"] = new("func len(v Type) int", "Returns the length of v, according to its type (array, slice, map, string, or channel).", "builtin"),
        ["make"] = new("func make(t Type, size ...IntegerType) Type", "Allocates and initializes an object of type slice, map, or chan.", "builtin"),
        ["new"] = new("func new(Type) *Type", "Allocates zeroed storage for a new value of the specified type and returns a pointer to it.", "builtin"),
        ["copy"] = new("func copy(dst, src []Type) int", "Copies slice elements from src to dst and returns the number of elements copied.", "builtin"),
        ["delete"] = new("func delete(m map[Type]Type1, key Type)", "Deletes the element with the specified key from the map.", "builtin"),
        ["close"] = new("func close(c chan<- Type)", "Closes a channel, preventing further sends and indicating to receivers that no more values will be sent.", "builtin"),
        ["panic"] = new("func panic(v any)", "Stops the normal execution of the current goroutine, unwinding stack and running deferred functions.", "builtin"),
        ["recover"] = new("func recover() any", "Regains control of a panicking goroutine. Only useful inside deferred functions.", "builtin"),

        // Keywords
        ["func"] = new("func name(params) (returns)", "Declares a function or method in Go.", "keyword"),
        ["go"] = new("go expr", "Starts the execution of a function call as an independent concurrent goroutine.", "keyword"),
        ["defer"] = new("defer expr", "Defers the execution of a function until the surrounding function returns.", "keyword"),
        ["chan"] = new("chan ElementType", "Represents a typed conduit through which you can send and receive values with the channel operator <-.", "keyword"),
        ["select"] = new("select { case ... }", "Lets a goroutine wait on multiple communication operations.", "keyword"),
        ["interface"] = new("type Name interface { ... }", "Defines an interface type containing a set of method signatures.", "keyword"),
        ["struct"] = new("type Name struct { ... }", "Defines a composite type containing a sequence of named fields.", "keyword"),
        ["range"] = new("for key, val := range collection", "Iterates over elements in a slice, array, map, string, or channel.", "keyword"),
        ["package"] = new("package name", "Declares the package to which the source file belongs.", "keyword"),
        ["import"] = new("import \"path\"", "Declares that the source file depends on functions or types in other packages.", "keyword"),

        // fmt package
        ["fmt.Println"] = new("func Println(a ...any) (n int, err error)", "Formats using default formats for its operands and writes to standard output, followed by a newline.", "fmt"),
        ["fmt.Printf"] = new("func Printf(format string, a ...any) (n int, err error)", "Formats according to a format specifier and writes to standard output.", "fmt"),
        ["fmt.Print"] = new("func Print(a ...any) (n int, err error)", "Formats using default formats for its operands and writes to standard output.", "fmt"),
        ["fmt.Sprintf"] = new("func Sprintf(format string, a ...any) string", "Formats according to a format specifier and returns the resulting string.", "fmt"),
        ["fmt.Errorf"] = new("func Errorf(format string, a ...any) error", "Formats according to a format specifier and returns the string as a value satisfying error.", "fmt"),
        ["Println"] = new("func Println(a ...any) (n int, err error)", "Formats using default formats for its operands and writes to standard output, followed by a newline.", "fmt"),
        ["Printf"] = new("func Printf(format string, a ...any) (n int, err error)", "Formats according to a format specifier and writes to standard output.", "fmt"),
        ["Sprintf"] = new("func Sprintf(format string, a ...any) string", "Formats according to a format specifier and returns the resulting string.", "fmt"),

        // strings package
        ["strings.Contains"] = new("func Contains(s, substr string) bool", "Reports whether substr is within s.", "strings"),
        ["strings.Split"] = new("func Split(s, sep string) []string", "Slices s into all substrings separated by sep and returns a slice of the substrings between those separators.", "strings"),
        ["strings.Join"] = new("func Join(elems []string, sep string) string", "Concatenates the elements of its first argument to create a single string, with sep in between.", "strings"),
        ["strings.ReplaceAll"] = new("func ReplaceAll(s, old, new string) string", "Returns a copy of the string s with all non-overlapping instances of old replaced by new.", "strings"),
        ["strings.HasPrefix"] = new("func HasPrefix(s, prefix string) bool", "Tests whether the string s begins with prefix.", "strings"),
        ["strings.HasSuffix"] = new("func HasSuffix(s, suffix string) bool", "Tests whether the string s ends with suffix.", "strings"),
        ["strings.ToLower"] = new("func ToLower(s string) string", "Returns s with all Unicode letters mapped to their lower case.", "strings"),
        ["strings.ToUpper"] = new("func ToUpper(s string) string", "Returns s with all Unicode letters mapped to their upper case.", "strings"),

        // time package
        ["time.Sleep"] = new("func Sleep(d Duration)", "Pauses the current goroutine for at least the duration d.", "time"),
        ["time.Now"] = new("func Now() Time", "Returns the current local time.", "time"),
        ["time.Since"] = new("func Since(t Time) Duration", "Returns the time elapsed since t.", "time"),
        ["time.Duration"] = new("type Duration int64", "Represents the elapsed time between two instants as an int64 nanosecond count.", "time"),

        // os package
        ["os.Open"] = new("func Open(name string) (*File, error)", "Opens the named file for reading.", "os"),
        ["os.Create"] = new("func Create(name string) (*File, error)", "Creates or truncates the named file.", "os"),
        ["os.ReadFile"] = new("func ReadFile(name string) ([]byte, error)", "Reads the named file and returns the contents.", "os"),
        ["os.WriteFile"] = new("func WriteFile(name string, data []byte, perm FileMode) error", "Writes data to the named file, creating it if necessary.", "os"),
        ["os.Getenv"] = new("func Getenv(key string) string", "Retrieves the value of the environment variable named by the key.", "os"),
        ["os.Exit"] = new("func Exit(code int)", "Causes the current program to exit with the given status code.", "os"),

        // json package
        ["json.Marshal"] = new("func Marshal(v any) ([]byte, error)", "Returns the JSON encoding of v.", "encoding/json"),
        ["json.Unmarshal"] = new("func Unmarshal(data []byte, v any) error", "Parses the JSON-encoded data and stores the result in the value pointed to by v.", "encoding/json")
    };
}
