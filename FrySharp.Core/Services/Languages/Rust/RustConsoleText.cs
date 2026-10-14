using System.Text.RegularExpressions;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;

/// <summary>
/// What rustc and a panic say, in the cell's own terms: they name the generated program's file and its lines (<c>/tmp/.../cell.rs:6:14</c>),
/// and the user wrote a cell, so the file becomes <c>cell</c> and the line the cell's own (<c>cell:2:14</c>).
/// </summary>
internal static partial class RustConsoleText
{
    // "6 | let y: i32 = ..." and "6 - old" / "6 + new" in a suggestion: the line number of a snippet rustc quotes.
    [GeneratedRegex(@"^(\s*)(\d+)(\s+[|+-](?:\s|$))")]
    private static partial Regex Gutter();

    [GeneratedRegex(@"could not compile `[^`]+` \(bin ""[^""]+""\)")]
    private static partial Regex CompileSummary();

    /// <summary>A position in the program's file, in a line of text, as a position in the cell: a panic's <c>panicked at …/cell.rs:8:2:</c>.</summary>
    public static string Positions(string text, string sourcePath, RustCellProgram program)
    {
        if (text.Length == 0 || text.IndexOf(sourcePath, StringComparison.Ordinal) < 0) return text;
        return Regex.Replace(text, Regex.Escape(sourcePath) + @"(?::(?<line>\d+)(?::(?<column>\d+))?)?", match =>
        {
            if (!match.Groups["line"].Success) return "cell";
            var line = program.CellLineOf(int.Parse(match.Groups["line"].Value));
            if (line == null) return "an earlier cell";
            return match.Groups["column"].Success ? $"cell:{line}:{match.Groups["column"].Value}" : $"cell:{line}";
        });
    }

    /// <summary>Rustc's messages for a program that didn't build: positions, the quoted lines' numbers and the generated crate's name.</summary>
    public static string Build(string text, string sourcePath, RustCellProgram program)
    {
        var lines = Positions(text, sourcePath, program).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var match = Gutter().Match(lines[i]);
            if (!match.Success) continue;

            // rustc numbers the lines of the program; the cell has fewer lines before its own, so the number is never wider.
            if (program.CellLineOf(int.Parse(match.Groups[2].Value)) is not { } cellLine) continue;
            lines[i] = match.Groups[1].Value + cellLine.ToString().PadLeft(match.Groups[2].Length) + lines[i][(match.Groups[1].Length + match.Groups[2].Length)..];
        }

        return CompileSummary().Replace(string.Join('\n', lines), "could not compile the cell");
    }
}
