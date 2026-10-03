using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class AsciiTableDetectorTests
{
    [Fact]
    public void DetectsSingleTable_FromSqliteCliOutput()
    {
        var tables = new List<DumpTableResult>();
        var detector = new AsciiTableDetector(tables.Add);

        var lines = new[]
        {
            "+----+-------+-------+",
            "| id | name  | score |",
            "+----+-------+-------+",
            "| 1  | Alice | 95.5  |",
            "| 2  | Bob   | 88.0  |",
            "+----+-------+-------+"
        };

        foreach (var line in lines)
        {
            detector.ProcessLine(line);
        }
        detector.Flush();

        Assert.Single(tables);
        var table = tables[0];
        Assert.Equal("Query Result", table.Title);
        Assert.Equal(3, table.Columns.Count);
        Assert.Equal("id", table.Columns[0].Header);
        Assert.Equal("name", table.Columns[1].Header);
        Assert.Equal("score", table.Columns[2].Header);

        Assert.True(table.Columns[0].IsNumeric);
        Assert.False(table.Columns[1].IsNumeric);
        Assert.True(table.Columns[2].IsNumeric);

        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("1", table.Rows[0].Cells[0].DisplayText);
        Assert.Equal("Alice", table.Rows[0].Cells[1].DisplayText);
        Assert.Equal("95.5", table.Rows[0].Cells[2].DisplayText);
        Assert.Equal("2", table.Rows[1].Cells[0].DisplayText);
        Assert.Equal("Bob", table.Rows[1].Cells[1].DisplayText);
        Assert.Equal("88.0", table.Rows[1].Cells[2].DisplayText);
    }

    [Fact]
    public void DetectsMultipleTables_FromMultiQueryOutput()
    {
        var tables = new List<DumpTableResult>();
        var detector = new AsciiTableDetector(tables.Add);

        var input = """
            +----+-------+
            | id | user  |
            +----+-------+
            | 10 | admin |
            +----+-------+
            +---------+-------+
            | product | price |
            +---------+-------+
            | Latte   | 4.75  |
            | Muffin  | 3.25  |
            +---------+-------+
            """;

        foreach (var line in input.Split('\n'))
        {
            detector.ProcessLine(line);
        }
        detector.Flush();

        Assert.Equal(2, tables.Count);
        Assert.Equal("Query Result", tables[0].Title);
        Assert.Equal("user", tables[0].Columns[1].Header);
        Assert.Single(tables[0].Rows);

        Assert.Equal("Query Result 2", tables[1].Title);
        Assert.Equal("price", tables[1].Columns[1].Header);
        Assert.Equal(2, tables[1].Rows.Count);
    }

    [Fact]
    public void PreservesInternalPipes_WhenSlicingWithExactColumnPositions()
    {
        var tables = new List<DumpTableResult>();
        var detector = new AsciiTableDetector(tables.Add);

        var input = """
            +----+---------------+
            | id | expression    |
            +----+---------------+
            | 1  | a || b | c    |
            +----+---------------+
            """;

        foreach (var line in input.Split('\n'))
        {
            detector.ProcessLine(line);
        }
        detector.Flush();

        Assert.Single(tables);
        var table = tables[0];
        Assert.Equal(2, table.Columns.Count);
        Assert.Single(table.Rows);
        Assert.Equal("a || b | c", table.Rows[0].Cells[1].DisplayText);
    }

    [Fact]
    public void DetectsNullAndBooleanValues()
    {
        var tables = new List<DumpTableResult>();
        var detector = new AsciiTableDetector(tables.Add);

        var input = """
            +----+--------+-------+
            | id | is_ok  | notes |
            +----+--------+-------+
            | 1  | true   | NULL  |
            | 2  | false  | valid |
            +----+--------+-------+
            """;

        foreach (var line in input.Split('\n'))
        {
            detector.ProcessLine(line);
        }
        detector.Flush();

        Assert.Single(tables);
        var table = tables[0];
        Assert.Equal(2, table.Rows.Count);

        var row0 = table.Rows[0];
        Assert.Equal(true, row0.Cells[1].RawValue);
        Assert.True(row0.Cells[2].IsNull);
        Assert.Equal("<null>", row0.Cells[2].DisplayText);

        var row1 = table.Rows[1];
        Assert.Equal(false, row1.Cells[1].RawValue);
        Assert.False(row1.Cells[2].IsNull);
    }

    [Fact]
    public void IgnoresOrdinaryTextAndIncompleteTables()
    {
        var tables = new List<DumpTableResult>();
        var detector = new AsciiTableDetector(tables.Add);

        var lines = new[]
        {
            "Executing query...",
            "Result: a + b | c",
            "+------------------",
            "This is just normal text with + and | symbols",
            "Done."
        };

        foreach (var line in lines)
        {
            detector.ProcessLine(line);
        }
        detector.Flush();

        Assert.Empty(tables);
    }

    [Fact]
    public void ExternalOutputProcessor_EmitsBothRichTableAndConsoleLines()
    {
        var consoleLines = new List<string>();
        var richOutputs = new List<RichCellOutput>();
        var processor = new ExternalOutputProcessor(consoleLines.Add, richOutputs.Add);

        var input = """
            Connecting to database...
            +----+-------+
            | id | name  |
            +----+-------+
            | 1  | Admin |
            +----+-------+
            Execution complete.
            """;

        processor.ProcessChunk(input);
        processor.Flush();

        // 1. Rich table output is emitted for the interactive visual table viewer
        Assert.Single(richOutputs);
        Assert.Equal(CellOutputKind.Table, richOutputs[0].Kind);
        Assert.NotNull(richOutputs[0].TableResult);
        Assert.Equal("name", richOutputs[0].TableResult!.Columns[1].Header);

        // 2. Terminal console text is preserved completely without loss
        var fullConsoleText = string.Concat(consoleLines);
        Assert.Contains("Connecting to database...", fullConsoleText);
        Assert.Contains("+----+-------+", fullConsoleText);
        Assert.Contains("| id | name  |", fullConsoleText);
        Assert.Contains("| 1  | Admin |", fullConsoleText);
        Assert.Contains("Execution complete.", fullConsoleText);
    }
}
