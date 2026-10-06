using System.Diagnostics;
using System.Text;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using Xunit;
using CSharpCodeStudioViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio.CSharpCodeStudioViewModel;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// Big tables and big CSV files: building a table is linear in its rows, a CSV preview parses only what it shows (and
/// counts the rest), and image sizes come from the header.
/// </summary>
public class TablePreviewScaleTests
{
    private static DumpTableRow Row(int i, string name) =>
        new(i, new[] { new DumpTableCell { DisplayText = i.ToString(), IsNumeric = true, RawValue = (long)i }, new DumpTableCell { DisplayText = name } });

    private static DumpTableResult NewTable()
    {
        var table = new DumpTableResult("People");
        table.Columns.Add(new DumpTableColumn { Header = "Id", IsNumeric = true });
        table.Columns.Add(new DumpTableColumn { Header = "Name" });
        return table;
    }

    [Fact]
    public void AddingRowsOneByOne_IsLinear_NotQuadratic()
    {
        // Every add used to re-filter, re-sort and rebuild the shown rows: 20,000 rows took minutes.
        var table = NewTable();
        int viewChanges = 0;
        table.RowsViewChanged += () => viewChanges++;
        var clock = Stopwatch.StartNew();
        for (int i = 0; i < 20_000; i++) table.Rows.Add(Row(i, $"person {i}"));

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(3), $"20,000 adds took {clock.Elapsed.TotalSeconds:F1} s");
        Assert.Equal(20_000, table.FilteredRows.Count);
        Assert.Same(table.Rows[19_999], table.FilteredRows[19_999]);
        Assert.Equal(20_000, viewChanges);
    }

    [Fact]
    public void AddRows_RaisesOneChange_AndAFilterOrSortStillAppliesToNewRows()
    {
        var table = NewTable();
        int filteredChanges = 0;
        table.FilteredRows.CollectionChanged += (_, _) => filteredChanges++;
        table.AddRows(Enumerable.Range(0, 1000).Select(i => Row(i, i % 2 == 0 ? "even" : "odd")).ToList());
        Assert.Equal(1, filteredChanges);
        Assert.Equal(1000, table.FilteredRows.Count);

        table.FilterText = "odd";
        Assert.Equal(500, table.FilteredRows.Count);
        table.Rows.Add(Row(1000, "odd one"));
        table.Rows.Add(Row(1001, "even one"));
        Assert.Equal(501, table.FilteredRows.Count);
        Assert.Contains(table.FilteredRows, r => r.Cells[1].DisplayText == "odd one");

        table.ClearFilter();
        table.SortByColumn(0);
        table.SortByColumn(0); // descending
        table.Rows.Add(Row(5000, "last added, biggest id"));
        Assert.Equal("5000", table.FilteredRows[0].Cells[0].DisplayText);
    }

    [Fact]
    public void ParseCsvRecords_StopsAtTheCap_AndCountsTheRest_MindingQuotedLineBreaks()
    {
        var csv = new StringBuilder("id,note\n");
        for (int i = 0; i < 1000; i++) csv.Append(i).Append(",\"line one\nline two\"\n");

        var records = CSharpCodeStudioViewModel.ParseCsvRecords(csv.ToString(), ',', 11, out int total, CancellationToken.None);

        Assert.Equal(11, records.Count);
        Assert.Equal(1001, total);
        Assert.Equal("line one\nline two", records[1][1]);
        Assert.Equal(1001, CSharpCodeStudioViewModel.ParseCsvRecords(csv.ToString(), ',').Count);
    }

    [Fact]
    public void BuildDumpTableFromRecords_SaysWhenItShowsOnlyTheFirstRows()
    {
        var records = new List<List<string>> { new() { "a", "b" }, new() { "1", "x" }, new() { "2", "y" } };
        var full = CSharpCodeStudioViewModel.BuildDumpTableFromRecords(records, "t.csv", ',');
        Assert.Equal("CSV Table", full.Label);

        var partial = CSharpCodeStudioViewModel.BuildDumpTableFromRecords(records, "t.csv", ',', totalDataRows: 250_000);
        Assert.Equal(2, partial.Rows.Count);
        Assert.Contains($"first 2 of {250_000:N0} rows", partial.Label);
    }

    [Fact]
    public void DetectDelimiter_ReadsOnlyTheFirstLine()
    {
        Assert.Equal(';', CSharpCodeStudioViewModel.DetectDelimiter("a.csv", "a;b;c\n1,2,3,4,5,6,7,8"));
        Assert.Equal('\t', CSharpCodeStudioViewModel.DetectDelimiter("a.txt", "a\tb\tc"));
        Assert.Equal(',', CSharpCodeStudioViewModel.DetectDelimiter("a.csv", string.Empty));
    }

    [Fact]
    public void ImageSizes_ComeFromTheHeader_WithoutDecoding()
    {
        // PNG: signature, IHDR length and type, then width and height big-endian.
        var png = new byte[32];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R' }.CopyTo(png, 0);
        png[18] = 0x17; png[19] = 0x70; // 6000
        png[22] = 0x0F; png[23] = 0xA0; // 4000
        Assert.True(ImageDecoder.TryReadDimensions(png, out int w, out int h));
        Assert.Equal((6000, 4000), (w, h));

        var gif = Encoding.ASCII.GetBytes("GIF89a").Concat(new byte[] { 0x40, 0x01, 0xF0, 0x00 }).ToArray();
        Assert.True(ImageDecoder.TryReadDimensions(gif, out w, out h));
        Assert.Equal((320, 240), (w, h));

        Assert.False(ImageDecoder.TryReadDimensions(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }, out _, out _));
    }
}
