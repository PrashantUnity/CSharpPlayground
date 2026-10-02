using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using Xunit;

namespace CSharpEditorPlugin.Tests.RealGo;

/// <summary>
/// Go programs debugged with the real Go toolchain and the real Delve (<c>dlv dap</c>). Each test returns at once where Delve isn't
/// installed, unless <c>FRY_REQUIRE_DELVE=1</c>, which makes that a failure.
/// </summary>
[Collection(RealGoCollection.Name)]
public class GoDebuggerTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromMinutes(3);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_GoDbg_" + Guid.NewGuid().ToString("N"));

    public GoDebuggerTests()
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

    private string Write(string name, string code)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, code.Replace("\r\n", "\n"));
        return path;
    }

    private GoLanguage Language => (GoLanguage)TestGo.Services(Path.Combine(_dir, ".studio")).Registry.Get(LanguageIds.Go)!;

    private DebugLaunchContext Context(string path, params int[] lines) => Context(path, null, lines);

    private DebugLaunchContext Context(string path, Action<string>? live, params int[] lines) => new(
        ScriptId: "go-debug-test",
        SourceFilePath: path,
        SourceCode: File.ReadAllText(path),
        Breakpoints: lines.Select(l => new BreakpointItem { LineNumber = l, IsEnabled = true }).ToList(),
        Toolchain: new ToolchainResolution(TestGo.Require()),
        OnLiveOutput: live,
        CancellationToken: CancellationToken.None);

    private async Task<bool> DelveInstalled(GoLanguage language)
    {
        var resolution = await language.Debugger!.ResolveDebuggerAsync(new ToolchainResolution(TestGo.Require()));
        if (resolution.IsAvailable) return true;
        if (Environment.GetEnvironmentVariable("FRY_REQUIRE_DELVE") == "1")
        {
            throw new InvalidOperationException("FRY_REQUIRE_DELVE is set but Delve (dlv) was not found.");
        }

        return false;
    }

    private const string Calc = """
        package main

        import "fmt"

        func main() {
        	a := 41
        	b := 1
        	c := a + b
        	fmt.Println(c)
        }
        """;

    [GoFact]
    public async Task TheDebugger_IsResolved_WhenDelveIsInstalled()
    {
        var language = Language;
        if (!await DelveInstalled(language)) return;

        var resolution = await language.Debugger!.ResolveDebuggerAsync(new ToolchainResolution(TestGo.Require()));

        Assert.True(resolution.IsAvailable);
        Assert.Equal("dlv", resolution.DebuggerName);
        Assert.NotNull(resolution.ExecutablePath);
        Assert.NotNull(resolution.Version);
    }

    [GoFact]
    public async Task ACompileError_ThrowsDebugCompilationException_WithTheGoDiagnostic()
    {
        var language = Language;
        var path = Write("broken.go", "package main\n\nfunc main() {\n\tvar x int = \"not a number\"\n\t_ = x\n}\n");
        using var cts = new CancellationTokenSource(Patience);

        var ex = await Assert.ThrowsAsync<DebugCompilationException>(() => language.Debugger!.LaunchAsync(Context(path, 4), cts.Token));

        Assert.Contains(ex.Diagnostics, d => d.Line == 4);
    }

    [GoFact]
    public async Task ABreakpoint_PausesTheProgram_AndItsLocalsAreShown()
    {
        var language = Language;
        if (!await DelveInstalled(language)) return;

        var path = Write("calc.go", Calc);
        using var cts = new CancellationTokenSource(Patience);
        var paused = new TaskCompletionSource<DebugPausedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var terminated = new TaskCompletionSource<DebugTerminatedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var output = new System.Text.StringBuilder();

        // Delve runs the program on its own standard output, which the studio shows as live output.
        await using var session = await language.Debugger!.LaunchAsync(Context(path, text => { lock (output) output.Append(text); }, 8), cts.Token);
        session.Paused += args => paused.TrySetResult(args);
        session.Terminated += args => terminated.TrySetResult(args);
        session.OutputReceived += text => { lock (output) output.Append(text); };

        var pausedArgs = await paused.Task.WaitAsync(Patience);

        Assert.Equal(8, pausedArgs.LineNumber);
        var locals = pausedArgs.Locals.ToDictionary(v => v.Name);
        Assert.Equal("41", locals["a"].ValueDisplay);
        Assert.Equal("1", locals["b"].ValueDisplay);

        await session.ContinueAsync(cts.Token);
        var end = await terminated.Task.WaitAsync(Patience);
        Assert.Equal(0, end.ExitCode);
        Assert.Contains("42", output.ToString());
        Assert.DoesNotContain("DAP server listening", output.ToString());
    }

    [GoFact]
    public async Task SteppingOver_MovesToTheNextLine_AndTheNewValueAppears()
    {
        var language = Language;
        if (!await DelveInstalled(language)) return;

        var path = Write("step.go", Calc);
        using var cts = new CancellationTokenSource(Patience);
        var pauses = new System.Collections.Concurrent.BlockingCollection<DebugPausedEventArgs>();
        var terminated = new TaskCompletionSource<DebugTerminatedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var session = await language.Debugger!.LaunchAsync(Context(path, 8), cts.Token);
        session.Paused += args => pauses.Add(args);
        session.Terminated += args => terminated.TrySetResult(args);

        var first = await Task.Run(() => pauses.Take(cts.Token));
        Assert.Equal(8, first.LineNumber);

        await session.StepOverAsync(cts.Token);
        var second = await Task.Run(() => pauses.Take(cts.Token));

        Assert.Equal(9, second.LineNumber);
        Assert.Equal("42", second.Locals.ToDictionary(v => v.Name)["c"].ValueDisplay);

        await session.ContinueAsync(cts.Token);
        Assert.Equal(0, (await terminated.Task.WaitAsync(Patience)).ExitCode);
    }

    [GoFact]
    public async Task AProgramThatExitsWithACode_EndsTheSessionWithThatCode()
    {
        var language = Language;
        if (!await DelveInstalled(language)) return;

        var path = Write("exits.go", "package main\n\nimport \"os\"\n\nfunc main() {\n\tos.Exit(3)\n}\n");
        using var cts = new CancellationTokenSource(Patience);
        var terminated = new TaskCompletionSource<DebugTerminatedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var session = await language.Debugger!.LaunchAsync(Context(path), cts.Token);
        session.Terminated += args => terminated.TrySetResult(args);

        var end = await terminated.Task.WaitAsync(Patience);

        Assert.Equal(3, end.ExitCode);
    }

    [GoFact]
    public async Task ABreakpointAddedWhileTheProgramIsPaused_IsHit()
    {
        var language = Language;
        if (!await DelveInstalled(language)) return;

        var path = Write("live.go", Calc);
        using var cts = new CancellationTokenSource(Patience);
        var pauses = new System.Collections.Concurrent.BlockingCollection<DebugPausedEventArgs>();
        var terminated = new TaskCompletionSource<DebugTerminatedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var session = await language.Debugger!.LaunchAsync(Context(path, 7), cts.Token);
        session.Paused += args => pauses.Add(args);
        session.Terminated += args => terminated.TrySetResult(args);
        Assert.Equal(7, (await Task.Run(() => pauses.Take(cts.Token))).LineNumber);

        await session.SetBreakpointsAsync(path,
            [new BreakpointItem { LineNumber = 7, IsEnabled = true }, new BreakpointItem { LineNumber = 9, IsEnabled = true }], cts.Token);
        await session.ContinueAsync(cts.Token);

        Assert.Equal(9, (await Task.Run(() => pauses.Take(cts.Token))).LineNumber);
        await session.ContinueAsync(cts.Token);
        Assert.Equal(0, (await terminated.Task.WaitAsync(Patience)).ExitCode);
    }
}
