using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;

/// <summary>
/// Reads errors from javac compiler output and Java runtime exception stack traces into Problems entries.
/// </summary>
public sealed partial class JavaCompilerDiagnosticParser : IDiagnosticParser
{
    private sealed record Frame(string Method, string File, int LineNumber);

    [GeneratedRegex(@"^(?<file>(?:[a-zA-Z]:)?[^:\r\n]+):(?<line>\d+):\s*(?:(?<col>\d+):\s*)?(?<severity>error|warning):\s*(?<message>.+)$")]
    private static partial Regex JavacDiagnosticHeaderRegex();

    [GeneratedRegex(@"^\s*\^\s*$")]
    private static partial Regex CaretMarkerRegex();

    [GeneratedRegex(@"^(?:Exception in thread ""[^""]*""\s+)?(?<type>[a-zA-Z0-9_.]+(?:Exception|Error))(?::\s*(?<message>.*))?$")]
    private static partial Regex ExceptionLineRegex();

    [GeneratedRegex(@"^\s*at\s+(?:(?<method>[a-zA-Z0-9_$.]+)\s*\()?(?<file>[^:]+):(?<line>\d+)\)?$")]
    private static partial Regex StackFrameRegex();

    [GeneratedRegex(@"package\s+(?<pkg>[a-zA-Z0-9_.]+)\s+does not exist", RegexOptions.IgnoreCase)]
    private static partial Regex MissingPackageRegex();

    [GeneratedRegex(@"cannot find symbol.*?\bsymbol:\s+class\s+(?<name>[a-zA-Z0-9_$]+)", RegexOptions.IgnoreCase)]
    private static partial Regex MissingClassRegex();

    [GeneratedRegex(@"cannot find symbol:\s*class\s+(?<name>[a-zA-Z0-9_$]+)", RegexOptions.IgnoreCase)]
    private static partial Regex InlineMissingClassRegex();

    public DiagnosticParseResult Parse(string output, string sourceFilePath)
    {
        if (string.IsNullOrWhiteSpace(output)) return DiagnosticParseResult.Empty;

        var lines = output.Replace("\r\n", "\n").Split('\n');
        var diagnostics = ParseCompilerDiagnostics(lines, sourceFilePath, out var missingDep);

        if (diagnostics.Count > 0)
        {
            return new DiagnosticParseResult(diagnostics, missingDep);
        }

        // If no javac compiler errors were found, parse runtime exceptions and stack traces
        var runtimeDiagnostics = ParseRuntimeExceptions(lines, sourceFilePath);
        return new DiagnosticParseResult(runtimeDiagnostics);
    }

