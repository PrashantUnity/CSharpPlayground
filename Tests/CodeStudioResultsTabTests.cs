using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
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
    [InlineData(CellOutputKind.Visualizer)]
    [InlineData(CellOutputKind.Chart)]
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

        studio.RichOutputs.Add(new RichCellOutput { Kind = CellOutputKind.Visualizer });
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
        studio.RichOutputs.Add(new RichCellOutput { Kind = CellOutputKind.Visualizer });

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
}
