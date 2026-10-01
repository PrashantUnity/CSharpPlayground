using System.Collections.Concurrent;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.JavaScript;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.RealJavaScript;

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
        var kernel = (ProtocolKernel)js.NotebookKernels.Create(new KernelCreationContext(() => _notebookFolder));
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
