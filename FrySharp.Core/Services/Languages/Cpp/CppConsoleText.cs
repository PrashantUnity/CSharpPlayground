using System.Text.RegularExpressions;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;

/// <summary>
/// What the compiler says, in the cell's own terms: it names the generated program's file and its lines (<c>/tmp/.../cell.cpp:12:5:</c>,
/// <c>cell.cpp(12,5)</c> for MSVC, and the quoted <c> 12 | code</c> lines), and the user wrote a cell, so the file becomes <c>cell</c> and the
/// lines the cell's own (<c>cell:3:5:</c>).
/// </summary>
internal static partial class CppConsoleText
{
    public const string ProgramFileName = "cell.cpp";

    [GeneratedRegex(@"(?:[^\s:""(<>]*[\\/])?cell\.cpp(?::(?<line>\d+)(?::(?<column>\d+))?|\((?<mline>\d+)(?:,(?<mcolumn>\d+))?\))?")]
    private static partial Regex Position();

    // " 12 | int x = ..." in a compiler's quoted source
    [GeneratedRegex(@"^(\s*)(\d+)(\s+\|)")]
    private static partial Regex Gutter();

    [GeneratedRegex(@"\A(?:In file included from [^\n]*\n)+")]
    private static partial Regex IncludeChain();

    public static string Positions(string text, CppCellProgram program)
    {
        if (text.Length == 0 || !text.Contains(ProgramFileName, StringComparison.Ordinal)) return text;
        return Position().Replace(text, match =>
        {
            var lineGroup = match.Groups["line"].Success ? match.Groups["line"] : match.Groups["mline"];
            if (!lineGroup.Success) return "cell";
            var line = program.CellLineOf(int.Parse(lineGroup.Value));
            if (line == null) return "an earlier cell";
            var column = match.Groups["column"].Success ? match.Groups["column"] : match.Groups["mcolumn"];
            return column.Success ? $"cell:{line}:{column.Value}" : $"cell:{line}";
        });
    }

    /// <summary>The compiler's messages for a program that didn't build: at the cell's positions, with the quoted lines' numbers the cell's.</summary>
    public static string Build(string text, CppCellProgram program)
    {
        // "In file included from ..." introduces a warning from a header: not about the cell
        var lines = Positions(text.Replace("\r\n", "\n"), program).Split('\n').Where(l => !l.StartsWith("In file included from", StringComparison.Ordinal) && !l.StartsWith("                 from ", StringComparison.Ordinal)).ToArray();
        for (var i = 0; i < lines.Length; i++)
        {
            var match = Gutter().Match(lines[i]);
            if (!match.Success) continue;
            if (program.CellLineOf(int.Parse(match.Groups[2].Value)) is not { } cellLine) continue;
            lines[i] = match.Groups[1].Value + cellLine.ToString().PadLeft(match.Groups[2].Length) + lines[i][(match.Groups[1].Length + match.Groups[2].Length)..];
        }

        return string.Join('\n', lines).Trim();
    }
}
