using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;

/// <summary>
/// Provides code completion for C++ source files and notebook cells.
/// Handles scope resolution (::), member access (., ->), keywords, modern snippets,
/// and standard library symbols from <see cref="CppQuickInfoProvider"/>.
/// </summary>
public sealed partial class CppCompletionService : ILanguageCompletionService
{
    [GeneratedRegex(@"\b(?:int|long|double|float|char|bool|void|auto|[A-Z][a-zA-Z0-9_<>:]*)\s+(?<name>[a-zA-Z_][a-zA-Z0-9_]*)\s*(?:=|;|,|\))", RegexOptions.Multiline)]
    private static partial Regex LocalVariableRegex();

    [GeneratedRegex(@"(?:(?:inline|static|virtual|const|constexpr)\s+)*[a-zA-Z0-9_<>,:*&]+\s+(?<name>[a-zA-Z_][a-zA-Z0-9_]*)\s*\(", RegexOptions.Multiline)]
    private static partial Regex FunctionRegex();

    [GeneratedRegex(@"\b(?:struct|class|enum\s+class|enum|union)\s+(?<name>[a-zA-Z_][a-zA-Z0-9_]*)", RegexOptions.Multiline)]
    private static partial Regex TypeRegex();

    public static readonly IReadOnlyList<CSharpCompletionItem> Keywords =
    [
        new() { DisplayText = "auto", InsertionText = "auto ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Specifies that the type of the variable that is being declared will be automatically deduced from its initializer." },
        new() { DisplayText = "int", InsertionText = "int ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Basic integer type." },
        new() { DisplayText = "long", InsertionText = "long ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Target type will have width of at least 32 or 64 bits." },
        new() { DisplayText = "double", InsertionText = "double ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Double precision floating point type." },
        new() { DisplayText = "float", InsertionText = "float ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Single precision floating point type." },
        new() { DisplayText = "char", InsertionText = "char ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Type for character representation." },
        new() { DisplayText = "bool", InsertionText = "bool ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Type capable of holding one of the two values: true or false." },
        new() { DisplayText = "void", InsertionText = "void ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Type with an empty set of values." },
        new() { DisplayText = "const", InsertionText = "const ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Specifies that a variable or method does not modify state." },
        new() { DisplayText = "constexpr", InsertionText = "constexpr ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Specifies that the value of a variable or function can be evaluated at compile time." },
        new() { DisplayText = "consteval", InsertionText = "consteval ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Immediate function evaluated strictly at compile time (C++20)." },
        new() { DisplayText = "concept", InsertionText = "concept ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Defines a named set of requirements on template arguments (C++20)." },
        new() { DisplayText = "requires", InsertionText = "requires ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Specifies constraints on template arguments or function declarations (C++20)." },
        new() { DisplayText = "template", InsertionText = "template <typename $0>", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Declares a template for a function, class, or variable." },
        new() { DisplayText = "typename", InsertionText = "typename ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Specifies a template type parameter." },
        new() { DisplayText = "struct", InsertionText = "struct ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Defines a structure with default public access." },
        new() { DisplayText = "class", InsertionText = "class ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Defines a class with default private access." },
        new() { DisplayText = "public", InsertionText = "public:\n    ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Public member access specifier." },
        new() { DisplayText = "private", InsertionText = "private:\n    ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Private member access specifier." },
        new() { DisplayText = "protected", InsertionText = "protected:\n    ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Protected member access specifier." },
        new() { DisplayText = "virtual", InsertionText = "virtual ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Declares a virtual function dynamically dispatched." },
        new() { DisplayText = "override", InsertionText = "override", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Explicitly states that a virtual function overrides a base class method." },
        new() { DisplayText = "final", InsertionText = "final", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Specifies that a virtual function cannot be overridden in a derived class or a class cannot be inherited." },
        new() { DisplayText = "namespace", InsertionText = "namespace ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Declares a namespace scope." },
        new() { DisplayText = "using", InsertionText = "using ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Type alias or namespace import." },
        new() { DisplayText = "return", InsertionText = "return ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Returns control and optionally a value from a function." },
        new() { DisplayText = "if", InsertionText = "if (", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Conditional branch execution." },
        new() { DisplayText = "else", InsertionText = "else {\n    $0\n}", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Branch executed when condition is false." },
        new() { DisplayText = "for", InsertionText = "for (", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Loop execution." },
        new() { DisplayText = "while", InsertionText = "while (", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Executes while condition is true." },
        new() { DisplayText = "switch", InsertionText = "switch (", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Multi-branch decision statement." },
        new() { DisplayText = "case", InsertionText = "case ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Branch label in switch statement." },
        new() { DisplayText = "break", InsertionText = "break;", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Exits innermost loop or switch." },
        new() { DisplayText = "continue", InsertionText = "continue;", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Skips remainder of current loop iteration." },
        new() { DisplayText = "co_await", InsertionText = "co_await ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Suspends execution until an awaitable completes (C++20 coroutines)." },
        new() { DisplayText = "co_yield", InsertionText = "co_yield ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Yields a value to the caller and suspends coroutine (C++20)." },
        new() { DisplayText = "co_return", InsertionText = "co_return ", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Completes execution and returns a value from coroutine (C++20)." },
        new() { DisplayText = "nullptr", InsertionText = "nullptr", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Literal representing a null pointer constant." },
        new() { DisplayText = "true", InsertionText = "true", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Boolean true constant." },
        new() { DisplayText = "false", InsertionText = "false", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Boolean false constant." },
        new() { DisplayText = "sizeof", InsertionText = "sizeof(", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Returns size in bytes of the object representation of type." },
        new() { DisplayText = "decltype", InsertionText = "decltype(", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Inspects the declared type of an entity or expression." },
        new() { DisplayText = "noexcept", InsertionText = "noexcept", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Specifies whether a function could throw exceptions." },
        new() { DisplayText = "static_cast", InsertionText = "static_cast<$0>()", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Converts between types using implicit and user-defined conversions." },
        new() { DisplayText = "dynamic_cast", InsertionText = "dynamic_cast<$0>()", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Safely converts pointers and references up, down, and across the inheritance hierarchy." },
        new() { DisplayText = "reinterpret_cast", InsertionText = "reinterpret_cast<$0>()", Kind = CompletionItemKind.Keyword, Priority = 850, Documentation = "Converts between types by reinterpreting the underlying bit pattern." }
    ];

    public static readonly IReadOnlyList<CSharpCompletionItem> Snippets =
    [
        new()
        {
            DisplayText = "main",
            InsertionText = "int main() {\n    $0\n    return 0;\n}",
            Kind = CompletionItemKind.Snippet,
            Priority = 950,
            Signature = "int main()",
            Documentation = "Standard C++ entry point."
        },
        new()
        {
            DisplayText = "cout",
            InsertionText = "std::cout << $0 << std::endl;",
            Kind = CompletionItemKind.Snippet,
            Priority = 950,
            Signature = "std::cout << ... << std::endl",
            Documentation = "Prints to standard output with newline."
        },
        new()
        {
            DisplayText = "cin",
            InsertionText = "std::cin >> $0;",
            Kind = CompletionItemKind.Snippet,
            Priority = 940,
            Signature = "std::cin >> ...",
            Documentation = "Reads formatted input from standard input."
        },
        new()
        {
            DisplayText = "fori",
            InsertionText = "for (int i = 0; i < length; ++i) {\n    $0\n}",
            Kind = CompletionItemKind.Snippet,
            Priority = 950,
            Signature = "for (int i = 0; i < length; ++i)",
            Documentation = "Standard index-based for loop."
        },
        new()
        {
            DisplayText = "fore",
            InsertionText = "for (const auto& item : collection) {\n    $0\n}",
            Kind = CompletionItemKind.Snippet,
            Priority = 950,
            Signature = "for (const auto& item : collection)",
            Documentation = "Range-based for loop."
        },
        new()
        {
            DisplayText = "class",
            InsertionText = "class Name {\npublic:\n    Name();\n    ~Name();\n\nprivate:\n    $0\n};",
            Kind = CompletionItemKind.Snippet,
            Priority = 920,
            Signature = "class declaration",
            Documentation = "C++ class skeleton."
        },
        new()
        {
            DisplayText = "struct",
            InsertionText = "struct Name {\n    $0\n};",
            Kind = CompletionItemKind.Snippet,
            Priority = 920,
            Signature = "struct declaration",
            Documentation = "C++ struct skeleton."
        },
        new()
        {
            DisplayText = "include",
            InsertionText = "#include <$0>",
            Kind = CompletionItemKind.Snippet,
            Priority = 910,
            Signature = "#include <header>",
            Documentation = "Standard header include directive."
        }
    ];

    public static readonly IReadOnlyList<CSharpCompletionItem> CommonMemberCompletions =
    [
        new() { DisplayText = "size", InsertionText = "size()", Kind = CompletionItemKind.Method, Priority = 900, Signature = "size_t size() const noexcept", Documentation = "Returns the number of elements in the container." },
        new() { DisplayText = "empty", InsertionText = "empty()", Kind = CompletionItemKind.Method, Priority = 900, Signature = "bool empty() const noexcept", Documentation = "Checks whether the container is empty." },
        new() { DisplayText = "push_back", InsertionText = "push_back($0);", Kind = CompletionItemKind.Method, Priority = 890, Signature = "void push_back(const T& value)", Documentation = "Appends the given element value to the end of the container." },
        new() { DisplayText = "emplace_back", InsertionText = "emplace_back($0);", Kind = CompletionItemKind.Method, Priority = 890, Signature = "template<class... Args> reference emplace_back(Args&&... args)", Documentation = "Appends a new element to the end of the container constructed in-place." },
        new() { DisplayText = "pop_back", InsertionText = "pop_back();", Kind = CompletionItemKind.Method, Priority = 880, Signature = "void pop_back()", Documentation = "Removes the last element of the container." },
        new() { DisplayText = "begin", InsertionText = "begin()", Kind = CompletionItemKind.Method, Priority = 880, Signature = "iterator begin() noexcept", Documentation = "Returns an iterator to the first element." },
        new() { DisplayText = "end", InsertionText = "end()", Kind = CompletionItemKind.Method, Priority = 880, Signature = "iterator end() noexcept", Documentation = "Returns an iterator to the element following the last element." },
        new() { DisplayText = "clear", InsertionText = "clear();", Kind = CompletionItemKind.Method, Priority = 870, Signature = "void clear() noexcept", Documentation = "Erases all elements from the container." },
        new() { DisplayText = "data", InsertionText = "data()", Kind = CompletionItemKind.Method, Priority = 870, Signature = "T* data() noexcept", Documentation = "Returns pointer to the underlying array serving as element storage." },
        new() { DisplayText = "c_str", InsertionText = "c_str()", Kind = CompletionItemKind.Method, Priority = 870, Signature = "const CharT* c_str() const noexcept", Documentation = "Returns a pointer to a null-terminated character array with data equivalent to those stored in the string." },
        new() { DisplayText = "length", InsertionText = "length()", Kind = CompletionItemKind.Method, Priority = 860, Signature = "size_t length() const noexcept", Documentation = "Returns the number of characters in the string." },
        new() { DisplayText = "find", InsertionText = "find($0)", Kind = CompletionItemKind.Method, Priority = 860, Signature = "iterator find(const Key& key)", Documentation = "Finds element with specific key." },
        new() { DisplayText = "insert", InsertionText = "insert($0)", Kind = CompletionItemKind.Method, Priority = 850, Signature = "iterator insert(const_iterator pos, const T& value)", Documentation = "Inserts elements into container." },
        new() { DisplayText = "erase", InsertionText = "erase($0)", Kind = CompletionItemKind.Method, Priority = 850, Signature = "iterator erase(const_iterator pos)", Documentation = "Erases elements from container." },
        new() { DisplayText = "front", InsertionText = "front()", Kind = CompletionItemKind.Method, Priority = 850, Signature = "reference front()", Documentation = "Accesses the first element." },
        new() { DisplayText = "back", InsertionText = "back()", Kind = CompletionItemKind.Method, Priority = 850, Signature = "reference back()", Documentation = "Accesses the last element." },
        new() { DisplayText = "first", InsertionText = "first", Kind = CompletionItemKind.Field, Priority = 840, Signature = "T1 first", Documentation = "The first value stored in a std::pair." },
        new() { DisplayText = "second", InsertionText = "second", Kind = CompletionItemKind.Field, Priority = 840, Signature = "T2 second", Documentation = "The second value stored in a std::pair." },
        new() { DisplayText = "reset", InsertionText = "reset();", Kind = CompletionItemKind.Method, Priority = 830, Signature = "void reset() noexcept", Documentation = "Replaces the managed object in smart pointers or resets optional." },
        new() { DisplayText = "get", InsertionText = "get()", Kind = CompletionItemKind.Method, Priority = 830, Signature = "T* get() const noexcept", Documentation = "Returns a pointer to the managed object." }
    ];

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

        // Find current word boundary
        int wordStart = caretOffset;
        while (wordStart > 0 && (char.IsLetterOrDigit(code[wordStart - 1]) || code[wordStart - 1] == '_'))
        {
            wordStart--;
        }

        var prefix = caretOffset > wordStart ? code.Substring(wordStart, caretOffset - wordStart) : string.Empty;

        // Check for scope resolution (::)
        if (wordStart >= 2 && code[wordStart - 1] == ':' && code[wordStart - 2] == ':')
        {
            return Task.FromResult(GetScopeResolutionCompletions(code, wordStart - 2, prefix));
        }

        // Check for arrow pointer access (->)
        if (wordStart >= 2 && code[wordStart - 1] == '>' && code[wordStart - 2] == '-')
        {
            return Task.FromResult(GetMemberAccessCompletions(prefix));
        }

        // Check for dot member access (.)
        if (wordStart >= 1 && code[wordStart - 1] == '.')
        {
            return Task.FromResult(GetMemberAccessCompletions(prefix));
        }

        return Task.FromResult(GetGeneralCompletions(code, prefix));
    }

    private static IReadOnlyList<CSharpCompletionItem> GetMemberAccessCompletions(string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return CommonMemberCompletions;
        }

        return CommonMemberCompletions
            .Where(m => m.DisplayText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static IReadOnlyList<CSharpCompletionItem> GetScopeResolutionCompletions(string code, int colonOffset, string prefix)
    {
        // Extract qualifier before ::
        int qualEnd = colonOffset;
        while (qualEnd > 0 && char.IsWhiteSpace(code[qualEnd - 1])) qualEnd--;

        int qualStart = qualEnd;
        while (qualStart > 0 && (char.IsLetterOrDigit(code[qualStart - 1]) || code[qualStart - 1] == '_' || code[qualStart - 1] == ':'))
        {
            qualStart--;
        }

        var qualifier = qualStart < qualEnd ? code.Substring(qualStart, qualEnd - qualStart).Trim() : string.Empty;

        var items = new List<CSharpCompletionItem>();

        // If qualifier is std or empty, populate standard library symbols from CppQuickInfoProvider database
        if (qualifier.Equals("std", StringComparison.Ordinal) || qualifier.EndsWith("::std", StringComparison.Ordinal))
        {
            // Populate common std members
            AddStdCompletions(items, prefix);
        }

        return items;
    }

    private static void AddStdCompletions(List<CSharpCompletionItem> items, string prefix)
    {
        var commonStd = new (string Name, string Sig, string Header, string Desc)[]
        {
            ("cout", "extern ostream cout;", "<iostream>", "Standard output stream."),
            ("cin", "extern istream cin;", "<iostream>", "Standard input stream."),
            ("cerr", "extern ostream cerr;", "<iostream>", "Standard error stream."),
            ("endl", "template<class CharT, class Traits> basic_ostream<CharT, Traits>& endl(basic_ostream<CharT, Traits>& os);", "<iostream>", "Inserts newline and flushes stream."),
            ("vector", "template<class T, class Allocator = allocator<T>> class vector;", "<vector>", "Sequence container representing a dynamically resizable array."),
            ("string", "typedef basic_string<char> string;", "<string>", "Sequence of characters."),
            ("map", "template<class Key, class T, class Compare = less<Key>> class map;", "<map>", "Sorted associative container that contains key-value pairs with unique keys."),
            ("unordered_map", "template<class Key, class T, class Hash = hash<Key>> class unordered_map;", "<unordered_map>", "Unordered associative container that contains key-value pairs with unique keys."),
            ("set", "template<class Key, class Compare = less<Key>> class set;", "<set>", "Associative container that contains a sorted set of unique objects of type Key."),
            ("unordered_set", "template<class Key, class Hash = hash<Key>> class unordered_set;", "<unordered_set>", "Associative container that contains a set of unique objects."),
            ("pair", "template<class T1, class T2> struct pair;", "<utility>", "Class template providing a way to store two heterogeneous objects as a single unit."),
            ("tuple", "template<class... Types> class tuple;", "<tuple>", "Fixed-size collection of heterogeneous values."),
            ("make_pair", "template<class T1, class T2> constexpr pair<V1, V2> make_pair(T1&& t, T2&& u);", "<utility>", "Creates a std::pair object, deducing target types from the types of arguments."),
            ("make_tuple", "template<class... Types> constexpr tuple<VTypes...> make_tuple(Types&&... args);", "<tuple>", "Creates a std::tuple object, deducing target types from the types of arguments."),
            ("make_shared", "template<class T, class... Args> shared_ptr<T> make_shared(Args&&... args);", "<memory>", "Creates a shared pointer that manages a new object."),
            ("make_unique", "template<class T, class... Args> unique_ptr<T> make_unique(Args&&... args);", "<memory>", "Constructs an object of type T and wraps it in a std::unique_ptr (C++14)."),
            ("shared_ptr", "template<class T> class shared_ptr;", "<memory>", "Retains shared ownership of an object through a pointer."),
            ("unique_ptr", "template<class T, class Deleter = default_delete<T>> class unique_ptr;", "<memory>", "Smart pointer that owns and manages another object through a pointer and disposes of that object when out of scope."),
            ("sort", "template<class RandomIt> void sort(RandomIt first, RandomIt last);", "<algorithm>", "Sorts the elements in the range [first, last) into ascending order."),
            ("min", "template<class T> constexpr const T& min(const T& a, const T& b);", "<algorithm>", "Returns the smaller of given values."),
            ("max", "template<class T> constexpr const T& max(const T& a, const T& b);", "<algorithm>", "Returns the greater of given values."),
            ("move", "template<class T> constexpr remove_reference_t<T>&& move(T&& t) noexcept;", "<utility>", "Indicates that an object may be 'moved from'."),
            ("forward", "template<class T> constexpr T&& forward(remove_reference_t<T>& t) noexcept;", "<utility>", "Forwards lvalues as either lvalues or rvalues, depending on T (perfect forwarding)."),
            ("optional", "template<class T> class optional;", "<optional>", "Manages an optional contained value (C++17)."),
            ("variant", "template<class... Types> class variant;", "<variant>", "Type-safe union (C++17)."),
            ("span", "template<class ElementType, size_t Extent = dynamic_extent> class span;", "<span>", "Non-owning view over a contiguous sequence of objects (C++20)."),
            ("ranges", "namespace ranges { ... }", "<ranges>", "Ranges library algorithms and views (C++20)."),
            ("views", "namespace views { ... }", "<ranges>", "Range adaptors and views (C++20).")
        };

        foreach (var (name, sig, header, desc) in commonStd)
        {
            if (string.IsNullOrEmpty(prefix) || name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                items.Add(new CSharpCompletionItem
                {
                    DisplayText = name,
                    InsertionText = name,
                    Kind = name[0] >= 'a' && name[0] <= 'z' && (name.Contains("make") || name == "sort" || name == "min" || name == "max" || name == "move" || name == "forward")
                        ? CompletionItemKind.Method : CompletionItemKind.Class,
                    ReturnType = header,
                    Signature = sig,
                    Documentation = desc,
                    Priority = 900
                });
            }
        }
    }

    private static IReadOnlyList<CSharpCompletionItem> GetGeneralCompletions(string code, string prefix)
    {
        var results = new List<CSharpCompletionItem>();

        // 1. Snippets
        foreach (var s in Snippets)
        {
            if (string.IsNullOrEmpty(prefix) || s.DisplayText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(s);
            }
        }

        // 2. Keywords
        foreach (var k in Keywords)
        {
            if (string.IsNullOrEmpty(prefix) || k.DisplayText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(k);
            }
        }

        // 3. Common std namespace symbols without std:: prefix
        AddStdCompletions(results, prefix);

        // 4. Document-declared symbols
        var localSymbols = ExtractLocalSymbols(code, prefix);
        results.AddRange(localSymbols);

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
                        Documentation = "Locally declared C++ variable or parameter."
                    });
                }
            }
        }

        foreach (Match m in FunctionRegex().Matches(code))
        {
            var name = m.Groups["name"].Value;
            if (name.Length > 0 && seen.Add(name) && name != "if" && name != "for" && name != "while" && name != "switch")
            {
                if (string.IsNullOrEmpty(prefix) || name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    items.Add(new CSharpCompletionItem
                    {
                        DisplayText = name,
                        InsertionText = $"{name}($0)",
                        Kind = CompletionItemKind.Method,
                        Priority = 875,
                        Documentation = "Locally declared function."
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
                        Kind = CompletionItemKind.Struct,
                        Priority = 870,
                        Documentation = "Locally declared structure, class, or type."
                    });
                }
            }
        }

        return items;
    }
}
