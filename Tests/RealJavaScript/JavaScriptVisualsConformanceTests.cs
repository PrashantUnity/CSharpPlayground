using System.Collections.Concurrent;
using System.Diagnostics;
using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.JavaScript;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Interaction;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;
using Xunit;

namespace CSharpEditorPlugin.Tests.RealJavaScript;

[Collection(RealJavaScriptCollection.Name)]
public class JavaScriptVisualsConformanceTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_JSVisuals_" + Guid.NewGuid().ToString("N"));
    private readonly List<INotebookKernel> _kernels = [];

    public JavaScriptVisualsConformanceTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        foreach (var k in _kernels) k.Dispose();
        try
        {
            Directory.Delete(_dir, recursive: true);
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

    private const string The18Calls = """
        const D = Display;
        class T { constructor(val, left = null, right = null) { this.val = val; this.left = left; this.right = right; } }
        class L { constructor(val, next = null) { this.val = val; this.next = next; } }
        D.lineChart([3, 1, null, 4], "Numbers");
        D.scatterChart([[1, 2], [2, 4.5]], "Pairs");
        D.barChart({ Mon: 3, Tue: 5 }, "Days");
        D.chart({ a: [1, 2], b: [3, 4] }, "Series");
        D.chart([{ name: "Jan", value: 10 }, { name: "Feb", value: 12 }], "Records");
        D.histogram([1, 2, 2, 3, 3, 3], "Samples", { bins: 3 });
        D.pieChart({ A: 1, B: 2 }, "Share");
        D.scatter3d([[1, 2, 3], [4, 5, 6]], "Points");
        D.surface3d([[1, 2], [3, 4]], "Heights");
        D.graph3d({ a: ["b", "c"], b: ["c"] }, "Links");
        D.matrix([[1, 0], [0, 1]], "Grid");
        D.array([1, 3, 5], { i: 0, j: 2 }, "Two pointers");
        D.tree(new T(2, new T(1), new T(3)), "Tree");
        D.tree([1, 2, 3, null, 4], "Level order");
        D.graph({ a: ["b"], b: ["c"] }, "Graph");
        D.linkedList(new L(1, new L(2, new L(3))), "List");
        D.bars([3, 1, 2], "Bars");
        D.islands([[1, 0], [1, 1]], "Islands");
        """;

    // The newer chart features (VisualConformanceFixturesTests.ChartFeatureCases), made the way JavaScript writes them.
    private const string TheFeatureCalls = """
        const D = Display;
        const months = ["Jan", "Feb", "Mar"];
        const rev = [40, 55, 48], costs = [30, 35, 38], margin = [25, 36, 21];
        D.barChart({ Revenue: rev, Costs: costs, Margin: { values: margin, kind: "line", axis: "right", dash: "dashed" } },
                   "Combo", { labels: months, y2Title: "Margin %", y2Min: 0, y2Max: 100 });
        D.stackedBarChart({ Revenue: rev, Costs: costs }, "Stacked", { labels: months });
        D.horizontalBarChart({ Revenue: rev }, "Horizontal", { labels: months });
        D.lineChart({ Smooth: { values: rev, interpolation: "smooth", tension: 0.5 },
                      Steps: { values: costs, step: "after", dash: "dotted", pointStyle: "star", pointRadius: 6 },
                      Filled: { values: margin, fill: true, color: "#ff8800" } }, "Line styles");
        D.bubbleChart([[1, 2, 8], [2, 4, 12]], "Bubbles");
        D.radarChart({ Ada: [8, 6, 9], Bo: [5, 9, 6] }, "Radar", { labels: ["Speed", "Power", "Skill"] });
        D.polarAreaChart({ A: 3, B: 5 }, "Polar");
        D.donutChart({ Done: 70, Left: 30 }, "Gauge", { gauge: true });
        D.lineChart({ Growth: [1, 10, 100] }, "Scales", { yScale: "log", ySuggestedMin: 1, ySuggestedMax: 1000, reverseX: true });
        """;

    private async Task<(ScriptRunSession Session, List<string> Console, List<RichCellOutput> Outputs, ExternalOutputProcessor Processor)> StartRun(
        string path,
        ExternalVisualSession? session = null)
    {
        var node = TestJavaScript.Require();
        var services = TestJavaScript.Services(Path.Combine(_dir, ".studio"));
        var language = (JavaScriptLanguage)services.Registry.Get(LanguageIds.JavaScript)!;
        var plan = await language.ScriptRunner.PlanAsync(new ScriptRunContext(path, Path.GetDirectoryName(path)!, node));
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

    private ProtocolKernel Kernel()
    {
        var services = new StudioLanguageServices(Path.Combine(_dir, "services"));
        var js = (JavaScriptLanguage)services.Registry.Get(LanguageIds.JavaScript)!;
        js.JavaScriptToolchain.Select(TestJavaScript.Require().ExecutablePath);
        var kernel = (ProtocolKernel)js.NotebookKernels.Create(new KernelCreationContext(() => _dir));
        _kernels.Add(kernel);
        return kernel;
    }

    private static async Task<(KernelExecutionResult Result, List<string> Console, List<RichCellOutput> Rich)> ExecuteCell(
        ProtocolKernel kernel,
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

    [JavaScriptFact]
    public async Task Run_The18Cases_DrawAsTheFixturesDo()
    {
        var script = Write("main.js", The18Calls);
        var (session, console, outputs, processor) = await StartRun(script);
        var result = await session.Completion.WaitAsync(Patience);
        processor.Flush();

        Assert.True(result.Succeeded, string.Concat(console));
        Assert.Equal(18, outputs.Count);

        for (var i = 0; i < VisualConformanceFixturesTests.Cases.Length; i++)
        {
            var name = VisualConformanceFixturesTests.Cases[i].Name;
            var (mime, drawn) = VisualConformanceFixturesTests.Expected(name);
            var v = outputs[i].Visual!;
            Assert.Equal(mime, v.MimeType);
            Assert.Equal(drawn, VisualConformanceFixturesTests.DrawnAs(mime, VisualJson.SerializeToElement(v.Spec)));
        }
    }

    [JavaScriptFact]
    public async Task Run_TheChartFeatureCases_DrawAsTheFixturesDo()
    {
        var script = Write("features.js", TheFeatureCalls);
        var (session, console, outputs, processor) = await StartRun(script);
        var result = await session.Completion.WaitAsync(Patience);
        processor.Flush();

        Assert.True(result.Succeeded, string.Concat(console));
        Assert.Equal(VisualConformanceFixturesTests.ChartFeatureCases.Length, outputs.Count);

        for (var i = 0; i < VisualConformanceFixturesTests.ChartFeatureCases.Length; i++)
        {
            var name = VisualConformanceFixturesTests.ChartFeatureCases[i].Name;
            var (mime, drawn) = VisualConformanceFixturesTests.Expected(name);
            var v = outputs[i].Visual!;
            Assert.Equal(mime, v.MimeType);
            Assert.Equal(drawn, VisualConformanceFixturesTests.DrawnAs(mime, VisualJson.SerializeToElement(v.Spec)));
        }
    }

    [JavaScriptFact]
    public async Task ASeriesOptionItDoesNotKnow_IsAnErrorThatListsTheOnesItDoes()
    {
        var script = Write("badoption.js", """
            Display.lineChart({ a: { values: [1, 2], dashed: true } });
            """);
        var (session, console, _, processor) = await StartRun(script);
        var result = await session.Completion.WaitAsync(Patience);
        processor.Flush();

        Assert.False(result.Succeeded);
        Assert.Contains("There is no series option 'dashed'", string.Concat(console));
    }

    [JavaScriptFact]
    public async Task Run_Update_RedrawsTheSameSingleOutput()
    {
        var script = Write("update.js", """
            const h = Display.lineChart([1, 2], "A");
            h.update({ title: "B" });
            """);
        var (session, console, outputs, processor) = await StartRun(script);
        var result = await session.Completion.WaitAsync(Patience);
        processor.Flush();

        Assert.True(result.Succeeded, string.Concat(console));
        var visual = Assert.Single(outputs).Visual!;
        var spec = Assert.IsType<ChartSpec>(visual.Spec);
        Assert.Equal("B", spec.Title);
    }

    [JavaScriptFact]
    public async Task Run_Click_NotifiesProgramOverLoopback()
    {
        using var session = new ExternalVisualSession();
        var script = Write("click.js", """
            const h = Display.lineChart([1, 2, 3], "C");
            h.onClick(e => console.log("clicked " + e.target.index));
            Display.wait(20);
            """);
        var (runSession, console, outputs, _) = await StartRun(script, session);

        await WaitUntil(() => outputs.Count > 0 && outputs[0].Visual?.IsInteractive == true);
        outputs[0].Visual!.Raise(VisualEvent.Click(new VisualEventTarget { Series = 0, Index = 1 }));

        await WaitUntil(() => string.Concat(console).Contains("clicked 1"));
        runSession.Stop();
    }

    [JavaScriptFact]
    public async Task Notebook_The18Cases_DrawAsTheFixturesDo()
    {
        var kernel = Kernel();
        var (result, console, rich) = await ExecuteCell(kernel, The18Calls);

        Assert.True(result.Success, string.Concat(console));
        Assert.Equal(18, rich.Count);

        for (var i = 0; i < VisualConformanceFixturesTests.Cases.Length; i++)
        {
            var name = VisualConformanceFixturesTests.Cases[i].Name;
            var (mime, drawn) = VisualConformanceFixturesTests.Expected(name);
            var v = rich[i].Visual!;
            Assert.Equal(mime, v.MimeType);
            Assert.Equal(drawn, VisualConformanceFixturesTests.DrawnAs(mime, VisualJson.SerializeToElement(v.Spec)));
        }
    }

    [JavaScriptFact]
    public async Task Notebook_Update_RedrawsTheSameSingleOutput()
    {
        var kernel = Kernel();
        var (result, console, rich) = await ExecuteCell(kernel, """
            const h = Display.lineChart([1, 2], "A");
            h.update({ title: "B" });
            """);

        Assert.True(result.Success, string.Concat(console));
        var visual = Assert.Single(rich).Visual!;
        var spec = Assert.IsType<ChartSpec>(visual.Spec);
        Assert.Equal("B", spec.Title);
    }

    [JavaScriptFact]
    public async Task Notebook_Click_PrintsInOwningCellAfterCellEnds()
    {
        var kernel = Kernel();
        var chunks = new ConcurrentQueue<string>();
        var rich = new List<RichCellOutput>();
        var result = await kernel.ExecuteAsync(new KernelExecutionRequest
        {
            Code = """
                const h = Display.lineChart([1, 2, 3], "C");
                h.onClick(e => console.log("clicked " + e.target.index));
                """.Replace("\r\n", "\n"),
            SourceId = "cell",
            OnConsole = chunks.Enqueue,
            OnRichOutput = rich.Add,
        }, CancellationToken.None).WaitAsync(Patience);

        Assert.True(result.Success, string.Concat(chunks));
        var visual = Assert.Single(rich).Visual!;
        await WaitUntil(() => visual.IsInteractive);

        visual.Raise(VisualEvent.Click(new VisualEventTarget { Series = 0, Index = 1 }));
        await WaitUntil(() => string.Concat(chunks).Contains("clicked 1"));
    }
}
