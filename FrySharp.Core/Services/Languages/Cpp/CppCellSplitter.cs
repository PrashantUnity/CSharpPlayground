using System.Text;
using System.Text.RegularExpressions;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;

internal enum CppChunkKind
{
    Include,
    Item,
    Statement
}

/// <summary>One top-level piece of a cell: an <c>#include</c>, a declaration (a function, a type, a using, a constant, a macro) or a statement.</summary>
/// <param name="Text">The lines as the cell has them.</param>
/// <param name="Masked">The same lines with comments blanked and the inside of literals blanked: what is structure and nothing else.</param>
/// <param name="StartLine">The cell's line (from 1) it begins on.</param>
/// <param name="EndsWithSemicolon">The piece ends with <c>;</c> (a last expression without one is the value to show).</param>
internal sealed record CppChunk(CppChunkKind Kind, string Text, string Masked, int StartLine, int LineCount, bool EndsWithSemicolon)
{
    /// <summary>"function", "type", "using", "template", "namespace", "macro" or "var" for a declaration.</summary>
    public string? ItemKind { get; init; }

    /// <summary>The names a declaration makes, or the variables a statement declares.</summary>
    public IReadOnlyList<string> Names { get; init; } = [];

    /// <summary>What replaces what: a later declaration with one of these keys takes the place of this one.</summary>
    public IReadOnlyList<string> Keys { get; init; } = [];

    /// <summary>A struct's or class's public data members, in order, when the notebook can tell what they are (it can't show a type it doesn't know the members of).</summary>
    public IReadOnlyList<string> Members { get; init; } = [];
}

/// <summary>A cell, taken apart.</summary>
internal sealed record CppCell(IReadOnlyList<string> Includes, IReadOnlyList<CppChunk> Items, IReadOnlyList<CppChunk> Statements)
{
    /// <summary>The cell is a whole program: it has its own <c>main</c>.</summary>
    public bool DefinesMain => Items.Any(i => i.ItemKind == "function" && i.Names.Contains("main"));
}

/// <summary>
/// Takes a C++ cell apart without a compiler: comments and the inside of literals are blanked, and a piece ends at a <c>;</c> or at the <c>}</c> that
/// closes its block, with the continuations C++ has (<c>else</c>, <c>catch</c>, <c>while</c> after <c>do</c>, the <c>;</c> after a type's <c>}</c>).
/// </summary>
internal static partial class CppCellSplitter
{
    private static readonly HashSet<string> Storage = new(StringComparer.Ordinal)
    {
        "const", "constexpr", "static", "volatile", "inline", "thread_local", "mutable", "register", "extern", "consteval", "constinit"
    };

    private static readonly HashSet<string> NotDeclarations = new(StringComparer.Ordinal)
    {
        "return", "delete", "throw", "goto", "break", "continue", "case", "default", "else", "do", "try", "catch", "co_return", "co_yield", "co_await",
        "if", "for", "while", "switch", "using", "typedef", "static_assert", "asm", "namespace", "new", "sizeof", "typeid", "dynamic_cast", "static_cast",
        "reinterpret_cast", "const_cast", "this", "template", "public", "private", "protected"
    };

    private static readonly HashSet<string> NotValues = new(StringComparer.Ordinal)
    {
        "if", "for", "while", "do", "switch", "return", "throw", "try", "catch", "else", "goto", "break", "continue", "case", "default", "using", "typedef",
        "namespace", "struct", "class", "enum", "union", "template", "static_assert", "delete", "co_return", "public", "private", "protected"
    };

    // Statements that print: their results aren't a value to show.
    [GeneratedRegex(@"^(?:std\s*::\s*)?(?:cout|cerr|clog|printf|puts|fputs|fprintf|putchar|print|println)\b|^fry\s*::|^display\s*::|^std\s*::\s*(?:print|println)\b")]
    private static partial Regex Printing();

    [GeneratedRegex(@"^#\s*include\s*(?<path>[<""][^>""]+[>""])")]
    private static partial Regex IncludeLine();

    [GeneratedRegex(@"^#\s*define\s+(?<name>[A-Za-z_]\w*)")]
    private static partial Regex DefineLine();

