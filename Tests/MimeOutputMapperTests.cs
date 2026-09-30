using System.IO;
using System.Linq;
using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Python;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>A kernel's MIME bundles become the studio's outputs, the richest kind first; and its files ship in the plugin.</summary>
public class MimeOutputMapperTests
{
    private static KernelOutput Map(string data, string metadata = "{}", string? displayId = null)
    {
        using var d = JsonDocument.Parse(data);
        using var m = JsonDocument.Parse(metadata);
        return MimeOutputMapper.Map(d.RootElement.Clone(), m.RootElement.Clone(), displayId);
    }

    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Visuals", name));

    // Any language shows a chart, 3D plot or visualizer by sending its spec; the display id names it for updates.
    [Theory]
    [InlineData("chart-line.json", VisualMimeTypes.Chart, CellOutputKind.Chart)]
    [InlineData("plot3d-surface.json", VisualMimeTypes.Plot3D, CellOutputKind.Plot3D)]
    [InlineData("visualizer-tree.json", VisualMimeTypes.Visualizer, CellOutputKind.Visualizer)]
    public void AVisualsSpec_BecomesThatVisual_NamedByItsDisplayId(string fixture, string mimeType, CellOutputKind kind)
    {
        var spec = Fixture(fixture);

        var output = Map($$"""{"{{mimeType}}": {{spec}}, "text/plain": "a visual"}""", displayId: "figure-1");

        Assert.Null(output.Text);
        Assert.Equal(kind, output.Rich!.Kind);
        var visual = output.Rich.Visual!;
        Assert.Equal("figure-1", visual.DisplayId);
        Assert.Equal(mimeType, visual.MimeType);
        Assert.Equal(VisualJson.Serialize(VisualJson.Deserialize(visual.Family, spec)), VisualJson.Serialize(visual.Spec));
    }

    // A spec with a mistake is shown as an error that says where the mistake is, not as a blank visual or a throw.
    [Fact]
    public void AnInvalidSpec_IsAnErrorThatSaysWhere()
    {
        var output = Map("""{"application/vnd.fry.chart.v1+json": {"series": [{"y": [1, "two"]}]}, "text/plain": "chart"}""");

        Assert.Equal(CellOutputKind.Error, output.Rich!.Kind);
        Assert.Contains("$.series[0].y[1]", output.Rich.Text);
    }

    [Fact]
    public void ASpecThatBreaksARule_IsAnErrorThatSaysWhich()
    {
        var output = Map("""{"application/vnd.fry.chart.v1+json": {"series": [{"x": [1, 2], "y": [1, 2, 3]}]}}""");

        Assert.Equal(CellOutputKind.Error, output.Rich!.Kind);
        Assert.Contains("$.series[0].x", output.Rich.Text);
    }

    // As in Jupyter, a type the studio doesn't know (a later version of a spec) leaves the bundle's other types.
    [Fact]
    public void AVersionThisStudioDoesntRead_FallsBackToTheBundlesText()
    {
        var output = Map("""{"application/vnd.fry.chart.v2+json": {"kind": "sankey"}, "text/plain": "a sankey diagram"}""");

        Assert.Null(output.Rich);
        Assert.Equal("a sankey diagram\n", output.Text);
    }

    [Fact]
    public void ASpecLargerThanAVisualCanBe_IsAnErrorThatSaysSo()
    {
        var values = string.Join(",", Enumerable.Repeat("0.1234567890123", VisualLimits.MaxPayloadBytes / 16 + 1));

        var output = Map($$$"""{"application/vnd.fry.chart.v1+json": {"series": [{"y": [{{{values}}}]}]}}""");

        Assert.Equal(CellOutputKind.Error, output.Rich!.Kind);
        Assert.Contains("32 MB", output.Rich.Text);
    }

    // The first 3D format, sent by programs written before the spec, still draws.
    [Fact]
    public void TheFirst3DFormat_IsStillRead()
    {
        var output = Map("""{"application/vnd.fry.plot3d+json": {"title": "Old", "type": "scatter", "points": [[1, 2, 3], {"x": 4, "y": 5, "z": 6, "label": "b"}]}}""");

        Assert.Equal(CellOutputKind.Plot3D, output.Rich!.Kind);
        var spec = Assert.IsType<Plot3DSpec>(output.Rich.Visual!.Spec);
        Assert.Equal("Old", spec.Title);
        var series = Assert.Single(spec.Series);
        Assert.Equal([1, 4], series.X);
        Assert.Equal([3, 6], series.Z);
        Assert.Equal([null, "b"], series.Labels!);
    }

    // A program that sends a bad 3D plot learns what is wrong with it, in its output; the mapper doesn't throw (a throw
    // here is inside the kernel's reader, where it loses the whole display).
    [Theory]
    [InlineData("""{"application/vnd.fry.plot3d+json": {"type": "scatter", "points": [["one", 2, 3]]}, "text/plain": "3D"}""")]
    [InlineData("""{"application/vnd.fry.plot3d+json": {"type": "scatter", "points": [{"x": null, "y": 2, "z": 3}]}, "text/plain": "3D"}""")]
    public void AMalformed3DPlot_IsShownAsAnError_NotAThrow(string data)
    {
        var output = Map(data);

        Assert.NotNull(output.Rich);
        Assert.Equal(CellOutputKind.Error, output.Rich!.Kind);
        Assert.Contains("points", output.Rich.Text);
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
