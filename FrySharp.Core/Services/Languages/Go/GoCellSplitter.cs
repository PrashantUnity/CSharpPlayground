using System.Text;
using System.Text.RegularExpressions;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;

internal enum GoChunkKind
{
    Item,
    Statement
}

/// <summary>One top-level piece of a cell: a declaration (a function, a type, a constant, a variable) or a statement, with where it is.</summary>
/// <param name="Text">The lines as the cell has them.</param>
/// <param name="Masked">The same lines with comments blanked and the inside of literals blanked: what is structure and nothing else.</param>
/// <param name="StartLine">The cell's line (from 1) it begins on.</param>
internal sealed record GoChunk(GoChunkKind Kind, string Text, string Masked, int StartLine, int LineCount)
{
    /// <summary>"func", "method", "type", "const" or "var" for a declaration.</summary>
    public string? ItemKind { get; init; }

    /// <summary>The names a declaration makes (or, for a statement, the variables <c>:=</c> makes).</summary>
    public IReadOnlyList<string> Names { get; init; } = [];

    /// <summary>What replaces what: a later declaration with one of these names takes the place of this one.</summary>
    public IReadOnlyList<string> Keys { get; init; } = [];
}

/// <summary>A cell, taken apart: what it imports, what it declares at the top of the program, and the statements that make up its <c>main</c>.</summary>
internal sealed record GoCell(IReadOnlyList<string> Imports, IReadOnlyList<GoChunk> Items, IReadOnlyList<GoChunk> Statements)
{
    /// <summary>The cell is a whole program: it has its own <c>func main</c>.</summary>
    public bool DefinesMain => Items.Any(i => i.ItemKind == "func" && i.Names.Contains("main"));
}

/// <summary>
/// Takes a Go cell apart without a Go parser: comments and the inside of literals are blanked, and a line ends a piece when no
/// bracket is open and the line isn't continued (an operator, a comma or a dot at its end), which is how Go itself reads line ends.
/// </summary>
internal static partial class GoCellSplitter
{
    private static readonly HashSet<string> NotValues = new(StringComparer.Ordinal)
    {
        "if", "for", "switch", "select", "go", "defer", "return", "break", "continue", "goto", "fallthrough",
        "var", "const", "type", "func", "import", "package"
    };

    // Statements that print or end the program: their results aren't a value to show.
    [GeneratedRegex(@"^(?:fmt\.(?:Fprint|Print)\w*|log\.\w+|fry\.\w+|println|print|panic|os\.Exit|time\.Sleep)\s*\(")]
    private static partial Regex NoValueCall();

    [GeneratedRegex(@"^func\s+([A-Za-z_]\w*)\s*[\[(]")]
    private static partial Regex FunctionName();

    [GeneratedRegex(@"^func\s*\(\s*(?:[A-Za-z_]\w*\s+)?\*?\s*([A-Za-z_]\w*)(?:\[[^\]]*\])?\s*\)\s*([A-Za-z_]\w*)\s*[\[(]")]
    private static partial Regex MethodName();

    [GeneratedRegex(@"^(?:type|const|var)\s+([A-Za-z_]\w*(?:\s*,\s*[A-Za-z_]\w*)*)")]
    private static partial Regex SingleDeclaration();

    [GeneratedRegex(@"^\s*([A-Za-z_]\w*(?:\s*,\s*[A-Za-z_]\w*)*)")]
    private static partial Regex GroupMember();

    [GeneratedRegex(@"^\s*([A-Za-z_]\w*(?:\s*,\s*[A-Za-z_]\w*)*)\s*:=")]
    private static partial Regex ShortDeclaration();

    [GeneratedRegex(@"^\s*(?:(?<alias>[A-Za-z_]\w*|\.)\s+)?""(?<path>[^""]+)""")]
    private static partial Regex ImportSpec();

    public static GoCell Split(string code)
    {
        var text = code.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = text.Split('\n');
        var masked = Mask(text, out var openAtLineEnd).Split('\n');

        var imports = new List<string>();
        var items = new List<GoChunk>();
        var statements = new List<GoChunk>();

        var line = 0;
        while (line < lines.Length)
        {
            if (masked[line].AsSpan().Trim().IsEmpty)
            {
                line++;
                continue;
            }

            var start = line;
            var depth = 0;
            while (true)
            {
                foreach (var c in masked[line])
                {
                    if (c is '(' or '{' or '[') depth++;
                    else if (c is ')' or '}' or ']') depth = Math.Max(0, depth - 1);
                }

                var continues = openAtLineEnd[line] || depth > 0 || EndsExpecting(masked[line]);
                line++;
                if (!continues || line >= lines.Length) break;
            }

            var chunkText = string.Join('\n', lines[start..line]);
            var chunkMasked = string.Join('\n', masked[start..line]);
            Classify(chunkText, chunkMasked, start + 1, line - start, imports, items, statements);
        }

        return new GoCell(imports, items, statements);
    }

