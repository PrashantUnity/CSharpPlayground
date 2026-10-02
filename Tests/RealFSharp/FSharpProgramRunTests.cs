using System.Collections.Concurrent;
using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.FSharp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using Xunit;

namespace CSharpEditorPlugin.Tests.RealFSharp;

/// <summary>.fsx files executed with real dotnet fsi / fsi: interactive runner and notebook kernel.</summary>
[Collection(RealFSharpCollection.Name)]
public class FSharpProgramRunTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(90);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_FSRun_" + Guid.NewGuid().ToString("N"));

    public FSharpProgramRunTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
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

    private async Task<(ScriptRunSession Session, ConcurrentQueue<string> Output)> Start(string path)
    {
        var fs = TestFSharp.Require();
        var services = TestFSharp.Services(Path.Combine(_dir, ".studio"));
        var language = (FSharpLanguage)services.Registry.Get(LanguageIds.FSharp)!;
        var plan = await language.ScriptRunner.PlanAsync(new ScriptRunContext(path, Path.GetDirectoryName(path)!, fs));
        var output = new ConcurrentQueue<string>();
        var session = new ScriptRunExecutor(services.Processes).Start(plan, path, language.RunDiagnostics, output.Enqueue);
        return (session, output);
    }

    [FSharpFact]
    public void TheInstalledFSharp_IsFound()
    {
        var fs = TestFSharp.Require();

        Assert.True(fs.Version >= FSharpToolchainProvider.MinimumVersion, fs.Label);
        Assert.True(File.Exists(fs.ExecutablePath), fs.ExecutablePath);
    }

    [FSharpFact]
    public async Task AFile_RunsAndPrints()
    {
        var path = Write("hello.fsx", """
            open System

            printfn "Hello from F# run test!"

            let numbers = [ 1 .. 5 ]
            let total = numbers |> List.sum
            printfn "TOTAL=%d" total
            """);

        var (session, output) = await Start(path);

        var result = await session.Completion.WaitAsync(Patience);

        Assert.Equal(0, result.ExitCode);
        var joined = string.Concat(output);
        Assert.Contains("Hello from F# run test!", joined);
        Assert.Contains("TOTAL=15", joined);
    }

    [FSharpFact]
    public async Task CompilationError_IsParsedIntoDiagnostics()
    {
        var path = Write("broken.fsx", """
            open System

            let x : int = "not an integer"
            printfn "%d" x
            """);

        var (session, output) = await Start(path);

        var result = await session.Completion.WaitAsync(Patience);

        Assert.NotEqual(0, result.ExitCode);
        Assert.NotEmpty(result.Diagnostics.Diagnostics);
        Assert.Contains(result.Diagnostics.Diagnostics, d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    [FSharpFact]
    public async Task NotebookKernel_ExecutesCellAndAccumulatesDefinitions()
    {
        var fs = TestFSharp.Require();
        var services = TestFSharp.Services(Path.Combine(_dir, ".studio"));
        var language = (FSharpLanguage)services.Registry.Get(LanguageIds.FSharp)!;

        var context = new KernelCreationContext(() => _dir, () => null);
        using var kernel = (FSharpNotebookKernel)language.NotebookKernels.Create(context);

        var consoleOut = new List<string>();
        var req1 = new KernelExecutionRequest
        {
            Code = """
                let add a b = a + b
                let res1 = add 10 20
                printfn "RESULT_1=%d" res1
                """,
            OnConsole = s => consoleOut.Add(s)
        };

        var res = await kernel.ExecuteAsync(req1, CancellationToken.None);

        Assert.True(res.Success, res.ErrorMessage ?? res.ConsoleOutput);
        Assert.Contains("RESULT_1=30", string.Join("", consoleOut));
    }
}
