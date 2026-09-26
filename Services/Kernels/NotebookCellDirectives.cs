using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Packages;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;

/// <summary><c>#!share --from csharp nums --as numbers</c>: copy <c>nums</c> from the C# kernel into this cell's kernel.</summary>
public sealed record ShareDirective(string FromLanguage, string Name, string? As, int LineNumber)
{
    public string TargetName => As ?? Name;
}

/// <summary>What the directive lines at the top of a cell ask for, and the code with those lines blanked out.</summary>
public sealed class CellDirectives
{
    /// <summary>The language a <c>#!python</c> / <c>#!csharp</c> line picked, or null.</summary>
    public string? LanguageId { get; init; }

    public IReadOnlyList<ShareDirective> Shares { get; init; } = Array.Empty<ShareDirective>();

    public IReadOnlyList<PackageCommand> PackageCommands { get; init; } = Array.Empty<PackageCommand>();

    /// <summary>Directives that couldn't be understood; the cell doesn't run while there are any.</summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    /// <summary>The cell's code with directive lines left blank, so error line numbers still match the editor.</summary>
    public required string Code { get; init; }
}

/// <summary>
/// Reads the Polyglot-style directives at the top of a notebook cell: <c>#!python</c> or <c>#!csharp</c> (any
/// registered language's id or alias), <c>#!share --from &lt;language&gt; &lt;name&gt; [--as &lt;new name&gt;]</c>, and the cell
/// language's package commands such as <c>%pip install numpy</c>. Only the leading lines are directives: blank lines
/// and comments may sit between them, and the first line of code ends them.
/// </summary>
public static class NotebookCellDirectives
{
    public static CellDirectives Parse(string? source, LanguageRegistry registry, string? defaultLanguageId)
    {
        source ??= string.Empty;
        var lines = source.Split('\n');
        var language = registry.Get(defaultLanguageId);
        string? pickedLanguage = null;
        var shares = new List<ShareDirective>();
        var packageCommands = new List<PackageCommand>();
        var errors = new List<string>();
        var blanked = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.Length == 0) continue;

            if (trimmed.StartsWith("#!", StringComparison.Ordinal))
            {
                if (trimmed.StartsWith("#!/", StringComparison.Ordinal)) continue; // a shebang line is only a comment

                var body = trimmed[2..].Trim();
                var word = FirstWord(body);
                if (word == "share")
                {
                    if (TryParseShare(body, i + 1, out var share, out var error)) shares.Add(share);
                    else errors.Add(error);
                }
                else if (registry.Get(word) is { } named && named.NotebookKernels != null)
                {
                    pickedLanguage = named.Id;
                    language = named;
                }
                else
                {
                    errors.Add($"Line {i + 1}: \"#!{word}\" isn't a directive. Use {LanguageDirectives(registry)} to pick the cell's language, or #!share --from <language> <name> to copy a value.");
                }

                lines[i] = Blank(lines[i]);
                blanked = true;
                continue;
            }

            if (language?.Packages is { } packages && packages.TryParseDirective(trimmed, out var command))
            {
                packageCommands.Add(command);
                lines[i] = Blank(lines[i]);
                blanked = true;
                continue;
            }

            if (language != null && trimmed.StartsWith(language.LineCommentPrefix, StringComparison.Ordinal)) continue;

            break;
        }

        return new CellDirectives
        {
            LanguageId = pickedLanguage,
            Shares = shares,
            PackageCommands = packageCommands,
            Errors = errors,
            Code = blanked ? string.Join('\n', lines) : source
        };
    }

    /// <summary>
    /// The language a cell's directives pick (<c>#!python</c>), or null; read by the same rules as <see cref="Parse"/>
    /// but without taking the cell apart, so it's cheap enough to call on every keystroke.
    /// </summary>
    public static string? LanguageOf(string? source, LanguageRegistry registry, string? defaultLanguageId)
    {
        if (string.IsNullOrEmpty(source)) return null;
        var language = registry.Get(defaultLanguageId);
        string? picked = null;
        for (var start = 0; start <= source.Length;)
        {
            var end = source.IndexOf('\n', start);
            if (end < 0) end = source.Length;
            var trimmed = source.AsSpan(start, end - start).Trim();
            start = end + 1;
            if (trimmed.IsEmpty) continue;

            if (trimmed.StartsWith("#!", StringComparison.Ordinal))
            {
                if (trimmed.StartsWith("#!/", StringComparison.Ordinal)) continue;
                var word = FirstWord(trimmed[2..].Trim().ToString());
                if (word != "share" && registry.Get(word) is { NotebookKernels: not null } named)
                {
                    picked = named.Id;
                    language = named;
                }
                continue;
            }

            var line = trimmed.ToString();
            if (language?.Packages is { } packages && packages.TryParseDirective(line, out _)) continue;
            if (language != null && line.StartsWith(language.LineCommentPrefix, StringComparison.Ordinal)) continue;
            break;
        }

        return picked;
    }

    private static bool TryParseShare(string body, int lineNumber, out ShareDirective share, out string error)
    {
        share = null!;
        string? from = null, name = null, rename = null;
        var words = body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        for (var i = 1; i < words.Length; i++)
        {
            switch (words[i])
            {
                case "--from" when i + 1 < words.Length:
                    from = words[++i];
                    break;
                case "--as" when i + 1 < words.Length:
                    rename = words[++i];
                    break;
                case var option when option.StartsWith("--", StringComparison.Ordinal):
                    error = $"Line {lineNumber}: #!share doesn't know \"{option}\"{(option is "--from" or "--as" ? " without a value" : string.Empty)}. Write #!share --from <language> <name> [--as <new name>].";
                    return false;
                default:
                    if (name != null)
                    {
                        error = $"Line {lineNumber}: #!share copies one value at a time; \"{words[i]}\" is a second name.";
                        return false;
                    }
                    name = words[i];
                    break;
            }
        }

        if (from == null || name == null)
        {
            error = $"Line {lineNumber}: write #!share --from <language> <name> [--as <new name>], e.g. #!share --from csharp numbers.";
            return false;
        }

        share = new ShareDirective(from, name, rename, lineNumber);
        error = string.Empty;
        return true;
    }

    private static string FirstWord(string text)
    {
        var end = 0;
        while (end < text.Length && !char.IsWhiteSpace(text[end])) end++;
        return text[..end];
    }

    // Keeps a "\r" line ending, so a CRLF cell keeps CRLF.
    private static string Blank(string line) => line.EndsWith('\r') ? "\r" : string.Empty;

    private static string LanguageDirectives(LanguageRegistry registry) =>
        string.Join(" or ", registry.NotebookLanguages.Select(l => "#!" + l.Id));
}
