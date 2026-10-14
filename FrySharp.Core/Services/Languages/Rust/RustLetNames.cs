namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;

/// <summary>
/// The variables a <c>let</c> statement makes, when they are plain names: <c>let x = 5</c>, <c>let mut v: Vec&lt;i32&gt; = …</c> and
/// <c>let (a, mut b) = …</c>. A pattern that names a type or a variant (<c>let Point { x, y } = p</c>, <c>let Some(v) = o else …</c>)
/// isn't read: the notebook keeps only the variables it is sure of.
/// </summary>
internal static class RustLetNames
{
    public static IReadOnlyList<string> Of(string statement)
    {
        var text = statement.TrimStart();
        if (text.Length < 4 || !text.StartsWith("let", StringComparison.Ordinal) || !char.IsWhiteSpace(text[3])) return [];

        // The pattern runs to the first top-level '=' (a value), ':' (a type; '::' is a path) or ';'.
        var depth = 0;
        var end = text.Length;
        for (var i = 3; i < text.Length; i++)
        {
            var c = text[i];
            if (c is '(' or '[' or '{') depth++;
            else if (c is ')' or ']' or '}') depth--;
            else if (depth == 0 && (c == '=' || c == ';')) { end = i; break; }
            else if (depth == 0 && c == ':' && !(i + 1 < text.Length && text[i + 1] == ':') && !(text[i - 1] == ':')) { end = i; break; }
        }

        return NamesIn(text[3..end].Trim());
    }

    private static IReadOnlyList<string> NamesIn(string pattern)
    {
        var names = new List<string>();
        var i = 0;
        while (i < pattern.Length)
        {
            var c = pattern[i];
            if (c is '(' or ')' or ',' || char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (!(char.IsLetter(c) || c == '_')) return [];
            var start = i;
            while (i < pattern.Length && (char.IsLetterOrDigit(pattern[i]) || pattern[i] == '_')) i++;
            var word = pattern[start..i];

            // r#type is a name too.
            if (word == "r" && i < pattern.Length && pattern[i] == '#')
            {
                i++;
                var rawStart = i;
                while (i < pattern.Length && (char.IsLetterOrDigit(pattern[i]) || pattern[i] == '_')) i++;
                word = "r#" + pattern[rawStart..i];
            }

            if (word is "mut" or "ref" or "_") continue;

            // A capital says it names a type, a variant or a constant, which makes this a pattern that isn't just variables.
            if (char.IsUpper(word[0])) return [];
            if (!names.Contains(word)) names.Add(word);
        }

        return names;
    }
}