    private static void Classify(string text, string masked, int startLine, int lineCount, List<string> imports, List<GoChunk> items, List<GoChunk> statements)
    {
        var trimmed = masked.TrimStart();
        var word = FirstWord(trimmed);

        switch (word)
        {
            case "package":
                return;
            case "import":
                foreach (var line in text.Split('\n'))
                {
                    var spec = line.TrimStart();
                    if (spec.StartsWith("import", StringComparison.Ordinal)) spec = spec["import".Length..];
                    spec = spec.TrimStart('(', ' ', '\t');
                    if (ImportSpec().Match(spec) is { Success: true } match)
                    {
                        imports.Add(match.Groups["alias"].Success ? $"{match.Groups["alias"].Value} \"{match.Groups["path"].Value}\"" : $"\"{match.Groups["path"].Value}\"");
                    }
                }

                return;
            case "func":
                if (MethodName().Match(trimmed) is { Success: true } method)
                {
                    var key = $"method:{method.Groups[1].Value}.{method.Groups[2].Value}";
                    items.Add(new GoChunk(GoChunkKind.Item, text, masked, startLine, lineCount) { ItemKind = "method", Names = [method.Groups[2].Value], Keys = [key] });
                    return;
                }

                if (FunctionName().Match(trimmed) is { Success: true } function)
                {
                    items.Add(new GoChunk(GoChunkKind.Item, text, masked, startLine, lineCount)
                    {
                        ItemKind = "func",
                        Names = [function.Groups[1].Value],
                        Keys = [$"name:{function.Groups[1].Value}"]
                    });
                    return;
                }

                break; // a func literal (func() {...}()) is a statement
            case "type" or "const" or "var":
                var names = DeclaredNames(trimmed, word);
                if (names.Count > 0)
                {
                    items.Add(new GoChunk(GoChunkKind.Item, text, masked, startLine, lineCount)
                    {
                        ItemKind = word,
                        Names = names.Where(n => n != "_").ToList(),
                        Keys = names.Where(n => n != "_").Select(n => $"name:{n}").ToList()
                    });
                    return;
                }

                break;
        }

        var declared = ShortDeclaration().Match(trimmed) is { Success: true } shortDeclaration
            ? shortDeclaration.Groups[1].Value.Split(',').Select(n => n.Trim()).Where(n => n != "_").Distinct().ToList()
            : [];
        statements.Add(new GoChunk(GoChunkKind.Statement, text, masked, startLine, lineCount) { Names = declared });
    }

    // type X ..., const a, b = ..., var x int; and the groups: type ( A struct{}; B int ), const ( ... ), var ( ... )
    private static List<string> DeclaredNames(string masked, string word)
    {
        var rest = masked[word.Length..].TrimStart();
        if (!rest.StartsWith('(')) return SingleDeclaration().Match(masked) is { Success: true } single ? SplitNames(single.Groups[1].Value) : [];

        var names = new List<string>();
        var depth = 0;
        var first = true;
        foreach (var line in masked.Split('\n'))
        {
            var depthAtStart = depth;
            foreach (var c in line)
            {
                if (c is '(' or '{' or '[') depth++;
                else if (c is ')' or '}' or ']') depth = Math.Max(0, depth - 1);
            }

            if (first)
            {
                first = false;
                // "var (" and the first member after it on the same line is rare enough to leave
                continue;
            }

            if (depthAtStart == 1 && GroupMember().Match(line) is { Success: true } member) names.AddRange(SplitNames(member.Groups[1].Value));
        }

        return names;
    }

    private static List<string> SplitNames(string names) => names.Split(',').Select(n => n.Trim()).Where(n => n.Length > 0).ToList();

    private static string FirstWord(string text)
    {
        var end = 0;
        while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_')) end++;
        return text[..end];
    }