    [GeneratedRegex(@"^#\s*undef\s+(?<name>[A-Za-z_]\w*)")]
    private static partial Regex UndefLine();

    [GeneratedRegex(@"^(?:struct|class|union|enum(?:\s+(?:class|struct))?)\s*(?:\[\[[^\]]*\]\]\s*)?(?<name>[A-Za-z_]\w*)?")]
    private static partial Regex TypeHead();

    [GeneratedRegex(@"^\s*(public|private|protected)\s*:")]
    private static partial Regex AccessLabel();

    public static CppCell Split(string code)
    {
        var text = code.Replace("\r\n", "\n").Replace('\r', '\n');
        var masked = Mask(text);

        var includes = new List<string>();
        var items = new List<CppChunk>();
        var statements = new List<CppChunk>();
        var namespaces = 0;

        var position = 0;
        var counted = 0;
        var line = 0; // the line (from 0) the text before `counted` ends on
        int LineAt(int index)
        {
            for (; counted < index && counted < masked.Length; counted++) if (masked[counted] == '\n') line++;
            return line;
        }

        while (position < masked.Length)
        {
            // blank space between pieces
            while (position < masked.Length && char.IsWhiteSpace(masked[position])) position++;
            if (position >= masked.Length) break;
            var start = position;
            var startLine = LineAt(start);

            if (masked[position] == '#' && OnlySpaceBefore(masked, position))
            {
                // a directive runs to the end of its line, and on past a backslash
                while (position < masked.Length)
                {
                    var end = masked.IndexOf('\n', position);
                    if (end < 0) { position = masked.Length; break; }
                    var continued = end > position && masked[end - 1] == '\\';
                    position = end + 1;
                    if (!continued) break;
                }

                AddDirective(text, masked, start, position, startLine, includes, items);
                continue;
            }

            var firstWord = FirstWord(masked, position);
            var typeDefinition = firstWord is "struct" or "class" or "union" or "enum";
            var depth = 0;
            var semicolon = false;
            while (position < masked.Length)
            {
                var c = masked[position++];
                if (c is '(' or '[' or '{') depth++;
                else if (c is ')' or ']') depth = Math.Max(0, depth - 1);
                else if (c == '}')
                {
                    depth = Math.Max(0, depth - 1);
                    if (depth == 0)
                    {
                        var next = NextWord(masked, position, out var at);
                        if (next == ";") { position = at + 1; semicolon = true; break; }
                        if ((firstWord == "if" && next == "else") || (firstWord == "try" && next == "catch") || firstWord == "do" || typeDefinition || next is "," or ")" or "=") continue;
                        break;
                    }
                }
                else if (c == ';' && depth == 0)
                {
                    semicolon = true;
                    break;
                }
            }

            var chunkText = text[start..position];
            var chunkMasked = masked[start..position];
            var count = chunkText.TrimEnd('\n').Count(ch => ch == '\n') + 1;
            Classify(chunkText.TrimEnd('\n'), chunkMasked.TrimEnd('\n'), startLine + 1, count, semicolon, items, statements, ref namespaces);
        }

        return new CppCell(includes, items, statements);
    }

    private static void AddDirective(string text, string masked, int start, int end, int startLine, List<string> includes, List<CppChunk> items)
    {
        var directive = text[start..end].TrimEnd('\n', '\r');
        var maskedDirective = masked[start..end].TrimEnd('\n');
        var trimmed = maskedDirective.Trim();
        var count = directive.Count(ch => ch == '\n') + 1;
        if (IncludeLine().Match(directive.Trim()) is { Success: true } include)
        {
            includes.Add($"#include {include.Groups["path"].Value}");
            return;
        }

        if (DefineLine().Match(trimmed) is { Success: true } define)
        {
            items.Add(new CppChunk(CppChunkKind.Item, directive, maskedDirective, startLine + 1, count, false) { ItemKind = "macro", Names = [define.Groups["name"].Value], Keys = [$"macro:{define.Groups["name"].Value}"] });
            return;
        }

        if (UndefLine().Match(trimmed).Success || trimmed.StartsWith("#pragma", StringComparison.Ordinal))
        {
            items.Add(new CppChunk(CppChunkKind.Item, directive, maskedDirective, startLine + 1, count, false) { ItemKind = "macro", Keys = [$"directive:{directive}"] });
        }
    }

