#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;

namespace DartSupportExtension;

/// <summary>
/// Parses compiler errors, analyzer diagnostics, and runtime traces from the Dart SDK into structured Problems items.
/// </summary>
public sealed class DartDiagnosticParser : IDiagnosticParser
{
    private static readonly Regex DartErrorRegex = new(
        @"^(?<file>(?:[a-zA-Z]:)?[^:\r\n]+):(?<line>\d+):(?<col>\d+):\s*(?:(?<severity>Error|Warning|Info):\s*)?(?<message>.+)$",
        RegexOptions.Compiled);

    private static readonly Regex MissingPackageRegex = new(
        @"(?:could not find package|Cannot find package|no version for package)\s+['""]?(?<pkg>[a-zA-Z0-9_\-]+)['""]?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex UnhandledExceptionRegex = new(
        @"^Unhandled exception:\s*(?<msg>.+)$",
        RegexOptions.Compiled);

    private static readonly Regex StackFrameRegex = new(
        @"^#\d+\s+.*?\((?<file>(?:[a-zA-Z]:)?[^:\r\n]+):(?<line>\d+):(?<col>\d+)\)$",
        RegexOptions.Compiled);

    public DiagnosticParseResult Parse(string output, string sourceFilePath)
    {
        if (string.IsNullOrWhiteSpace(output)) return DiagnosticParseResult.Empty;

        var lines = output.Replace("\r\n", "\n").Split('\n');
        var diagnostics = new List<DiagnosticItem>();
        string? missingDep = null;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // 1. Check missing package
            var missingMatch = MissingPackageRegex.Match(trimmed);
            if (missingMatch.Success && missingDep == null)
            {
                missingDep = missingMatch.Groups["pkg"].Value;
            }

            // 2. Check compiler diagnostic: file.dart:line:col: Error: message
            var match = DartErrorRegex.Match(trimmed);
            if (match.Success)
            {
                int.TryParse(match.Groups["line"].Value, out var lineNum);
                int.TryParse(match.Groups["col"].Value, out var colNum);
                var message = match.Groups["message"].Value.Trim();
                var sevText = match.Groups["severity"].Value;

                var severity = sevText.Equals("Warning", StringComparison.OrdinalIgnoreCase)
                    ? DiagnosticSeverity.Warning
                    : sevText.Equals("Info", StringComparison.OrdinalIgnoreCase)
                        ? DiagnosticSeverity.Info
                        : DiagnosticSeverity.Error;

                diagnostics.Add(new DiagnosticItem
                {
                    Id = severity == DiagnosticSeverity.Error ? "DART001" : "DART002",
                    Message = message,
                    Severity = severity,
                    Line = lineNum,
                    Column = colNum,
                    EndLine = lineNum,
                    EndColumn = colNum + 1
                });
                continue;
            }

            // 3. Stack trace frame: #0 main (file.dart:12:5)
            var frameMatch = StackFrameRegex.Match(trimmed);
            if (frameMatch.Success)
            {
                int.TryParse(frameMatch.Groups["line"].Value, out var lineNum);
                int.TryParse(frameMatch.Groups["col"].Value, out var colNum);

                diagnostics.Add(new DiagnosticItem
                {
                    Id = "DART_TRACE",
                    Message = "Runtime exception frame",
                    Severity = DiagnosticSeverity.Error,
                    Line = lineNum,
                    Column = colNum,
                    EndLine = lineNum,
                    EndColumn = colNum + 1
                });
            }
        }

        return new DiagnosticParseResult(diagnostics, missingDep);
    }
}
