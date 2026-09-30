using System.Text;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;

/// <summary>What a piece of a notebook cell is.</summary>
internal enum RustChunkKind
{
    /// <summary>An item: <c>fn</c>, <c>struct</c>, <c>use</c>…, which later cells can use too.</summary>
    Item,

    /// <summary>A statement or expression that runs in the cell.</summary>
    Statement,

    /// <summary>An inner attribute such as <c>#![allow(unused)]</c>, which belongs at the top of the program.</summary>
    InnerAttribute
}

/// <summary>One top-level piece of a notebook cell, with where it starts so a compiler message can be put back on the cell's own line.</summary>
/// <param name="Text">From the piece's first character (a comment or attribute before it included) to its last.</param>
/// <param name="StartLine">The 1-based line of the cell it starts on.</param>
/// <param name="StartColumn">The 0-based column it starts at.</param>
/// <param name="ItemKind"><c>fn</c>, <c>struct</c>, <c>enum</c>, <c>trait</c>, <c>impl</c>, <c>use</c>, <c>const</c>, <c>static</c>, <c>type</c>, <c>mod</c>, <c>union</c>, <c>macro_rules</c>, <c>extern crate</c> or <c>extern block</c>; null for anything else.</param>
/// <param name="Name">The name an item declares; null for an <c>impl</c>, a <c>use</c> and the like.</param>
/// <param name="Key">What makes two items the same definition: running a cell again replaces its items rather than repeating them.</param>
/// <param name="EndsWithSemicolon">Whether the last thing in a statement is <c>;</c>; without it the last statement's value is what the cell shows.</param>
/// <param name="StartsWithLet">A <c>let</c> is a statement even without its semicolon, never a value to show.</param>
/// <param name="IsBraceMacro">A macro that takes braces (<c>thread_local! { … }</c>) is a statement of its own, not a value.</param>
internal sealed record RustCellChunk(
    RustChunkKind Kind, string Text, int StartLine, int StartColumn, string? ItemKind, string? Name, string Key,
    bool EndsWithSemicolon, bool StartsWithLet, bool IsBraceMacro)
{
    /// <summary>Whether this piece can be the value a cell shows: the last statement, without a semicolon, that isn't a brace macro.</summary>
    public bool CanBeShownValue => Kind == RustChunkKind.Statement && !EndsWithSemicolon && !IsBraceMacro;
}

/// <summary>Where a macro call at the top of a cell goes when the splitter can't tell what it expands to.</summary>
internal enum MacroPlacement
{
    /// <summary>Into the cell's <c>main</c>, as a statement: right for <c>println!</c>, <c>vec!</c> and every macro that is code to run.</summary>
    Statements,

    /// <summary>Among the items, as a macro can be at the top of a module: right for one that makes an <c>impl</c>, a <c>static</c>, a <c>struct</c>.</summary>
    ItemsWhenUnknown
}

/// <summary>A notebook cell taken apart.</summary>
internal sealed class RustCell
{
    public required IReadOnlyList<RustCellChunk> Chunks { get; init; }

    public IEnumerable<RustCellChunk> Items => Chunks.Where(c => c.Kind == RustChunkKind.Item);

    public IEnumerable<RustCellChunk> Statements => Chunks.Where(c => c.Kind == RustChunkKind.Statement);

    public IEnumerable<RustCellChunk> InnerAttributes => Chunks.Where(c => c.Kind == RustChunkKind.InnerAttribute);

    /// <summary>
    /// Some macro call was placed among the items only because it might expand to items. If the cell then doesn't build, the caller
    /// splits it again with <see cref="MacroPlacement.Statements"/>.
    /// </summary>
    public bool HasUncertainMacros => Chunks.Any(c => c.ItemKind == "macro call");

    /// <summary>The cell is a whole program: it declares its own <c>fn main</c>.</summary>
    public bool DefinesMain => Items.Any(i => i.ItemKind == "fn" && i.Name == "main");
}

