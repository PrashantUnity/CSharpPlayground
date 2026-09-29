using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.RealPython;

/// <summary>Python scripts debugged with the real interpreter and debugpy; skipped where debugpy isn't installed.</summary>
[Collection(RealPythonCollection.Name)]
public class PythonDebuggerTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(45);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_PyDbg_" + Guid.NewGuid().ToString("N"));

    public PythonDebuggerTests()
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

    [PythonFact]
    public async Task ABreakpoint_PausesTheScript_AndItsLocalsAreShown()
    {
        var python = TestPython.Require();
        var services = TestPython.Services(Path.Combine(_dir, ".studio"));
        var language = services.Registry.Get(LanguageIds.Python)!;
        var resolution = await language.Debugger!.ResolveDebuggerAsync(new ToolchainResolution(python));
        if (!resolution.IsAvailable) return;

        var path = Path.Combine(_dir, "calc.py");
        File.WriteAllText(path, "a = 41\nb = 1\nc = a + b\nprint(c)\n");
        using var cts = new CancellationTokenSource(Patience);
        var paused = new TaskCompletionSource<DebugPausedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var terminated = new TaskCompletionSource<DebugTerminatedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var session = await language.Debugger.LaunchAsync(new DebugLaunchContext(
            "py-debug-test", path, File.ReadAllText(path),
            [new BreakpointItem { LineNumber = 3, IsEnabled = true }],
            new ToolchainResolution(python), null, cts.Token), cts.Token);
        session.Paused += args => paused.TrySetResult(args);
        session.Terminated += args => terminated.TrySetResult(args);

        var pausedArgs = await paused.Task.WaitAsync(Patience);
        Assert.Equal(3, pausedArgs.LineNumber);
        var locals = pausedArgs.Locals.ToDictionary(v => v.Name);
        Assert.Equal("41", locals["a"].ValueDisplay);
        Assert.Equal("1", locals["b"].ValueDisplay);

        await session.ContinueAsync(cts.Token);
        var end = await terminated.Task.WaitAsync(Patience);
        Assert.Equal(0, end.ExitCode);
    }
}