    private static void Classify(string text, string masked, int startLine, int lineCount, bool semicolon, List<CppChunk> items, List<CppChunk> statements, ref int namespaces)
    {
        var trimmed = masked.Trim();
        var word = FirstWord(trimmed, 0);

        CppChunk Item(string kind, IReadOnlyList<string> names, IReadOnlyList<string> keys, IReadOnlyList<string>? members = null) =>
            new(CppChunkKind.Item, text, masked, startLine, lineCount, semicolon) { ItemKind = kind, Names = names, Keys = keys, Members = members ?? [] };

        switch (word)
        {
            case "using":
                var alias = Regex.Match(trimmed, @"^using\s+(?<name>[A-Za-z_]\w*)\s*=");
                var key = alias.Success ? $"name:{alias.Groups["name"].Value}" : "using:" + Collapse(trimmed);
                items.Add(Item("using", alias.Success ? [alias.Groups["name"].Value] : [], [key]));
                return;
            case "typedef":
                var typedefName = Regex.Match(trimmed.TrimEnd(';').TrimEnd(), @"([A-Za-z_]\w*)\s*$");
                items.Add(Item("using", typedefName.Success ? [typedefName.Groups[1].Value] : [], [typedefName.Success ? $"name:{typedefName.Groups[1].Value}" : "typedef:" + Collapse(trimmed)]));
                return;
            case "namespace":
                // open: more of the same namespace joins it, so no piece replaces another
                var spaceName = Regex.Match(trimmed, @"^namespace\s+([A-Za-z_][\w:]*)?");
                items.Add(Item("namespace", spaceName.Groups[1].Success ? [spaceName.Groups[1].Value] : [], [$"namespace:{namespaces++}"]));
                return;
            case "template":
                items.Add(Item("template", [], ["template:" + Collapse(HeaderOf(trimmed))]));
                return;
            case "struct" or "class" or "union" or "enum":
                if (TypeHead().Match(trimmed) is { Success: true } head && IsTypeDefinition(trimmed))
                {
                    var name = head.Groups["name"].Value;
                    items.Add(Item("type", name.Length > 0 ? [name] : [], name.Length > 0 ? [$"name:{name}"] : [$"type:{namespaces++}"], word is "struct" or "class" ? PublicMembers(text, masked, word) : []));
                    return;
                }

                break;
        }

        if (IsFunction(trimmed, out var functionName, out var signature, out var definition))
        {
            items.Add(Item("function", [functionName], [$"function:{signature}"]));
            _ = definition;
            return;
        }

        // a declaration of something every cell may use: a constant, a static, an extern
        var first = FirstWord(trimmed, 0);
        if (Storage.Contains(first) && first is not ("mutable" or "register") && TryParseDeclaration(trimmed, out var globals))
        {
            items.Add(Item("var", globals, globals.Select(g => $"name:{g}").ToList()));
            return;
        }

        var declared = TryParseDeclaration(trimmed, out var names) ? names : [];
        statements.Add(new CppChunk(CppChunkKind.Statement, text, masked, startLine, lineCount, semicolon) { Names = declared });
    }

    // struct Name { ... }; / struct Name;  (not "struct Name var = ...;" nor a function returning a struct)
    private static bool IsTypeDefinition(string masked)
    {
        var brace = masked.IndexOf('{');
        var paren = masked.IndexOf('(');
        var equals = masked.IndexOf('=');
        if (brace >= 0 && (paren < 0 || brace < paren) && (equals < 0 || brace < equals)) return true;
        return Regex.IsMatch(masked, @"^(?:struct|class|union|enum(?:\s+(?:class|struct))?)\s+[A-Za-z_]\w*\s*;\s*$");
    }