/// <summary>
/// Cuts a Rust notebook cell into its top-level pieces: items (which later cells keep using) and statements (which run once),
/// without parsing the language. A small scanner knows strings, raw strings, characters versus lifetimes and nested comments,
/// so a brace or semicolon inside one is never taken for the end of a piece.
/// </summary>
internal static class RustCellSplitter
{
    public static RustCell Split(string? source, MacroPlacement macros = MacroPlacement.Statements)
    {
        var text = (source ?? string.Empty).Replace("\r\n", "\n");
        var tokens = RustLexer.Scan(text);
        var significant = tokens.Where(t => t.Kind != RustTokenKind.LineComment && t.Kind != RustTokenKind.BlockComment).ToList();
        var lineStarts = LineStarts(text);
        var chunks = new List<RustCellChunk>();
        var previousEnd = 0;
        var i = 0;

        char Punct(int index) => index < significant.Count && significant[index].Kind == RustTokenKind.Punct ? text[significant[index].Start] : '\0';

        while (i < significant.Count)
        {
            // The piece starts at the first thing after the last one: a comment before an item belongs to it.
            var startOffset = tokens.First(t => t.Start >= previousEnd).Start;
            var classification = Classify(significant, text, i, macros);

            int last;
            RustChunkKind kind;
            if (classification.InnerAttributeEnd is { } attributeEnd)
            {
                kind = RustChunkKind.InnerAttribute;
                last = attributeEnd;
            }
            else
            {
                kind = classification.ItemKind != null ? RustChunkKind.Item : RustChunkKind.Statement;
                last = FindEnd(significant, text, i, classification);
            }

            var endOffset = significant[last].End;
            var chunkText = text.Substring(startOffset, endOffset - startOffset);
            var (line, column) = Position(lineStarts, startOffset);
            var endsWithSemicolon = Punct(last) == ';';
            var startsWithLet = classification.FirstKeyword == "let";
            var key = classification.ItemKind != null ? KeyOf(classification, chunkText) : string.Empty;
            chunks.Add(new RustCellChunk(kind, chunkText, line, column, classification.ItemKind, classification.Name, key, endsWithSemicolon, startsWithLet, classification.IsBraceMacro));

            previousEnd = endOffset;
            i = last + 1;
        }

        return new RustCell { Chunks = chunks };
    }

    // Macros that are always code to run or a value to use, never items.
    private static readonly HashSet<string> StatementMacros = new(StringComparer.Ordinal)
    {
        "println", "print", "eprintln", "eprint", "dbg", "assert", "assert_eq", "assert_ne", "debug_assert", "debug_assert_eq",
        "debug_assert_ne", "vec", "format", "format_args", "write", "writeln", "panic", "unreachable", "todo", "unimplemented",
        "matches", "concat", "stringify", "env", "option_env", "line", "column", "file", "module_path", "cfg", "include_str",
        "include_bytes", "compile_error", "dump", "table", "share", "json", "html", "image"
    };

    // Words that can be followed by ! and a bracket without being a macro: `if !(a && b) {`.
    private static readonly HashSet<string> NotAMacroName = new(StringComparer.Ordinal)
    {
        "if", "while", "match", "return", "break", "continue", "for", "loop", "let", "in", "else", "unsafe", "move", "async", "await",
        "yield", "mut", "ref", "as", "where", "impl", "dyn"
    };

    private sealed record Classification(string? ItemKind, string? Name, int FirstKeywordIndex, string FirstKeyword, bool BracedItem, bool BlockLikeStatement, bool IsBraceMacro, int? InnerAttributeEnd);

