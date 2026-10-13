using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.CSharp;

/// <summary>
/// Reads diagnostic errors from <c>dotnet build</c> / Roslyn command-line outputs and .NET runtime exception stack traces.
/// Supports extracting missing package/namespace dependencies for CS0246 and CS0103.
/// </summary>
public sealed partial class CSharpCompilerDiagnosticParser : IDiagnosticParser
{
    [GeneratedRegex(@"^(?<file>[^(\r\n]+)\((?<line>\d+),(?<col>\d+)\):\s*(?<severity>error|warning)\s*(?<code>[A-Z0-9]+):\s*(?<message>.+?)(?:\s*\[[^\]]+\])?$", RegexOptions.Multiline)]
    private static partial Regex DotNetDiagnosticRegex();

    [GeneratedRegex(@"The type or namespace name '(?<name>[^']+)' could not be found")]
    private static partial Regex MissingTypeRegex();

    [GeneratedRegex(@"The name '(?<name>[^']+)' does not exist in the current context")]
    private static partial Regex MissingNameRegex();

    [GeneratedRegex(@"^(?<type>[a-zA-Z0-9_.]+(?:Exception|Error))(?::\s*(?<message>.*))?$")]
    private static partial Regex ExceptionLineRegex();

    [GeneratedRegex(@"^\s*at\s+(?:(?<method>[^\(]+)\s*\()?[^:]*in\s+(?<file>[^:]+):line\s+(?<line>\d+)")]
    private static partial Regex StackFrameRegex();

    public DiagnosticParseResult Parse(string output, string sourceFilePath)
    {
        if (string.IsNullOrWhiteSpace(output)) return DiagnosticParseResult.Empty;

        var lines = output.Replace("\r\n", "\n").Split('\n');
        var diagnostics = ParseCompilerDiagnostics(lines, sourceFilePath, out var missingDep);

        if (diagnostics.Count > 0)
        {
            return new DiagnosticParseResult(diagnostics, missingDep);
        }

        var runtimeDiagnostics = ParseRuntimeExceptions(lines, sourceFilePath);
        return new DiagnosticParseResult(runtimeDiagnostics);
    }

    private static List<DiagnosticItem> ParseCompilerDiagnostics(string[] lines, string sourceFilePath, out string? missingDependency)
    {
        var diagnostics = new List<DiagnosticItem>();
        missingDependency = null;

        foreach (var line in lines)
        {
            var match = DotNetDiagnosticRegex().Match(line.Trim());
            if (!match.Success) continue;

            var file = match.Groups["file"].Value.Trim();
            if (!SamePath(file, sourceFilePath) && !SameFileName(file, sourceFilePath))
            {
                continue;
            }

            int.TryParse(match.Groups["line"].Value, out var lineNumber);
            int.TryParse(match.Groups["col"].Value, out var colNumber);
            lineNumber = Math.Max(1, lineNumber);
            colNumber = Math.Max(1, colNumber);

            var severityStr = match.Groups["severity"].Value;
            var severity = severityStr.Equals("warning", StringComparison.OrdinalIgnoreCase)
                ? DiagnosticSeverity.Warning
                : DiagnosticSeverity.Error;

            var code = match.Groups["code"].Value;
            var message = match.Groups["message"].Value.Trim();

            if (code == "CS0246" && missingDependency == null)
            {
                var typeMatch = MissingTypeRegex().Match(message);
                if (typeMatch.Success)
                {
                    missingDependency = typeMatch.Groups["name"].Value;
                }
            }
            else if (code == "CS0103" && missingDependency == null)
            {
                var nameMatch = MissingNameRegex().Match(message);
                if (nameMatch.Success)
                {
                    missingDependency = nameMatch.Groups["name"].Value;
                }
            }

            diagnostics.Add(new DiagnosticItem
            {
                Id = code,
                Message = message,
                Severity = severity,
                Line = lineNumber,
                Column = colNumber
            });
        }

        return diagnostics;
    }

    private static List<DiagnosticItem> ParseRuntimeExceptions(string[] lines, string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticItem>();
        string? currentExceptionType = null;
        string? currentMessage = null;

        for (var i = 0; i < lines.Length; i++)
        {
            var raw = lines[i].Trim();
            if (raw.StartsWith("Unhandled exception.", StringComparison.OrdinalIgnoreCase))
            {
                raw = raw["Unhandled exception.".Length..].Trim();
            }

            var exMatch = ExceptionLineRegex().Match(raw);
            if (exMatch.Success)
            {
                currentExceptionType = exMatch.Groups["type"].Value;
                currentMessage = exMatch.Groups["message"].Success && !string.IsNullOrWhiteSpace(exMatch.Groups["message"].Value)
                    ? exMatch.Groups["message"].Value.Trim()
                    : currentExceptionType;
                continue;
            }

            var frameMatch = StackFrameRegex().Match(raw);
            if (frameMatch.Success && currentExceptionType != null)
            {
                var file = frameMatch.Groups["file"].Value.Trim();
                if (SamePath(file, sourceFilePath) || SameFileName(file, sourceFilePath))
                {
                    int.TryParse(frameMatch.Groups["line"].Value, out var lineNum);
                    diagnostics.Add(new DiagnosticItem
                    {
                        Id = currentExceptionType,
                        Message = currentMessage ?? currentExceptionType,
                        Severity = DiagnosticSeverity.Error,
                        Line = Math.Max(1, lineNum),
                        Column = 1
                    });
                    break;
                }
            }
        }

        return diagnostics;
    }

    private static bool SamePath(string a, string b)
    {
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return true;
        try
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool SameFileName(string a, string b)
    {
        return string.Equals(Path.GetFileName(a), Path.GetFileName(b), StringComparison.OrdinalIgnoreCase);
    }
}
