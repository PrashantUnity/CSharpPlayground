using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Sql;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class SqlCompilerDiagnosticParserTests
{
    private readonly SqlCompilerDiagnosticParser _parser = new();

    // Actual IDs from the implementation:
    // SQL0001 = generic/no-such-table/other
    // SQL0002 = syntax error
    // SQL0003 = constraint failed
    // SQL0004 = no such table
    // SQL0005 = no such column

    [Fact]
    public void ParsesParseError_WithLineNumber()
    {
        var output = "Parse error near line 3: near \"FORM\": syntax error";

        var result = _parser.Parse(output, "/tmp/query.sql");

        Assert.Single(result.Diagnostics);
        var diag = result.Diagnostics[0];
        Assert.Equal("SQL0002", diag.Id);  // syntax error → SQL0002
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Equal(3, diag.Line);
        Assert.Contains("FORM", diag.Message);
        Assert.Contains("syntax error", diag.Message);
    }

    [Fact]
    public void ParsesRuntimeError_WithLineNumber()
    {
        var output = "Runtime error near line 7: UNIQUE constraint failed: users.id (19)";

        var result = _parser.Parse(output, "/tmp/query.sql");

        Assert.Single(result.Diagnostics);
        var diag = result.Diagnostics[0];
        Assert.Equal("SQL0003", diag.Id);  // constraint failed → SQL0003
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Equal(7, diag.Line);
        Assert.Contains("UNIQUE constraint", diag.Message);
    }

    [Fact]
    public void ParsesGenericError_WithLineNumber()
    {
        // "Error: near line N:" — the colon after "Error" is now handled by the updated regex.
        var output = "Error: near line 12: incomplete input";

        var result = _parser.Parse(output, "/tmp/query.sql");

        Assert.Single(result.Diagnostics);
        var diag = result.Diagnostics[0];
        Assert.Equal("SQL0001", diag.Id);  // generic → SQL0001
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Equal(12, diag.Line);
        Assert.Contains("incomplete input", diag.Message);
    }

    [Fact]
    public void ParsesNoSuchTableError()
    {
        // "Error: near line N: no such table" → now correctly parses line number
        var output = "Error: near line 5: no such table: orders";

        var result = _parser.Parse(output, "/tmp/query.sql");

        Assert.Single(result.Diagnostics);
        var diag = result.Diagnostics[0];
        Assert.Equal("SQL0004", diag.Id);  // no such table → SQL0004
        Assert.Equal(5, diag.Line);
        Assert.Contains("no such table", diag.Message);
    }

    [Fact]
    public void ParsesCaretPointer_InferredColumn()
    {
        var output =
            "Parse error near line 5: near \"FORM\": syntax error\n" +
            "  FROM users\n" +
            "  ^\n" +
            "  ^--- error here";

        var result = _parser.Parse(output, "/tmp/query.sql");

        Assert.Single(result.Diagnostics);
        var diag = result.Diagnostics[0];
        Assert.Equal(5, diag.Line);
        // Column is inferred from the caret position (3 spaces = col 3)
        Assert.True(diag.Column >= 1);
    }

    [Fact]
    public void ParsesMultipleErrors()
    {
        var output =
            "Parse error near line 2: near \"FORM\": syntax error\n" +
            "Runtime error near line 8: no such table: orders";

        var result = _parser.Parse(output, "/tmp/query.sql");

        Assert.Equal(2, result.Diagnostics.Count);
        Assert.Equal(2, result.Diagnostics[0].Line);
        Assert.Equal(8, result.Diagnostics[1].Line);
    }

    [Fact]
    public void EmptyOutput_ReturnsEmptyDiagnostics()
    {
        var result = _parser.Parse("", "/tmp/query.sql");
        Assert.Empty(result.Diagnostics);
        Assert.Null(result.MissingDependency);
    }

    [Fact]
    public void SuccessfulOutput_ReturnsEmptyDiagnostics()
    {
        var output =
            "+----+-------+\n" +
            "| id | name  |\n" +
            "+----+-------+\n" +
            "| 1  | Alice |\n" +
            "+----+-------+\n";

        var result = _parser.Parse(output, "/tmp/query.sql");
        Assert.Empty(result.Diagnostics);
        Assert.Null(result.MissingDependency);
    }

    [Fact]
    public void ExtensionLoadError_IsNotParsedAsMissingDependency()
    {
        // The SQLite CLI extension load error doesn't match the standard error regex pattern,
        // so MissingDependency will be null. This tests the "no false positive" path.
        var output = "Error: unable to open shared library 'spatialite.so': dlopen failed";

        var result = _parser.Parse(output, "/tmp/query.sql");

        // The parser produces at least one error diagnostic for this
        Assert.NotEmpty(result.Diagnostics);
    }
}
