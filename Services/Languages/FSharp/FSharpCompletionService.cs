using System.Text.RegularExpressions;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.FSharp;

/// <summary>
/// Provides code completion suggestions for F# files and notebook cells:
/// module members after dot (List., Array., Seq., String., Option., Result., Display.),
/// keywords, core functions, and local declarations.
/// </summary>
public sealed partial class FSharpCompletionService : ILanguageCompletionService
{
    [GeneratedRegex(@"\blet\s+(?:rec\s+)?(?<name>[a-zA-Z_][a-zA-Z0-9_']*)", RegexOptions.Multiline)]
    private static partial Regex LetBindingRegex();

    [GeneratedRegex(@"\btype\s+(?<name>[a-zA-Z_][a-zA-Z0-9_']*)", RegexOptions.Multiline)]
    private static partial Regex TypeBindingRegex();

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
        while (wordStart > 0 && (char.IsLetterOrDigit(code[wordStart - 1]) || code[wordStart - 1] == '_' || code[wordStart - 1] == '\''))
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

        var prefix = code[wordStart..caretOffset];
        return Task.FromResult(GetGeneralCompletions(code, prefix));
    }

    private static IReadOnlyList<CSharpCompletionItem> GetMemberCompletions(string code, int dotOffset, int wordStart, int caretOffset)
    {
        int targetStart = dotOffset;
        while (targetStart > 0 && (char.IsLetterOrDigit(code[targetStart - 1]) || code[targetStart - 1] == '_'))
        {
            targetStart--;
        }

        var moduleName = code[targetStart..dotOffset].Trim();
        var prefix = code[wordStart..caretOffset];
        var list = new List<CSharpCompletionItem>();

        switch (moduleName)
        {
            case "List":
                Add(list, prefix, "map", "mapping -> list -> 'T list", "Builds a new collection whose elements are the results of applying the given function.", CompletionItemKind.Method);
                Add(list, prefix, "filter", "predicate -> list -> 'T list", "Returns a new collection containing only the elements for which the given predicate returns true.", CompletionItemKind.Method);
                Add(list, prefix, "fold", "folder -> state -> list -> 'State", "Applies a function to each element of the collection, threading an accumulator argument.", CompletionItemKind.Method);
                Add(list, prefix, "foldBack", "folder -> list -> state -> 'State", "Applies a function to each element of the collection, threading an accumulator argument from right to left.", CompletionItemKind.Method);
                Add(list, prefix, "length", "list -> int", "Returns the number of elements in the list.", CompletionItemKind.Method);
                Add(list, prefix, "head", "list -> 'T", "Returns the first element of the list.", CompletionItemKind.Method);
                Add(list, prefix, "tail", "list -> 'T list", "Returns the list after removing the first element.", CompletionItemKind.Method);
                Add(list, prefix, "isEmpty", "list -> bool", "Returns true if the list contains no elements.", CompletionItemKind.Method);
                Add(list, prefix, "iter", "action -> list -> unit", "Applies the given function to each element of the list.", CompletionItemKind.Method);
                Add(list, prefix, "collect", "mapping -> list -> 'U list", "Applies the given function to each element of the list and concatenates the resulting lists.", CompletionItemKind.Method);
                Add(list, prefix, "choose", "chooser -> list -> 'U list", "Applies the given function to each element of the list and returns a list comprised of results for each element where the function returns Some.", CompletionItemKind.Method);
                Add(list, prefix, "zip", "list1 -> list2 -> ('T1 * 'T2) list", "Combines the two lists into a list of pairs.", CompletionItemKind.Method);
                Add(list, prefix, "sort", "list -> 'T list", "Sorts the given list using Operators.compare.", CompletionItemKind.Method);
                Add(list, prefix, "sortBy", "projection -> list -> 'T list", "Sorts the given list using keys given by the supplied projection.", CompletionItemKind.Method);
                Add(list, prefix, "rev", "list -> 'T list", "Returns a new list with the elements in reverse order.", CompletionItemKind.Method);
                Add(list, prefix, "distinct", "list -> 'T list", "Returns a list that contains no duplicate entries according to generic hash and equality comparisons.", CompletionItemKind.Method);
                break;

            case "Array":
                Add(list, prefix, "map", "mapping -> array -> 'T[]", "Builds a new array whose elements are the results of applying the given function.", CompletionItemKind.Method);
                Add(list, prefix, "filter", "predicate -> array -> 'T[]", "Returns a new array containing only the elements for which the given predicate returns true.", CompletionItemKind.Method);
                Add(list, prefix, "fold", "folder -> state -> array -> 'State", "Applies a function to each element of the array, threading an accumulator argument.", CompletionItemKind.Method);
                Add(list, prefix, "length", "array -> int", "Returns the length of the array.", CompletionItemKind.Method);
                Add(list, prefix, "create", "count -> value -> 'T[]", "Creates an array whose elements are all initially the given value.", CompletionItemKind.Method);
                Add(list, prefix, "zeroCreate", "count -> 'T[]", "Creates an array where the entries are initially the default value.", CompletionItemKind.Method);
                Add(list, prefix, "init", "count -> initializer -> 'T[]", "Creates an array given the dimension and a generator function.", CompletionItemKind.Method);
                Add(list, prefix, "iter", "action -> array -> unit", "Applies the given function to each element of the array.", CompletionItemKind.Method);
                Add(list, prefix, "sort", "array -> 'T[]", "Sorts the elements of an array in place using generic comparison.", CompletionItemKind.Method);
                Add(list, prefix, "sortBy", "projection -> array -> 'T[]", "Sorts the elements of an array in place using keys given by the supplied projection.", CompletionItemKind.Method);
                break;

            case "Seq":
                Add(list, prefix, "map", "mapping -> seq -> seq<'U>", "Applies a function to each element of the sequence, yielding a new sequence.", CompletionItemKind.Method);
                Add(list, prefix, "filter", "predicate -> seq -> seq<'T>", "Filters a sequence based on a predicate.", CompletionItemKind.Method);
                Add(list, prefix, "fold", "folder -> state -> seq -> 'State", "Applies a function to each element of the sequence, threading an accumulator.", CompletionItemKind.Method);
                Add(list, prefix, "take", "count -> seq -> seq<'T>", "Yields a sequence with up to a specified number of contiguous elements.", CompletionItemKind.Method);
                Add(list, prefix, "skip", "count -> seq -> seq<'T>", "Skips a specified number of elements and then yields the remaining elements.", CompletionItemKind.Method);
                Add(list, prefix, "toList", "seq -> 'T list", "Builds a list from the given sequence.", CompletionItemKind.Method);
                Add(list, prefix, "toArray", "seq -> 'T[]", "Builds an array from the given sequence.", CompletionItemKind.Method);
                Add(list, prefix, "head", "seq -> 'T", "Returns the first element of the sequence.", CompletionItemKind.Method);
                break;

            case "Option":
                Add(list, prefix, "map", "mapping -> option -> 'U option", "Evaluates an option value by applying a function if it is Some.", CompletionItemKind.Method);
                Add(list, prefix, "bind", "binder -> option -> 'U option", "Applies a function that evaluates to an option if the input is Some.", CompletionItemKind.Method);
                Add(list, prefix, "isSome", "option -> bool", "Returns true if the option is Some.", CompletionItemKind.Method);
                Add(list, prefix, "isNone", "option -> bool", "Returns true if the option is None.", CompletionItemKind.Method);
                Add(list, prefix, "get", "option -> 'T", "Gets the value of the option if Some, otherwise raises ArgumentException.", CompletionItemKind.Method);
                Add(list, prefix, "defaultValue", "value -> option -> 'T", "Gets the value of the option if Some, otherwise returns the specified default.", CompletionItemKind.Method);
                break;

            case "Result":
                Add(list, prefix, "map", "mapping -> result -> Result<'T2, 'TError>", "Applies a function to the value inside Ok.", CompletionItemKind.Method);
                Add(list, prefix, "mapError", "mapping -> result -> Result<'T, 'TError2>", "Applies a function to the error inside Error.", CompletionItemKind.Method);
                Add(list, prefix, "bind", "binder -> result -> Result<'T2, 'TError>", "Applies a function returning a Result if the input is Ok.", CompletionItemKind.Method);
                Add(list, prefix, "isOk", "result -> bool", "Returns true if the result is Ok.", CompletionItemKind.Method);
                Add(list, prefix, "isError", "result -> bool", "Returns true if the result is Error.", CompletionItemKind.Method);
                break;

            case "Display":
                Add(list, prefix, "Dump", "Display.Dump(value: obj) -> unit", "Serializes value to JSON and renders it in Results (.DUMP).", CompletionItemKind.Method);
                Add(list, prefix, "Html", "Display.Html(htmlContent: string) -> unit", "Renders arbitrary HTML in Results (.DUMP).", CompletionItemKind.Method);
                Add(list, prefix, "Image", "Display.Image(pathOrBase64: string) -> unit", "Displays image from file path or base64 in Results.", CompletionItemKind.Method);
                Add(list, prefix, "Json", "Display.Json(jsonString: string) -> unit", "Renders JSON string in Results.", CompletionItemKind.Method);
                break;
        }

        return list;
    }

    private static IReadOnlyList<CSharpCompletionItem> GetGeneralCompletions(string code, string prefix)
    {
        var list = new List<CSharpCompletionItem>();

        // Keywords
        var keywords = new[]
        {
            "let", "rec", "mutable", "type", "open", "module", "namespace",
            "match", "with", "if", "then", "else", "elif", "for", "in", "to",
            "do", "while", "yield", "return", "try", "with", "finally",
            "fun", "function", "async", "task", "member", "static", "interface",
            "abstract", "default", "override", "new", "inline", "lazy"
        };
        foreach (var kw in keywords) Add(list, prefix, kw, "keyword", $"F# keyword: {kw}", CompletionItemKind.Keyword);

        // Common core functions & types
        Add(list, prefix, "printfn", "format -> ... -> unit", "Prints formatted text to standard output followed by newline.", CompletionItemKind.Method);
        Add(list, prefix, "sprintf", "format -> ... -> string", "Formats a string using type-safe format specifiers.", CompletionItemKind.Method);
        Add(list, prefix, "eprintfn", "format -> ... -> unit", "Prints formatted text to standard error followed by newline.", CompletionItemKind.Method);
        Add(list, prefix, "failwith", "message -> 'a", "Raises a Failure exception with given message.", CompletionItemKind.Method);
        Add(list, prefix, "Some", "'T -> 'T option", "Constructs an option value containing Some x.", CompletionItemKind.Method);
        Add(list, prefix, "None", "'T option", "The option value representing absence of value.", CompletionItemKind.Method);
        Add(list, prefix, "Ok", "'T -> Result<'T, 'TError>", "Constructs a successful Result value.", CompletionItemKind.Method);
        Add(list, prefix, "Error", "'TError -> Result<'T, 'TError>", "Constructs a failed Result value.", CompletionItemKind.Method);

        // Modules
        Add(list, prefix, "List", "module Microsoft.FSharp.Collections.List", "Operations on immutable linked lists.", CompletionItemKind.Class);
        Add(list, prefix, "Array", "module Microsoft.FSharp.Collections.Array", "Operations on fixed-size zero-indexed mutable arrays.", CompletionItemKind.Class);
        Add(list, prefix, "Seq", "module Microsoft.FSharp.Collections.Seq", "Operations on lazy enumerations (IEnumerable<'T>).", CompletionItemKind.Class);
        Add(list, prefix, "Option", "module Microsoft.FSharp.Core.Option", "Operations on option values.", CompletionItemKind.Class);
        Add(list, prefix, "Result", "module Microsoft.FSharp.Core.Result", "Operations on Result<'T, 'TError> values.", CompletionItemKind.Class);
        Add(list, prefix, "Display", "module Fry.Display", "FrySharp visual inspector deck helpers.", CompletionItemKind.Class);

        // Parse declarations in document
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in LetBindingRegex().Matches(code))
        {
            var name = m.Groups["name"].Value;
            if (seen.Add(name)) Add(list, prefix, name, "binding", $"Local F# binding: {name}", CompletionItemKind.Variable);
        }
        foreach (Match m in TypeBindingRegex().Matches(code))
        {
            var name = m.Groups["name"].Value;
            if (seen.Add(name)) Add(list, prefix, name, "type", $"Local F# type: {name}", CompletionItemKind.Class);
        }

        return list;
    }

    private static void Add(List<CSharpCompletionItem> list, string prefix, string text, string signature, string doc, CompletionItemKind kind)
    {
        if (!string.IsNullOrEmpty(prefix) && !text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        list.Add(new CSharpCompletionItem
        {
            DisplayText = text,
            InsertionText = text,
            Signature = signature,
            Documentation = doc,
            Kind = kind
        });
    }
}
