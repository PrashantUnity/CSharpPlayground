using System.Collections.Concurrent;
using System.Diagnostics;
using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Python;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Interaction;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;
using Xunit;

namespace CSharpEditorPlugin.Tests.RealPython;

[Collection(RealPythonCollection.Name)]
public class PythonVisualsConformanceTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_PythonVisuals_" + Guid.NewGuid().ToString("N"));
    private readonly List<INotebookKernel> _kernels = [];

    public PythonVisualsConformanceTests()
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
        from fry import Display as D
        class T:
            def __init__(s, val, left=None, right=None): s.val, s.left, s.right = val, left, right
        class L:
            def __init__(s, val, next=None): s.val, s.next = val, next
        D.line_chart([3,1,None,4], "Numbers")
        D.scatter_chart([[1,2],[2,4.5]], "Pairs")
        D.bar_chart({"Mon":3,"Tue":5}, "Days")
        D.chart({"a":[1,2],"b":[3,4]}, "Series")
        D.chart([{"name":"Jan","value":10},{"name":"Feb","value":12}], "Records")
        D.histogram([1,2,2,3,3,3], "Samples", bins=3)
        D.pie_chart({"A":1,"B":2}, "Share")
        D.scatter3d([(1,2,3),(4,5,6)], "Points")
        D.surface3d([[1,2],[3,4]], "Heights")
        D.graph3d({"a":["b","c"],"b":["c"]}, "Links")
        D.matrix([[1,0],[0,1]], "Grid")
        D.array([1,3,5], {"i":0,"j":2}, "Two pointers")
        D.tree(T(2, T(1), T(3)), "Tree")
        D.tree([1,2,3,None,4], "Level order")
        D.graph({"a":["b"],"b":["c"]}, "Graph")
        D.linked_list(L(1, L(2, L(3))), "List")
        D.bars([3,1,2], "Bars")
        D.islands([[1,0],[1,1]], "Islands")
        """;

    private async Task<(ScriptRunSession Session, List<string> Console, List<RichCellOutput> Outputs, ExternalOutputProcessor Processor)> StartRun(
        string path,
        ExternalVisualSession? session = null)
    {
        var python = TestPython.Require();
        var services = TestPython.Services(Path.Combine(_dir, ".studio"));
        var language = (PythonLanguage)services.Registry.Get(LanguageIds.Python)!;
        var plan = await language.ScriptRunner.PlanAsync(new ScriptRunContext(path, Path.GetDirectoryName(path)!, python));
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
        var python = (PythonLanguage)services.Registry.Get(LanguageIds.Python)!;
        python.PythonToolchain.Select(TestPython.Require().ExecutablePath);
        var kernel = (ProtocolKernel)python.NotebookKernels.Create(new KernelCreationContext(() => _dir));
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

    [PythonFact]
    public async Task Run_The18Cases_DrawAsTheFixturesDo()
    {
        var script = Write("main.py", The18Calls);
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

    [PythonFact]
    public async Task Run_Update_RedrawsTheSameSingleOutput()
    {
        var script = Write("update.py", """
            from fry import Display as D
            h = D.line_chart([1, 2], "A")
            h.update(title="B")
            """);
        var (session, console, outputs, processor) = await StartRun(script);
        var result = await session.Completion.WaitAsync(Patience);
        processor.Flush();

        Assert.True(result.Succeeded, string.Concat(console));
        var visual = Assert.Single(outputs).Visual!;
        var spec = Assert.IsType<ChartSpec>(visual.Spec);
        Assert.Equal("B", spec.Title);
    }

    [PythonFact]
    public async Task Run_Click_NotifiesProgramOverLoopback()
    {
        using var session = new ExternalVisualSession();
        var script = Write("click.py", """
            from fry import Display as D
            h = D.line_chart([1, 2, 3], "C")
            h.on_click(lambda e: print("clicked", e.target["index"]))
            D.wait(10)
            """);
        var (runSession, console, outputs, _) = await StartRun(script, session);

        await WaitUntil(() => outputs.Count > 0 && outputs[0].Visual?.IsInteractive == true);
        outputs[0].Visual!.Raise(VisualEvent.Click(new VisualEventTarget { Series = 0, Index = 1 }));

        await WaitUntil(() => string.Concat(console).Contains("clicked 1"));
        runSession.Stop();
    }

    [PythonFact]
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

    [PythonFact]
    public async Task Notebook_Update_RedrawsTheSameSingleOutput()
    {
        var kernel = Kernel();
        var (result, console, rich) = await ExecuteCell(kernel, """
            from fry import Display as D
            h = D.line_chart([1, 2], "A")
            h.update(title="B")
            """);

        Assert.True(result.Success, string.Concat(console));
        var visual = Assert.Single(rich).Visual!;
        var spec = Assert.IsType<ChartSpec>(visual.Spec);
        Assert.Equal("B", spec.Title);
    }

    [PythonFact]
    public async Task Notebook_Click_PrintsInOwningCellAfterCellEnds()
    {
        var kernel = Kernel();
        var chunks = new ConcurrentQueue<string>();
        var rich = new List<RichCellOutput>();
        var result = await kernel.ExecuteAsync(new KernelExecutionRequest
        {
            Code = """
                from fry import Display as D
                h = D.line_chart([1, 2, 3], "C")
                h.on_click(lambda e: print("clicked", e.target["index"]))
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
