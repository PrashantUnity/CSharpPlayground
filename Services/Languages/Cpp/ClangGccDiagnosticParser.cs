using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;

/// <summary>
/// Reads compiler diagnostics and runtime crash traces from Clang, GCC, and MSVC into Problems entries.
/// </summary>
public sealed partial class ClangGccDiagnosticParser : IDiagnosticParser
{
    [GeneratedRegex(@"^(?<file>[^:\r\n]+):(?<line>\d+):(?<col>\d+):\s*(?<severity>error|fatal error|warning|note):\s*(?<message>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex ClangGccDiagnosticHeaderRegex();

    [GeneratedRegex(@"^(?<file>[^(\r\n]+)\((?<line>\d+)(?:,(?<col>\d+))?\):\s*(?<severity>error|fatal error|warning)\s*(?<code>[A-Z0-9]+)?:\s*(?<message>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex MsvcDiagnosticHeaderRegex();

    [GeneratedRegex(@"^\s*(?:~*)\^(?:~*)\s*$")]
    private static partial Regex CaretMarkerRegex();

    [GeneratedRegex(@"Assertion failed:\s*\((?<expr>.*)\),\s*function\s*(?<fn>.*),\s*file\s*(?<file>[^,\r\n]+),\s*line\s*(?<line>\d+)\.", RegexOptions.IgnoreCase)]
    private static partial Regex AppleAssertionRegex();

    [GeneratedRegex(@"a\.out:\s*(?<file>[^:]+):(?<line>\d+):\s*(?:(?<fn>.*):\s*)?Assertion\s*`(?<expr>.*)'\s*failed\.", RegexOptions.IgnoreCase)]
    private static partial Regex GnuAssertionRegex();

    [GeneratedRegex(@"terminate called after throwing an instance of '(?<type>[^']+)'(?:\s+what\(\):\s*(?<msg>.*))?", RegexOptions.IgnoreCase)]
    private static partial Regex CppTerminateRegex();

    public DiagnosticParseResult Parse(string output, string sourceFilePath)
    {
        if (string.IsNullOrWhiteSpace(output)) return DiagnosticParseResult.Empty;

        var lines = output.Replace("\r\n", "\n").Split('\n');
        var diagnostics = ParseCompilerDiagnostics(lines, sourceFilePath);

        if (diagnostics.Count > 0)
        {
            return new DiagnosticParseResult(diagnostics);
        }

        // If no compiler errors matched, parse runtime crash / assertion messages
        var runtimeDiagnostics = ParseRuntimeExceptions(lines, sourceFilePath);
        return new DiagnosticParseResult(runtimeDiagnostics);
    }

    private static List<DiagnosticItem> ParseCompilerDiagnostics(string[] lines, string sourceFilePath)
    {
        var diagnostics = new List<DiagnosticItem>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            string file;
            int lineNumber;
            int col;
            string severityStr;
            string message;
            string? errorCode = null;

            var clangMatch = ClangGccDiagnosticHeaderRegex().Match(line);
            if (clangMatch.Success)
            {
                file = clangMatch.Groups["file"].Value.Trim();
                int.TryParse(clangMatch.Groups["line"].Value, out lineNumber);
                int.TryParse(clangMatch.Groups["col"].Value, out col);
                severityStr = clangMatch.Groups["severity"].Value;
                message = clangMatch.Groups["message"].Value.Trim();
            }
            else
            {
                var msvcMatch = MsvcDiagnosticHeaderRegex().Match(line);
                if (!msvcMatch.Success) continue;

                file = msvcMatch.Groups["file"].Value.Trim();
                int.TryParse(msvcMatch.Groups["line"].Value, out lineNumber);
                col = msvcMatch.Groups["col"].Success && int.TryParse(msvcMatch.Groups["col"].Value, out var c) ? c : 1;
                severityStr = msvcMatch.Groups["severity"].Value;
                message = msvcMatch.Groups["message"].Value.Trim();
                errorCode = msvcMatch.Groups["code"].Success ? msvcMatch.Groups["code"].Value.Trim() : null;
            }

            if (!SamePath(file, sourceFilePath)) continue;

            lineNumber = Math.Max(1, lineNumber);
            col = Math.Max(1, col);

            var severity = severityStr.Contains("warning", StringComparison.OrdinalIgnoreCase)
                ? DiagnosticSeverity.Warning
                : DiagnosticSeverity.Error;

            // Look ahead for caret pointer
            var lookAheadLimit = Math.Min(lines.Length, i + 4);
            for (var j = i + 1; j < lookAheadLimit; j++)
            {
                var nextLine = lines[j];
                if (ClangGccDiagnosticHeaderRegex().IsMatch(nextLine) || MsvcDiagnosticHeaderRegex().IsMatch(nextLine)) break;

                var caretMatch = CaretMarkerRegex().Match(nextLine);
                if (caretMatch.Success)
                {
                    col = Math.Max(1, nextLine.IndexOf('^') + 1);
                    break;
                }
            }

            var id = errorCode ?? (severity == DiagnosticSeverity.Error ? "CXX_ERR" : "CXX_WARN");

            diagnostics.Add(new DiagnosticItem
            {
                Id = id,
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

        foreach (var raw in lines)
        {
            var line = raw.Trim();

            // 1. Apple assertion failure
            var appleAssert = AppleAssertionRegex().Match(line);
            if (appleAssert.Success)
            {
                var file = appleAssert.Groups["file"].Value.Trim();
                int.TryParse(appleAssert.Groups["line"].Value, out var assertLine);
                var expr = appleAssert.Groups["expr"].Value.Trim();
                if (SamePath(file, sourceFilePath))
                {
                    diagnostics.Add(new DiagnosticItem
                    {
                        Id = "ASSERT_FAIL",
                        Message = $"Assertion failed: ({expr})",
                        Severity = DiagnosticSeverity.Error,
                        Line = Math.Max(1, assertLine),
                        Column = 1,
                        EndLine = Math.Max(1, assertLine),
                        EndColumn = 1
                    });
                    return diagnostics;
                }
            }

            // 2. GNU assertion failure
            var gnuAssert = GnuAssertionRegex().Match(line);
            if (gnuAssert.Success)
            {
                var file = gnuAssert.Groups["file"].Value.Trim();
                int.TryParse(gnuAssert.Groups["line"].Value, out var assertLine);
                var expr = gnuAssert.Groups["expr"].Value.Trim();
                if (SamePath(file, sourceFilePath))
                {
                    diagnostics.Add(new DiagnosticItem
                    {
                        Id = "ASSERT_FAIL",
                        Message = $"Assertion failed: `{expr}`",
                        Severity = DiagnosticSeverity.Error,
                        Line = Math.Max(1, assertLine),
                        Column = 1,
                        EndLine = Math.Max(1, assertLine),
                        EndColumn = 1
                    });
                    return diagnostics;
                }
            }

            // 3. std::terminate / uncaught exception
            var terminateMatch = CppTerminateRegex().Match(line);
            if (terminateMatch.Success)
            {
                var type = terminateMatch.Groups["type"].Value.Trim();
                var msg = terminateMatch.Groups["msg"].Success ? terminateMatch.Groups["msg"].Value.Trim() : null;
                diagnostics.Add(new DiagnosticItem
                {
                    Id = type,
                    Message = !string.IsNullOrWhiteSpace(msg) ? $"Unhandled exception ({type}): {msg}" : $"Unhandled exception ({type})",
                    Severity = DiagnosticSeverity.Error,
                    Line = 1,
                    Column = 1,
                    EndLine = 1,
                    EndColumn = 1
                });
                return diagnostics;
            }

            // 4. Segmentation fault
            if (line.Contains("Segmentation fault", StringComparison.OrdinalIgnoreCase) || line.Contains("SIGSEGV", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new DiagnosticItem
                {
                    Id = "SIGSEGV",
                    Message = "Segmentation fault (core dumped) - invalid memory access",
                    Severity = DiagnosticSeverity.Error,
                    Line = 1,
                    Column = 1,
                    EndLine = 1,
                    EndColumn = 1
                });
                return diagnostics;
            }
        }

        return diagnostics;
    }

    private static bool SamePath(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return true;
        var fileNameA = Path.GetFileName(a);
        var fileNameB = Path.GetFileName(b);
        if (string.Equals(fileNameA, fileNameB, StringComparison.OrdinalIgnoreCase)) return true;

        var isCppA = fileNameA.EndsWith(".cpp", StringComparison.OrdinalIgnoreCase) || fileNameA.EndsWith(".cc", StringComparison.OrdinalIgnoreCase);
        var isCppB = fileNameB.EndsWith(".cpp", StringComparison.OrdinalIgnoreCase) || fileNameB.EndsWith(".cc", StringComparison.OrdinalIgnoreCase);
        if (isCppA && isCppB) return true;

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
