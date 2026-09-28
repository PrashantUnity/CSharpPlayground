using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Python;

/// <summary>
/// Reads the error at the end of a failed Python run into a Problems entry: the exception and message, at the last
/// line of the script the traceback passes through (where the script called into the code that failed). Understands
/// Python 3.9 to 3.14: chained exceptions, exception groups, the <c>~~^^</c> markers of 3.11+, and syntax errors, which
/// have no "Traceback" heading. A missing module is reported so the Problems entry can offer to install it.
/// </summary>
public sealed partial class PythonTracebackParser : IDiagnosticParser
{
    private sealed record Line(string Text, int Indent, int GroupDepth);

    private sealed record Frame(string File, int LineNumber, int Index);

    [GeneratedRegex(@"^File ""(?<file>.+?)"", line (?<line>\d+)(?:, in .*)?$")]
    private static partial Regex FrameLine();

    [GeneratedRegex(@"^(?<type>[A-Za-z_][\w.]*)(?::\s?(?<message>.*))?$")]
    private static partial Regex ExceptionLine();

    [GeneratedRegex(@"No module named '(?<name>[^']+)'")]
    private static partial Regex NoModuleNamed();

    [GeneratedRegex(@"^[\s^~]+$")]
    private static partial Regex MarkerLine();

    public DiagnosticParseResult Parse(string output, string sourceFilePath)
    {
        if (string.IsNullOrWhiteSpace(output)) return DiagnosticParseResult.Empty;

        var lines = output.Replace("\r\n", "\n").Split('\n').Select(ReadLine).ToList();
        var summaryIndex = FindExceptionSummary(lines);
        if (summaryIndex < 0) return DiagnosticParseResult.Empty;

        var match = ExceptionLine().Match(lines[summaryIndex].Text);
        var type = match.Groups["type"].Value;
        var message = match.Groups["message"].Success ? match.Groups["message"].Value.Trim() : string.Empty;
        var shortType = type.Contains('.') ? type[(type.LastIndexOf('.') + 1)..] : type;

        var frames = FramesOf(lines, summaryIndex);
        var inScript = frames.LastOrDefault(f => SamePath(f.File, sourceFilePath));
        var deepest = frames.LastOrDefault();

        var diagnostic = new DiagnosticItem
        {
            Id = shortType,
            Message = message.Length > 0 ? message : shortType,
            Severity = DiagnosticSeverity.Error,
            Line = 1,
            Column = 1
        };

        if (inScript != null)
        {
            diagnostic.Line = inScript.LineNumber;
            var (start, end) = ColumnsAt(lines, inScript, SourceLine(sourceFilePath, inScript.LineNumber));
            diagnostic.Column = start;
            diagnostic.EndColumn = end;
        }
        else if (deepest != null)
        {
            // The failure is in code the script doesn't contain (and no frame passes through it): say where.
            diagnostic.Message += $" (in {Path.GetFileName(deepest.File)}, line {deepest.LineNumber})";
        }

        diagnostic.EndLine = diagnostic.Line;
        if (diagnostic.EndColumn < diagnostic.Column) diagnostic.EndColumn = diagnostic.Column;

        string? missing = null;
        if (shortType is "ModuleNotFoundError" or "ImportError" && NoModuleNamed().Match(message) is { Success: true } module)
        {
            missing = PythonPackageMap.TopLevel(module.Groups["name"].Value);
        }

        return new DiagnosticParseResult([diagnostic], missing);
    }

    // Exception-group tracebacks draw "  | " and "  +-+---" borders; a line's text is what's inside them.
    private static Line ReadLine(string raw)
    {
        var text = raw.TrimEnd();
        var groupDepth = 0;
        var trimmed = text.TrimStart();
        if (trimmed.StartsWith('|') || trimmed.StartsWith('+'))
        {
            groupDepth = text.Length - trimmed.Length; // how far the border is indented: 2 for the group, 4 for its members
            if (trimmed.StartsWith("+-", StringComparison.Ordinal) || trimmed.StartsWith("+=", StringComparison.Ordinal))
            {
                return new Line(string.Empty, 0, groupDepth);
            }

            text = trimmed.Length > 1 && trimmed[1] == ' ' ? trimmed[2..] : trimmed[1..];
        }

        var content = text.TrimStart();
        return new Line(content, text.Length - content.Length, groupDepth);
    }

