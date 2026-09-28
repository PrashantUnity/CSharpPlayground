using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Python;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>A kernel's MIME bundles become the studio's outputs, the richest kind first; and its files ship in the plugin.</summary>
public class MimeOutputMapperTests
{
    private static KernelOutput Map(string data, string metadata = "{}")
    {
        using var d = JsonDocument.Parse(data);
        using var m = JsonDocument.Parse(metadata);
        return MimeOutputMapper.Map(d.RootElement.Clone(), m.RootElement.Clone());
    }

    [Fact]
    public void ATable_IsTheStudiosTable_WithItsTitleColumnsAndCells()
    {
        var output = Map("""
            {"application/vnd.fry.table+json": {"title": "DataFrame (1500 × 2)", "columns": ["", "a", "b"],
              "numeric": [true, true, false], "rows": [[0, 1.5, "x"], [1, null, "y"]], "totalRows": 1500, "totalColumns": 2},
             "text/html": "<table/>", "text/plain": "…"}
            """);

        var table = output.Rich!.TableResult!;
        Assert.Equal(CellOutputKind.Table, output.Rich.Kind);
        Assert.Equal("DataFrame (1500 × 2)", table.Title);
        Assert.Equal(new[] { "", "a", "b" }, table.Columns.Select(c => c.Header));
        Assert.Equal(new[] { true, true, false }, table.Columns.Select(c => c.IsNumeric));
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("1.5", table.Rows[0].Cells[1].DisplayText);
        Assert.True(table.Rows[1].Cells[1].IsNull);
        Assert.Equal("showing the first 2 of 1,500 rows", table.Label);
    }

    [Fact]
    public void ADataFrameOfNumbersAndBooleans_FillsACellsTable()
    {
        var output = Map("""
            {"application/vnd.fry.table+json": {"title": "DataFrame (2 × 3)", "columns": ["", "n", "square", "even"],
              "numeric": [true, true, true, false], "rows": [[0, 3, 9, false], [1, 4, 16, true]], "totalRows": 2, "totalColumns": 3},
             "text/plain": "   n  square   even"}
            """);
        var cell = new PdfEditorApp.Plugins.CSharpEditor.ViewModels.NotebookCellViewModel(new NotebookCellItem { Type = CellType.Code, Source = "frame" });

        cell.SetTableOutput(output.Rich!.TableResult!);

        Assert.True(cell.HasOutput);
        Assert.True(cell.IsTableTabActive);
        Assert.Equal("True", cell.TableResult!.Rows[1].Cells[3].DisplayText);
    }

    [Fact]
    public void APng_IsAnImage_WithItsSize()
    {
        var png = Convert.ToBase64String([0x89, 0x50, 0x4E, 0x47, 1, 2, 3]);

        var output = Map($$"""{"image/png": "{{png}}", "text/plain": "<Figure>"}""", """{"image/png": {"width": 640, "height": 480}}""");

        Assert.Equal(CellOutputKind.Image, output.Rich!.Kind);
        Assert.Equal("PNG", output.Rich.ImageFormat);
        Assert.Equal(640, output.Rich.ImageWidth);
        Assert.Equal(480, output.Rich.ImageHeight);
        Assert.Equal(7, output.Rich.ImageBytes!.Length);
    }

    [Fact]
    public void SvgAndHtml_AreHtml()
    {
        Assert.Contains("<svg", Map("""{"image/svg+xml": "<svg></svg>"}""").Rich!.HtmlContent);
        Assert.Equal("<b>x</b>", Map("""{"text/html": "<b>x</b>", "text/plain": "x"}""").Rich!.HtmlContent);
    }

    [Fact]
    public void PlainText_IsTextOnItsOwnLine()
    {
        var output = Map("""{"text/plain": "42"}""");

        Assert.Null(output.Rich);
        Assert.Equal("42\n", output.Text);
    }

    [Fact]
    public void ThePythonKernel_IsExtractedOnce_IntoAFolderNamedByItsContent()
    {
        var root = Path.Combine(Path.GetTempPath(), "FryPDF_KernelFiles_" + Guid.NewGuid().ToString("N"));
        try
        {
            var assembly = typeof(PythonKernelLauncher).Assembly;
            var folder = EmbeddedKernelFiles.Extract(assembly, PythonKernelLauncher.ResourcePrefix, root);
            var again = EmbeddedKernelFiles.Extract(assembly, PythonKernelLauncher.ResourcePrefix, root);

            Assert.Equal(folder, again);
            Assert.Equal(new[] { "fry_display.py", "fry_kernel.py", "fry_matplotlib.py" }, Directory.GetFiles(folder).Select(Path.GetFileName).OrderBy(n => n));
            Assert.Contains("def main():", File.ReadAllText(Path.Combine(folder, "fry_kernel.py")));
            Assert.Single(Directory.GetDirectories(root)); // no leftover staging folders
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
