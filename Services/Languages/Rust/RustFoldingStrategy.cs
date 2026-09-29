using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;

/// <summary>
/// Folding for Rust: <c>{ … }</c>, <c>[ … ]</c> and <c>( … )</c> blocks that span lines, <c>/* … */</c> comments (which nest in
/// Rust), multi-line raw strings, and <c>// region: name</c> … <c>// endregion</c> markers. A <c>'</c> is a char literal only
/// when it closes as one (<c>'a'</c>, <c>'\n'</c>); otherwise it starts a lifetime or a loop label (<c>'a</c>, <c>'outer</c>),
/// which must not hide the braces after it.
/// </summary>
public sealed class RustFoldingStrategy : ILanguageFolding
{
    public IEnumerable<NewFolding> CreateFoldings(TextDocument document, out int firstErrorOffset)
    {
        firstErrorOffset = -1;
        var foldings = new List<NewFolding>();
        var text = document.Text;
        if (string.IsNullOrEmpty(text)) return foldings;

        var braces = new Stack<int>();
        var brackets = new Stack<int>();
        var parens = new Stack<int>();
        var regions = new Stack<(int Start, string Name)>();

        var blockDepth = 0;
        var blockStart = -1;

        void Fold(int start, int end, string name)
        {
            if (document.GetLineByOffset(start).LineNumber < document.GetLineByOffset(Math.Min(end, text.Length)).LineNumber)
            {
                foldings.Add(new NewFolding(start, end) { Name = name });
            }
        }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            var next = i + 1 < text.Length ? text[i + 1] : '\0';

            if (blockDepth > 0)
            {
                if (c == '/' && next == '*')
                {
                    blockDepth++;
                    i++;
                }
                else if (c == '*' && next == '/')
                {
                    blockDepth--;
                    i++;
                    if (blockDepth == 0) Fold(blockStart, i + 1, "/* ... */");
                }

                continue;
            }

            if (c == '/' && next == '/')
            {
                var lineEnd = text.IndexOf('\n', i);
                if (lineEnd < 0) lineEnd = text.Length;
                ReadRegionMarker(text[(i + 2)..lineEnd], i, lineEnd, regions, Fold);
                i = lineEnd - 1;
                continue;
            }

            if (c == '/' && next == '*')
            {
                blockDepth = 1;
                blockStart = i;
                i++;
                continue;
            }

            if (c == '"')
            {
                i = SkipString(text, i, document, foldings);
                continue;
            }

            if (c == 'r' && IsRawStringStart(text, i, out var hashes, out var quoteAt))
            {
                i = SkipRawString(text, i, quoteAt, hashes, document, foldings);
                continue;
            }

            if (c == '\'')
            {
                i += CharLiteralLength(text, i) - 1;
                continue;
            }

            switch (c)
            {
                case '{': braces.Push(i); break;
                case '[': brackets.Push(i); break;
                case '(': parens.Push(i); break;
                case '}' when braces.Count > 0: Fold(braces.Pop(), i + 1, "{ ... }"); break;
                case ']' when brackets.Count > 0: Fold(brackets.Pop(), i + 1, "[ ... ]"); break;
                case ')' when parens.Count > 0: Fold(parens.Pop(), i + 1, "( ... )"); break;
            }
        }

        foldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        return foldings;
    }

    // "// region: Name" opens a folded region, "// endregion" closes the latest one.
    private static void ReadRegionMarker(string comment, int commentStart, int lineEnd, Stack<(int Start, string Name)> regions, Action<int, int, string> fold)
    {
        var marker = comment.Trim();
        if (marker.StartsWith("endregion", StringComparison.OrdinalIgnoreCase))
        {
            if (regions.Count > 0)
            {
                var (start, name) = regions.Pop();
                fold(start, lineEnd, name);
            }
        }
        else if (marker.StartsWith("region", StringComparison.OrdinalIgnoreCase) &&
                 (marker.Length == 6 || marker[6] is ':' or ' '))
        {
            var name = marker[6..].TrimStart(':', ' ').Trim();
            regions.Push((commentStart, name.Length > 0 ? name : "region"));
        }
    }

    // Strings may span lines; a backslash hides the next character. Returns the index of the closing quote.
    private static int SkipString(string text, int start, TextDocument document, List<NewFolding> foldings)
    {
        for (var i = start + 1; i < text.Length; i++)
        {
            if (text[i] == '\\') i++;
            else if (text[i] == '"') return i;
        }

        return text.Length;
    }

    // r"…", r#"…"#, br"…", cr#"…"#: the r isn't part of a longer name (an optional b or c may come first).
    private static bool IsRawStringStart(string text, int at, out int hashes, out int quoteAt)
    {
        hashes = 0;
        quoteAt = -1;

        var before = at - 1;
        if (before >= 0 && text[before] is 'b' or 'c') before--;
        if (before >= 0 && (char.IsLetterOrDigit(text[before]) || text[before] == '_')) return false;

        var i = at + 1;
        while (i < text.Length && text[i] == '#') { hashes++; i++; }
        if (i >= text.Length || text[i] != '"') return false;

        quoteAt = i;
        return true;
    }

    private static int SkipRawString(string text, int start, int quoteAt, int hashes, TextDocument document, List<NewFolding> foldings)
    {
        var terminator = "\"" + new string('#', hashes);
        var end = text.IndexOf(terminator, quoteAt + 1, StringComparison.Ordinal);
        if (end < 0) return text.Length;

        var stop = end + terminator.Length;
        if (document.GetLineByOffset(start).LineNumber < document.GetLineByOffset(Math.Min(stop, text.Length)).LineNumber)
        {
            foldings.Add(new NewFolding(start, stop) { Name = "r\"...\"" });
        }

        return stop - 1;
    }

    // 'a' or '\n' or '\u{1F600}' is a char literal; 'a alone is a lifetime or label (length 1: just the quote).
    private static int CharLiteralLength(string text, int at)
    {
        if (at + 1 >= text.Length) return 1;

        if (text[at + 1] == '\\')
        {
            var close = text.IndexOf('\'', at + 2);
            var lineEnd = text.IndexOf('\n', at + 1);
            return close > 0 && (lineEnd < 0 || close < lineEnd) ? close - at + 1 : 1;
        }

        var width = char.IsHighSurrogate(text[at + 1]) && at + 2 < text.Length ? 2 : 1;
        return at + 1 + width < text.Length && text[at + 1 + width] == '\'' && text[at + 1] != '\'' ? width + 2 : 1;
    }
}
