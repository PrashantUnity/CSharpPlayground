using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Documentation;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Docs;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>A documentation sample runs where it is read: its output, charts and tables come back under it, and Stop and the time limit end it.</summary>
public class SnippetRunTests : IDisposable
{
    private readonly SnippetRunService _service = new(StudioLanguageServices.Default.Registry);

    public void Dispose() => _service.Dispose();

    private static DocCodeSnippet Snippet(string code, string language = "csharp") => new() { Id = "s", Title = "s", Code = code, Language = language };

    // The time limit is generous unless a test is about it: the first C# kernel of a loaded full-suite run takes a while to start.
    private SnippetRunViewModel Runner(DocCodeSnippet snippet, TimeSpan? timeout = null) => new(_service, snippet, timeout ?? TimeSpan.FromMinutes(3));

    [Fact]
    public async Task ASampleThatPrints_ShowsItsOutput_AndIsDone()
    {
        var run = Runner(Snippet("Console.WriteLine(\"hello from the docs\");"));

        await run.RunAsync();

        Assert.Equal(SnippetRunStatus.Done, run.Status);
        Assert.Contains("hello from the docs", run.ConsoleText);
        Assert.True(run.IsOutputOpen);
        Assert.StartsWith("Done in", run.StatusText);
    }

    [Fact]
    public async Task AChartAndASurface_AreDrawnUnderTheSample()
    {
        var run = Runner(Snippet("""
            Display.LineChart(new[] { 1, 3, 2 }, "Sales", xLabel: "Month");
            Charts.Surface((x, y) => x * y).Title("Saddle").Show();
            """));

        await run.RunAsync();

        Assert.Equal(SnippetRunStatus.Done, run.Status);
        Assert.Equal([CellOutputKind.Chart, CellOutputKind.Plot3D], run.Outputs.Select(o => o.Kind));
        Assert.Equal("Sales", run.Outputs[0].Visual!.Spec.Title);
    }

    [Fact]
    public async Task ADumpedTable_IsAnOutput()
    {
        var run = Runner(Snippet("new[] { new { Name = \"a\", Count = 1 }, new { Name = \"b\", Count = 2 } }.Dump(\"Rows\");"));

        await run.RunAsync();

        Assert.Equal(SnippetRunStatus.Done, run.Status);
        Assert.Contains(run.Outputs, o => o.Kind == CellOutputKind.Table);
    }

    [Fact]
    public async Task ASampleThatThrows_FailsAndSaysWhy()
    {
        var run = Runner(Snippet("throw new InvalidOperationException(\"boom\");"));

        await run.RunAsync();

        Assert.Equal(SnippetRunStatus.Failed, run.Status);
        Assert.Contains("boom", run.ConsoleText);
        Assert.Contains("boom", run.StatusText);
    }

    [Fact]
    public async Task ASampleThatDoesNotCompile_FailsWithItsErrors()
    {
        var run = Runner(Snippet("var x = ;"));

        await run.RunAsync();

        Assert.Equal(SnippetRunStatus.Failed, run.Status);
        Assert.False(string.IsNullOrWhiteSpace(run.ConsoleText));
    }

    [Fact]
    public async Task EachCSharpSampleStartsClean_SoItNeverDependsOnTheOneBefore()
    {
        var first = Runner(Snippet("var shared = 41; Console.WriteLine(shared + 1);"));
        var second = Runner(Snippet("Console.WriteLine(shared);"));

        await first.RunAsync();
        await second.RunAsync();

        Assert.Equal(SnippetRunStatus.Done, first.Status);
        Assert.Contains("42", first.ConsoleText);
        Assert.Equal(SnippetRunStatus.Failed, second.Status);
    }

    [Fact]
    public async Task RunningAgain_ReplacesTheOutput()
    {
        var run = Runner(Snippet("Display.BarChart(new[] { 1, 2 }); Console.WriteLine(\"once\");"));

        await run.RunAsync();
        await run.RunAsync();

        Assert.Single(run.Outputs);
        Assert.Equal(1, run.ConsoleText.Split("once").Length - 1);
    }

    [Fact]
    public async Task Stop_EndsASampleThatIsWaiting()
    {
        var run = Runner(Snippet("await Task.Delay(60000, Display.CancellationToken);"));

        var running = run.RunAsync();
        await WaitUntil(() => run.IsRunning);
        run.Stop();
        await running.WaitAsync(TimeSpan.FromSeconds(15));

        Assert.Equal(SnippetRunStatus.Stopped, run.Status);
        Assert.Equal("Stopped", run.StatusText);
    }

