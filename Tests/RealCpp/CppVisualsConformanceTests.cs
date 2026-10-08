using System.Diagnostics;
using System.Text;
using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Interaction;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;
using Xunit;

namespace CSharpEditorPlugin.Tests.RealCpp;

[Collection(RealCppCollection.Name)]
public class CppVisualsConformanceTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromMinutes(3);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_CppVisuals_" + Guid.NewGuid().ToString("N"));
    private readonly List<INotebookKernel> _kernels = [];

    public CppVisualsConformanceTests()
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

    private const string The18Declarations = """
        struct Record {
            std::string name;
            int value;
        };
        """;

    private const string The18Statements = """
        fry::line_chart(std::vector<std::optional<double>>{3.0, 1.0, std::nullopt, 4.0}, "Numbers");
        fry::scatter_chart(std::vector<std::vector<double>>{{1.0, 2.0}, {2.0, 4.5}}, "Pairs");
        fry::bar_chart(std::vector<std::pair<std::string, int>>{{"Mon", 3}, {"Tue", 5}}, "Days");
        fry::chart(std::map<std::string, std::vector<int>>{{"a", {1, 2}}, {"b", {3, 4}}}, "Series");
        fry::chart(std::vector<Record>{{"Jan", 10}, {"Feb", 12}}, "Records");
        fry::histogram(std::vector<int>{1, 2, 2, 3, 3, 3}, "Samples", 3);
        fry::pie_chart(std::vector<std::pair<std::string, int>>{{"A", 1}, {"B", 2}}, "Share");
        fry::scatter3d(std::vector<std::vector<int>>{{1, 2, 3}, {4, 5, 6}}, "Points");
        fry::surface3d(std::vector<std::vector<int>>{{1, 2}, {3, 4}}, "Heights");
        fry::graph3d(std::map<std::string, std::vector<std::string>>{{"a", {"b", "c"}}, {"b", {"c"}}}, "Links");
        fry::matrix(std::vector<std::vector<int>>{{1, 0}, {0, 1}}, "Grid");
        fry::array(std::vector<int>{1, 3, 5}, std::vector<std::pair<std::string, int>>{{"i", 0}, {"j", 2}}, "Two pointers");
        fry::tree(fry::make_tree(2, fry::make_tree(1), fry::make_tree(3)), "Tree");
        fry::tree(std::vector<std::optional<int>>{1, 2, 3, std::nullopt, 4}, "Level order");
        fry::graph(std::map<std::string, std::vector<std::string>>{{"a", {"b"}}, {"b", {"c"}}}, "Graph");
        fry::linked_list(fry::make_list(1, fry::make_list(2, fry::make_list(3))), "List");
        fry::bars(std::vector<int>{3, 1, 2}, "Bars");
        fry::islands(std::vector<std::vector<int>>{{1, 0}, {1, 1}}, "Islands");
        """;

    // The newer chart features (VisualConformanceFixturesTests.ChartFeatureCases), made the way C++ writes them.
    private const string TheFeatureStatements = """
        using Named = std::vector<std::pair<std::string, std::vector<int>>>;
        std::vector<std::string> months{"Jan", "Feb", "Mar"};
        std::vector<int> rev{40, 55, 48}, costs{30, 35, 38}, margin{25, 36, 21};
        fry::bar_chart(Named{{"Revenue", rev}, {"Costs", costs}, {"Margin", margin}},
            fry::ChartStyle().title("Combo").labels(months).y2_axis("Margin %", 0, 100)
                .series("Margin", fry::SeriesStyle().kind("line").right_axis().dash("dashed")));
        fry::stacked_bar_chart(Named{{"Revenue", rev}, {"Costs", costs}}, fry::ChartStyle().title("Stacked").labels(months));
        fry::horizontal_bar_chart(Named{{"Revenue", rev}}, fry::ChartStyle().title("Horizontal").labels(months));
        fry::line_chart(Named{{"Smooth", rev}, {"Steps", costs}, {"Filled", margin}}, fry::ChartStyle().title("Line styles")
            .series("Smooth", fry::SeriesStyle().smooth(0.5))
            .series("Steps", fry::SeriesStyle().step("after").dash("dotted").point_style("star", 6))
            .series("Filled", fry::SeriesStyle().fill(true).color("#ff8800")));
        fry::bubble_chart(std::vector<std::vector<double>>{{1, 2, 8}, {2, 4, 12}}, "Bubbles");
        fry::radar_chart(Named{{"Ada", {8, 6, 9}}, {"Bo", {5, 9, 6}}}, fry::ChartStyle().title("Radar").labels({"Speed", "Power", "Skill"}));
        fry::polar_area_chart(std::vector<std::pair<std::string, int>>{{"A", 3}, {"B", 5}}, "Polar");
        fry::donut_chart(std::vector<std::pair<std::string, int>>{{"Done", 70}, {"Left", 30}}, fry::ChartStyle().title("Gauge").gauge());
        fry::line_chart(Named{{"Growth", {1, 10, 100}}}, fry::ChartStyle().title("Scales").y_scale("log").suggested_y(1, 1000).reverse_x());
        """;

    private static string BuildMainProgram(string statements) => $$"""
        #include <fry/display.hpp>
        #include <vector>
        #include <string>
        #include <optional>
        #include <map>

        {{The18Declarations}}

        int main() {
            {{statements}}
            return 0;
        }
        """;

    private async Task<(ScriptRunSession Session, List<string> Console, List<RichCellOutput> Outputs, ExternalOutputProcessor Processor)> StartRun(
        string path,
        ExternalVisualSession? session = null)
    {
        var cpp = TestCpp.Require();
        var services = TestCpp.Services(Path.Combine(_dir, ".studio"));
        var language = (CppLanguage)services.Registry.Get(LanguageIds.Cpp)!;
        var plan = await language.ScriptRunner!.PlanAsync(new ScriptRunContext(path, Path.GetDirectoryName(path)!, cpp));
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
        TestCpp.Require();
        var services = TestCpp.Services(Path.Combine(_dir, ".studio"));
        var language = (CppLanguage)services.Registry.Get(LanguageIds.Cpp)!;
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

    [CppFact]
    public async Task Run_TheChartFeatureCases_DrawAsTheFixturesDo()
    {
        var script = Write("features.cpp", BuildMainProgram(TheFeatureStatements));
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

    [CppFact]
    public async Task Run_The18Cases_DrawAsTheFixturesDo()
    {
        var scriptCode = BuildMainProgram(The18Statements);
        var script = Write("main.cpp", scriptCode);
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

    [CppFact]
    public async Task Run_Update_RedrawsTheSameSingleOutput()
    {
        var scriptCode = BuildMainProgram("""
            auto h = fry::line_chart(std::vector<int>{1, 2}, "A");
            h.update("B");
            fry::wait(0.2);
        """);
        var script = Write("main.cpp", scriptCode);
        var (session, console, outputs, processor) = await StartRun(script);
        var result = await session.Completion.WaitAsync(Patience);
        processor.Flush();

        Assert.True(result.Succeeded, string.Concat(console));
        var visual = Assert.Single(outputs).Visual!;
        var spec = Assert.IsType<ChartSpec>(visual.Spec);
        Assert.Equal("B", spec.Title);
    }

    [CppFact]
    public async Task Run_Click_NotifiesProgramOverLoopback()
    {
        using var visualSession = new ExternalVisualSession();
        var scriptCode = BuildMainProgram("""
            auto h = fry::line_chart(std::vector<int>{1, 2, 3}, "C");
            h.on_click([](const fry::Event& e) {
                if (auto idx = e.get("index")) {
                    std::cout << "clicked " << *idx << std::endl;
                }
            });
            fry::wait(10.0);
        """);
        var script = Write("main.cpp", scriptCode);
        var (session, console, outputs, processor) = await StartRun(script, visualSession);

        await WaitUntil(() => outputs.Count == 1 && outputs[0].Visual?.IsInteractive == true);
        outputs[0].Visual!.Raise(VisualEvent.Click(new VisualEventTarget { Series = 0, Index = 1 }));

        await WaitUntil(() => string.Concat(console).Contains("clicked 1"));
        session.Stop();
    }

    [CppFact]
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

    [CppFact]
    public async Task Notebook_Update_RedrawsTheSameSingleOutput()
    {
        var kernel = Kernel();
        var (result, console, rich) = await ExecuteCell(kernel, """
            auto h = fry::line_chart(std::vector<int>{1, 2}, "A");
            h.update("B");
            fry::wait(0.2);
            """);

        Assert.True(result.Success, console + " " + result.ErrorMessage);
        var visual = Assert.Single(rich).Visual!;

        await WaitUntil(() => ((ChartSpec)rich[0].Visual!.Spec).Title == "B");
        Assert.Equal("B", ((ChartSpec)rich[0].Visual!.Spec).Title);
    }
}
