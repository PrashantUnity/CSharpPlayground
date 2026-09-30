using System.Collections.Concurrent;
using System.Diagnostics;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Interaction;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Output;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Spec;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.RealGo;

[Collection(RealGoCollection.Name)]
public class GoVisualsConformanceTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_GoVisuals_" + Guid.NewGuid().ToString("N"));
    private readonly List<INotebookKernel> _kernels = [];

    public GoVisualsConformanceTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        foreach (var k in _kernels) k.Dispose();
        try
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private string Write(string name, string code)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, code.Replace("\r\n", "\n"));
        return path;
    }

    private static async Task WaitUntil(Func<bool> condition, TimeSpan? timeout = null)
    {
        var clock = Stopwatch.StartNew();
        var limit = timeout ?? Patience;
        while (!condition())
        {
            if (clock.Elapsed > limit) throw new TimeoutException("Condition was not met within " + limit);
            await Task.Delay(20);
        }
    }

    private const string The18Statements = """
        fry.LineChart([]any{3, 1, nil, 4}, "Numbers")
        fry.ScatterChart([][]any{{1, 2}, {2, 4.5}}, "Pairs")
        fry.BarChart(fry.Map("Mon", 3, "Tue", 5), "Days")
        fry.Chart(fry.Map("a", []int{1, 2}, "b", []int{3, 4}), "Series")
        fry.Chart([]any{fry.Map("name", "Jan", "value", 10), fry.Map("name", "Feb", "value", 12)}, "Records")
        fry.Histogram([]int{1, 2, 2, 3, 3, 3}, "Samples", fry.Bins(3))
        fry.PieChart(fry.Map("A", 1, "B", 2), "Share")
        fry.Scatter3D([][]int{{1, 2, 3}, {4, 5, 6}}, "Points")
        fry.Surface3D([][]int{{1, 2}, {3, 4}}, "Heights")
        fry.Graph3D(fry.Map("a", []string{"b", "c"}, "b", []string{"c"}), "Links")
        fry.Matrix([][]int{{1, 0}, {0, 1}}, "Grid")
        fry.Array([]int{1, 3, 5}, fry.Map("i", 0, "j", 2), "Two pointers")
        fry.Tree(&fry.TreeNode{Val: 2, Left: &fry.TreeNode{Val: 1}, Right: &fry.TreeNode{Val: 3}}, "Tree")
        fry.Tree([]any{1, 2, 3, nil, 4}, "Level order")
        fry.Graph(fry.Map("a", []string{"b"}, "b", []string{"c"}), "Graph")
        fry.LinkedList(&fry.ListNode{Val: 1, Next: &fry.ListNode{Val: 2, Next: &fry.ListNode{Val: 3}}}, "List")
        fry.Bars([]int{3, 1, 2}, "Bars")
        fry.Islands([][]int{{1, 0}, {1, 1}}, "Islands")
        """;

    private static string BuildMainProgram(string statements) => $$"""
        package main

        import (
            "fry"
        )

        func main() {
            {{statements}}
        }
        """;

    private async Task<(ScriptRunSession Session, List<string> Console, List<RichCellOutput> Outputs, ExternalOutputProcessor Processor)> StartRun(
        string path,
        ExternalVisualSession? session = null)
    {
        var go = TestGo.Require();
        var services = TestGo.Services(Path.Combine(_dir, ".studio"));
        var language = (GoLanguage)services.Registry.Get(LanguageIds.Go)!;
        var plan = await language.ScriptRunner.PlanAsync(new ScriptRunContext(path, Path.GetDirectoryName(path)!, go));
        var console = new List<string>();
        var outputs = new List<RichCellOutput>();
        var processor = new ExternalOutputProcessor(console.Add, outputs.Add, visuals: session?.Visuals);
        var runSession = new ScriptRunExecutor(services.Processes).Start(
            plan,
            path,
            language.RunDiagnostics,
            processor.ProcessChunk,
            environment: session?.Environment);
        return (runSession, console, outputs, processor);
    }

    private INotebookKernel Kernel()
    {
        var services = new StudioLanguageServices(Path.Combine(_dir, "services"));
        var go = (GoLanguage)services.Registry.Get(LanguageIds.Go)!;
        go.GoToolchain.Select(TestGo.Require().ExecutablePath);
        var kernel = go.NotebookKernels.Create(new KernelCreationContext(() => _dir));
        _kernels.Add(kernel);
        return kernel;
    }

    private static async Task<(KernelExecutionResult Result, List<string> Console, List<RichCellOutput> Rich)> ExecuteCell(
        INotebookKernel kernel,
        string code)
    {
        var console = new List<string>();
        var rich = new List<RichCellOutput>();
        var result = await kernel.ExecuteAsync(new KernelExecutionRequest
        {
            Code = code.Replace("\r\n", "\n"),
            SourceId = "cell",
            OnConsole = console.Add,
            OnRichOutput = rich.Add,
        }, CancellationToken.None).WaitAsync(Patience);
        return (result, console, rich);
    }

    [GoFact]
    public async Task Run_The18Cases_DrawAsTheFixturesDo()
    {
        var scriptCode = BuildMainProgram(The18Statements);
        var script = Write("main.go", scriptCode);
        var (session, console, outputs, processor) = await StartRun(script);
        var result = await session.Completion.WaitAsync(Patience);
        processor.Flush();

        Assert.True(result.Succeeded, string.Concat(console));
        Assert.Equal(18, outputs.Count);

        for (var i = 0; i < VisualConformanceFixturesTests.Cases.Length; i++)
        {
            var (name, _) = VisualConformanceFixturesTests.Cases[i];
            var (mime, drawn) = VisualConformanceFixturesTests.Expected(name);
            var v = outputs[i].Visual;
            Assert.NotNull(v);
            Assert.Equal(mime, v.MimeType);
            Assert.Equal(drawn, VisualConformanceFixturesTests.DrawnAs(mime, VisualJson.SerializeToElement(v.Spec)));
        }
    }

    [GoFact]
    public async Task Run_Update_RedrawsTheSameSingleOutput()
    {
        var scriptCode = BuildMainProgram("""
            h := fry.LineChart([]int{1, 2}, "A")
            h.Update("B")
            fry.Wait(0.2)
        """);
        var script = Write("main.go", scriptCode);
        var (session, console, outputs, processor) = await StartRun(script);
        var result = await session.Completion.WaitAsync(Patience);
        processor.Flush();

        Assert.True(result.Succeeded, string.Concat(console));
        var visual = Assert.Single(outputs).Visual!;
        var spec = Assert.IsType<ChartSpec>(visual.Spec);
        Assert.Equal("B", spec.Title);
    }

    [GoFact]
    public async Task Run_Click_NotifiesProgramOverLoopback()
    {
        using var visualSession = new ExternalVisualSession();
        var scriptCode = BuildMainProgram("""
            h := fry.LineChart([]int{1, 2, 3}, "C")
            h.OnClick(func(e map[string]any) {
                target, _ := e["target"].(map[string]any)
                println("clicked", int(target["index"].(float64)))
            })
            fry.Wait(10)
        """);
        var script = Write("main.go", scriptCode);
        var (session, console, outputs, processor) = await StartRun(script, visualSession);

        await WaitUntil(() => outputs.Count == 1 && outputs[0].Visual?.IsInteractive == true);
        outputs[0].Visual!.Raise(VisualEvent.Click(new VisualEventTarget { Series = 0, Index = 1 }));

        await WaitUntil(() => console.Any(c => c.Contains("clicked 1")));
        session.Stop();
    }

    [GoFact]
    public async Task Notebook_The18Cases_DrawAsTheFixturesDo()
    {
        var kernel = Kernel();
        var (result, console, rich) = await ExecuteCell(kernel, The18Statements);

        Assert.True(result.Success, string.Concat(console) + " " + result.ErrorMessage);
        Assert.Equal(18, rich.Count);

        for (var i = 0; i < VisualConformanceFixturesTests.Cases.Length; i++)
        {
            var (name, _) = VisualConformanceFixturesTests.Cases[i];
            var (mime, drawn) = VisualConformanceFixturesTests.Expected(name);
            var v = rich[i].Visual;
            Assert.NotNull(v);
            Assert.Equal(mime, v.MimeType);
            Assert.Equal(drawn, VisualConformanceFixturesTests.DrawnAs(mime, VisualJson.SerializeToElement(v.Spec)));
        }
    }

    [GoFact]
    public async Task Notebook_Update_RedrawsTheSameSingleOutput()
    {
        var kernel = Kernel();
        var (result, console, rich) = await ExecuteCell(kernel, """
            h := fry.LineChart([]int{1, 2}, "A")
            h.Update("B")
            fry.Wait(0.2)
            """);

        Assert.True(result.Success, string.Concat(console) + " " + result.ErrorMessage);
        var visual = Assert.Single(rich).Visual!;

        await WaitUntil(() => ((ChartSpec)rich[0].Visual!.Spec).Title == "B");
        Assert.Equal("B", ((ChartSpec)rich[0].Visual!.Spec).Title);
    }
}
