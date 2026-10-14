using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.FSharp;

/// <summary>
/// Parses F# compiler diagnostics and runtime stack traces from <c>dotnet fsi</c> into structured Problems items.
/// </summary>
public sealed partial class FSharpCompilerDiagnosticParser : IDiagnosticParser
{
    // Matches: path/file.fsx(line,col): error FS0001: message
    // or: path/file.fsx(line,col,endLine,endCol): warning FS0025: message
    [GeneratedRegex(@"^(?<file>(?:[a-zA-Z]:)?[^(\r\n]+)\((?<line>\d+),(?<col>\d+)(?:,\d+,\d+)?\):\s*(?<sev>error|warning)\s*(?<code>[A-Za-z0-9]+):\s*(?<msg>.+)$")]
    private static partial Regex FSharpCompilerRegex();

    // Matches missing module: error FS0039: The namespace or module 'X' is not defined.
    [GeneratedRegex(@"error\s+FS0039:\s*The\s+(?:namespace\s+or\s+module|field,\s+constructor\s+or\s+member)\s+'(?<pkg>[^']+)'\s+is\s+not\s+defined", RegexOptions.IgnoreCase)]
    private static partial Regex MissingModuleRegex();

    // Matches missing assembly: error FS0074: The type referenced through 'X' is defined in an assembly that is not referenced
    [GeneratedRegex(@"error\s+FS0074:\s*The\s+type\s+referenced\s+through\s+'(?<pkg>[^']+)'\s+is\s+defined\s+in\s+an\s+assembly\s+that\s+is\s+not\s+referenced", RegexOptions.IgnoreCase)]
    private static partial Regex MissingAssemblyRegex();

    // Matches runtime stack frame: at ... in /path/to/file.fsx:line 12
    [GeneratedRegex(@"^\s*at\s+.*?\s+in\s+(?<file>(?:[a-zA-Z]:)?[^:\r\n]+):line\s+(?<line>\d+)$")]
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
        return new DiagnosticParseResult(runtimeDiagnostics, missingDep);
    }

    private static List<DiagnosticItem> ParseCompilerDiagnostics(string[] lines, string sourceFilePath, out string? missingDependency)
    {
        var diagnostics = new List<DiagnosticItem>();
        missingDependency = null;

        var sourceBaseName = Path.GetFileName(sourceFilePath);

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var modMatch = MissingModuleRegex().Match(line);
            if (modMatch.Success)
            {
                missingDependency ??= modMatch.Groups["pkg"].Value.Trim();
            }
            else
            {
                var asmMatch = MissingAssemblyRegex().Match(line);
                if (asmMatch.Success)
                {
                    missingDependency ??= asmMatch.Groups["pkg"].Value.Trim();
                }
            }

            var match = FSharpCompilerRegex().Match(line);
            if (!match.Success) continue;

            var reportedFile = match.Groups["file"].Value.Trim();
            var lineNum = int.Parse(match.Groups["line"].Value);
            var colNum = int.Parse(match.Groups["col"].Value);
            var sevText = match.Groups["sev"].Value.Trim();
            var code = match.Groups["code"].Value.Trim();
            var msg = match.Groups["msg"].Value.Trim();

            var severity = sevText.Equals("warning", StringComparison.OrdinalIgnoreCase)
                ? DiagnosticSeverity.Warning
                : DiagnosticSeverity.Error;

            diagnostics.Add(new DiagnosticItem
            {
                Id = code,
                Message = msg,
                Severity = severity,
                Line = lineNum,
                Column = colNum,
                EndLine = lineNum,
                EndColumn = colNum + 1
            });
        }

        return diagnostics;
    }

    private static List<DiagnosticItem> ParseRuntimeExceptions(string[] lines, string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticItem>();
        var sourceBaseName = Path.GetFileName(sourceFilePath);

        string? exceptionMessage = null;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("System.", StringComparison.Ordinal) && line.Contains(':') && exceptionMessage == null)
            {
                exceptionMessage = line;
            }

            var frameMatch = StackFrameRegex().Match(line);
            if (frameMatch.Success)
            {
                var reportedFile = frameMatch.Groups["file"].Value.Trim();
                var lineNum = int.Parse(frameMatch.Groups["line"].Value);

                if (string.Equals(Path.GetFileName(reportedFile), sourceBaseName, StringComparison.OrdinalIgnoreCase) ||
                    reportedFile.Contains(sourceBaseName, StringComparison.OrdinalIgnoreCase))
                {
                    diagnostics.Add(new DiagnosticItem
                    {
                        Id = "FS-EXC",
                        Message = exceptionMessage ?? "Runtime Exception occurred in F# script.",
                        Severity = DiagnosticSeverity.Error,
                        Line = lineNum,
                        Column = 1,
                        EndLine = lineNum,
                        EndColumn = 2
                    });
                    break;
                }
            }
        }

        return diagnostics;
    }
}
