using System.Collections.Concurrent;
using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.JavaScript;
using Xunit;

namespace CSharpEditorPlugin.Tests.RealJavaScript;

/// <summary>The notebook's JavaScript kernel with real Node.js: state, output, errors, values, variables.</summary>
[Collection(RealJavaScriptCollection.Name)]
public class JavaScriptKernelTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    private readonly string _baseDir = Path.Combine(Path.GetTempPath(), "FryPDF_JSKernel_" + Guid.NewGuid().ToString("N"));
    private readonly string _notebookFolder;
    private readonly List<INotebookKernel> _kernels = new();

    public JavaScriptKernelTests()
    {
        _notebookFolder = Path.Combine(_baseDir, "notebooks");
        Directory.CreateDirectory(_notebookFolder);
    }

    public void Dispose()
    {
        foreach (var kernel in _kernels) kernel.Dispose();
        try
        {
            if (Directory.Exists(_baseDir)) Directory.Delete(_baseDir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private ProtocolKernel Kernel()
    {
        var services = new StudioLanguageServices(Path.Combine(_baseDir, "services"));
        var js = (JavaScriptLanguage)services.Registry.Get(LanguageIds.JavaScript)!;
        js.JavaScriptToolchain.Select(TestJavaScript.Require().ExecutablePath);
        var kernel = (ProtocolKernel)js.Engine.NotebookKernels.Create(new KernelCreationContext(() => _notebookFolder));
        _kernels.Add(kernel);
        return kernel;
    }

    private sealed record Run(KernelExecutionResult Result, string Console, List<RichCellOutput> Rich, List<string> Chunks);

    private static async Task<Run> Execute(ProtocolKernel kernel, string code, CancellationToken ct = default)
    {
        var chunks = new ConcurrentQueue<string>();
        var rich = new ConcurrentQueue<RichCellOutput>();
        var result = await kernel.ExecuteAsync(new KernelExecutionRequest
        {
            Code = code.Replace("\r\n", "\n"),
            SourceId = "cell",
            OnConsole = chunks.Enqueue,
            OnRichOutput = rich.Enqueue
        }, ct).WaitAsync(Patience);
        return new Run(result, string.Concat(chunks), rich.ToList(), chunks.ToList());
    }

    [JavaScriptFact]
    public async Task LastExpression_AnObject_OpensInTheObjectExplorer_LikeACSharpValue()
    {
        var kernel = Kernel();

        var run = await Execute(kernel, """
            class People {
                constructor() {
                    this.name = 'Prashant';
                    this.numbers = [1, 34, 45, 235, 25];
                    this.another = this;
                }
                get shout() { return this.name.toUpperCase(); }
            }
            const person = new People();
            person
            """);

        Assert.True(run.Result.Success, run.Console);
        Assert.DoesNotContain("People {", run.Console);
        var node = Assert.IsType<ObjectInspectorNode>(Assert.Single(run.Rich, r => r.Kind == CellOutputKind.ObjectInspector).InspectorNode);
        Assert.Equal("People", node.HeaderTitle);
        Assert.Equal(["name", "numbers", "another", "shout"], node.Properties.Select(p => p.Name));
        Assert.Equal("Prashant", node.Properties[0].SimpleValueText);
        Assert.True(node.Properties[0].IsString);
        Assert.Equal("[ 1, 34, 45, 235, 25 ]", node.Properties[1].SimpleValueText);
        Assert.True(node.Properties[2].ChildNode!.IsCircularReference);
        Assert.Equal("PRASHANT", node.Properties[3].SimpleValueText);
        InspectorChain.AssertFiveLevels(node, "another", ["name", "numbers", "another", "shout"]);
    }

    [JavaScriptFact]
    public async Task LastExpression_ObjectsMapsAndArraysOfObjects_AreInspected_ScalarsStayText()
    {
        var kernel = Kernel();

        var plain = await Execute(kernel, "({ a: 1, b: { c: [1, 2] } })");
        var map = await Execute(kernel, "new Map([['k', { v: 1 }], ['z', 2]])"); // a Map of the notebook's own vm context
        var scalar = await Execute(kernel, "40 + 2");
        var numbers = await Execute(kernel, "[1, 2, 3]");

        var top = Assert.Single(plain.Rich).InspectorNode!;
        Assert.Equal(["a", "b"], top.Properties.Select(p => p.Name));
        Assert.Equal("c", top.Properties[1].ChildNode!.Properties.Single().Name);

        var entries = Assert.Single(map.Rich).InspectorNode!;
        Assert.Equal("Map", entries.HeaderTitle);
        Assert.Equal(["k", "z"], entries.Properties.Select(p => p.Name));

        Assert.Empty(scalar.Rich);
        Assert.Equal("42\n", scalar.Console);
        Assert.Empty(numbers.Rich);
        Assert.Equal("[ 1, 2, 3 ]\n", numbers.Console);
    }

    [JavaScriptFact]
    public async Task State_CarriesOverBetweenCells_AndTheLastExpressionIsShown()
    {
        var kernel = Kernel();

        var first = await Execute(kernel, "const x = 41;");
        var second = await Execute(kernel, "x + 1;");

        Assert.True(first.Result.Success, first.Console);
        Assert.Equal("42\n", second.Console);
        Assert.StartsWith("Node.js ", kernel.DisplayName);
    }

    [JavaScriptFact]
    public async Task OutputStreams_ConsoleLogPrints()
    {
        var kernel = Kernel();

        var run = await Execute(kernel, "console.log('Streamed from JS');");

        Assert.True(run.Result.Success);
        Assert.Equal("Streamed from JS\n", run.Console);
    }

    [JavaScriptFact]
    public async Task Values_CanBeShared_BothWays()
    {
        var kernel = Kernel();

        await kernel.SetValueFromJsonAsync("sharedVal", "{\"msg\":\"hello\"}", CancellationToken.None);
        var json = await kernel.GetValueJsonAsync("sharedVal", CancellationToken.None);

        Assert.Equal("{\"msg\":\"hello\"}", json);

        var run = await Execute(kernel, "sharedVal.msg;");
        Assert.Contains("hello", run.Console);
    }

    [JavaScriptFact]
    public async Task Variables_AreListed()
    {
        var kernel = Kernel();

        await Execute(kernel, "const count = 100; function testFn() {}");

        var variables = await kernel.GetVariablesAsync(CancellationToken.None);

        Assert.Contains(variables, v => v.Name == "count" && v.ValueDisplay.Contains("100"));
        Assert.Contains(variables, v => v.Name == "testFn" && v.Kind == "function");
    }

    [JavaScriptFact]
    public async Task AnError_IsReported()
    {
        var kernel = Kernel();

        var run = await Execute(kernel, "throw new TypeError('Invalid call in notebook cell');");

        Assert.False(run.Result.Success);
        Assert.Contains("TypeError", run.Result.ErrorMessage);
        Assert.Contains("Invalid call in notebook cell", run.Result.ErrorMessage);
    }
}
