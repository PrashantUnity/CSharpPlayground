using System.Text.RegularExpressions;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;

/// <summary>
/// What the Go toolchain and a panic say, in the cell's own terms: they name the generated program's file and its lines
/// (<c>./main.go:12:5</c>, <c>/tmp/.../main.go:15 +0x1d</c>), and the user wrote a cell, so the file becomes <c>cell</c> and the line the cell's own.
/// </summary>
internal static partial class GoConsoleText
{
    // The generated program's file, with the folder it is in (or ./ for the one the build runs in), and a line and a column.
    [GeneratedRegex(@"(?:\./|(?:[a-zA-Z]:)?[^\s:""]*[\\/])?main\.go(?::(?<line>\d+)(?::(?<column>\d+))?)?")]
    private static partial Regex Position();

    [GeneratedRegex(@"\A(?:# [^\n]*\n)+")]
    private static partial Regex PackageHeader();

    public static string Positions(string text, GoCellProgram program)
    {
        if (text.Length == 0 || !text.Contains(GoNotebookKernel.ProgramFileName, StringComparison.Ordinal)) return text;
        return Position().Replace(text, match =>
        {
            if (!match.Groups["line"].Success) return "cell";
            var line = program.CellLineOf(int.Parse(match.Groups["line"].Value));
            if (line == null) return "an earlier cell";
            return match.Groups["column"].Success ? $"cell:{line}:{match.Groups["column"].Value}" : $"cell:{line}";
        });
    }

    /// <summary>The compiler's messages for a program that didn't build: without the "# package" line, and at the cell's positions.</summary>
    public static string Build(string text, GoCellProgram program) => Positions(PackageHeader().Replace(text.Replace("\r\n", "\n"), string.Empty), program).Trim();
}