    [Fact]
    public async Task ASampleThatRunsTooLong_IsStoppedByTheTimeLimit()
    {
        var run = Runner(Snippet("await Task.Delay(60000, Display.CancellationToken);"), TimeSpan.FromMilliseconds(400));

        await run.RunAsync().WaitAsync(TimeSpan.FromSeconds(15));

        Assert.Equal(SnippetRunStatus.TimedOut, run.Status);
        Assert.StartsWith("Stopped after", run.StatusText);
    }

    [Fact]
    public async Task ASampleThatIgnoresStop_IsGivenUpOn_AndTheNextOneStillRuns()
    {
        var stuck = Runner(Snippet("System.Threading.Thread.Sleep(20000);"), TimeSpan.FromMilliseconds(300));
        var next = Runner(Snippet("Console.WriteLine(\"still alive\");"));

        await stuck.RunAsync().WaitAsync(TimeSpan.FromSeconds(15));
        await next.RunAsync().WaitAsync(TimeSpan.FromSeconds(15));

        Assert.Equal(SnippetRunStatus.TimedOut, stuck.Status);
        Assert.Equal(SnippetRunStatus.Done, next.Status);
        Assert.Contains("still alive", next.ConsoleText);
    }

    [Fact]
    public async Task Clear_RemovesTheOutput()
    {
        var run = Runner(Snippet("Display.BarChart(new[] { 1, 2 }); Console.WriteLine(\"x\");"));
        await run.RunAsync();

        run.Clear();

        Assert.Equal(SnippetRunStatus.Idle, run.Status);
        Assert.Empty(run.Outputs);
        Assert.Equal(string.Empty, run.ConsoleText);
        Assert.False(run.IsOutputOpen);
    }

    [Fact]
    public async Task TooMuchOutput_IsCutOff()
    {
        var run = Runner(Snippet("for (var i = 0; i < 60000; i++) Console.WriteLine(\"line number \" + i);"));

        await run.RunAsync();

        Assert.Equal(SnippetRunStatus.Done, run.Status);
        Assert.True(run.ConsoleText.Length < 300_000);
        Assert.Contains("too much", run.ConsoleText);
    }

    [Fact]
    public void OnlySamplesInALanguageThatRunsInNotebooks_CanRun()
    {
        Assert.True(Runner(Snippet("1", "csharp")).CanRun);
        Assert.False(Runner(Snippet("1", "no-such-language")).CanRun);
        Assert.False(Runner(new DocCodeSnippet { Code = "1", Language = "csharp", NotRunnable = true }).CanRun);
    }

    [Fact]
    public void SwitchingTheLanguageTab_UpdatesWhetherTheSampleCanRun()
    {
        var snippet = Snippet("1", "no-such-language");
        var run = Runner(snippet);
        var changed = new List<string?>();
        run.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        snippet.Language = "csharp";

        Assert.True(run.CanRun);
        Assert.Contains(nameof(SnippetRunViewModel.CanRun), changed);
    }

    [Fact]
    public void TheGuidesThatNeedAServerOrAPlugin_GetNoRunButton_AndTheChartGuideDoes()
    {
        var categories = DocumentationService.Instance.Categories;

        foreach (var id in new[] { "aspnet_core", "ef_core", "extensibility_customization" })
        {
            var snippets = categories.Single(c => c.Id == id).Articles.SelectMany(a => a.CodeSnippets).ToList();
            Assert.NotEmpty(snippets);
            Assert.All(snippets, s => Assert.True(s.NotRunnable, $"{id}: {s.Title}"));
        }

        var quickStart = categories.SelectMany(c => c.Articles).Single(a => a.Id == "charts_quickstart");
        Assert.All(quickStart.CodeSnippets, s => Assert.False(s.NotRunnable));
    }

    [Fact]
    public void SelectingAnArticle_GivesItsSamplesTheirRunButtons_AndLeavingItStopsThem()
    {
        using var docs = new CSharpDocsViewModel(languages: StudioLanguageServices.Default);
        var article = DocumentationService.Instance.GetArticle("charts_quickstart")!;

        docs.SelectArticle(article);

        Assert.All(article.CodeSnippets, s => Assert.True(s.Run!.CanRun));
        var other = DocumentationService.Instance.GetArticle("dump_api")!;
        var before = article.CodeSnippets[0].Run;
        docs.SelectArticle(other);
        docs.SelectArticle(article);
        Assert.Same(before, article.CodeSnippets[0].Run);
    }

