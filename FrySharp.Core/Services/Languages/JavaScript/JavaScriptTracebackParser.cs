using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.JavaScript;

/// <summary>
/// Reads errors from a failed Node.js run into a Problems entry, identifying syntax errors, unhandled exceptions,
/// stack frames, and missing npm packages.
/// </summary>
public sealed partial class JavaScriptTracebackParser : IDiagnosticParser
{
    private sealed record Frame(string File, int LineNumber, int Column);

    [GeneratedRegex(@"^\s*at (?:(?<method>.+?)\s+\()?(?:file:\/\/)?(?<file>.+?):(?<line>\d+):(?<col>\d+)\)?$")]
    private static partial Regex StackFrameRegex();

    [GeneratedRegex(@"^(?![A-Za-z]:[\\/])(?:Error\s*\[(?<code>[A-Z0-9_]+)\]|(?<type>[A-Za-z_$][\w.$]*)):(?:\s*(?<message>.*))?$")]
    private static partial Regex ExceptionLineRegex();

    [GeneratedRegex(@"^(?:file:\/\/)?(?<file>(?:[a-zA-Z]:)?[^:\r\n]+):(?<line>\d+)(?::(?<col>\d+))?$")]
    private static partial Regex HeaderLocationRegex();

    [GeneratedRegex(@"Cannot find (?:module|package) '(?<name>[^']+)'")]
    private static partial Regex MissingModuleRegex();

    [GeneratedRegex(@"^\s*\^\s*$")]
    private static partial Regex CaretMarkerRegex();

    public DiagnosticParseResult Parse(string output, string sourceFilePath)
    {
        if (string.IsNullOrWhiteSpace(output)) return DiagnosticParseResult.Empty;

        var lines = output.Replace("\r\n", "\n").Split('\n');
        var (summaryLine, errorType, errorMessage) = FindExceptionSummary(lines);

        var frames = FramesOf(lines);
        var inScript = frames.LastOrDefault(f => SamePath(f.File, sourceFilePath));
        var headerLocation = FindHeaderLocation(lines, sourceFilePath);

        var id = errorType ?? "Error";
        var message = !string.IsNullOrWhiteSpace(errorMessage) ? errorMessage : id;

        var line = 1;
        var column = 1;

        if (inScript != null)
        {
            line = inScript.LineNumber;
            column = inScript.Column;
        }
        else if (headerLocation != null)
        {
            line = headerLocation.LineNumber;
            column = headerLocation.Column;
        }
        else if (frames.Count > 0)
        {
            var deepest = frames[0];
            message += $" (in {Path.GetFileName(deepest.File)}, line {deepest.LineNumber})";
        }

        var diagnostic = new DiagnosticItem
        {
            Id = id,
            Message = message,
            Severity = DiagnosticSeverity.Error,
            Line = line,
            EndLine = line,
            Column = column,
            EndColumn = column
        };

        string? missingPackage = null;
        var missingMatch = MissingModuleRegex().Match(output);
        if (missingMatch.Success)
        {
            var rawName = missingMatch.Groups["name"].Value;
            missingPackage = JavaScriptPackageMap.PackageFor(rawName);
        }

        return new DiagnosticParseResult([diagnostic], missingPackage);
    }

    private static (int Index, string? Type, string? Message) FindExceptionSummary(string[] lines)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            var text = lines[i].Trim();
            if (string.IsNullOrEmpty(text)) continue;

            var match = ExceptionLineRegex().Match(text);
            if (match.Success)
            {
                var type = match.Groups["code"].Success
                    ? match.Groups["code"].Value
                    : match.Groups["type"].Value;

                var msg = match.Groups["message"].Success
                    ? match.Groups["message"].Value.Trim()
                    : string.Empty;

                return (i, type, msg);
            }
        }

        return (-1, null, null);
    }

    private static List<Frame> FramesOf(string[] lines)
    {
        var frames = new List<Frame>();
        foreach (var raw in lines)
        {
            var match = StackFrameRegex().Match(raw);
            if (!match.Success) continue;

            var file = match.Groups["file"].Value.Trim();
            var line = int.Parse(match.Groups["line"].Value);
            var col = int.Parse(match.Groups["col"].Value);
            frames.Add(new Frame(file, line, col));
        }

        return frames;
    }

    private static Frame? FindHeaderLocation(string[] lines, string sourceFilePath)
    {
        for (var i = 0; i < Math.Min(lines.Length, 6); i++)
        {
            var line = lines[i].Trim();
            var match = HeaderLocationRegex().Match(line);
            if (match.Success)
            {
                var file = match.Groups["file"].Value.Trim();
                var lineNum = int.Parse(match.Groups["line"].Value);
                var col = match.Groups["col"].Success ? int.Parse(match.Groups["col"].Value) : 1;

                if (i + 2 < lines.Length && CaretMarkerRegex().IsMatch(lines[i + 2]))
                {
                    col = lines[i + 2].IndexOf('^') + 1;
                }

                if (SamePath(file, sourceFilePath) || file == "[eval]" || file.StartsWith("[eval", StringComparison.Ordinal))
                {
                    return new Frame(file, lineNum, col);
                }
            }
        }

        return null;
    }

    private static bool SamePath(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        var normA = a.Replace('\\', '/').TrimEnd('/');
        var normB = b.Replace('\\', '/').TrimEnd('/');
        return string.Equals(normA, normB, StringComparison.OrdinalIgnoreCase) ||
               normA.EndsWith("/" + Path.GetFileName(normB), StringComparison.OrdinalIgnoreCase);
    }
}
