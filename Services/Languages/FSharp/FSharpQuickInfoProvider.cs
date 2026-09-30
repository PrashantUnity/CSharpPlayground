using System.Collections.Frozen;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.FSharp;

/// <summary>
/// Produces hover (Quick Info) documentation for F# symbols, keywords, and core library functions
/// without requiring an external language server.
/// </summary>
public static class FSharpQuickInfoProvider
{
    public sealed record QuickInfo(string Signature, string Summary, string? Module = null);

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

        // Check for module qualification before dot, e.g. List.map
        if (start > 1 && code[start - 1] == '.')
        {
            int modStart = start - 1;
            while (modStart > 0 && IsIdentChar(code[modStart - 1])) modStart--;
            if (modStart < start - 1)
            {
                var qualified = code[modStart..end];
                if (Entries.ContainsKey(qualified)) return qualified;
            }
        }

        return code[start..end];
    }

    private static bool IsIdentChar(char c) =>
        char.IsLetterOrDigit(c) || c == '_' || c == '\'';

    private static Dictionary<string, QuickInfo> BuildEntries()
    {
        var d = new Dictionary<string, QuickInfo>(StringComparer.Ordinal);

        // Core Functions
        d["printfn"] = new("printfn : TextWriterFormat<'T> -> 'T", "Prints to stdout formatted according to the given format specifier, followed by a new line.", "Microsoft.FSharp.Core.ExtraTopLevelOperators");
        d["sprintf"] = new("sprintf : StringFormat<'T> -> 'T", "Prints to a string using the given format specifier.", "Microsoft.FSharp.Core.ExtraTopLevelOperators");
        d["eprintfn"] = new("eprintfn : TextWriterFormat<'T> -> 'T", "Prints to stderr formatted according to the given format specifier, followed by a new line.", "Microsoft.FSharp.Core.ExtraTopLevelOperators");
        d["failwith"] = new("failwith : string -> 'T", "Throws a System.Exception with the given error message.", "Microsoft.FSharp.Core.Operators");

        // List functions
        d["List.map"] = new("List.map : ('T -> 'U) -> 'T list -> 'U list", "Builds a new collection whose elements are the results of applying the given function to each of the elements of the collection.", "Microsoft.FSharp.Collections.List");
        d["List.filter"] = new("List.filter : ('T -> bool) -> 'T list -> 'T list", "Returns a new collection containing only the elements for which the given predicate returns true.", "Microsoft.FSharp.Collections.List");
        d["List.fold"] = new("List.fold : ('State -> 'T -> 'State) -> 'State -> 'T list -> 'State", "Applies a function to each element of the collection, threading an accumulator argument through the computation.", "Microsoft.FSharp.Collections.List");
        d["List.length"] = new("List.length : 'T list -> int", "Returns the number of elements in the list.", "Microsoft.FSharp.Collections.List");
        d["List.head"] = new("List.head : 'T list -> 'T", "Returns the first element of the list.", "Microsoft.FSharp.Collections.List");
        d["List.tail"] = new("List.tail : 'T list -> 'T list", "Returns the list after removing the first element.", "Microsoft.FSharp.Collections.List");
        d["List.rev"] = new("List.rev : 'T list -> 'T list", "Returns a new list with elements in reverse order.", "Microsoft.FSharp.Collections.List");

        // Array functions
        d["Array.map"] = new("Array.map : ('T -> 'U) -> 'T[] -> 'U[]", "Builds a new array whose elements are the results of applying the given function.", "Microsoft.FSharp.Collections.Array");
        d["Array.filter"] = new("Array.filter : ('T -> bool) -> 'T[] -> 'T[]", "Returns a new array containing only the elements for which the given predicate returns true.", "Microsoft.FSharp.Collections.Array");
        d["Array.fold"] = new("Array.fold : ('State -> 'T -> 'State) -> 'State -> 'T[] -> 'State", "Applies a function to each element of the array, threading an accumulator argument.", "Microsoft.FSharp.Collections.Array");
        d["Array.length"] = new("Array.length : 'T[] -> int", "Returns the length of the array.", "Microsoft.FSharp.Collections.Array");

        // Seq functions
        d["Seq.map"] = new("Seq.map : ('T -> 'U) -> seq<'T> -> seq<'U>", "Applies a function to each element of the sequence, yielding a new sequence.", "Microsoft.FSharp.Collections.Seq");
        d["Seq.filter"] = new("Seq.filter : ('T -> bool) -> seq<'T> -> seq<'T>", "Filters a sequence based on a predicate.", "Microsoft.FSharp.Collections.Seq");
        d["Seq.fold"] = new("Seq.fold : ('State -> 'T -> 'State) -> 'State -> seq<'T> -> 'State", "Applies a function to each element of the sequence, threading an accumulator.", "Microsoft.FSharp.Collections.Seq");
        d["Seq.toList"] = new("Seq.toList : seq<'T> -> 'T list", "Builds a list from the given sequence.", "Microsoft.FSharp.Collections.Seq");
        d["Seq.toArray"] = new("Seq.toArray : seq<'T> -> 'T[]", "Builds an array from the given sequence.", "Microsoft.FSharp.Collections.Seq");

        // Display
        d["Display.Dump"] = new("Display.Dump : obj -> unit", "Serializes value to JSON and renders it in Results (.DUMP) deck.", "Fry.Display");
        d["Display.Html"] = new("Display.Html : string -> unit", "Renders arbitrary HTML in Results (.DUMP) deck.", "Fry.Display");
        d["Display.Image"] = new("Display.Image : string -> unit", "Displays image from file path or base64 in Results.", "Fry.Display");
        d["Display.Json"] = new("Display.Json : string -> unit", "Renders JSON string in Results.", "Fry.Display");

        // Keywords
        d["let"] = new("let [rec] <name> [args] = <expr>", "Binds a value or function to an identifier.", "F# Keyword");
        d["match"] = new("match <expr> with | <pattern> -> <result>", "Performs pattern matching on an expression.", "F# Keyword");
        d["type"] = new("type <name> = ...", "Declares a record, discriminated union, class, interface, or type abbreviation.", "F# Keyword");
        d["open"] = new("open <namespace-or-module>", "Imports declarations from a namespace or module into the current scope.", "F# Keyword");

        return d;
    }
}
