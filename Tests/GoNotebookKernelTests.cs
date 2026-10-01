using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class GoNotebookKernelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FakeHostEnvironment _host;
    private readonly GoToolchainProvider _toolchain;

    public GoNotebookKernelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FryPDF_GoNotebookTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _host = new FakeHostEnvironment(FakeOs.MacOS);
        _host.AddGo("/opt/homebrew/bin/go", "1.22.4");
        _toolchain = new GoToolchainProvider(_host, new FakeProcessLauncher(), new ToolchainSettingsStore(Path.Combine(_tempDir, "tc.json")), _tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task SharedVariables_SetAndGetRoundTrip()
    {
        var context = new KernelCreationContext(() => _tempDir, () => null);
        using var kernel = new GoNotebookKernel(_toolchain, new FakeProcessLauncher(), _host, context);

        await kernel.SetValueFromJsonAsync("myInt", "42", CancellationToken.None);
        await kernel.SetValueFromJsonAsync("myStr", "\"hello go\"", CancellationToken.None);
        await kernel.SetValueFromJsonAsync("myBool", "true", CancellationToken.None);

        var intVal = await kernel.GetValueJsonAsync("myInt", CancellationToken.None);
        var strVal = await kernel.GetValueJsonAsync("myStr", CancellationToken.None);
        var boolVal = await kernel.GetValueJsonAsync("myBool", CancellationToken.None);

        Assert.Equal("42", intVal);
        Assert.Equal("\"hello go\"", strVal);
        Assert.Equal("true", boolVal);

        var vars = await kernel.GetVariablesAsync(CancellationToken.None);
        Assert.Equal(3, vars.Count);
        Assert.Contains(vars, v => v.Name == "myInt" && v.TypeName == "int64");
        Assert.Contains(vars, v => v.Name == "myStr" && v.TypeName == "string");
        Assert.Contains(vars, v => v.Name == "myBool" && v.TypeName == "bool");
    }

    [Fact]
    public async Task MissingVariable_ThrowsKernelValueException()
    {
        var context = new KernelCreationContext(() => _tempDir, () => null);
        using var kernel = new GoNotebookKernel(_toolchain, new FakeProcessLauncher(), _host, context);

        await Assert.ThrowsAsync<KernelValueException>(async () =>
        {
            await kernel.GetValueJsonAsync("nonExistent", CancellationToken.None);
        });
    }

    [Fact]
    public async Task HardReset_ClearsVariablesAndState()
    {
        var context = new KernelCreationContext(() => _tempDir, () => null);
        using var kernel = new GoNotebookKernel(_toolchain, new FakeProcessLauncher(), _host, context);

        await kernel.SetValueFromJsonAsync("count", "10", CancellationToken.None);
        Assert.True(kernel.IsSessionActive);

        kernel.HardReset();
        Assert.False(kernel.IsSessionActive);
    }

    private const string Go = "/opt/homebrew/bin/go";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(45);

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Patience;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition never became true.");
            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task StoppingACellWhileGoBuilds_KillsTheBuild_AndReportsACancellation()
    {
        FakeProcess? build = null;
        var launcher = new FakeProcessLauncher
        {
            Behavior = async (spec, process) =>
            {
                build = process;
                await Task.Delay(Timeout.Infinite, process.KilledToken);
            }
        };
        using var kernel = new GoNotebookKernel(_toolchain, launcher, _host, new KernelCreationContext(() => _tempDir, () => null));
        using var cts = new CancellationTokenSource();

        var running = kernel.ExecuteAsync(new KernelExecutionRequest { Code = "fmt.Println(1)" }, cts.Token);
        await WaitUntil(() => build != null);
        cts.Cancel();
        var result = await running.WaitAsync(Patience);

        Assert.True(result.WasCancelled);
        Assert.True(build!.WasKilled);
    }

    [Fact]
    public async Task StoppingACellWhileTheProgramRuns_KillsTheProgram_AndReportsACancellation()
    {
        FakeProcess? program = null;
        var launcher = new FakeProcessLauncher
        {
            Behavior = async (spec, process) =>
            {
                if (spec.FileName == Go)
                {
                    process.Exit(0);
                    return;
                }

                program = process;
                await Task.Delay(Timeout.Infinite, process.KilledToken);
            }
        };
        using var kernel = new GoNotebookKernel(_toolchain, launcher, _host, new KernelCreationContext(() => _tempDir, () => null));
        using var cts = new CancellationTokenSource();

        var running = kernel.ExecuteAsync(new KernelExecutionRequest { Code = "for {}" }, cts.Token);
        await WaitUntil(() => program != null);
        cts.Cancel();
        var result = await running.WaitAsync(Patience);

        Assert.True(result.WasCancelled);
        Assert.True(program!.WasKilled);
    }

    [Fact]
    public async Task ACellThatRunsForALongTime_IsNotEndedByALimitOfTheKernelsOwn()
    {
        // The studio's own setting (ExecutionTimeoutSeconds, unlimited unless changed) decides how long a cell may run, through the
        // cancellation token; a limit fixed inside the kernel would end a long computation with an error nobody asked for.
        var finish = new TaskCompletionSource();
        var launcher = new FakeProcessLauncher
        {
            Behavior = async (spec, process) =>
            {
                if (spec.FileName != Go) await finish.Task;
                process.Exit(0);
            }
        };
        using var kernel = new GoNotebookKernel(_toolchain, launcher, _host, new KernelCreationContext(() => _tempDir, () => null));

        var running = kernel.ExecuteAsync(new KernelExecutionRequest { Code = "fmt.Println(1)" }, CancellationToken.None);
        await Task.Delay(200);
        Assert.False(running.IsCompleted);
        finish.SetResult();

        Assert.True((await running.WaitAsync(Patience)).Success);
    }
}