    private static Classification Classify(List<RustToken> tokens, string text, int start, MacroPlacement macros)
    {
        string Word(int index) => index < tokens.Count && tokens[index].Kind == RustTokenKind.Ident ? text.Substring(tokens[index].Start, tokens[index].Length) : string.Empty;
        char Punct(int index) => index < tokens.Count && tokens[index].Kind == RustTokenKind.Punct ? text[tokens[index].Start] : '\0';

        var j = start;

        // Attributes: #![inner] stands alone; #[outer] belongs to whatever follows.
        while (Punct(j) == '#')
        {
            if (Punct(j + 1) == '!' && Punct(j + 2) == '[')
            {
                var close = MatchingClose(tokens, text, j + 2);
                return new Classification(null, null, j, "#!", false, false, false, close);
            }

            if (Punct(j + 1) != '[') break;
            j = MatchingClose(tokens, text, j + 1) + 1;
        }

        // Visibility: pub, pub(crate), pub(in path)
        if (Word(j) == "pub")
        {
            j++;
            if (Punct(j) == '(') j = MatchingClose(tokens, text, j) + 1;
        }

        var first = Word(j);
        string? kind = null;
        string? name = null;
        var braced = false;

        switch (first)
        {
            case "fn":
                (kind, name, braced) = ("fn", Word(j + 1), true);
                break;
            case "struct" or "enum" or "trait" or "mod":
                (kind, name, braced) = (first, Word(j + 1), true);
                break;
            case "union" when Word(j + 1).Length > 0 && Punct(j + 2) is '{' or '<':
                (kind, name, braced) = ("union", Word(j + 1), true);
                break;
            case "impl":
                (kind, braced) = ("impl", true);
                break;
            case "use":
                kind = "use";
                break;
            case "type" when Word(j + 1).Length > 0:
                (kind, name) = ("type", Word(j + 1));
                break;
            case "static":
                {
                    var n = Word(j + 1) == "mut" ? j + 2 : j + 1;
                    if (Word(n).Length > 0 && Punct(n + 1) == ':') (kind, name) = ("static", Word(n));
                    break;
                }
            case "macro_rules" when Punct(j + 1) == '!':
                (kind, name, braced) = ("macro_rules", Word(j + 2), true);
                break;
            case "const":
                {
                    var n = j + 1;
                    if (Word(n) is "fn" or "unsafe" or "async" or "extern")
                    {
                        (kind, name, braced) = QualifiedFn(tokens, text, n);
                    }
                    else if (Word(n).Length > 0 && Punct(n + 1) == ':')
                    {
                        (kind, name) = ("const", Word(n));
                    }

                    break;
                }
            case "unsafe" when Word(j + 1) is "fn" or "impl" or "trait" or "extern" or "async":
                (kind, name, braced) = QualifiedItem(tokens, text, j + 1);
                break;
            case "async" when Word(j + 1) is "fn" or "unsafe":
                (kind, name, braced) = QualifiedFn(tokens, text, j + 1);
                break;
            case "extern":
                (kind, name, braced) = QualifiedItem(tokens, text, j);
                break;
        }

        var blockLike = kind == null && (first is "if" or "match" or "for" or "while" or "loop"
            || (first == "unsafe" && Punct(j + 1) == '{') || Punct(j) == '{'
            || (tokens.Count > j + 1 && tokens[j].Kind == RustTokenKind.Lifetime && Punct(j + 1) == ':'));

        // path::name!(…);  path::name![…];  path::name!{…}: a macro call. One with braces ends where its braces do, like a block.
        var macroCall = false;
        var macroBraces = false;
        var macroName = string.Empty;
        if (kind == null && first.Length > 0 && !NotAMacroName.Contains(first))
        {
            var k = j + 1;
            macroName = first;
            while (Punct(k) == ':' && Punct(k + 1) == ':' && Word(k + 2).Length > 0)
            {
                macroName = Word(k + 2);
                k += 3;
            }

            if (Punct(k) == '!' && Punct(k + 1) is '(' or '[' or '{')
            {
                macroCall = true;
                macroBraces = Punct(k + 1) == '{';
            }
        }

        // Something that makes items (an impl for each of a list of types, a thread_local static) has to be one, or the cell's main
        // would keep them and no later cell would see them. What isn't known to be code to run is tried as an item.
        if (macroCall && macros == MacroPlacement.ItemsWhenUnknown && !StatementMacros.Contains(macroName))
        {
            return new Classification("macro call", null, j, first, macroBraces, false, false, null);
        }

        var braceMacro = macroCall && macroBraces;

        return new Classification(kind, name, j, first, braced, blockLike, braceMacro, null);
    }

    // extern crate x;  extern "C" fn f() {}  extern "C" { ... }  unsafe impl X {}  unsafe extern "C" { ... }
    private static (string? Kind, string? Name, bool Braced) QualifiedItem(List<RustToken> tokens, string text, int at)
    {
        string Word(int index) => index < tokens.Count && tokens[index].Kind == RustTokenKind.Ident ? text.Substring(tokens[index].Start, tokens[index].Length) : string.Empty;
        char Punct(int index) => index < tokens.Count && tokens[index].Kind == RustTokenKind.Punct ? text[tokens[index].Start] : '\0';

        switch (Word(at))
        {
            case "fn" or "async":
                return QualifiedFn(tokens, text, at);
            case "impl":
                return ("impl", null, true);
            case "trait":
                return ("trait", Word(at + 1), true);
            case "extern":
                if (Word(at + 1) == "crate") return ("extern crate", Word(at + 2), false);
                var n = at + 1;
                if (n < tokens.Count && tokens[n].Kind == RustTokenKind.String) n++;
                if (Word(n) is "fn" or "unsafe" or "async") return QualifiedFn(tokens, text, n);
                return Punct(n) == '{' ? ("extern block", null, true) : (null, null, false);
            default:
                return (null, null, false);
        }
    }

    private static (string? Kind, string? Name, bool Braced) QualifiedFn(List<RustToken> tokens, string text, int at)
    {
        // The name is the identifier after the first `fn` among the qualifiers (const, async, unsafe, extern "C").
        for (var n = at; n < tokens.Count && n < at + 6; n++)
        {
            if (tokens[n].Kind == RustTokenKind.Ident && text.AsSpan(tokens[n].Start, tokens[n].Length).SequenceEqual("fn"))
            {
                var name = n + 1 < tokens.Count && tokens[n + 1].Kind == RustTokenKind.Ident ? text.Substring(tokens[n + 1].Start, tokens[n + 1].Length) : null;
                return ("fn", name, true);
            }
        }

        return (null, null, false);
    }

