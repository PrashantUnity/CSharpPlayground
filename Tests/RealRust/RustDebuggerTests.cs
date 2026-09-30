using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.RealRust;

/// <summary>Rust programs debugged with the real toolchain and the real lldb-dap; each test is skipped where lldb-dap isn't installed.</summary>
[Collection(RealRustCollection.Name)]
public class RustDebuggerTests : IClassFixture<RustStudioFixture>, IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromMinutes(3);
    private readonly RustStudioFixture _studio;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_RustDbg_" + Guid.NewGuid().ToString("N"));

    public RustDebuggerTests(RustStudioFixture studio)
    {
        _studio = studio;
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

    private RustLanguage Language => (RustLanguage)_studio.Services.Registry.Get(LanguageIds.Rust)!;

    private DebugLaunchContext Context(string path, params int[] lines) => new(
        ScriptId: "rust-debug-test",
        SourceFilePath: path,
        SourceCode: File.ReadAllText(path),
        Breakpoints: lines.Select(l => new BreakpointItem { LineNumber = l, IsEnabled = true }).ToList(),
        Toolchain: new ToolchainResolution(TestRust.Require()),
        OnLiveOutput: null,
        CancellationToken: CancellationToken.None);

    private async Task<bool> DebuggerInstalled() =>
        (await Language.Debugger!.ResolveDebuggerAsync(new ToolchainResolution(TestRust.Require()))).IsAvailable;

    [RustFact]
    public async Task TheDebugger_IsResolved_OrTheGuidanceSaysWhatToInstall()
    {
        var resolution = await Language.Debugger!.ResolveDebuggerAsync(new ToolchainResolution(TestRust.Require()));

        if (resolution.IsAvailable)
        {
            Assert.NotNull(resolution.ExecutablePath);
            Assert.Contains("lldb", resolution.DebuggerName, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            Assert.Contains("lldb-dap", resolution.MissingGuidance!.Summary);
            Assert.Contains("Install it with:", resolution.MissingGuidance.Summary);
        }
    }

    [RustFact]
    public async Task ACompileError_ThrowsDebugCompilationException_WithTheRustDiagnostic()
    {
        var path = Write("broken.rs", "fn main() {\n    let x: i32 = \"not a number\";\n}\n");
        using var cts = new CancellationTokenSource(Patience);

        var ex = await Assert.ThrowsAsync<DebugCompilationException>(() => Language.Debugger!.LaunchAsync(Context(path, 2), cts.Token));

        var diagnostic = Assert.Single(ex.Diagnostics);
        Assert.Equal("E0308", diagnostic.Id);
        Assert.Equal(2, diagnostic.Line);
    }

    [RustFact]
    public async Task ABreakpoint_PausesTheProgram_AndTheLocalsArePrettyPrinted()
    {
        if (!await DebuggerInstalled()) return;

        var path = Write("locals.rs", """
            fn main() {
                let numbers = vec![1, 2, 3];
                let name = String::from("Ferris");
                let total: i32 = numbers.iter().sum();
                println!("{} {}", name, total);
            }
            """);
        using var cts = new CancellationTokenSource(Patience);
        var paused = new TaskCompletionSource<DebugPausedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var terminated = new TaskCompletionSource<DebugTerminatedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var session = await Language.Debugger!.LaunchAsync(Context(path, 5), cts.Token);
        session.Paused += args => paused.TrySetResult(args);
        session.Terminated += args => terminated.TrySetResult(args);

        // A breakpoint hit while the session was starting is told to this subscriber too.
        var pausedArgs = await paused.Task.WaitAsync(Patience);

        Assert.Equal(5, pausedArgs.LineNumber);
        var locals = pausedArgs.Locals.ToDictionary(v => v.Name);
        Assert.Equal("size=3", locals["numbers"].ValueDisplay);            // Vec, printed by Rust's own pretty-printer
        Assert.Contains("Ferris", locals["name"].ValueDisplay);           // String, not a pointer and a length
        Assert.Equal("6", locals["total"].ValueDisplay);

        // println! spreads one source line over several places, so the breakpoint is hit at each of them: keep going.
        session.Paused += args => { _ = session.ContinueAsync(cts.Token); };
        await session.ContinueAsync(cts.Token);
        var end = await terminated.Task.WaitAsync(Patience);
        Assert.Equal(0, end.ExitCode);
    }

    [RustFact]
    public async Task AProgramThatPanics_EndsWithItsExitCode_NotZero()
    {
        if (!await DebuggerInstalled()) return;

        var path = Write("panics.rs", "fn main() {\n    let v: Vec<i32> = Vec::new();\n    println!(\"{}\", v[3]);\n}\n");
        using var cts = new CancellationTokenSource(Patience);
        var terminated = new TaskCompletionSource<DebugTerminatedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var session = await Language.Debugger!.LaunchAsync(Context(path), cts.Token);
        session.Terminated += args => terminated.TrySetResult(args);

        // A program that panics at once may have ended before this subscribes; the session tells a late subscriber.
        var end = await terminated.Task.WaitAsync(Patience);

        Assert.Equal(101, end.ExitCode);
        Assert.Equal(DebugSessionState.Terminated, session.State);
    }

    [RustFact]
    public async Task SteppingOver_MovesToTheNextLine()
    {
        if (!await DebuggerInstalled()) return;

        var path = Write("steps.rs", """
            fn main() {
                let a = 1;
                let b = 2;
                let c = a + b;
                println!("{}", c);
            }
            """);
        using var cts = new CancellationTokenSource(Patience);
        var pauses = new System.Collections.Concurrent.BlockingCollection<DebugPausedEventArgs>();

        await using var session = await Language.Debugger!.LaunchAsync(Context(path, 3), cts.Token);
        session.Paused += args => pauses.Add(args);

        var first = await Task.Run(() => pauses.Take(cts.Token));
        Assert.Equal(3, first.LineNumber);

        await session.StepOverAsync(cts.Token);
        var second = await Task.Run(() => pauses.Take(cts.Token));

        Assert.Equal(4, second.LineNumber);
        Assert.Equal("2", second.Locals.Single(v => v.Name == "b").ValueDisplay);
    }
}
