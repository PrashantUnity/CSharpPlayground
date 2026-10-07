using System.Diagnostics;
using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Interaction;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;
using Xunit;

namespace CSharpEditorPlugin.Tests.RealJava;

[Collection(RealJavaCollection.Name)]
public class JavaVisualsConformanceTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_JavaVisuals_" + Guid.NewGuid().ToString("N"));
    private readonly List<INotebookKernel> _kernels = [];

    public JavaVisualsConformanceTests()
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

    private const string The18Statements = """
        class T { int val; T left, right; T(int val) { this.val = val; } T(int val, T left, T right) { this.val = val; this.left = left; this.right = right; } }
        class L { int val; L next; L(int val) { this.val = val; } L(int val, L next) { this.val = val; this.next = next; } }
        Display.lineChart(Arrays.asList(3, 1, null, 4), "Numbers");
        Display.scatterChart(List.of(List.of(1, 2), List.of(2, 4.5)), "Pairs");
        Display.barChart(Map.of("Mon", 3, "Tue", 5), "Days");
        Display.chart(Map.of("a", List.of(1, 2), "b", List.of(3, 4)), "Series");
        Display.chart(List.of(Map.of("name", "Jan", "value", 10), Map.of("name", "Feb", "value", 12)), "Records");
        Display.histogram(List.of(1, 2, 2, 3, 3, 3), "Samples", 3);
        Display.pieChart(Map.of("A", 1, "B", 2), "Share");
        Display.scatter3d(List.of(List.of(1, 2, 3), List.of(4, 5, 6)), "Points");
        Display.surface3d(List.of(List.of(1, 2), List.of(3, 4)), "Heights");
        Display.graph3d(Map.of("a", List.of("b", "c"), "b", List.of("c")), "Links");
        Display.matrix(List.of(List.of(1, 0), List.of(0, 1)), "Grid");
        Display.array(List.of(1, 3, 5), Map.of("i", 0, "j", 2), "Two pointers");
        Display.tree(new T(2, new T(1), new T(3)), "Tree");
        Display.tree(Arrays.asList(1, 2, 3, null, 4), "Level order");
        Display.graph(Map.of("a", List.of("b"), "b", List.of("c")), "Graph");
        Display.linkedList(new L(1, new L(2, new L(3))), "List");
        Display.bars(List.of(3, 1, 2), "Bars");
        Display.islands(List.of(List.of(1, 0), List.of(1, 1)), "Islands");
        """;

    private const string The18Program = $$"""
        import java.util.*;

        public class Main {
            {{The18Statements}}

            public static void main(String[] args) {
                // statements are evaluated via method or inside main
            }
        }
        """;

    private static string BuildMainProgram(string statements) => $$"""
        import java.util.*;

        public class Main {
            static class T { int val; T left, right; T(int val) { this.val = val; } T(int val, T left, T right) { this.val = val; this.left = left; this.right = right; } }
            static class L { int val; L next; L(int val) { this.val = val; } L(int val, L next) { this.val = val; this.next = next; } }

            public static void main(String[] args) {
                {{statements}}
            }
        }
        """;

    private async Task<(ScriptRunSession Session, List<string> Console, List<RichCellOutput> Outputs, ExternalOutputProcessor Processor)> StartRun(
        string path,
        ExternalVisualSession? session = null)
    {
        var java = TestJava.Require();
        var services = TestJava.Services(Path.Combine(_dir, ".studio"));
        var language = (JavaLanguage)services.Registry.Get(LanguageIds.Java)!;
        var plan = await language.ScriptRunner.PlanAsync(new ScriptRunContext(path, Path.GetDirectoryName(path)!, java));
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
        var java = (JavaLanguage)services.Registry.Get(LanguageIds.Java)!;
        java.JavaToolchain.Select(TestJava.Require().ExecutablePath);
        var kernel = (ProtocolKernel)java.NotebookKernels.Create(new KernelCreationContext(() => _dir));
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

    [JavaFact]
    public async Task Run_The18Cases_DrawAsTheFixturesDo()
    {
        var scriptCode = BuildMainProgram("""
            Display.lineChart(Arrays.asList(3, 1, null, 4), "Numbers");
            Display.scatterChart(List.of(List.of(1, 2), List.of(2, 4.5)), "Pairs");
            Display.barChart(Display.map("Mon", 3, "Tue", 5), "Days");
            Display.chart(Display.map("a", List.of(1, 2), "b", List.of(3, 4)), "Series");
            Display.chart(List.of(Map.of("name", "Jan", "value", 10), Map.of("name", "Feb", "value", 12)), "Records");
            Display.histogram(List.of(1, 2, 2, 3, 3, 3), "Samples", 3);
            Display.pieChart(Display.map("A", 1, "B", 2), "Share");
            Display.scatter3d(List.of(List.of(1, 2, 3), List.of(4, 5, 6)), "Points");
            Display.surface3d(List.of(List.of(1, 2), List.of(3, 4)), "Heights");
            Display.graph3d(Display.map("a", List.of("b", "c"), "b", List.of("c")), "Links");
            Display.matrix(List.of(List.of(1, 0), List.of(0, 1)), "Grid");
            Display.array(List.of(1, 3, 5), Display.map("i", 0, "j", 2), "Two pointers");
            Display.tree(new T(2, new T(1), new T(3)), "Tree");
            Display.tree(Arrays.asList(1, 2, 3, null, 4), "Level order");
            Display.graph(Display.map("a", List.of("b"), "b", List.of("c")), "Graph");
            Display.linkedList(new L(1, new L(2, new L(3))), "List");
            Display.bars(List.of(3, 1, 2), "Bars");
            Display.islands(List.of(List.of(1, 0), List.of(1, 1)), "Islands");
        """);
        var script = Write("Main.java", scriptCode);
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

    [JavaFact]
    public async Task Run_TheChartFeatureCases_DrawAsTheFixturesDo()
    {
        // The newer chart features (VisualConformanceFixturesTests.ChartFeatureCases), made the way Java writes them.
        var scriptCode = BuildMainProgram("""
            List<Integer> rev = List.of(40, 55, 48), costs = List.of(30, 35, 38), margin = List.of(25, 36, 21);
            Display.barChart(Display.map("Revenue", rev, "Costs", costs, "Margin", Display.series(margin).kind("line").rightAxis().dash("dashed")),
                Display.options().title("Combo").labels("Jan", "Feb", "Mar").y2Axis("Margin %", 0, 100));
            Display.stackedBarChart(Display.map("Revenue", rev, "Costs", costs), Display.options().title("Stacked").labels("Jan", "Feb", "Mar"));
            Display.horizontalBarChart(Display.map("Revenue", rev), Display.options().title("Horizontal").labels("Jan", "Feb", "Mar"));
            Display.lineChart(Display.map("Smooth", Display.series(rev).smooth(0.5),
                "Steps", Display.series(costs).step("after").dash("dotted").pointStyle("star", 6),
                "Filled", Display.series(margin).fill(true).color("#ff8800")), "Line styles");
            Display.bubbleChart(List.of(List.of(1, 2, 8), List.of(2, 4, 12)), "Bubbles");
            Display.radarChart(Display.map("Ada", List.of(8, 6, 9), "Bo", List.of(5, 9, 6)), Display.options().title("Radar").labels("Speed", "Power", "Skill"));
            Display.polarAreaChart(Display.map("A", 3, "B", 5), "Polar");
            Display.donutChart(Display.map("Done", 70, "Left", 30), Display.options().title("Gauge").gauge());
            Display.lineChart(Display.map("Growth", List.of(1, 10, 100)), Display.options().title("Scales").yScale("log").suggestedY(1, 1000).reverseX());
        """);
        var script = Write("Main.java", scriptCode);
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

    [JavaFact]
    public async Task Run_AnUpdate_DrawsTheNewTitle()
    {
        var scriptCode = BuildMainProgram("""
            var h = Display.lineChart(List.of(1, 2), "A");
            h.update("B");
            Display.wait(0.2);
        """);
        var script = Write("Main.java", scriptCode);
        var (session, console, outputs, processor) = await StartRun(script);
        var result = await session.Completion.WaitAsync(Patience);
        processor.Flush();

        Assert.True(result.Succeeded, string.Concat(console));
        var visual = Assert.Single(outputs).Visual!;
        var spec = Assert.IsType<ChartSpec>(visual.Spec);
        Assert.Equal("B", spec.Title);
    }

    [JavaFact]
    public async Task Run_AClick_TriggersTheListener()
    {
        using var visualSession = new ExternalVisualSession();
        var scriptCode = BuildMainProgram("""
            var h = Display.lineChart(List.of(1, 2, 3), "C");
            h.onClick(e -> {
                var target = (java.util.Map<?, ?>) e.get("target");
                System.out.println("clicked " + target.get("index"));
            });
            Display.wait(20);
        """);
        var script = Write("Main.java", scriptCode);
        var (session, console, outputs, processor) = await StartRun(script, visualSession);

        await WaitUntil(() => outputs.Count == 1 && outputs[0].Visual?.IsInteractive == true);
        outputs[0].Visual!.Raise(VisualEvent.Click(new VisualEventTarget { Series = 0, Index = 1 }));

        await WaitUntil(() => console.Any(c => c.Contains("clicked 1")));
        session.Stop();
    }

    [JavaFact]
    public async Task Notebook_The18Cases_DrawAsTheFixturesDo()
    {
        var kernel = Kernel();
        var code = """
            class T { int val; T left, right; T(int val) { this.val = val; } T(int val, T left, T right) { this.val = val; this.left = left; this.right = right; } }
            class L { int val; L next; L(int val) { this.val = val; } L(int val, L next) { this.val = val; this.next = next; } }
            Display.lineChart(Arrays.asList(3, 1, null, 4), "Numbers");
            Display.scatterChart(List.of(List.of(1, 2), List.of(2, 4.5)), "Pairs");
            Display.barChart(Display.map("Mon", 3, "Tue", 5), "Days");
            Display.chart(Display.map("a", List.of(1, 2), "b", List.of(3, 4)), "Series");
            Display.chart(List.of(Map.of("name", "Jan", "value", 10), Map.of("name", "Feb", "value", 12)), "Records");
            Display.histogram(List.of(1, 2, 2, 3, 3, 3), "Samples", 3);
            Display.pieChart(Display.map("A", 1, "B", 2), "Share");
            Display.scatter3d(List.of(List.of(1, 2, 3), List.of(4, 5, 6)), "Points");
            Display.surface3d(List.of(List.of(1, 2), List.of(3, 4)), "Heights");
            Display.graph3d(Display.map("a", List.of("b", "c"), "b", List.of("c")), "Links");
            Display.matrix(List.of(List.of(1, 0), List.of(0, 1)), "Grid");
            Display.array(List.of(1, 3, 5), Display.map("i", 0, "j", 2), "Two pointers");
            Display.tree(new T(2, new T(1), new T(3)), "Tree");
            Display.tree(Arrays.asList(1, 2, 3, null, 4), "Level order");
            Display.graph(Display.map("a", List.of("b"), "b", List.of("c")), "Graph");
            Display.linkedList(new L(1, new L(2, new L(3))), "List");
            Display.bars(List.of(3, 1, 2), "Bars");
            Display.islands(List.of(List.of(1, 0), List.of(1, 1)), "Islands");
            """;
        var (result, console, rich) = await ExecuteCell(kernel, code);

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

    [JavaFact]
    public async Task Notebook_AnUpdate_DrawsTheNewTitle()
    {
        var kernel = Kernel();
        var (result, console, rich) = await ExecuteCell(kernel, """
            var h = Display.lineChart(List.of(1, 2), "A");
            h.update("B");
            """);

        Assert.True(result.Success, string.Concat(console) + " " + result.ErrorMessage);
        var visual = Assert.Single(rich).Visual!;

        await WaitUntil(() => ((ChartSpec)rich[0].Visual!.Spec).Title == "B");
        Assert.Equal("B", ((ChartSpec)rich[0].Visual!.Spec).Title);
    }

    [JavaFact]
    public async Task Notebook_AClick_OnEndedCell_PrintsInThatCell()
    {
        var kernel = Kernel();
        var console = new List<string>();
        var rich = new List<RichCellOutput>();
        var result = await kernel.ExecuteAsync(new KernelExecutionRequest
        {
            Code = """
                var h = Display.lineChart(List.of(1, 2, 3), "C");
                h.onClick(e -> {
                    var target = (java.util.Map<?, ?>) e.get("target");
                    System.out.println("clicked " + target.get("index"));
                });
                """.Replace("\r\n", "\n"),
            SourceId = "cell-1",
            OnConsole = console.Add,
            OnRichOutput = rich.Add,
        }, CancellationToken.None).WaitAsync(Patience);

        Assert.True(result.Success, string.Concat(console) + " " + result.ErrorMessage);
        Assert.Single(rich);
        var visual = rich[0].Visual;
        Assert.NotNull(visual);

        visual.Raise(VisualEvent.Click(new VisualEventTarget { Series = 0, Index = 1 }));

        await WaitUntil(() => console.Any(c => c.Contains("clicked 1")));
    }
}