    [Fact]
    public async Task EveryQuickStartSample_RunsToTheEnd()
    {
        var article = DocumentationService.Instance.GetArticle("charts_quickstart")!;

        foreach (var snippet in article.CodeSnippets)
        {
            var run = Runner(snippet);
            await run.RunAsync().WaitAsync(TimeSpan.FromSeconds(60));

            Assert.True(run.Status == SnippetRunStatus.Done, $"{snippet.Id}: {run.StatusText} {run.ConsoleText}");
            Assert.NotEmpty(run.Outputs);
        }
    }

    [Fact]
    public void TheWheelZoomsEverywhereExceptWhereAPageAsksForCtrl()
    {
        var plain = new Avalonia.Controls.Border();
        var inDocs = new Avalonia.Controls.Border();
        PdfEditorApp.Plugins.CSharpEditor.Controls.Common.WheelZoomGate.SetRequireCtrl(inDocs, true);

        Assert.True(PdfEditorApp.Plugins.CSharpEditor.Controls.Common.WheelZoomGate.ShouldZoom(plain, Avalonia.Input.KeyModifiers.None));
        Assert.False(PdfEditorApp.Plugins.CSharpEditor.Controls.Common.WheelZoomGate.ShouldZoom(inDocs, Avalonia.Input.KeyModifiers.None));
        Assert.False(PdfEditorApp.Plugins.CSharpEditor.Controls.Common.WheelZoomGate.ShouldZoom(inDocs, Avalonia.Input.KeyModifiers.Shift));
        Assert.True(PdfEditorApp.Plugins.CSharpEditor.Controls.Common.WheelZoomGate.ShouldZoom(inDocs, Avalonia.Input.KeyModifiers.Control));
        Assert.True(PdfEditorApp.Plugins.CSharpEditor.Controls.Common.WheelZoomGate.ShouldZoom(inDocs, Avalonia.Input.KeyModifiers.Meta));
    }

    [Fact]
    public async Task ARunThatDrawsAChart_SaysHowToZoomIt()
    {
        var run = Runner(Snippet("Display.BarChart(new[] { 1, 2 });"));
        await run.RunAsync();
        Assert.True(run.HasVisuals);

        var textOnly = Runner(Snippet("Console.WriteLine(1);"));
        await textOnly.RunAsync();
        Assert.False(textOnly.HasVisuals);
    }

    [Fact]
    public async Task EveryChartGallerySample_DrawsAValidChart()
    {
        var gallery = DocumentationService.Instance.Categories.Single(c => c.Id == "chart_gallery");
        var samples = gallery.Articles.SelectMany(a => a.CodeSnippets).ToList();
        Assert.True(samples.Count >= 30, "The gallery should have a sample for each kind of chart.");

        foreach (var snippet in samples)
        {
            var run = Runner(snippet);
            await run.RunAsync().WaitAsync(TimeSpan.FromSeconds(60));

            // A spec that can't be drawn shows a warning with what is wrong instead of a chart.
            Assert.True(run.Status == SnippetRunStatus.Done && !run.ConsoleText.Contains("⚠️"), $"{snippet.Id}: {run.StatusText} {run.ConsoleText}");
            Assert.True(run.Outputs.Count > 0 && run.Outputs.All(o => o.Kind == CellOutputKind.Chart), $"{snippet.Id}: {string.Join(", ", run.Outputs.Select(o => o.Kind))}");
        }
    }

    [Fact]
    public async Task EveryAnimationSample_RunsToTheEnd_AndLeavesALiveControlOrChart()
    {
        var article = DocumentationService.Instance.GetArticle("animate_and_cancellation")!;
        Assert.True(article.CodeSnippets.Count >= 8, "The animation guide should show several kinds of animation.");

        foreach (var snippet in article.CodeSnippets)
        {
            var run = Runner(snippet);
            await run.RunAsync().WaitAsync(TimeSpan.FromSeconds(60));

            Assert.True(run.Status == SnippetRunStatus.Done, $"{snippet.Id}: {run.StatusText} {run.ConsoleText}");
            Assert.NotEmpty(run.Outputs);
            Assert.All(run.Outputs, o => Assert.True(o.Kind is CellOutputKind.Control or CellOutputKind.Chart, $"{snippet.Id}: {o.Kind}"));

            // Clearing the output stops what animates.
            var animations = run.Outputs.Select(o => o.InteractiveControl).OfType<PdfEditorApp.Plugins.CSharpEditor.Controls.Visuals.AnimatedRenderControl>().ToList();
            run.Clear();
            Assert.All(animations, a => Assert.False(a.IsTicking));
        }
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < until, "The condition never became true.");
            await Task.Delay(20);
        }
    }
}
