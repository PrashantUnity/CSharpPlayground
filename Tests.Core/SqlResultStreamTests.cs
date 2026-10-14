using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Sql;
using Xunit;

namespace FrySharp.Core.Tests;

/// <summary>What <c>sqlite3 -json</c> prints, read into result sets and text (no sqlite3 needed: the text is what it prints).</summary>
public class SqlResultStreamTests
{
    private static SqlTableSegment OnlyTable(string output) => Assert.IsType<SqlTableSegment>(Assert.Single(SqlResultStream.Split(output)));

    [Fact]
    public void ARowPerLine_IsOneTable_WithItsColumns_AndRows()
    {
        var table = OnlyTable("[{\"id\":1,\"name\":\"Ada\",\"score\":1.5,\"note\":null},\n{\"id\":2,\"name\":\"Linus\",\"score\":2.0,\"note\":\"it's\"}]\n").Table;

        Assert.Equal(["id", "name", "score", "note"], table.Columns.Select(c => c.Header));
        Assert.Equal([true, false, true, false], table.Columns.Select(c => c.IsNumeric)); // a column of numbers (and nulls) is numeric
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("Ada", table.Rows[0].Cells[1].DisplayText);
        Assert.True(table.Rows[0].Cells[3].IsNull);
        Assert.Equal("it's", table.Rows[1].Cells[3].DisplayText);
    }

    [Fact]
    public void AResultSetOfOneRow_IsATable()
    {
        var table = OnlyTable("[{\"n\":2}]\n").Table;

        Assert.Equal(["n"], table.Columns.Select(c => c.Header));
        Assert.Equal("2", Assert.Single(table.Rows).Cells[0].DisplayText);
    }

    [Fact]
    public void TwoColumnsWithTheSameName_AreTwoColumns()
    {
        var table = OnlyTable("[{\"a\":1,\"a\":2}]\n").Table;

        Assert.Equal(["a", "a"], table.Columns.Select(c => c.Header));
        Assert.Equal(["1", "2"], table.Rows[0].Cells.Select(c => c.DisplayText));
    }

    [Fact]
    public void NewlinesInAValue_AreEscapedByTheTool_SoTheRowStaysOnItsLine()
    {
        var table = OnlyTable("[{\"note\":\"a\\nb\"}]\n").Table;

        Assert.Equal("a\nb", table.Rows[0].Cells[0].DisplayText);
    }

    [Fact]
    public void TextAroundResultSets_StaysText_InOrder()
    {
        var segments = SqlResultStream.Split("hello\n[{\"n\":1}]\nbye\n[{\"m\":2},\n{\"m\":3}]\n");

        Assert.Collection(segments,
            s => Assert.Equal("hello\n", Assert.IsType<SqlTextSegment>(s).Text),
            s => Assert.Single(Assert.IsType<SqlTableSegment>(s).Table.Rows),
            s => Assert.Equal("bye\n", Assert.IsType<SqlTextSegment>(s).Text),
            s => Assert.Equal(2, Assert.IsType<SqlTableSegment>(s).Table.Rows.Count));
    }

    [Fact]
    public void TheSegments_HaveTheirOffsetsInTheOutput()
    {
        const string output = "a\n[{\"n\":1}]\nb\n";
        var segments = SqlResultStream.Split(output);

        Assert.Equal([(0, 2), (2, 12), (12, 14)], segments.Select(s => (s.Start, s.End)));
        Assert.Equal("[{\"n\":1}]\n", output[segments[1].Start..segments[1].End]);
    }

    [Fact]
    public void BackToBackResultSets_AreTwoTables()
    {
        var segments = SqlResultStream.Split("[{\"a\":1}]\n[{\"b\":2}]\n");

        Assert.Equal(2, segments.OfType<SqlTableSegment>().Count());
    }

    [Fact]
    public void ALineThatLooksLikeTheStartButIsNotJson_IsText()
    {
        var segments = SqlResultStream.Split("[{ this is a line a script printed\nnext\n");

        Assert.All(segments, s => Assert.IsType<SqlTextSegment>(s));
        Assert.Equal("[{ this is a line a script printed\nnext\n", SqlResultStream.TextOf(segments));
    }

    [Fact]
    public void AScriptThatChangesTheMode_GetsItsTextAsItIs()
    {
        var segments = SqlResultStream.Split("id,name\n1,Ada\n2,Linus\n");

        Assert.All(segments, s => Assert.IsType<SqlTextSegment>(s));
        Assert.Equal("id,name\n1,Ada\n2,Linus\n", SqlResultStream.TextOf(segments));
    }

    [Fact]
    public void WindowsLineEndings_AreRead()
    {
        var segments = SqlResultStream.Split("[{\"a\":1},\r\n{\"a\":2}]\r\ntext\r\n");

        Assert.Equal(2, Assert.IsType<SqlTableSegment>(segments[0]).Table.Rows.Count);
        Assert.Equal("text\r\n", Assert.IsType<SqlTextSegment>(segments[1]).Text);
    }

    [Fact]
    public void NothingPrinted_IsNoSegments()
    {
        Assert.Empty(SqlResultStream.Split(string.Empty));
    }

    [Fact]
    public void ARowThatEndsLikeAResultSet_InsideAText_DoesNotEndTheTableEarly()
    {
        // the value ends in "}]" but the row ends in "}," : only the last row's line ends the result set
        var table = OnlyTable("[{\"v\":\"x}]\"},\n{\"v\":\"y\"}]\n").Table;

        Assert.Equal(["x}]", "y"], table.Rows.Select(r => r.Cells[0].DisplayText));
    }
}