    private static List<DiagnosticItem> ParseCompilerDiagnostics(string[] lines, string sourceFilePath, out string? missingDependency)
    {
        var diagnostics = new List<DiagnosticItem>();
        missingDependency = null;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var match = JavacDiagnosticHeaderRegex().Match(line);
            if (!match.Success) continue;

            var file = match.Groups["file"].Value.Trim();
            if (!SamePath(file, sourceFilePath)) continue;

            int.TryParse(match.Groups["line"].Value, out var lineNumber);
            lineNumber = Math.Max(1, lineNumber);

            var col = 1;
            if (match.Groups["col"].Success && int.TryParse(match.Groups["col"].Value, out var parsedCol))
            {
                col = Math.Max(1, parsedCol);
            }

            var severityStr = match.Groups["severity"].Value;
            var severity = string.Equals(severityStr, "warning", StringComparison.OrdinalIgnoreCase)
                ? DiagnosticSeverity.Warning
                : DiagnosticSeverity.Error;

            var message = match.Groups["message"].Value.Trim();

            // Check for missing package directive in primary message
            if (missingDependency == null)
            {
                var pkgMatch = MissingPackageRegex().Match(message);
                if (pkgMatch.Success)
                {
                    missingDependency = pkgMatch.Groups["pkg"].Value;
                }
                else
                {
                    var inlineMatch = InlineMissingClassRegex().Match(message);
                    if (inlineMatch.Success)
                    {
                        missingDependency = inlineMatch.Groups["name"].Value;
                    }
                }
            }

            // Look ahead for caret column and additional message details (symbol:, location:)
            var extraDetails = new List<string>();
            var lookAheadLimit = Math.Min(lines.Length, i + 6);
            for (var j = i + 1; j < lookAheadLimit; j++)
            {
                var nextLine = lines[j];
                if (JavacDiagnosticHeaderRegex().IsMatch(nextLine)) break;
                if (Regex.IsMatch(nextLine, @"^\d+\s+(?:error|warning)s?")) break;

                var caretMatch = CaretMarkerRegex().Match(nextLine);
                if (caretMatch.Success)
                {
                    col = Math.Max(1, nextLine.IndexOf('^') + 1);
                    continue;
                }

                var trimmed = nextLine.Trim();
                if (trimmed.StartsWith("symbol:", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("location:", StringComparison.OrdinalIgnoreCase))
                {
                    extraDetails.Add(trimmed);

                    if (missingDependency == null && trimmed.StartsWith("symbol:", StringComparison.OrdinalIgnoreCase))
                    {
                        var symMatch = Regex.Match(trimmed, @"symbol:\s+class\s+(?<name>[a-zA-Z0-9_$]+)", RegexOptions.IgnoreCase);
                        if (symMatch.Success)
                        {
                            missingDependency = symMatch.Groups["name"].Value;
                        }
                    }
                }
            }

            if (extraDetails.Count > 0)
            {
                message = $"{message} ({string.Join("; ", extraDetails)})";
            }

            diagnostics.Add(new DiagnosticItem
            {
                Id = severity == DiagnosticSeverity.Error ? "JAVAC" : "JAVAC_WARN",
                Message = message,
                Severity = severity,
                Line = lineNumber,
                Column = col,
                EndLine = lineNumber,
                EndColumn = col + 1
            });
        }

        return diagnostics;
    }

    private static List<DiagnosticItem> ParseRuntimeExceptions(string[] lines, string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticItem>();
        var (errorType, errorMessage) = FindExceptionSummary(lines);
        if (errorType == null) return diagnostics;

        var frames = FramesOf(lines);
        var inScript = frames.LastOrDefault(f => SamePath(f.File, sourceFilePath));

        var line = inScript?.LineNumber ?? (frames.Count > 0 ? frames[0].LineNumber : 1);
        var id = errorType;
        var message = !string.IsNullOrWhiteSpace(errorMessage) ? $"{id}: {errorMessage}" : id;

        diagnostics.Add(new DiagnosticItem
        {
            Id = id,
            Message = message,
            Severity = DiagnosticSeverity.Error,
            Line = Math.Max(1, line),
            Column = 1,
            EndLine = Math.Max(1, line),
            EndColumn = 1
        });

        return diagnostics;
    }

    private static (string? errorType, string? errorMessage) FindExceptionSummary(string[] lines)
    {
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            var match = ExceptionLineRegex().Match(line);
            if (match.Success)
            {
                var type = match.Groups["type"].Value;
                var msg = match.Groups["message"].Success ? match.Groups["message"].Value.Trim() : null;
                return (type, msg);
            }
        }
        return (null, null);
    }

    private static List<Frame> FramesOf(string[] lines)
    {
        var frames = new List<Frame>();
        foreach (var raw in lines)
        {
            var match = StackFrameRegex().Match(raw);
            if (!match.Success) continue;

            var method = match.Groups["method"].Success ? match.Groups["method"].Value : string.Empty;
            var file = match.Groups["file"].Value;
            int.TryParse(match.Groups["line"].Value, out var line);

            frames.Add(new Frame(method, file, line));
        }
        return frames;
    }

    private static bool SamePath(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return true;
        var fileNameA = Path.GetFileName(a);
        var fileNameB = Path.GetFileName(b);
        if (string.Equals(fileNameA, fileNameB, StringComparison.OrdinalIgnoreCase)) return true;

        if (a.EndsWith(".java", StringComparison.OrdinalIgnoreCase) && b.EndsWith(".java", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
