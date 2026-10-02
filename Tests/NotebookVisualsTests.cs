using System.Text.Json;
using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Output;
using Xunit;
using NotebookCellViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Notebooks.NotebookCellViewModel;
using NotebookTabViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Notebooks.NotebookTabViewModel;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// A notebook cell's charts, 3D plots and visualizers: every one it shows, in order; cleared with the rest of its output;
/// saved with the notebook and shown again when it is reopened, including one this version can't draw, which says why.
/// </summary>
public class NotebookVisualsTests : IDisposable
{
    private const string FourVisuals = """
        Display.Chart(new[] { 1, 4, 9 }, title: "First chart");
        Display.Scatter3D(new[] { new Point3D(1, 2, 3), new Point3D(4, 5, 6) }, title: "Points");
        Display.Matrix(new[,] { { 1, 0 }, { 0, 1 } }, title: "Grid");
        Display.Chart(new[] { 2, 3, 5 }, title: "Second chart");
        """;

    private readonly string _storageDir = Path.Combine(Path.GetTempPath(), "FryPDF_NotebookVisualsTests_" + Guid.NewGuid().ToString("N"));
    private readonly LocalScriptStorageService _storage;

    public NotebookVisualsTests()
    {
        _storage = new LocalScriptStorageService(_storageDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_storageDir)) Directory.Delete(_storageDir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    // A cell had one slot per family, so it kept only its last chart, 3D plot and visualizer.
    [Fact]
    public async Task ACell_KeepsEveryVisualItShows_InOrder()
    {
        var (_, cell) = await RunCell(FourVisuals);

        Assert.False((bool)cell.HasError, cell.OutputText);
        Assert.Equal(["First chart", "Points", "Grid", "Second chart"], cell.Visuals.Select(v => v.Spec.Title));
        Assert.Equal(["First chart", "Second chart"], cell.ChartVisuals.Select(v => v.Spec.Title));
        Assert.Single(cell.Plot3DVisuals);
        Assert.Single(cell.VisualizerVisuals);
        Assert.Equal(3, cell.AvailableOutputCount);
    }

    // Clearing a cell's output left its 3D plots and visualizers on screen, and in the saved notebook.
    [Fact]
    public async Task ClearingACellsOutput_ClearsEveryVisual()
    {
        var (_, cell) = await RunCell(FourVisuals);

        cell.ClearOutput();

        Assert.Empty(cell.Visuals);
        Assert.False(cell.HasChartOutput || cell.HasPlot3DOutput || cell.HasVisualizerOutput);
        Assert.Null(cell.Model.Visuals);
        Assert.False(cell.Model.HasOutput);
        Assert.Equal(0, cell.AvailableOutputCount);
    }

    [Fact]
    public async Task RunningACellAgain_ReplacesItsVisuals()
    {
        var (tab, cell) = await RunCell(FourVisuals);

        await tab.RunSingleCellAsync(cell);

        Assert.Equal(4, cell.Visuals.Count);
        Assert.Equal(4, cell.Model.Visuals!.Count);
    }

    [Fact]
    public async Task ACellThatShowsMoreVisualsThanItKeeps_SaysHowManyItShows()
    {
        var shown = VisualLimits.MaxVisualsPerCell + 5;

        var (_, cell) = await RunCell($"for (var i = 0; i < {shown}; i++) Display.Chart(new[] {{ i, i + 1 }}, title: $\"Chart {{i}}\");");

        Assert.Equal(VisualLimits.MaxVisualsPerCell, cell.Visuals.Count);
        Assert.Equal(VisualLimits.MaxVisualsPerCell, cell.Model.Visuals!.Count);
        Assert.Equal((string?)$"showing the first {VisualLimits.MaxVisualsPerCell} of {shown} visuals", (string?)cell.VisualsNotice);
    }

    // A reopened notebook used to lose its charts, 3D plots and visualizers: the cell had nowhere to save them.
    [Fact]
    public async Task ASavedNotebook_ReopensWithEveryVisual()
    {
        var notebook = Notebook(FourVisuals);
        var tab = new NotebookTabViewModel(notebook);
        await tab.RunSingleCellAsync(tab.Cells[0]);
        var shown = tab.Cells[0].Visuals.Select(v => VisualJson.Serialize(v.Spec)).ToList();

        var cell = Assert.Single((await SaveAndReopen(notebook)).Cells);

        Assert.Equal(4, shown.Count);
        Assert.Equal(shown, cell.Visuals.Select(v => VisualJson.Serialize(v.Spec)));
        Assert.Equal(2, cell.ChartVisuals.Count);
        Assert.True((bool)cell.HasOutput);
    }

    [Fact]
    public async Task AVisualTooLargeToSave_ReopensAsAFrameThatSaysHowToRedrawIt()
    {
        var big = SampleSpecs.Chart("Big");
        big.Series[0].Y = Enumerable.Range(0, 100_000).Select(i => (double?)i).ToList();
        big.Series[0].Labels = Enumerable.Range(0, 100_000).Select(i => (string?)$"a long label for the point numbered {i:D6}").ToList();
        var notebook = Notebook("Display.Show(big);");
        var tab = new NotebookTabViewModel(notebook);
        tab.Cells[0].AddVisual(VisualOutput.Create(big));

        var cell = Assert.Single((await SaveAndReopen(notebook)).Cells);

        Assert.True(VisualJson.SerializeToUtf8Bytes(big).Length > VisualOutput.MaxSavedBytes);
        var frame = Assert.IsType<ChartSpec>(Assert.Single(cell.ChartVisuals).Spec);
        Assert.Equal("Big", frame.Title);
        Assert.Contains("run the cell again", frame.Subtitle);
        Assert.Empty(frame.Series);
    }

    // A newer studio may save a version of a visual this one can't read: it is shown as a frame that says so, and the
    // notebook keeps it as it was, so saving here doesn't lose it.
    [Fact]
    public async Task AVisualSavedByANewerVersion_IsAFrameSayingSo_AndIsSavedAgainUnchanged()
    {
        const string newer = "application/vnd.fry.chart.v2+json";
        var notebook = Notebook("Display.Sankey(flows);");
        notebook.Cells[0].Visuals = [new VisualOutputSnapshot { MimeType = newer, Data = Json("""{"kind":"sankey","links":[]}"""), Title = "Flows" }];

        var reopened = await SaveAndReopen(notebook);
        var cell = Assert.Single(reopened.Cells);
        var frame = Assert.IsType<ChartSpec>(Assert.Single(cell.ChartVisuals).Spec);
        Assert.Equal("Flows", frame.Title);
        Assert.Contains("newer version", frame.Subtitle);

        var savedAgain = Assert.Single(Assert.Single((await SaveAndReopen(reopened.Notebook)).Cells).Model.Visuals!);
        Assert.Equal(newer, savedAgain.MimeType);
        Assert.Equal("sankey", savedAgain.Data!.Value.GetProperty("kind").GetString());
    }

    [Fact]
    public void ADamagedSavedVisual_IsAFrameThatSaysWhatIsWrong()
    {
        var saved = new VisualOutputSnapshot { MimeType = VisualMimeTypes.Chart, Data = Json("""{"titel":"Oops"}"""), Title = "Oops" };

        var cell = new NotebookCellViewModel(new NotebookCellItem { Type = CellType.Code, Visuals = [saved] });

        var frame = Assert.IsType<ChartSpec>(Assert.Single(cell.ChartVisuals).Spec);
        Assert.Contains("titel", frame.Subtitle);
        Assert.Contains("run the cell again", frame.Subtitle);
    }

    // A display that can't be drawn says what is wrong, in the cell, rather than showing nothing.
    [Fact]
    public async Task ASpecWithAMistake_IsReportedInTheCell_WithWhereItIs()
    {
        var (_, cell) = await RunCell("Display.Show(new ChartSpec { Series = { new ChartSeriesSpec { X = new() { 1, 2 }, Y = new() { 1, 2, 3 } } } });");

        Assert.Empty(cell.Visuals);
        Assert.Contains((string)"⚠️", (string?)cell.OutputText);
        Assert.Contains((string)"$.series[0].x", (string?)cell.OutputText);
    }

    // A cell that returns a spec, or anything that describes itself as one, shows it as the visual.
    [Fact]
    public async Task ACellThatReturnsASpec_ShowsIt()
    {
        var (_, cell) = await RunCell("new ChartSpec { Title = \"Returned\", Series = { new ChartSeriesSpec { Y = new() { 1, 2 } } } }");

        Assert.Equal("Returned", Assert.Single(cell.ChartVisuals).Spec.Title);
        Assert.False((bool)cell.HasInspectorOutput);
    }

    private static NotebookDocumentItem Notebook(string code) => new()
    {
        Title = "Visuals " + Guid.NewGuid().ToString("N")[..6],
        Cells = { new NotebookCellItem { Type = CellType.Code, Source = code } }
    };

    private static async Task<(NotebookTabViewModel Tab, NotebookCellViewModel Cell)> RunCell(string code)
    {
        var tab = new NotebookTabViewModel(Notebook(code));
        var cell = tab.Cells[0];
        await tab.RunSingleCellAsync(cell);
        return (tab, cell);
    }

    // The notebook as the studio saves it to disk and opens it again.
    private async Task<NotebookTabViewModel> SaveAndReopen(NotebookDocumentItem notebook)
    {
        Assert.True(await _storage.SaveNotebookAsync(notebook));
        var loaded = await _storage.LoadNotebookAsync(notebook.Id);
        Assert.NotNull(loaded);
        return new NotebookTabViewModel(loaded);
    }

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
