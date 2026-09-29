using System.Collections.Frozen;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;

/// <summary>
/// Hover (Quick Info) text for Rust keywords, macros, standard-library types, traits, functions and methods, without a
/// language server. <c>println!</c>, <c>HashMap</c>, <c>String::new</c>, <c>std::mem::swap</c> and a bare method name such
/// as <c>unwrap</c> are all recognised.
/// </summary>
public static class RustQuickInfoProvider
{
    public sealed record QuickInfo(string Signature, string Summary, string? Owner = null, RustSymbolKind Kind = RustSymbolKind.Function);

    /// <summary>The symbol under the pointer: its text as looked up, and the span of the code it covers.</summary>
    public sealed record SymbolAt(string Text, int Start, int Length);

    private static readonly FrozenDictionary<string, QuickInfo> Entries = BuildEntries().ToFrozenDictionary(StringComparer.Ordinal);

    public static QuickInfo? Lookup(string symbol) =>
        !string.IsNullOrWhiteSpace(symbol) && Entries.TryGetValue(symbol, out var info) ? info : null;

    public static SymbolAt? ExtractSymbol(string code, int position)
    {
        if (string.IsNullOrEmpty(code) || position < 0 || position > code.Length) return null;
        if (position == code.Length) position--;
        if (position < 0) return null;

        var start = position;
        while (start > 0 && IsIdentChar(code[start - 1])) start--;
        var end = position;
        while (end < code.Length && IsIdentChar(code[end])) end++;
        if (start >= end) return null;

        // println!( … ): the ! belongs to the name.
        if (end < code.Length && code[end] == '!' && IsMacroCall(code, end + 1))
        {
            var macro = code[start..(end + 1)];
            return new SymbolAt(macro, start, macro.Length);
        }

        // Type::function and std::mem::swap: try the longest path the table knows, then Type::name.
        var segmentStarts = new List<int> { start };
        var cursor = start;
        while (cursor >= 3 && code[cursor - 1] == ':' && code[cursor - 2] == ':' && IsIdentChar(code[cursor - 3]))
        {
            var segment = cursor - 3;
            while (segment > 0 && IsIdentChar(code[segment - 1])) segment--;
            segmentStarts.Add(segment);
            cursor = segment;
        }

        foreach (var from in segmentStarts.Skip(1).Reverse())
        {
            var path = code[from..end];
            if (Entries.ContainsKey(path)) return new SymbolAt(path, from, path.Length);
        }

        return new SymbolAt(code[start..end], start, end - start);
    }

    private static bool IsMacroCall(string code, int from)
    {
        var i = from;
        while (i < code.Length && (code[i] == ' ' || code[i] == '\t')) i++;
        return i < code.Length && code[i] is '(' or '[' or '{';
    }

    private static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static IEnumerable<KeyValuePair<string, QuickInfo>> BuildEntries()
    {
        var entries = new Dictionary<string, QuickInfo>(StringComparer.Ordinal);

        // A name that is both a keyword and, say, a method keeps the language meaning; methods fill in what is left.
        void Add(RustSymbol symbol, string? key = null)
        {
            var name = key ?? symbol.Name;
            entries.TryAdd(name, new QuickInfo(symbol.Signature, symbol.Summary, symbol.Owner, symbol.Kind));
        }

        foreach (var symbol in RustStandardLibrary.Keywords) Add(symbol);
        foreach (var symbol in RustStandardLibrary.Macros) Add(symbol);
        foreach (var symbol in RustStandardLibrary.Types) Add(symbol);
        foreach (var symbol in RustStandardLibrary.Traits) Add(symbol);
        foreach (var symbol in RustStandardLibrary.Functions)
        {
            Add(symbol);
            // std::mem::swap is also known as mem::swap.
            if (symbol.Name.StartsWith("std::", StringComparison.Ordinal)) Add(symbol, symbol.Name["std::".Length..]);
        }

        foreach (var symbol in RustStandardLibrary.Methods) Add(symbol);

        entries["Some"] = new QuickInfo("Some(value)", "An Option that holds a value.", "Option", RustSymbolKind.Variant);
        entries["None"] = new QuickInfo("None", "An Option that holds nothing.", "Option", RustSymbolKind.Variant);
        entries["Ok"] = new QuickInfo("Ok(value)", "A successful Result.", "Result", RustSymbolKind.Variant);
        entries["Err"] = new QuickInfo("Err(error)", "A failed Result.", "Result", RustSymbolKind.Variant);
        return entries;
    }
}
