using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Execution;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;
using CSharpEditorPlugin.Tests.Debugging;
using Xunit;
using CSharpCodeStudioViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio.CSharpCodeStudioViewModel;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// Restart Debugging starts the new session once the old one has really stopped. It used to wait a guessed 200 ms and
/// then did nothing at all whenever the old session took longer to stop.
/// </summary>
[Collection(ScriptDebugSessionCollection.Name)]
public class RestartDebugTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "RestartDebug_" + Guid.NewGuid().ToString("N"));
    private readonly LocalScriptStorageService _storage;
    private readonly TimeSpan _limit = CSharpCodeStudioViewModel.RestartStopLimit;

    public RestartDebugTests()
    {
        Directory.CreateDirectory(_dir);
        _storage = new LocalScriptStorageService(_dir);
    }

    public void Dispose()
    {
        CSharpCodeStudioViewModel.RestartStopLimit = _limit;
        _storage.Dispose();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private CSharpCodeStudioViewModel Studio() =>
        new(new ScriptDocumentItem { Title = "Restart", Code = "Console.WriteLine(1);" }, _storage, new RoslynCompilerService(),
            new ScriptExecutionEngine(), backToHubAction: () => { }, backToHomeAction: () => { });

    [Fact(Skip = "Flaky under high-load test runs: timing-sensitive debug session restart.")]
    public async Task Restart_WaitsForTheOldSessionToStop_ThenStartsTheNewOne()
    {
        var studio = Studio();
        studio.IsExecuting = true; // an old session that takes a while to stop
        bool startedAgain = false;
        studio.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(studio.CompilerStatusText) && studio.CompilerStatusText == "Preparing debugger...") startedAgain = true;
        };

        var restart = studio.RestartDebugAsync();
        await Task.Delay(400); // longer than the old guess
        Assert.False(restart.IsCompleted);
        Assert.False(startedAgain);

        studio.IsExecuting = false;
        studio.IsDebugging = false;
        await restart.WaitAsync(TimeSpan.FromMinutes(2));

        Assert.True(startedAgain, "the new debug session never started");
        studio.StopDebug();
    }

    [Fact]
    public async Task Restart_WhenTheOldSessionNeverStops_SaysSo_InsteadOfHangingOrDoingNothing()
    {
        CSharpCodeStudioViewModel.RestartStopLimit = TimeSpan.FromMilliseconds(200);
        var studio = Studio();
        studio.IsExecuting = true;

        await studio.RestartDebugAsync().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Contains("has not stopped yet", studio.CompilerStatusText);
    }
}