    // The last "SomeError: message" line at the left margin, below a frame. Members of an exception group are indented
    // one border further in; the group itself is the error.
    private static int FindExceptionSummary(List<Line> lines)
    {
        for (var i = lines.Count - 1; i > 0; i--)
        {
            var line = lines[i];
            if (line.Indent != 0 || line.GroupDepth > 2 || line.Text.Length == 0) continue;
            if (line.Text.StartsWith("Traceback", StringComparison.Ordinal) || line.Text.StartsWith("During handling", StringComparison.Ordinal) ||
                line.Text.StartsWith("The above exception", StringComparison.Ordinal))
            {
                continue;
            }

            if (!ExceptionLine().IsMatch(line.Text) || !LooksLikeExceptionName(line.Text)) continue;

            for (var j = i - 1; j >= 0; j--)
            {
                if (FrameLine().IsMatch(lines[j].Text)) return i;
                if (lines[j].Indent == 0 && lines[j].Text.Length > 0 && !IsHeading(lines[j].Text)) break;
            }
        }

        return -1;
    }

    private static bool IsHeading(string text) =>
        text.EndsWith("(most recent call last):", StringComparison.Ordinal);

    private static bool LooksLikeExceptionName(string text)
    {
        var colon = text.IndexOf(':');
        var name = colon < 0 ? text : text[..colon];
        return name.EndsWith("Error", StringComparison.Ordinal) || name.EndsWith("Exception", StringComparison.Ordinal) ||
               name.EndsWith("Warning", StringComparison.Ordinal) || name.EndsWith("Group", StringComparison.Ordinal) ||
               name is "KeyboardInterrupt" or "SystemExit" or "StopIteration" or "StopAsyncIteration" or "GeneratorExit";
    }

    // The frames of the traceback that ends at the summary: back to its heading, or (a syntax error has no heading)
    // the frame just above.
    private static List<Frame> FramesOf(List<Line> lines, int summaryIndex)
    {
        var frames = new List<Frame>();
        for (var i = summaryIndex - 1; i >= 0; i--)
        {
            var line = lines[i];
            if (line.Indent == 0 && IsHeading(line.Text)) break;
            if (FrameLine().Match(line.Text) is { Success: true } frame && int.TryParse(frame.Groups["line"].Value, out var number))
            {
                frames.Add(new Frame(frame.Groups["file"].Value, number, i));
            }
            else if (line.Indent == 0 && line.Text.Length > 0)
            {
                break; // the end of an earlier, chained traceback
            }
        }

        frames.Reverse();
        return frames;
    }

    // Python prints the frame's code without its indentation, four spaces in, and under it the ^ or ~^^ markers (3.11+)
    // or a syntax error's caret. The markers' position, put back onto the source line, is the column.
    private static (int Start, int End) ColumnsAt(List<Line> lines, Frame frame, string? sourceLine)
    {
        if (frame.Index + 2 >= lines.Count) return (1, 1);
        var code = lines[frame.Index + 1];
        var marker = lines[frame.Index + 2];
        var markerText = new string(' ', marker.Indent) + marker.Text;
        if (marker.Text.Length == 0 || FrameLine().IsMatch(marker.Text) || !MarkerLine().IsMatch(markerText)) return (1, 1);

        var first = markerText.IndexOfAny(['^', '~']);
        var last = markerText.LastIndexOfAny(['^', '~']);
        if (first < code.Indent) return (1, 1);

        var sourceIndent = 0;
        if (sourceLine != null && sourceLine.Trim() == code.Text.Trim())
        {
            sourceIndent = sourceLine.Length - sourceLine.TrimStart().Length;
        }

        return (sourceIndent + first - code.Indent + 1, sourceIndent + last - code.Indent + 1);
    }

    private static string? SourceLine(string path, int lineNumber)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var line = File.ReadLines(path).Skip(lineNumber - 1).FirstOrDefault();
            return line?.TrimEnd('\r');
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool SamePath(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b) || a.StartsWith('<')) return false;
        try
        {
            return string.Equals(
                Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}
