using System.Diagnostics;
using System.Text;
using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Interaction;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;
using Xunit;

namespace CSharpEditorPlugin.Tests.RealRust;

[Collection(RealRustCollection.Name)]
public class RustVisualsConformanceTests : IClassFixture<RustStudioFixture>, IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromMinutes(3);
    private readonly RustStudioFixture _studio;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_RustVisuals_" + Guid.NewGuid().ToString("N"));
    private readonly List<INotebookKernel> _kernels = [];

    public RustVisualsConformanceTests(RustStudioFixture studio)
    {
        _studio = studio;
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
        #[derive(Debug)]
        struct Record {
            name: &'static str,
            value: i32,
        }

        fry::line_chart(&vec![Some(3.0), Some(1.0), None, Some(4.0)]).title("Numbers").show();
        fry::scatter_chart(&vec![vec![1.0, 2.0], vec![2.0, 4.5]]).title("Pairs").show();
        fry::bar_chart(&vec![("Mon", 3), ("Tue", 5)]).title("Days").show();
        fry::chart(&vec![("a", vec![1, 2]), ("b", vec![3, 4])]).title("Series").show();
        fry::chart(&vec![Record { name: "Jan", value: 10 }, Record { name: "Feb", value: 12 }]).title("Records").show();
        fry::histogram(&vec![1, 2, 2, 3, 3, 3]).title("Samples").bins(3).show();
        fry::pie_chart(&vec![("A", 1), ("B", 2)]).title("Share").show();
        fry::scatter3d(&vec![(1, 2, 3), (4, 5, 6)]).title("Points").show();
        fry::surface3d(&vec![vec![1, 2], vec![3, 4]]).title("Heights").show();
        fry::graph3d(&vec![("a", vec!["b", "c"]), ("b", vec!["c"])]).title("Links").show();
        fry::matrix(&vec![vec![1, 0], vec![0, 1]]).title("Grid").show();
        fry::array(&vec![1, 3, 5], &vec![("i", 0), ("j", 2)]).title("Two pointers").show();
        fry::tree(&fry::TreeNode::with_children(2, fry::TreeNode::new(1), fry::TreeNode::new(3))).title("Tree").show();
        fry::tree(&vec![Some(1), Some(2), Some(3), None, Some(4)]).title("Level order").show();
        fry::graph(&vec![("a", vec!["b"]), ("b", vec!["c"])]).title("Graph").show();
        fry::linked_list(&fry::ListNode::with_next(1, fry::ListNode::with_next(2, fry::ListNode::new(3)))).title("List").show();
        fry::bars(&vec![3, 1, 2]).title("Bars").show();
        fry::islands(&vec![vec![1, 0], vec![1, 1]]).title("Islands").show();
        """;

    // The newer chart features (VisualConformanceFixturesTests.ChartFeatureCases), made the way Rust writes them.
    private const string TheFeatureStatements = """
        let months = ["Jan", "Feb", "Mar"];
        let rev = vec![40, 55, 48];
        let costs = vec![30, 35, 38];
        let margin = vec![25, 36, 21];
        fry::bar_chart(&vec![("Revenue", rev.clone()), ("Costs", costs.clone()), ("Margin", margin.clone())])
            .series("Margin", fry::SeriesStyle::new().kind("line").right_axis().dash("dashed"))
            .labels(&months).y2_axis("Margin %", 0.0, 100.0).title("Combo").show();
        fry::bar_chart(&vec![("Revenue", rev.clone()), ("Costs", costs.clone())]).stacked().labels(&months).title("Stacked").show();
        fry::horizontal_bar_chart(&vec![("Revenue", rev.clone())]).labels(&months).title("Horizontal").show();
        fry::line_chart(&vec![("Smooth", rev.clone()), ("Steps", costs.clone()), ("Filled", margin.clone())])
            .series("Smooth", fry::SeriesStyle::new().smooth(0.5))
            .series("Steps", fry::SeriesStyle::new().step("after").dash("dotted").point_style("star", 6.0))
            .series("Filled", fry::SeriesStyle::new().fill(true).color("#ff8800"))
            .title("Line styles").show();
        fry::bubble_chart(&vec![(1.0, 2.0, 8.0), (2.0, 4.0, 12.0)]).title("Bubbles").show();
        fry::radar_chart(&vec![("Ada", vec![8, 6, 9]), ("Bo", vec![5, 9, 6])]).labels(&["Speed", "Power", "Skill"]).title("Radar").show();
        fry::polar_area_chart(&vec![("A", 3), ("B", 5)]).title("Polar").show();
        fry::donut_chart(&vec![("Done", 70), ("Left", 30)]).gauge().title("Gauge").show();
        fry::line_chart(&vec![("Growth", vec![1, 10, 100])]).y_scale("log").suggested_y(1.0, 1000.0).reverse_x().title("Scales").show();
        """;

    private static string BuildMainProgram(string statements) => $$"""
        fn main() {
            {{statements}}
        }
        """;

    private async Task<(ScriptRunSession Session, List<string> Console, List<RichCellOutput> Outputs, ExternalOutputProcessor Processor)> StartRun(
        string path,
        ExternalVisualSession? session = null)
    {
        var rust = TestRust.Require();
        var language = (RustLanguage)_studio.Services.Registry.Get(LanguageIds.Rust)!;
        var plan = await language.ScriptRunner.PlanAsync(new ScriptRunContext(path, Path.GetDirectoryName(path)!, rust));
        var console = new List<string>();
        var outputs = new List<RichCellOutput>();
        var processor = new ExternalOutputProcessor(console.Add, outputs.Add, visuals: session?.Visuals);
        var runSession = new ScriptRunExecutor(_studio.Services.Processes).Start(
            plan,
            path,
            language.RunDiagnostics,
            processor.ProcessChunk,
            environment: session?.Environment);
        return (runSession, console, outputs, processor);
    }

    private INotebookKernel Kernel()
    {
        TestRust.Require();
        var language = (RustLanguage)_studio.Services.Registry.Get(LanguageIds.Rust)!;
        var kernel = language.NotebookKernels.Create(new KernelCreationContext(() => _dir));
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

    [RustFact]
    public async Task Run_TheChartFeatureCases_DrawAsTheFixturesDo()
    {
        var script = Write("features.rs", BuildMainProgram(TheFeatureStatements));
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

    [RustFact]
    public async Task Run_The18Cases_DrawAsTheFixturesDo()
    {
        var scriptCode = BuildMainProgram(The18Statements);
        var script = Write("main.rs", scriptCode);
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

    [RustFact]
    public async Task Run_Update_RedrawsTheSameSingleOutput()
    {
        var scriptCode = BuildMainProgram("""
            let h = fry::line_chart(&vec![1, 2]).title("A").show();
            h.update("B");
            fry::wait(0.2);
        """);
        var script = Write("main.rs", scriptCode);
        var (session, console, outputs, processor) = await StartRun(script);
        var result = await session.Completion.WaitAsync(Patience);
        processor.Flush();

        Assert.True(result.Succeeded, string.Concat(console));
        var visual = Assert.Single(outputs).Visual!;
        var spec = Assert.IsType<ChartSpec>(visual.Spec);
        Assert.Equal("B", spec.Title);
    }

    [RustFact]
    public async Task Run_Click_NotifiesProgramOverLoopback()
    {
        using var visualSession = new ExternalVisualSession();
        var scriptCode = BuildMainProgram("""
            let h = fry::line_chart(&vec![1, 2, 3]).title("C").show();
            h.on_click(|e| {
                if let Some(idx) = e.target.get("index") {
                    println!("clicked {}", idx);
                }
            });
            fry::wait(10.0);
        """);
        var script = Write("main.rs", scriptCode);
        var (session, console, outputs, processor) = await StartRun(script, visualSession);

        await WaitUntil(() => outputs.Count == 1 && outputs[0].Visual?.IsInteractive == true);
        outputs[0].Visual!.Raise(VisualEvent.Click(new VisualEventTarget { Series = 0, Index = 1 }));

        await WaitUntil(() => string.Concat(console).Contains("clicked 1"));
        session.Stop();
    }

    [RustFact]
    public async Task Notebook_The18Cases_DrawAsTheFixturesDo()
    {
        var kernel = Kernel();
        var (result, console, rich) = await ExecuteCell(kernel, The18Statements);

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

    [RustFact]
    public async Task Notebook_Update_RedrawsTheSameSingleOutput()
    {
        var kernel = Kernel();
        var (result, console, rich) = await ExecuteCell(kernel, """
            let h = fry::line_chart(&vec![1, 2]).title("A").show();
            h.update("B");
            fry::wait(0.2);
            """);

        Assert.True(result.Success, console + " " + result.ErrorMessage);
        var visual = Assert.Single(rich).Visual!;

        await WaitUntil(() => ((ChartSpec)rich[0].Visual!.Spec).Title == "B");
        Assert.Equal("B", ((ChartSpec)rich[0].Visual!.Spec).Title);
    }
}
