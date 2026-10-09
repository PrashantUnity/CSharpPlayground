using System.Diagnostics;
using System.Text;
using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.FSharp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Interaction;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;
using Xunit;

namespace CSharpEditorPlugin.Tests.RealFSharp;

[Collection(RealFSharpCollection.Name)]
public class FSharpVisualsConformanceTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromMinutes(3);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_FSVisuals_" + Guid.NewGuid().ToString("N"));
    private readonly List<INotebookKernel> _kernels = [];

    public FSharpVisualsConformanceTests()
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

    private const string The18Declarations = "type RecordItem = { name: string; value: int }";

    private const string The18Statements = """
Display.lineChart([ Some 3.0; Some 1.0; None; Some 4.0 ], "Numbers") |> ignore
Display.scatterChart([ [ 1.0; 2.0 ]; [ 2.0; 4.5 ] ], "Pairs") |> ignore
Display.barChart([ ("Mon", 3); ("Tue", 5) ], "Days") |> ignore
Display.chart(dict [ ("a", [ 1; 2 ]); ("b", [ 3; 4 ]) ], "Series") |> ignore
Display.chart([ { name = "Jan"; value = 10 }; { name = "Feb"; value = 12 } ], "Records") |> ignore
Display.histogram([ 1; 2; 2; 3; 3; 3 ], "Samples", bins = 3) |> ignore
Display.pieChart([ ("A", 1); ("B", 2) ], "Share") |> ignore
Display.scatter3d([ (1, 2, 3); (4, 5, 6) ], "Points") |> ignore
Display.surface3d([ [ 1.0; 2.0 ]; [ 3.0; 4.0 ] ], "Heights") |> ignore
Display.graph3d(dict [ ("a", [ "b"; "c" ]); ("b", [ "c" ]) ], "Links") |> ignore
Display.matrix([ [ 1; 0 ]; [ 0; 1 ] ], "Grid") |> ignore
Display.array([ 1; 3; 5 ], dict [ ("i", 0); ("j", 2) ], "Two pointers") |> ignore
Display.tree(TreeNode(2, TreeNode(1), TreeNode(3)), "Tree") |> ignore
Display.tree([ Some 1; Some 2; Some 3; None; Some 4 ], "Level order") |> ignore
Display.graph(dict [ ("a", [ "b" ]); ("b", [ "c" ]) ], "Graph") |> ignore
Display.linkedList(ListNode(1, ListNode(2, ListNode(3))), "List") |> ignore
Display.bars([ 3; 1; 2 ], "Bars") |> ignore
Display.islands([ [ 1; 0 ]; [ 1; 1 ] ], "Islands") |> ignore
""";

    // The newer chart features (VisualConformanceFixturesTests.ChartFeatureCases), made the way F# writes them.
    private const string TheFeatureStatements = """
let months = [ "Jan"; "Feb"; "Mar" ]
let rev = [ 40; 55; 48 ]
let costs = [ 30; 35; 38 ]
let margin = [ 25; 36; 21 ]
let s (v: int list) = Display.series(v)
Display.barChart([ ("Revenue", s rev); ("Costs", s costs); ("Margin", Display.series(margin).Kind("line").RightAxis().Dash("dashed")) ], ChartStyle().Title("Combo").Labels(months).Y2Axis("Margin %", 0.0, 100.0)) |> ignore
Display.stackedBarChart([ ("Revenue", s rev); ("Costs", s costs) ], ChartStyle().Title("Stacked").Labels(months)) |> ignore
Display.horizontalBarChart([ ("Revenue", s rev) ], ChartStyle().Title("Horizontal").Labels(months)) |> ignore
Display.lineChart([ ("Smooth", Display.series(rev).Smooth(0.5)); ("Steps", Display.series(costs).Step("after").Dash("dotted").PointStyle("star", 6.0)); ("Filled", Display.series(margin).Fill(true).Color("#ff8800")) ], ChartStyle().Title("Line styles")) |> ignore
Display.bubbleChart([ (1, 2, 8); (2, 4, 12) ], ChartStyle().Title("Bubbles")) |> ignore
Display.radarChart([ ("Ada", s [ 8; 6; 9 ]); ("Bo", s [ 5; 9; 6 ]) ], ChartStyle().Title("Radar").Labels([ "Speed"; "Power"; "Skill" ])) |> ignore
Display.polarAreaChart([ ("A", 3); ("B", 5) ], ChartStyle().Title("Polar")) |> ignore
Display.donutChart([ ("Done", 70); ("Left", 30) ], ChartStyle().Title("Gauge").Gauge()) |> ignore
Display.lineChart([ ("Growth", s [ 1; 10; 100 ]) ], ChartStyle().Title("Scales").YScale("log").SuggestedY(1.0, 1000.0).ReverseX()) |> ignore
""";

    private static string BuildMainProgram(string statements) =>
        "open Fry\n\n" + statements.Trim() + "\n";

    private async Task<(ScriptRunSession Session, List<string> Console, List<RichCellOutput> Outputs, ExternalOutputProcessor Processor)> StartRun(
        string path,
        ExternalVisualSession? session = null)
    {
        var fs = TestFSharp.Require();
        var services = TestFSharp.Services(Path.Combine(_dir, ".studio"));
        var language = (FSharpLanguage)services.Registry.Get(LanguageIds.FSharp)!;
        var plan = await language.ScriptRunner!.PlanAsync(new ScriptRunContext(path, Path.GetDirectoryName(path)!, fs));
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
        TestFSharp.Require();
        var services = TestFSharp.Services(Path.Combine(_dir, ".studio"));
        var language = (FSharpLanguage)services.Registry.Get(LanguageIds.FSharp)!;
        var kernel = language.NotebookKernels!.Create(new KernelCreationContext(() => _dir));
        _kernels.Add(kernel);
        return kernel;
    }

    private static async Task<(KernelExecutionResult Result, string Console, List<RichCellOutput> Rich)> ExecuteCell(
        INotebookKernel kernel,
        string code)
    {
        var console = new StringBuilder();
        var rich = new List<RichCellOutput>();
        var result = await kernel.ExecuteAsync(new KernelExecutionRequest
        {
            Code = code.Replace("\r\n", "\n"),
            SourceId = "cell",
            OnConsole = text => { lock (console) console.Append(text); },
            OnRichOutput = rich.Add,
        }, CancellationToken.None).WaitAsync(Patience);
        return (result, console.ToString(), rich);
    }

    [FSharpFact]
    public async Task Run_The18Cases_DrawAsTheFixturesDo()
    {
        var scriptCode = BuildMainProgram(The18Declarations + "\n\n" + The18Statements);
        var script = Write("main.fsx", scriptCode);
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

    [FSharpFact]
    public async Task Run_TheChartFeatureCases_DrawAsTheFixturesDo()
    {
        var script = Write("features.fsx", BuildMainProgram(TheFeatureStatements));
        var (session, console, outputs, processor) = await StartRun(script);
        var result = await session.Completion.WaitAsync(Patience);
        processor.Flush();

        Assert.True(result.Succeeded, string.Concat(console));
        Assert.Equal(VisualConformanceFixturesTests.ChartFeatureCases.Length, outputs.Count);

        for (var i = 0; i < VisualConformanceFixturesTests.ChartFeatureCases.Length; i++)
        {
            var name = VisualConformanceFixturesTests.ChartFeatureCases[i].Name;
            var (mime, drawn) = VisualConformanceFixturesTests.Expected(name);
            var v = outputs[i].Visual;
            Assert.NotNull(v);
            Assert.Equal(mime, v.MimeType);
            Assert.Equal(drawn, VisualConformanceFixturesTests.DrawnAs(mime, VisualJson.SerializeToElement(v.Spec)));
        }
    }

    [FSharpFact]
    public async Task Run_Update_RedrawsTheSameSingleOutput()
    {
        var scriptCode = BuildMainProgram("""
let h = Display.lineChart([ 1; 2 ], "A")
h.update("B") |> ignore
Display.wait(0.2)
""");
        var script = Write("main.fsx", scriptCode);
        var (session, console, outputs, processor) = await StartRun(script);
        var result = await session.Completion.WaitAsync(Patience);
        processor.Flush();

        Assert.True(result.Succeeded, string.Concat(console));
        var visual = Assert.Single(outputs).Visual!;
        var spec = Assert.IsType<ChartSpec>(visual.Spec);
        Assert.Equal("B", spec.Title);
    }

    [FSharpFact]
    public async Task Run_Click_NotifiesProgramOverLoopback()
    {
        using var visualSession = new ExternalVisualSession();
        var scriptCode = BuildMainProgram("""
let h = Display.lineChart([ 1; 2; 3 ], "C")
h.on_click(fun (e: Event) ->
    match e.Get("index") with
    | Some idx -> printfn "clicked %s" idx
    | None -> ()
) |> ignore
Display.wait(30.0)
""");
        var script = Write("main.fsx", scriptCode);
        var (session, console, outputs, processor) = await StartRun(script, visualSession);

        await WaitUntil(() => outputs.Count == 1 && outputs[0].Visual?.IsInteractive == true);
        outputs[0].Visual!.Raise(VisualEvent.Click(new VisualEventTarget { Series = 0, Index = 1 }));

        await WaitUntil(() => string.Concat(console).Contains("clicked 1"));
        session.Stop();
    }

    [FSharpFact]
    public async Task Notebook_The18Cases_DrawAsTheFixturesDo()
    {
        var kernel = Kernel();
        var cellCode = The18Declarations + "\n" + The18Statements;
        var (result, console, rich) = await ExecuteCell(kernel, cellCode);

        Assert.True(result.Success, console + " " + result.ErrorMessage);
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

    [FSharpFact]
    public async Task Notebook_Update_RedrawsTheSameSingleOutput()
    {
        var kernel = Kernel();
        var (result, console, rich) = await ExecuteCell(kernel, """
let h = Display.lineChart([ 1; 2 ], "A")
h.update("B") |> ignore
Display.wait(0.2)
""");

        Assert.True(result.Success, console + " " + result.ErrorMessage);
        var visual = Assert.Single(rich).Visual!;

        await WaitUntil(() => ((ChartSpec)rich[0].Visual!.Spec).Title == "B");
        Assert.Equal("B", ((ChartSpec)rich[0].Visual!.Spec).Title);
    }
}
