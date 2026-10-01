using System.Collections.Concurrent;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.RealJava;

/// <summary>The notebook's Java kernel with real JDK: state, output, errors, values, variables.</summary>
[Collection(RealJavaCollection.Name)]
public class JavaKernelTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    private readonly string _baseDir = Path.Combine(Path.GetTempPath(), "FryPDF_JavaKernel_" + Guid.NewGuid().ToString("N"));
    private readonly string _notebookFolder;
    private readonly List<INotebookKernel> _kernels = new();

    public JavaKernelTests()
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
        var java = (JavaLanguage)services.Registry.Get(LanguageIds.Java)!;
        java.JavaToolchain.Select(TestJava.Require().ExecutablePath);
        var kernel = (ProtocolKernel)java.NotebookKernels.Create(new KernelCreationContext(() => _notebookFolder));
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

    [JavaFact]
    public async Task State_CarriesOverBetweenCells_AndTheLastExpressionIsShown()
    {
        var kernel = Kernel();

        var first = await Execute(kernel, "int x = 41;");
        var second = await Execute(kernel, "x + 1;");

        Assert.True(first.Result.Success, first.Console);
        Assert.Equal("42\n", second.Console);
        Assert.StartsWith("Java", kernel.DisplayName);
    }

    [JavaFact]
    public async Task OutputStreams_SystemOutPrints()
    {
        var kernel = Kernel();

        var run = await Execute(kernel, "System.out.println(\"Streamed from Java\");");

        Assert.True(run.Result.Success, run.Console + " Err: " + run.Result.ErrorMessage);
        Assert.Equal("Streamed from Java\n", run.Console.Replace("\r\n", "\n"));
    }

    [JavaFact]
    public async Task Values_CanBeShared_BothWays()
    {
        var kernel = Kernel();

        await kernel.SetValueFromJsonAsync("scores", "[88,95,72,91,84]", CancellationToken.None);
        var json = await kernel.GetValueJsonAsync("scores", CancellationToken.None);

        Assert.Equal("[88,95,72,91,84]", json);

        var run = await Execute(kernel, "scores[1];");
        Assert.Contains("95", run.Console);
    }

    [JavaFact]
    public async Task Variables_AreReported()
    {
        var kernel = Kernel();

        await Execute(kernel, "int age = 30;\nString city = \"Zurich\";");
        var vars = await kernel.GetVariablesAsync(CancellationToken.None);

        Assert.Contains(vars, v => v.Name == "age" && v.TypeName == "int" && v.ValueDisplay == "30");
        Assert.Contains(vars, v => v.Name == "city" && v.TypeName == "String");
    }
}