    // A line that stops in the middle: "x := a +", "f(a,", "obj."
    private static bool EndsExpecting(string maskedLine)
    {
        var end = maskedLine.Length;
        while (end > 0 && char.IsWhiteSpace(maskedLine[end - 1])) end--;
        if (end == 0) return false;
        var last = maskedLine[end - 1];
        if (last is '+' or '-' && end > 1 && maskedLine[end - 2] == last) return false; // x++ and x-- end a statement
        return last is '+' or '-' or '*' or '/' or '%' or '&' or '|' or '^' or '<' or '>' or '=' or '!' or ',' or '.';
    }

    /// <summary>
    /// Whether a statement is an expression whose value could be shown (<c>x + 1</c>, <c>strconv.Itoa(5)</c>, a bare <c>name</c>), as against an
    /// assignment, a control statement, or something that prints. A call that returns nothing looks like one: the build tells.
    /// </summary>
    public static bool IsValueExpression(GoChunk statement)
    {
        var masked = statement.Masked.Trim();
        if (masked.Length == 0) return false;
        if (NotValues.Contains(FirstWord(masked))) return false;
        if (NoValueCall().IsMatch(masked)) return false;
        if (masked.EndsWith(':')) return false; // a label

        var depth = 0;
        for (var i = 0; i < masked.Length; i++)
        {
            var c = masked[i];
            if (c is '(' or '{' or '[') depth++;
            else if (c is ')' or '}' or ']') depth = Math.Max(0, depth - 1);
            if (depth != 0) continue;

            var next = i + 1 < masked.Length ? masked[i + 1] : '\0';
            var previous = i > 0 ? masked[i - 1] : '\0';
            if (c == ':' && next == '=') return false;
            if (c == '=' && next != '=' && (previous is not ('=' or '!' or '<' or '>' or ':') || (i > 1 && (masked.AsSpan(i - 2, 2) is "<<" or ">>" or "&^")))) return false;
            if (c is '+' or '-' && next == c && masked[(i + 2)..].Trim().Length == 0) return false;
            if (c == '<' && next == '-' && PreviousIsOperand(masked, i)) return false; // a send: ch <- v
        }

        return true;
    }

    private static bool PreviousIsOperand(string masked, int index)
    {
        var i = index - 1;
        while (i >= 0 && char.IsWhiteSpace(masked[i])) i--;
        return i >= 0 && (char.IsLetterOrDigit(masked[i]) || masked[i] is '_' or ')' or ']');
    }

    /// <summary>Where code ends on a line: its length without a trailing comment or spaces (the line is given masked, with its original text).</summary>
    public static int CodeEnd(string maskedLine)
    {
        var end = maskedLine.Length;
        while (end > 0 && char.IsWhiteSpace(maskedLine[end - 1])) end--;
        return end;
    }

    /// <summary>The text with every comment and the inside of every string, rune and raw string literal turned to spaces (line breaks stay).</summary>
    public static string Mask(string text) => Mask(text, out _);

    private static string Mask(string text, out bool[] openAtLineEnd)
    {
        var result = new StringBuilder(text.Length);
        var open = new List<bool>();
        var state = 0; // 0 code, 1 line comment, 2 block comment, 3 string, 4 raw string, 5 rune
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            var next = i + 1 < text.Length ? text[i + 1] : '\0';
            if (c == '\n')
            {
                open.Add(state is 2 or 4);
                if (state is 1 or 3 or 5) state = 0;
                result.Append('\n');
                continue;
            }

            switch (state)
            {
                case 0:
                    if (c == '/' && next == '/') { state = 1; result.Append("  "); i++; }
                    else if (c == '/' && next == '*') { state = 2; result.Append("  "); i++; }
                    else if (c == '"') { state = 3; result.Append(c); }
                    else if (c == '`') { state = 4; result.Append(c); }
                    else if (c == '\'') { state = 5; result.Append(c); }
                    else result.Append(c);
                    break;
                case 1:
                    result.Append(' ');
                    break;
                case 2:
                    if (c == '*' && next == '/') { state = 0; result.Append("  "); i++; }
                    else result.Append(' ');
                    break;
                case 3 or 5:
                    if (c == '\\' && next != '\n' && next != '\0') { result.Append("  "); i++; }
                    else if ((state == 3 && c == '"') || (state == 5 && c == '\'')) { state = 0; result.Append(c); }
                    else result.Append(' ');
                    break;
                case 4:
                    if (c == '`') { state = 0; result.Append(c); }
                    else result.Append(' ');
                    break;
            }
        }

        open.Add(state is 2 or 4);
        openAtLineEnd = open.ToArray();
        return result.ToString();
    }
}