    // ── functions ───────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A function definition (<c>int f(int a) { ... }</c>) or a prototype (<c>int f(int);</c>), with its name and its signature (the name and the parameter types).</summary>
    private static bool IsFunction(string masked, out string name, out string signature, out bool definition)
    {
        name = signature = string.Empty;
        definition = false;
        var open = IndexAtDepthZero(masked, '(');
        if (open <= 0) return false;
        var before = masked[..open].TrimEnd();
        var nameMatch = Regex.Match(before, @"(operator\s*[^\s\w(]+|operator\s+\w+|~?[A-Za-z_]\w*(?:\s*::\s*~?[A-Za-z_]\w*)*)\s*$");
        if (!nameMatch.Success) return false;
        var prefix = before[..nameMatch.Index].Trim();
        var functionName = nameMatch.Groups[1].Value;
        if (NotDeclarations.Contains(FirstWord(masked, 0)) || NotDeclarations.Contains(functionName)) return false;
        if (prefix.Length == 0 && functionName != "main") return false; // a call, or a constructor
        if (prefix.Contains('=') || prefix.Contains('.') || prefix.Contains("->")) return false;
        var close = MatchingParen(masked, open);
        if (close < 0) return false;

        var after = masked[(close + 1)..].TrimStart();
        // qualifiers, a trailing return type, an exception specification
        var tail = Regex.Match(after, @"^(?:(?:const|volatile|noexcept(?:\s*\([^)]*\))?|override|final|&&|&)\s*)*(?:->[^;{]*)?");
        var rest = after[tail.Length..].TrimStart();
        var parameters = masked[(open + 1)..close];
        if (rest.StartsWith('{') || rest.StartsWith(':') && prefix.Length > 0)
        {
            name = functionName;
            signature = functionName + "(" + NormalizeParameterTypes(parameters) + ")";
            definition = true;
            return true;
        }

        if ((rest.Length == 0 || rest == ";") && LooksLikePrototype(parameters))
        {
            name = functionName;
            signature = functionName + "(" + NormalizeParameterTypes(parameters) + ")";
            return true;
        }

        return false;
    }

    private static readonly HashSet<string> BuiltinTypes = new(StringComparer.Ordinal)
    {
        "int", "long", "short", "char", "float", "double", "bool", "unsigned", "signed", "void", "size_t", "auto", "wchar_t", "char8_t", "char16_t", "char32_t",
        "int8_t", "int16_t", "int32_t", "int64_t", "uint8_t", "uint16_t", "uint32_t", "uint64_t"
    };

    // Widget w(3); is a variable; int f(int); and void g(const std::string&); are prototypes.
    private static bool LooksLikePrototype(string parameters)
    {
        var trimmed = parameters.Trim();
        if (trimmed.Length == 0 || trimmed == "void") return true;
        foreach (var parameter in SplitTopLevel(trimmed, ','))
        {
            var p = parameter.Trim();
            var first = FirstWord(p, 0);
            if (p.Length == 0 || char.IsDigit(p[0]) || p[0] is '"' or '\'' or '-' or '(' or '{') return false;
            if (BuiltinTypes.Contains(first) || first is "const" or "volatile" or "struct" or "class" or "typename" or "enum") continue;
            // Type name, Type& name, std::string name: two names, or a qualified one, or a pointer or a reference
            if (Regex.IsMatch(p, @"^[A-Za-z_][\w:<>,\s]*[\s*&]+[A-Za-z_]\w*\s*(=.*)?$") || p.Contains("::") || p.Contains('<') || p.Contains('*') || p.Contains('&')) continue;
            return false;
        }

        return true;
    }

    // "const std::string& name, int count = 3" -> "const std::string&,int": what tells overloads apart
    private static string NormalizeParameterTypes(string parameters)
    {
        var types = new List<string>();
        foreach (var parameter in SplitTopLevel(parameters, ','))
        {
            var p = parameter.Trim();
            var equals = IndexAtDepthZero(p, '=');
            if (equals >= 0) p = p[..equals].Trim();
            var trailingName = Regex.Match(p, @"^(.*?[\s*&])([A-Za-z_]\w*)$");
            if (trailingName.Success && !BuiltinTypes.Contains(trailingName.Groups[2].Value) && trailingName.Groups[2].Value is not ("const" or "volatile")) p = trailingName.Groups[1].Value.Trim();
            if (p.Length > 0) types.Add(Collapse(p));
        }

        return string.Join(",", types);
    }

