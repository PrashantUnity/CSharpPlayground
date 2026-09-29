using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;

/// <summary>
/// Parses compiler errors and runtime panic traces from the Go toolchain into structured Problems items.
/// </summary>
public sealed partial class GoCompilerDiagnosticParser : IDiagnosticParser
{
    // Matches: path/file.go:line:col: message
    [GeneratedRegex(@"^(?<file>(?:[a-zA-Z]:)?[^:\r\n]+):(?<line>\d+):(?<col>\d+):\s*(?<message>.+)$")]
    private static partial Regex GoCompilerWithColumnRegex();

    // Matches: path/file.go:line: message
    [GeneratedRegex(@"^(?<file>(?:[a-zA-Z]:)?[^:\r\n]+):(?<line>\d+):\s*(?<message>.+)$")]
    private static partial Regex GoCompilerLineOnlyRegex();

    // Missing package patterns
    [GeneratedRegex(@"no required module provides package\s+(?<pkg>[^;:\s]+)", RegexOptions.IgnoreCase)]
    private static partial Regex MissingModuleRegex();

    [GeneratedRegex(@"package\s+(?<pkg>[^:\s]+)\s+is not in GOROOT", RegexOptions.IgnoreCase)]
    private static partial Regex NotInGorootRegex();

    [GeneratedRegex(@"cannot find package ""(?<pkg>[^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex CannotFindPackageRegex();

    // Panic pattern
    [GeneratedRegex(@"^panic:\s*(?<msg>.+)$")]
    private static partial Regex PanicRegex();

    // Goroutine frame pattern: \t/path/to/file.go:14 +0x5c
    [GeneratedRegex(@"^\s*(?<file>(?:[a-zA-Z]:)?[^:\r\n]+):(?<line>\d+)(?:\s+\+0x[0-9a-fA-F]+)?$")]
    private static partial Regex PanicFrameRegex();

    public DiagnosticParseResult Parse(string output, string sourceFilePath)
    {
        if (string.IsNullOrWhiteSpace(output)) return DiagnosticParseResult.Empty;

        var lines = output.Replace("\r\n", "\n").Split('\n');
        var diagnostics = ParseCompilerDiagnostics(lines, sourceFilePath, out var missingDep);

        if (diagnostics.Count > 0)
        {
            return new DiagnosticParseResult(diagnostics, missingDep);
        }

        var runtimeDiagnostics = ParseRuntimePanics(lines, sourceFilePath);
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

            // Check for missing dependency directives
            var modMatch = MissingModuleRegex().Match(line);
            if (modMatch.Success)
            {
                missingDependency = modMatch.Groups["pkg"].Value.Trim();
            }
            else
            {
                var gorootMatch = NotInGorootRegex().Match(line);
                if (gorootMatch.Success)
                {
                    missingDependency = gorootMatch.Groups["pkg"].Value.Trim();
                }
                else
                {
                    var cannotFindMatch = CannotFindPackageRegex().Match(line);
                    if (cannotFindMatch.Success)
                    {
                        missingDependency = cannotFindMatch.Groups["pkg"].Value.Trim();
                    }
                }
            }

            var colMatch = GoCompilerWithColumnRegex().Match(line);
            if (colMatch.Success)
            {
                var file = colMatch.Groups["file"].Value.Trim();
                int.TryParse(colMatch.Groups["line"].Value, out var lineNum);
                int.TryParse(colMatch.Groups["col"].Value, out var colNum);
                var message = colMatch.Groups["message"].Value.Trim();

                // If error mentions standard warning or note
                var severity = message.Contains("warning:", StringComparison.OrdinalIgnoreCase)
                    ? DiagnosticSeverity.Warning
                    : DiagnosticSeverity.Error;

                var isCurrentFile = string.Equals(file, sourceFilePath, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(Path.GetFileName(file), sourceBaseName, StringComparison.OrdinalIgnoreCase);

                diagnostics.Add(new DiagnosticItem
                {
                    Id = "GO0001",
                    Message = message,
                    Severity = severity,
                    Line = lineNum,
                    Column = colNum,
                    EndLine = lineNum,
                    EndColumn = colNum + 1
                });
                continue;
            }

            var lineOnlyMatch = GoCompilerLineOnlyRegex().Match(line);
            if (lineOnlyMatch.Success)
            {
                var file = lineOnlyMatch.Groups["file"].Value.Trim();
                int.TryParse(lineOnlyMatch.Groups["line"].Value, out var lineNum);
                var message = lineOnlyMatch.Groups["message"].Value.Trim();

                diagnostics.Add(new DiagnosticItem
                {
                    Id = "GO0002",
                    Message = message,
                    Severity = DiagnosticSeverity.Error,
                    Line = lineNum,
                    Column = 1,
                    EndLine = lineNum,
                    EndColumn = 2
                });
            }
        }

        return diagnostics;
    }

    private static List<DiagnosticItem> ParseRuntimePanics(string[] lines, string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticItem>();
        string? panicMessage = null;
        var sourceBaseName = Path.GetFileName(sourceFilePath);

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var panicMatch = PanicRegex().Match(line);
            if (panicMatch.Success)
            {
                panicMessage = panicMatch.Groups["msg"].Value.Trim();
                continue;
            }

            if (panicMessage != null)
            {
                var frameMatch = PanicFrameRegex().Match(line);
                if (frameMatch.Success)
                {
                    var file = frameMatch.Groups["file"].Value.Trim();
                    int.TryParse(frameMatch.Groups["line"].Value, out var lineNum);

                    var isCurrentFile = string.Equals(file, sourceFilePath, StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(Path.GetFileName(file), sourceBaseName, StringComparison.OrdinalIgnoreCase);

                    diagnostics.Add(new DiagnosticItem
                    {
                        Id = "PANIC",
                        Message = $"panic: {panicMessage}",
                        Severity = DiagnosticSeverity.Error,
                        Line = lineNum,
                        Column = 1,
                        EndLine = lineNum,
                        EndColumn = 2
                    });

                    // Found top-most frame in user's program
                    if (isCurrentFile) break;
                }
            }
        }

        return diagnostics;
    }
}
