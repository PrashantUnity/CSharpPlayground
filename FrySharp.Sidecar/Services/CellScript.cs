using System.Text;
using System.Text.RegularExpressions;

namespace FrySharp.Sidecar.Services;

/// <summary>
/// A notebook cell and what the cells before it define, as one piece of C# for the analysis that completes and checks code
/// (which reads one document). The kernel runs cells one at a time and keeps what they define; this is the same seen from
/// the text: the earlier cells first, then this one, so the variables, types and methods of the earlier ones are in scope.
/// </summary>
public static partial class CellScript
{
    [GeneratedRegex(@"^using\s+(static\s+)?[A-Za-z_][\w.]*\s*(=\s*[\w.<>,\s?\[\]]+)?;\s*$")]
    private static partial Regex UsingDirective();

    /// <summary>
    /// The earlier cells (their <c>using</c> lines first, as C# wants them; the directives of the notebook, <c>#!python</c>
    /// and <c>#r</c>, as empty lines) followed by <paramref name="source"/>, and where in that text the caret is.
    /// </summary>
    public static (string Text, int Offset, int PrefixLines) Compose(IReadOnlyList<string>? context, string source, int offset)
    {
        var usings = new StringBuilder();
        var body = new StringBuilder();
        foreach (var cell in context ?? [])
        {
            foreach (var line in cell.Replace("\r\n", "\n").Split('\n'))
            {
                var trimmed = line.Trim();
                if (UsingDirective().IsMatch(trimmed))
                {
                    usings.Append(trimmed).Append('\n');
                }
                else if (IsNotebookDirective(trimmed))
                {
                    body.Append('\n');
                }
                else
                {
                    body.Append(line).Append('\n');
                }
            }
        }

        var prefix = usings.Append(body).ToString();
        var lines = prefix.Count(c => c == '\n');
        return (prefix + source, prefix.Length + Math.Clamp(offset, 0, source.Length), lines);
    }

    // #!csharp, #!share ... (a notebook's own), and the references a script loads: not something the analysis can read.
    private static bool IsNotebookDirective(string trimmed) =>
        (trimmed.StartsWith("#!", StringComparison.Ordinal) && !trimmed.StartsWith("#![", StringComparison.Ordinal))
        || trimmed.StartsWith("#r ", StringComparison.Ordinal)
        || trimmed.StartsWith("#load ", StringComparison.Ordinal);
}