    // ── declarations ────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Whether a statement declares variables (<c>int x = 5;</c>, <c>std::vector&lt;int&gt; v{1, 2};</c>, <c>auto [a, b] = f();</c>), and their names.</summary>
    public static bool TryParseDeclaration(string masked, out List<string> names)
    {
        names = [];
        var text = masked.Trim().TrimEnd(';').Trim();
        if (text.Length == 0) return false;
        var position = 0;
        var typeTokens = 0;

        // storage and qualifiers, then the type: names joined by ::, with template arguments, pointers, references and const
        while (true)
        {
            SkipSpace(text, ref position);
            if (position >= text.Length) return false;
            var word = FirstWord(text, position);
            if (word.Length > 0 && NotDeclarations.Contains(word)) return false;
            if (word.Length > 0 && Storage.Contains(word)) { position += word.Length; continue; }
            break;
        }

        var sawAuto = false;
        var lastIdentifier = -1;
        var lastIdentifierEnd = -1;
        while (position < text.Length)
        {
            SkipSpace(text, ref position);
            if (position >= text.Length) break;
            var c = text[position];
            if (char.IsLetter(c) || c == '_')
            {
                var word = FirstWord(text, position);
                if (word == "auto") sawAuto = true;
                lastIdentifier = position;
                lastIdentifierEnd = position + word.Length;
                position += word.Length;
                typeTokens++;
                continue;
            }

            if (c == ':' && position + 1 < text.Length && text[position + 1] == ':')
            {
                position += 2;
                typeTokens--; // the name before :: was part of the type, not the declarator
                if (typeTokens < 0) typeTokens = 0;
                continue;
            }

            if (c == '<')
            {
                var close = MatchingAngle(text, position);
                if (close < 0) return false;
                position = close + 1;
                continue;
            }

            if (c is '*' or '&') { position++; continue; }
            if (c == '[' && sawAuto)
            {
                // a structured binding: auto [a, b] = f();
                var close = text.IndexOf(']', position);
                if (close < 0) return false;
                names = text[(position + 1)..close].Split(',').Select(n => n.Trim()).Where(n => n.Length > 0 && n != "_").ToList();
                return names.Count > 0;
            }

            break; // the declarator's end: = ( { [ ; ,
        }

        if (lastIdentifier < 0 || typeTokens < 2) return false;
        // the last name before the end is the declarator's, the rest was the type
        var firstName = text[lastIdentifier..lastIdentifierEnd];
        if (BuiltinTypes.Contains(firstName) && !sawAuto) return false; // "int" alone: a type with nothing declared
        if (position < text.Length && text[position] is not ('=' or '(' or '{' or '[' or ',' or ';' or ':')) return false;
        names.Add(firstName);

        // more declarators: int a = 1, b = 2;
        var remaining = text[position..];
        var depth = 0;
        for (var i = 0; i < remaining.Length; i++)
        {
            var ch = remaining[i];
            if (ch is '(' or '{' or '[' or '<') depth++;
            else if (ch is ')' or '}' or ']' or '>') depth = Math.Max(0, depth - 1);
            else if (ch == ',' && depth == 0)
            {
                var match = Regex.Match(remaining[(i + 1)..], @"^\s*[*&]*\s*([A-Za-z_]\w*)");
                if (match.Success) names.Add(match.Groups[1].Value);
            }
        }

        return names.Count > 0;
    }