    // Where a piece ends: the semicolon that ends a statement or a declaration, or the brace that closes a block or an item's body.
    private static int FindEnd(List<RustToken> tokens, string text, int start, Classification c)
    {
        char Punct(int index) => index < tokens.Count && tokens[index].Kind == RustTokenKind.Punct ? text[tokens[index].Start] : '\0';
        string Word(int index) => index < tokens.Count && tokens[index].Kind == RustTokenKind.Ident ? text.Substring(tokens[index].Start, tokens[index].Length) : string.Empty;

        var endsAtBrace = c.BracedItem || c.BlockLikeStatement || c.IsBraceMacro;
        var depth = 0;
        for (var k = start; k < tokens.Count; k++)
        {
            var p = Punct(k);
            if (p is '(' or '[' or '{')
            {
                depth++;
            }
            else if (p is ')' or ']' or '}')
            {
                depth = Math.Max(0, depth - 1);
                if (p == '}' && depth == 0 && endsAtBrace)
                {
                    // if … else …: the chain goes on after the brace.
                    if (c.BlockLikeStatement && Word(k + 1) == "else") continue;
                    return k;
                }
            }
            else if (p == ';' && depth == 0)
            {
                return k;
            }
        }

        return tokens.Count - 1;
    }

    private static int MatchingClose(List<RustToken> tokens, string text, int open)
    {
        var depth = 0;
        for (var k = open; k < tokens.Count; k++)
        {
            if (tokens[k].Kind != RustTokenKind.Punct) continue;
            var p = text[tokens[k].Start];
            if (p is '(' or '[' or '{') depth++;
            else if (p is ')' or ']' or '}' && --depth == 0) return k;
        }

        return tokens.Count - 1;
    }

    private static string KeyOf(Classification c, string chunkText)
    {
        var collapsed = string.Join(' ', chunkText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return c.ItemKind switch
        {
            "struct" or "enum" or "trait" or "union" or "type" when c.Name != null => "type:" + c.Name,
            "fn" or "const" or "static" when c.Name != null => "value:" + c.Name,
            "mod" when c.Name != null => "mod:" + c.Name,
            "macro_rules" when c.Name != null => "macro:" + c.Name,
            "extern crate" when c.Name != null => "crate:" + c.Name,
            _ => c.ItemKind + ":" + collapsed
        };
    }

    private static List<int> LineStarts(string text)
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n') starts.Add(i + 1);
        }

        return starts;
    }

    private static (int Line, int Column) Position(List<int> lineStarts, int offset)
    {
        var line = lineStarts.BinarySearch(offset);
        if (line < 0) line = ~line - 1;
        return (line + 1, offset - lineStarts[line]);
    }
}

internal enum RustTokenKind
{
    Ident,
    Punct,
    String,
    Char,
    Lifetime,
    Number,
    LineComment,
    BlockComment
}

internal readonly record struct RustToken(RustTokenKind Kind, int Start, int End)
{
    public int Length => End - Start;
}

