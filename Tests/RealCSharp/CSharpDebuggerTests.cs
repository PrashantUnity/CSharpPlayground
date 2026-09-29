using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging.Dap;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.RealCSharp;

/// <summary>
/// C# scripts debugged with the real netcoredbg. Each test returns at once where netcoredbg isn't installed, unless
/// <c>FRY_REQUIRE_NETCOREDBG=1</c>, which makes that a failure.
/// </summary>
[Collection(RealCSharpCollection.Name)]
public class CSharpDebuggerTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromMinutes(2);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_CsDbg_" + Guid.NewGuid().ToString("N"));

    public CSharpDebuggerTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private CSharpDebuggerProvider Provider()
    {
        var compiler = new RoslynCompilerService();
        var engine = new ScriptExecutionEngine();
        return new CSharpDebuggerProvider(compiler, new ScriptDebuggerService(compiler, engine), engine, new ProcessLauncher(), new HostEnvironment(),
            cacheDirectory: Path.Combine(_dir, "build"));
    }

    private static async Task<bool> NetCoreDbgInstalled(CSharpDebuggerProvider provider)
    {
        var resolution = await provider.ResolveDebuggerAsync(null);
        if (resolution.ExecutablePath != null) return true;
        if (Environment.GetEnvironmentVariable("FRY_REQUIRE_NETCOREDBG") == "1")
        {
            throw new InvalidOperationException("FRY_REQUIRE_NETCOREDBG is set but netcoredbg was not found.");
        }

        return false;
    }

    // The document is called "Notes.cs" but the debug build records its source as script.cs.
    private static DebugLaunchContext Context(string code, Action<string>? live = null, params int[] lines) => new(
        ScriptId: "cs-debug-test",
        SourceFilePath: "Notes.cs",
        SourceCode: code,
        Breakpoints: lines.Select(l => new BreakpointItem { LineNumber = l, IsEnabled = true }).ToList(),
        Toolchain: null,
        OnLiveOutput: live,
        CancellationToken: CancellationToken.None);

    private const string Calc = "var a = 41;\nvar b = 1;\nvar c = a + b;\nConsole.WriteLine(c);\n";

    [Fact]
    public async Task TheDebugger_IsResolved_WhenNetCoreDbgIsInstalled()
    {
        var provider = Provider();
        if (!await NetCoreDbgInstalled(provider)) return;

        var resolution = await provider.ResolveDebuggerAsync(null);

        Assert.Equal("CoreCLR (netcoredbg)", resolution.DebuggerName);
        Assert.True(File.Exists(resolution.ExecutablePath), resolution.ExecutablePath);
    }

    [Fact]
    public async Task ABreakpoint_PausesTheScript_AndItsLocalsAreShown()
    {
        var provider = Provider();
        if (!await NetCoreDbgInstalled(provider)) return;
        using var cts = new CancellationTokenSource(Patience);
        var paused = new TaskCompletionSource<DebugPausedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var terminated = new TaskCompletionSource<DebugTerminatedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var output = new System.Text.StringBuilder();

        await using var session = await provider.LaunchAsync(Context(Calc, text => { lock (output) output.Append(text); }, 3), cts.Token);
        session.Paused += args => paused.TrySetResult(args);
        session.Terminated += args => terminated.TrySetResult(args);
        session.OutputReceived += text => { lock (output) output.Append(text); };

        // The real debugger, not the in-process fallback that a failed launch would quietly have given.
        Assert.IsType<DapDebugSession>(session);

        var pausedArgs = await paused.Task.WaitAsync(Patience);
        Assert.Equal(3, pausedArgs.LineNumber);
        var locals = pausedArgs.Locals.ToDictionary(v => v.Name);
        Assert.Equal("41", locals["a"].ValueDisplay);
        Assert.Equal("1", locals["b"].ValueDisplay);

        await session.ContinueAsync(cts.Token);
        var end = await terminated.Task.WaitAsync(Patience);
        Assert.Equal(0, end.ExitCode);
        Assert.Contains("42", output.ToString());
    }

    [Fact]
    public async Task SteppingOver_MovesToTheNextLine_AndTheNewValueAppears()
    {
        var provider = Provider();
        if (!await NetCoreDbgInstalled(provider)) return;
        using var cts = new CancellationTokenSource(Patience);
        var pauses = new System.Collections.Concurrent.BlockingCollection<DebugPausedEventArgs>();
        var terminated = new TaskCompletionSource<DebugTerminatedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var session = await provider.LaunchAsync(Context(Calc, null, 3), cts.Token);
        session.Paused += args => pauses.Add(args);
        session.Terminated += args => terminated.TrySetResult(args);
        Assert.IsType<DapDebugSession>(session);

        Assert.Equal(3, (await Task.Run(() => pauses.Take(cts.Token))).LineNumber);
        await session.StepOverAsync(cts.Token);
        var second = await Task.Run(() => pauses.Take(cts.Token));

        Assert.Equal(4, second.LineNumber);
        Assert.Equal("42", second.Locals.ToDictionary(v => v.Name)["c"].ValueDisplay);

        await session.ContinueAsync(cts.Token);
        Assert.Equal(0, (await terminated.Task.WaitAsync(Patience)).ExitCode);
    }

    [Fact]
    public async Task ABreakpointAddedWhileTheScriptIsPaused_IsHit()
    {
        var provider = Provider();
        if (!await NetCoreDbgInstalled(provider)) return;
        using var cts = new CancellationTokenSource(Patience);
        var pauses = new System.Collections.Concurrent.BlockingCollection<DebugPausedEventArgs>();
        var terminated = new TaskCompletionSource<DebugTerminatedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var session = await provider.LaunchAsync(Context(Calc, null, 2), cts.Token);
        session.Paused += args => pauses.Add(args);
        session.Terminated += args => terminated.TrySetResult(args);
        Assert.Equal(2, (await Task.Run(() => pauses.Take(cts.Token))).LineNumber);

        // The editor calls the document "Notes.cs"; the debugger must still be told the file it knows.
        await session.SetBreakpointsAsync("Notes.cs",
            [new BreakpointItem { LineNumber = 2, IsEnabled = true }, new BreakpointItem { LineNumber = 4, IsEnabled = true }], cts.Token);
        await session.ContinueAsync(cts.Token);

        Assert.Equal(4, (await Task.Run(() => pauses.Take(cts.Token))).LineNumber);
        await session.ContinueAsync(cts.Token);
        Assert.Equal(0, (await terminated.Task.WaitAsync(Patience)).ExitCode);
    }

    [Fact]
    public async Task AScriptThatIsNeverPaused_RunsToTheEnd_AndItsOutputIsShown()
    {
        var provider = Provider();
        if (!await NetCoreDbgInstalled(provider)) return;
        using var cts = new CancellationTokenSource(Patience);
        var terminated = new TaskCompletionSource<DebugTerminatedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var output = new System.Text.StringBuilder();

        // A breakpoint on a line that never runs binds to nothing and must not stop the program.
        await using var session = await provider.LaunchAsync(Context(Calc, text => { lock (output) output.Append(text); }, 99), cts.Token);
        session.Terminated += args => terminated.TrySetResult(args);
        session.OutputReceived += text => { lock (output) output.Append(text); };
        Assert.IsType<DapDebugSession>(session);

        // (netcoredbg reports exit code 0 whatever the program's: 3.2.0-1 was seen to for Environment.Exit(3), so codes aren't asserted.)
        var end = await terminated.Task.WaitAsync(Patience);
        Assert.False(end.WasCancelled);
        Assert.Contains("42", output.ToString());
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RealCSharpCollection
{
    public const string Name = "RealCSharp";
}