    // The public data members of a struct or class, in order: the notebook can show a type whose members it knows.
    private static IReadOnlyList<string> PublicMembers(string text, string masked, string word)
    {
        var open = masked.IndexOf('{');
        var close = masked.LastIndexOf('}');
        if (open < 0 || close <= open) return [];
        if (masked[..open].Contains('<') && masked[..open].Contains("template")) return [];
        var inner = masked[(open + 1)..close];
        var members = new List<string>();
        var isPublic = word == "struct";
        var position = 0;
        while (position < inner.Length)
        {
            while (position < inner.Length && char.IsWhiteSpace(inner[position])) position++;
            if (position >= inner.Length) break;
            var start = position;
            var depth = 0;
            while (position < inner.Length)
            {
                var c = inner[position++];
                if (c is '(' or '[' or '{') depth++;
                else if (c is ')' or ']') depth = Math.Max(0, depth - 1);
                else if (c == '}')
                {
                    depth = Math.Max(0, depth - 1);
                    if (depth == 0)
                    {
                        // a method's body or a nested type: ends at }, or at the ; after it
                        var next = NextWord(inner, position, out var at);
                        if (next == ";") position = at + 1;
                        break;
                    }
                }
                else if (c == ';' && depth == 0) break;
            }

            var member = inner[start..position].Trim();
            var label = AccessLabel().Match(member);
            if (label.Success)
            {
                isPublic = label.Groups[1].Value == "public";
                member = member[label.Length..].Trim();
                if (member.Length == 0) continue;
            }

            if (!isPublic || !member.EndsWith(';')) continue;
            var memberWord = FirstWord(member, 0);
            if (memberWord is "using" or "typedef" or "friend" or "static" or "template" or "enum" or "struct" or "class" or "union" or "virtual" or "explicit" or "operator" or "constexpr" or "inline")
            {
                if (memberWord is "struct" or "class" or "union" or "enum" && !member.Contains('{')) { }
                continue;
            }

            if (member.Contains('~') || member.Contains(':') && !member.Contains("::") || HasParenthesisOutsideAngles(member)) continue;
            // a const or reference member can't be read back into: the type isn't shown as a record
            if (Regex.IsMatch(member, @"\bconst\b") || Regex.IsMatch(member, @"[A-Za-z_>\]]\s*&\s*[A-Za-z_]")) return [];
            if (TryParseDeclaration(member, out var names)) members.AddRange(names);
        }

        return members;
    }

    private static bool HasParenthesisOutsideAngles(string text)
    {
        var angle = 0;
        foreach (var c in text)
        {
            if (c == '<') angle++;
            else if (c == '>') angle = Math.Max(0, angle - 1);
            else if (c == '(' && angle == 0) return true;
        }

        return false;
    }

    // ── what a statement is ─────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Whether the last statement, which has no <c>;</c>, is an expression whose value could be shown (<c>x * 2</c>, <c>v.size()</c>, a bare name), as
    /// against a declaration, an assignment, something that prints. A call that returns nothing looks like one: the build tells.
    /// </summary>
    public static bool IsValueExpression(CppChunk statement)
    {
        if (statement.EndsWithSemicolon) return false;
        var masked = statement.Masked.Trim();
        if (masked.Length == 0 || masked[0] == '{') return false; // nothing, or a block
        if (NotValues.Contains(FirstWord(masked, 0))) return false;
        if (Printing().IsMatch(masked)) return false;
        if (TryParseDeclaration(masked, out _)) return false;

        var depth = 0;
        for (var i = 0; i < masked.Length; i++)
        {
            var c = masked[i];
            if (c is '(' or '{' or '[') depth++;
            else if (c is ')' or '}' or ']') depth = Math.Max(0, depth - 1);
            if (depth != 0) continue;
            var next = i + 1 < masked.Length ? masked[i + 1] : '\0';
            var previous = i > 0 ? masked[i - 1] : '\0';
            if (c == '=' && next != '=' && (previous is not ('=' or '!' or '<' or '>') || (i > 1 && masked.AsSpan(i - 2, 2) is "<<" or ">>"))) return false;
            if (c is '+' or '-' && next == c && masked[(i + 2)..].Trim().Length == 0) return false;
            if (c is '+' or '-' && next == c && i == 0) return false;
        }

        return true;
    }