/// <summary>A scanner for Rust source that finds the extent of each token; it never fails, whatever the text.</summary>
internal static class RustLexer
{
    public static List<RustToken> Scan(string text)
    {
        var tokens = new List<RustToken>();
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
            }
            else if (c == '/' && Next(text, i) == '/')
            {
                var end = text.IndexOf('\n', i);
                end = end < 0 ? text.Length : end;
                tokens.Add(new RustToken(RustTokenKind.LineComment, i, end));
                i = end;
            }
            else if (c == '/' && Next(text, i) == '*')
            {
                var end = SkipBlockComment(text, i);
                tokens.Add(new RustToken(RustTokenKind.BlockComment, i, end));
                i = end;
            }
            else if (TryRawString(text, i, out var rawEnd))
            {
                tokens.Add(new RustToken(RustTokenKind.String, i, rawEnd));
                i = rawEnd;
            }
            else if (c == '"' || (c is 'b' or 'c' && Next(text, i) == '"'))
            {
                var end = SkipString(text, c == '"' ? i : i + 1);
                tokens.Add(new RustToken(RustTokenKind.String, i, end));
                i = end;
            }
            else if (c == 'b' && Next(text, i) == '\'' && TryChar(text, i + 1, out var byteEnd))
            {
                tokens.Add(new RustToken(RustTokenKind.Char, i, byteEnd));
                i = byteEnd;
            }
            else if (c == '\'')
            {
                if (TryChar(text, i, out var charEnd))
                {
                    tokens.Add(new RustToken(RustTokenKind.Char, i, charEnd));
                    i = charEnd;
                }
                else if (IsIdentStart(Next(text, i)))
                {
                    var end = i + 1;
                    while (end < text.Length && IsIdentPart(text[end])) end++;
                    tokens.Add(new RustToken(RustTokenKind.Lifetime, i, end));
                    i = end;
                }
                else
                {
                    tokens.Add(new RustToken(RustTokenKind.Punct, i, i + 1));
                    i++;
                }
            }
            else if (IsIdentStart(c))
            {
                var end = i + 1;

                // r#type: a raw identifier
                if (c == 'r' && end < text.Length && text[end] == '#' && end + 1 < text.Length && IsIdentStart(text[end + 1])) end += 1;
                while (end < text.Length && IsIdentPart(text[end])) end++;
                tokens.Add(new RustToken(RustTokenKind.Ident, i, end));
                i = end;
            }
            else if (char.IsAsciiDigit(c))
            {
                var end = i + 1;
                while (end < text.Length)
                {
                    var d = text[end];
                    if (char.IsLetterOrDigit(d) || d == '_') end++;
                    else if (d == '.' && end + 1 < text.Length && char.IsAsciiDigit(text[end + 1])) end++;
                    else if ((d == '+' || d == '-') && text[end - 1] is 'e' or 'E' && end + 1 < text.Length && char.IsAsciiDigit(text[end + 1]) && !text.AsSpan(i, end - i).StartsWith("0x")) end++;
                    else break;
                }

                tokens.Add(new RustToken(RustTokenKind.Number, i, end));
                i = end;
            }
            else
            {
                tokens.Add(new RustToken(RustTokenKind.Punct, i, i + 1));
                i++;
            }
        }

        return tokens;
    }

    private static char Next(string text, int i) => i + 1 < text.Length ? text[i + 1] : '\0';

    private static bool IsIdentStart(char c) => c == '_' || char.IsLetter(c);

    private static bool IsIdentPart(char c) => c == '_' || char.IsLetterOrDigit(c);

    private static int SkipBlockComment(string text, int start)
    {
        var depth = 0;
        var i = start;
        while (i < text.Length)
        {
            if (text[i] == '/' && Next(text, i) == '*')
            {
                depth++;
                i += 2;
            }
            else if (text[i] == '*' && Next(text, i) == '/')
            {
                depth--;
                i += 2;
                if (depth == 0) return i;
            }
            else
            {
                i++;
            }
        }

        return text.Length;
    }

    // "…" with \" escapes; the opening quote is at `quote`.
    private static int SkipString(string text, int quote)
    {
        var i = quote + 1;
        while (i < text.Length)
        {
            if (text[i] == '\\') i += 2;
            else if (text[i] == '"') return i + 1;
            else i++;
        }

        return text.Length;
    }

    // r"…", r#"…"#, br"…", cr#"…"#
    private static bool TryRawString(string text, int start, out int end)
    {
        end = start;
        var i = start;
        if (i < text.Length && text[i] is 'b' or 'c') i++;
        if (i >= text.Length || text[i] != 'r') return false;
        i++;
        var hashes = 0;
        while (i < text.Length && text[i] == '#')
        {
            hashes++;
            i++;
        }

        if (i >= text.Length || text[i] != '"') return false;

        // An identifier such as `r` or `br` followed by something else isn't a string; the quote settled it.
        var close = "\"" + new string('#', hashes);
        var found = text.IndexOf(close, i + 1, StringComparison.Ordinal);
        end = found < 0 ? text.Length : found + close.Length;
        return true;
    }

    // 'x'  '\n'  '\u{1F600}'  but not a lifetime such as 'a
    private static bool TryChar(string text, int quote, out int end)
    {
        end = quote;
        var i = quote + 1;
        if (i >= text.Length) return false;
        if (text[i] == '\\')
        {
            i++;
            if (i < text.Length && text[i] == 'u' && i + 1 < text.Length && text[i + 1] == '{')
            {
                var close = text.IndexOf('}', i);
                if (close < 0) return false;
                i = close + 1;
            }
            else if (i < text.Length && text[i] == 'x')
            {
                i += 3;
            }
            else
            {
                i++;
            }
        }
        else if (text[i] == '\'' || text[i] == '\n')
        {
            return false;
        }
        else
        {
            i += char.IsSurrogate(text[i]) ? 2 : 1;
        }

        if (i < text.Length && text[i] == '\'')
        {
            end = i + 1;
            return true;
        }

        return false;
    }
}
