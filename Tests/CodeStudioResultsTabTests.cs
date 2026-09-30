using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Output;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>
/// The Results tab shows its "No Visual Dumps Yet" hint only when it has nothing to draw: a visualizer, chart, image or
/// inspector counts as much as a table dump.
/// </summary>
public class CodeStudioResultsTabTests : IDisposable
{
    private readonly string _storageDir = Path.Combine(Path.GetTempPath(), "FryPDF_ResultsTabTests_" + Guid.NewGuid().ToString("N"));
    private readonly LocalScriptStorageService _storage;

    public CodeStudioResultsTabTests()
    {
        _storage = new LocalScriptStorageService(_storageDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_storageDir)) Directory.Delete(_storageDir, recursive: true);
        }
        catch { }
    }

    private CSharpCodeStudioViewModel Studio(ScriptDocumentItem? script = null) =>
        new(script ?? new ScriptDocumentItem { Title = "Results tab test", Code = "var x = 1;" },
            _storage, new RoslynCompilerService(), new ScriptExecutionEngine(), backToHubAction: () => { });

    [Fact]
    public void BeforeAnyRun_TheHintShows()
    {
        Assert.True(Studio().HasNoResults);
    }

    [Theory]
    [InlineData(CellOutputKind.Control)]
    [InlineData(CellOutputKind.Image)]
    [InlineData(CellOutputKind.Html)]
    [InlineData(CellOutputKind.ObjectInspector)]
    public void AnythingTheTabDraws_HidesTheHint(CellOutputKind kind)
    {
        var studio = Studio();

        studio.RichOutputs.Add(new RichCellOutput { Kind = kind });

        Assert.False(studio.HasNoResults);
    }

    [Theory]
    [InlineData(VisualFamily.Chart)]
    [InlineData(VisualFamily.Plot3D)]
    [InlineData(VisualFamily.Visualizer)]
    public void AVisual_HidesTheHint(VisualFamily family)
    {
        var studio = Studio();

        studio.RichOutputs.Add(VisualOutputs.FromSpec(SampleSpecs.Of(family, "shown")));

        Assert.False(studio.HasNoResults);
    }

    // A chart in Code Studio's Results was blank: Display.Chart built its control on the script's thread, where it
    // couldn't load. The output is now the chart's spec, which the Results view draws on the UI thread.
    [Fact]
    public async Task RunningAScriptThatShowsAChart_PutsTheChartInResults()
    {
        var studio = Studio(new ScriptDocumentItem { Title = "Chart", Code = "Display.Chart(new[] { 1, 4, 9 }, title: \"Squares\");" });

        await studio.RunCodeCommand.ExecuteAsync(null);

        var chart = Assert.Single(studio.RichOutputs, o => o.IsVisualKind);
        Assert.Equal("Squares", chart.ChartSpec().Title);
        Assert.Null(chart.InteractiveControl);
        Assert.False(studio.HasNoResults);
        Assert.Equal(0, studio.SelectedBottomTabIndex); // Results, as after a program in any other language
    }

    [Fact]
    public async Task RunningAProgramThatShowsAChart_PutsTheChartInResults()
    {
        var studio = Studio(new ScriptDocumentItem
        {
            Title = "Program",
            Code = "public static class Program { public static void Main() { Display.Chart(new[] { 1, 4, 9 }, title: \"From Main\"); } }"
        });
        studio.SelectedLanguageModeIndex = 1; // C# Program

        await studio.RunCodeCommand.ExecuteAsync(null);

        Assert.True(studio.RichOutputs.Any(o => o.IsVisualKind), studio.ConsoleOutput);
        Assert.Equal("From Main", Assert.Single(studio.RichOutputs, o => o.IsVisualKind).ChartSpec().Title);
        Assert.Equal(0, studio.SelectedBottomTabIndex);
    }

    [Fact]
    public async Task ASpecWithAMistake_IsReportedInTheTerminal_WithWhereItIs()
    {
        var studio = Studio(new ScriptDocumentItem
        {
            Title = "Mistake",
            Code = "Display.Show(new ChartSpec { Series = { new ChartSeriesSpec { X = new() { 1, 2 }, Y = new() { 1, 2, 3 } } } });"
        });

        await studio.RunCodeCommand.ExecuteAsync(null);

        Assert.Contains("⚠️", studio.ConsoleOutput);
        Assert.Contains("$.series[0].x", studio.ConsoleOutput);
        Assert.True(studio.HasNoResults);
    }

    [Theory]
    [InlineData(CellOutputKind.Text)]
    [InlineData(CellOutputKind.Error)]
    public void OutputTheTabDoesNotDraw_LeavesTheHint(CellOutputKind kind)
    {
        var studio = Studio();

        studio.RichOutputs.Add(new RichCellOutput { Kind = kind, Text = "shown in the Terminal instead" });

        Assert.True(studio.HasNoResults);
    }

    [Fact]
    public void ATableDump_HidesTheHint()
    {
        var studio = Studio();

        studio.DumpResults.Add(new DumpTableResult("numbers"));

        Assert.False(studio.HasNoResults);
    }

    [Fact]
    public void TheViewHearsEveryChange_AndClearingBringsTheHintBack()
    {
        var studio = Studio();
        var changes = new List<string?>();
        studio.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        studio.RichOutputs.Add(VisualOutputs.FromSpec(SampleSpecs.Visualizer()));
        Assert.Contains(nameof(CSharpCodeStudioViewModel.HasNoResults), changes);

        changes.Clear();
        studio.ClearResults();
        Assert.True(studio.HasNoResults);
        Assert.Contains(nameof(CSharpCodeStudioViewModel.HasNoResults), changes);
    }

    [Fact]
    public async Task SwitchingTabs_ShowsEachTabsOwnResults()
    {
        var withResults = await _storage.CreateNewScriptAsync("With a visualizer");
        var withoutResults = await _storage.CreateNewScriptAsync("Nothing drawn");
        var studio = Studio(withResults);
        studio.RichOutputs.Add(VisualOutputs.FromSpec(SampleSpecs.Visualizer()));

        await studio.UpdateActiveScriptAsync(withoutResults);
        Assert.True(studio.HasNoResults);

        await studio.SwitchToTabAsync(studio.OpenTabs.First(t => t.Id == withResults.Id));
        Assert.False(studio.HasNoResults);
    }

    [Fact]
    public async Task RunningABlind75Script_LeavesItsVisualizerWithoutTheHint()
    {
        var studio = Studio(Blind75CatalogService.ConvertToScript(Blind75CatalogService.GetProblemByNumber(1)!));
        Assert.True(studio.HasNoResults);

        await studio.RunCodeCommand.ExecuteAsync(null);

        Assert.Contains(studio.RichOutputs, o => o.IsVisualizerKind);
        Assert.Empty(studio.DumpResults);
        Assert.False(studio.HasNoResults);
    }

    [Fact]
    public void ExternalOutputProcessor_VisualDump_PopulatesStudioResultsAndSelectsTab()
    {
        var studio = Studio();
        studio.SelectedBottomTabIndex = 1; // Console tab
        Assert.True(studio.HasNoResults);

        var processor = new PdfEditorApp.Plugins.CSharpEditor.Services.Processes.ExternalOutputProcessor(
            text => studio.ConsoleOutput += text,
            rich =>
            {
                studio.RichOutputs.Add(rich);
                if (rich.TableResult != null)
                {
                    studio.DumpResults.Add(rich.TableResult);
                    studio.SelectedBottomTabIndex = 0;
                }
            });

        const string dumpPayload = """
            __FRY_DISPLAY__ {"type":"display","data":{"application/vnd.fry.table+json":{"title":"Sorted Array","columns":["Index","Value"],"numeric":[true,true],"rows":[[0,7],[1,11],[2,12]],"totalRows":3,"totalColumns":2}},"metadata":{}}
            """;

        processor.ProcessChunk("Sorting completed.\n" + dumpPayload + "\nAll done.\n");
        processor.Flush();

        Assert.Contains("Sorting completed.", studio.ConsoleOutput);
        Assert.Contains("All done.", studio.ConsoleOutput);
        Assert.DoesNotContain("__FRY_DISPLAY__", studio.ConsoleOutput);

        Assert.False(studio.HasNoResults);
        Assert.Single(studio.DumpResults);
        Assert.Equal("Sorted Array", studio.DumpResults[0].Title);
        Assert.Equal(3, studio.DumpResults[0].Rows.Count);
        Assert.Equal(0, studio.SelectedBottomTabIndex);
    }
}