    // ── masking and small scanners ──────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The text with every comment and the inside of every string, character and raw string literal turned to spaces (line breaks stay).</summary>
    public static string Mask(string text)
    {
        var result = new StringBuilder(text.Length);
        var state = 0; // 0 code, 1 line comment, 2 block comment, 3 string, 4 char
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            var next = i + 1 < text.Length ? text[i + 1] : '\0';
            if (c == '\n')
            {
                if (state is 1 or 3 or 4) state = 0;
                result.Append('\n');
                continue;
            }

            switch (state)
            {
                case 0:
                    if (c == '/' && next == '/') { state = 1; result.Append("  "); i++; }
                    else if (c == '/' && next == '*') { state = 2; result.Append("  "); i++; }
                    else if (c == '"' && i > 0 && text[i - 1] == 'R' && RawStringEnd(text, i) is var end && end > i)
                    {
                        // R"delim( ... )delim": blank the inside, keep the line breaks
                        for (var k = i; k < end; k++) result.Append(text[k] == '\n' ? '\n' : k == i || k == end - 1 ? '"' : ' ');
                        i = end - 1;
                    }
                    else if (c == '"') { state = 3; result.Append(c); }
                    else if (c == '\'' && !(i > 0 && char.IsDigit(text[i - 1]) && Uri.IsHexDigit(next))) { state = 4; result.Append(c); }
                    else result.Append(c);
                    break;
                case 1:
                    result.Append(' ');
                    break;
                case 2:
                    if (c == '*' && next == '/') { state = 0; result.Append("  "); i++; }
                    else result.Append(' ');
                    break;
                case 3 or 4:
                    if (c == '\\' && next != '\n' && next != '\0') { result.Append("  "); i++; }
                    else if ((state == 3 && c == '"') || (state == 4 && c == '\'')) { state = 0; result.Append(c); }
                    else result.Append(' ');
                    break;
            }
        }

        return result.ToString();
    }

    // Where R"delim(...)delim" ends (the index after its closing quote), or -1 when it doesn't.
    private static int RawStringEnd(string text, int quote)
    {
        var open = text.IndexOf('(', quote);
        if (open < 0 || open - quote > 17) return -1;
        var delimiter = text[(quote + 1)..open];
        if (delimiter.Any(ch => char.IsWhiteSpace(ch) || ch is ')' or '\\')) return -1;
        var closing = ")" + delimiter + "\"";
        var end = text.IndexOf(closing, open + 1, StringComparison.Ordinal);
        return end < 0 ? -1 : end + closing.Length;
    }

    private static bool OnlySpaceBefore(string text, int position)
    {
        for (var i = position - 1; i >= 0 && text[i] != '\n'; i--)
        {
            if (!char.IsWhiteSpace(text[i])) return false;
        }

        return true;
    }

    private static string FirstWord(string text, int position)
    {
        var end = position;
        while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_')) end++;
        return text[position..end];
    }

    // The next thing after position that isn't space: ";" or a word or a single character, and where it is.
    private static string NextWord(string text, int position, out int at)
    {
        at = position;
        while (at < text.Length && char.IsWhiteSpace(text[at])) at++;
        if (at >= text.Length) return string.Empty;
        if (char.IsLetter(text[at]) || text[at] == '_') return FirstWord(text, at);
        return text[at].ToString();
    }

    private static void SkipSpace(string text, ref int position)
    {
        while (position < text.Length && char.IsWhiteSpace(text[position])) position++;
    }

    private static int IndexAtDepthZero(string text, char target)
    {
        var depth = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == target && depth == 0) return i;
            if (c is '(' or '[' or '{') depth++;
            else if (c is ')' or ']' or '}') depth = Math.Max(0, depth - 1);
        }

        return -1;
    }

    private static int MatchingParen(string text, int open)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '(') depth++;
            else if (text[i] == ')' && --depth == 0) return i;
        }

        return -1;
    }

    private static int MatchingAngle(string text, int open)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '<') depth++;
            else if (text[i] == '>' && --depth == 0) return i;
            else if (text[i] is ';' or '{' or '}') return -1;
        }

        return -1;
    }

    private static List<string> SplitTopLevel(string text, char separator)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is '(' or '[' or '{' or '<') depth++;
            else if (c is ')' or ']' or '}' or '>') depth = Math.Max(0, depth - 1);
            else if (c == separator && depth == 0)
            {
                parts.Add(text[start..i]);
                start = i + 1;
            }
        }

        parts.Add(text[start..]);
        return parts;
    }

    private static string HeaderOf(string masked)
    {
        var brace = masked.IndexOf('{');
        var semicolon = masked.IndexOf(';');
        var end = new[] { brace, semicolon }.Where(i => i >= 0).DefaultIfEmpty(masked.Length).Min();
        return masked[..end];
    }

    private static string Collapse(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Where code ends on a line: its length without trailing spaces (the line is given masked, so a trailing comment is spaces).</summary>
    public static int CodeEnd(string maskedLine)
    {
        var end = maskedLine.Length;
        while (end > 0 && char.IsWhiteSpace(maskedLine[end - 1])) end--;
        return end;
    }
}
