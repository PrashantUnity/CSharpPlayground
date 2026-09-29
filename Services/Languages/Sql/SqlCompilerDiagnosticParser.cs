using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Sql;

/// <summary>
/// Parses SQLite 3 compiler, syntax, and runtime execution errors into structured Problems items with line/column precision.
/// </summary>
public sealed partial class SqlCompilerDiagnosticParser : IDiagnosticParser
{
    // Matches: Parse error near line 4: near "FORM": syntax error
    // or: Parse error at line 4, column 12: near "FORM": syntax error
    // or: Runtime error near line 8: UNIQUE constraint failed: users.id (19)
    // or: Error: near line 5: no such table: customers  (colon right after Error)
    [GeneratedRegex(@"^(?:(?:Parse|Runtime)\s+error|Error):?\s*(?:(?:at\s+line\s+(?<line>\d+)(?:,\s*column\s+(?<col>\d+))?|near\s+line\s+(?<line>\d+))\s*:)?\s*(?<msg>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex SqliteErrorRegex();

    // Matches caret pointer lines: "         ^--- error here"
    [GeneratedRegex(@"^(?<indent>\s*)\^---(?:\s*error\s+here)?", RegexOptions.IgnoreCase)]
    private static partial Regex CaretRegex();

    public DiagnosticParseResult Parse(string output, string sourceFilePath)
    {
        if (string.IsNullOrWhiteSpace(output)) return DiagnosticParseResult.Empty;

        var lines = output.Replace("\r\n", "\n").Split('\n');
        var diagnostics = new List<DiagnosticItem>();
        string? missingDependency = null;

        for (var i = 0; i < lines.Length; i++)
        {
            var rawLine = lines[i].TrimEnd();
            if (string.IsNullOrWhiteSpace(rawLine)) continue;

            var match = SqliteErrorRegex().Match(rawLine.Trim());
            if (match.Success)
            {
                var lineNum = 1;
                if (match.Groups["line"].Success && int.TryParse(match.Groups["line"].Value, out var parsedLine))
                {
                    lineNum = Math.Max(1, parsedLine);
                }

                var colNum = 1;
                if (match.Groups["col"].Success && int.TryParse(match.Groups["col"].Value, out var parsedCol))
                {
                    colNum = Math.Max(1, parsedCol);
                }
                else
                {
                    // Check if subsequent lines have a caret pointing to column
                    for (var j = i + 1; j < Math.Min(lines.Length, i + 4); j++)
                    {
                        var caretMatch = CaretRegex().Match(lines[j]);
                        if (caretMatch.Success)
                        {
                            colNum = caretMatch.Groups["indent"].Length + 1;
                            break;
                        }
                    }
                }

                var msg = match.Groups["msg"].Value.Trim();
                var id = "SQL0001";
                if (msg.Contains("syntax error", StringComparison.OrdinalIgnoreCase))
                {
                    id = "SQL0002";
                }
                else if (msg.Contains("constraint failed", StringComparison.OrdinalIgnoreCase))
                {
                    id = "SQL0003";
                }
                else if (msg.Contains("no such table", StringComparison.OrdinalIgnoreCase))
                {
                    id = "SQL0004";
                }
                else if (msg.Contains("no such column", StringComparison.OrdinalIgnoreCase))
                {
                    id = "SQL0005";
                }

                diagnostics.Add(new DiagnosticItem
                {
                    Id = id,
                    Message = msg,
                    Severity = DiagnosticSeverity.Error,
                    Line = lineNum,
                    Column = colNum,
                    EndLine = lineNum,
                    EndColumn = colNum + 6
                });
            }
        }

        return new DiagnosticParseResult(diagnostics, missingDependency);
    }
}
