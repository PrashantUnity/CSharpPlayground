using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>
/// Regression coverage for the bug where a script/cell that synchronously blocks on a Task (the
/// natural-looking way to call e.g. HttpClient without async/await) deadlocked the whole app, and
/// for the "abandon a stuck execution" recovery path that replaces it.
/// </summary>
public class ExecutionDeadlockAndAbandonmentTests : IDisposable
{
    private readonly string _testBaseDir;
    private readonly LocalScriptStorageService _testStorage;

    public ExecutionDeadlockAndAbandonmentTests()
    {
        _testBaseDir = Path.Combine(Path.GetTempPath(), "FryPDF_DeadlockTests_" + Guid.NewGuid().ToString("N"));
        _testStorage = new LocalScriptStorageService(_testBaseDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testBaseDir))
            {
                Directory.Delete(_testBaseDir, recursive: true);
            }
        }
        catch { }
    }

    private CSharpCodeStudioViewModel CreateStudio(ScriptDocumentItem initialScript, Func<int>? getTimeoutSeconds = null)
    {
        return new CSharpCodeStudioViewModel(
            initialScript,
            _testStorage,
            new RoslynCompilerService(),
            new ScriptExecutionEngine(),
            backToHubAction: () => { },
            backToHomeAction: () => { },
            getTimeoutSeconds: getTimeoutSeconds);
    }

    // ── ExecutionAbandonment.WaitWithGraceAsync — fast, isolated unit tests ──

    [Fact]
    public async Task WaitWithGraceAsync_TaskCompletesBeforeCancellation_ReturnsTrue()
    {
        using var cts = new CancellationTokenSource();
        var task = Task.Delay(10);

        var completed = await ExecutionAbandonment.WaitWithGraceAsync(task, cts.Token);

        Assert.True(completed);
    }

    [Fact]
    public async Task WaitWithGraceAsync_CancelledButTaskFinishesWithinGracePeriod_ReturnsTrue()
    {
        using var cts = new CancellationTokenSource();
        var task = Task.Delay(100); // well within the 1s GracePeriod even under load
        cts.CancelAfter(10);

        var completed = await ExecutionAbandonment.WaitWithGraceAsync(task, cts.Token);

        Assert.True(completed);
    }

    [Fact]
    public async Task WaitWithGraceAsync_TaskNeverFinishes_GivesUpAfterGracePeriodInsteadOfHangingForever()
    {
        using var cts = new CancellationTokenSource();
        var neverCompletes = new TaskCompletionSource<bool>().Task;
        cts.CancelAfter(10);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var completed = await ExecutionAbandonment.WaitWithGraceAsync(neverCompletes, cts.Token);
        sw.Stop();

        Assert.False(completed);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3), $"Should give up shortly after the grace period, took {sw.Elapsed}");
    }

    // ── Deadlock regression: a script that blocks synchronously must not freeze a UI-like thread ──

    [Fact]
    public async Task RunCodeCommand_ScriptBlocksSynchronouslyOnATask_DoesNotDeadlockUiThread()
    {
        // This is the exact shape of bug users hit with `httpClient.GetStringAsync(url).Result`:
        // a non-async top-level statement that blocks on a Task. Before the fix, running this
        // inline on a captured UI SynchronizationContext deadlocked forever.
        var script = await _testStorage.CreateNewScriptAsync("Blocking Script");
        script.Code = "System.Threading.Tasks.Task.Delay(150).Wait();\nConsole.WriteLine(\"done blocking\");";
        await _testStorage.SaveScriptAsync(script);

        var studio = CreateStudio(script);

        var pump = new SingleThreadSynchronizationContext();
        var completed = new TaskCompletionSource<bool>();

        var pumpThread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(pump);
            pump.Post(async _ =>
            {
                try
                {
                    await studio.RunCodeCommand.ExecuteAsync(null);
                    completed.SetResult(true);
                }
                catch (Exception ex)
                {
                    completed.SetException(ex);
                }
                finally
                {
                    pump.Complete();
                }
            }, null);
            pump.RunOnCurrentThread();
        })
        { IsBackground = true };
        pumpThread.Start();

        var finished = await Task.WhenAny(completed.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.True(ReferenceEquals(finished, completed.Task),
            "RunCodeCommand deadlocked on a UI-like single-threaded SynchronizationContext.");
        await completed.Task;

        Assert.Contains("done blocking", studio.ConsoleOutput);
    }

    [Fact]
    public async Task RunSingleCellAsync_CellBlocksSynchronouslyOnATask_DoesNotDeadlockUiThread()
    {
        var tab = new NotebookTabViewModel(new NotebookDocumentItem { Title = "Deadlock Test" });
        var cell = tab.Cells[0];
        cell.Source = "System.Threading.Tasks.Task.Delay(150).Wait();\nConsole.WriteLine(\"done blocking\");";

        var pump = new SingleThreadSynchronizationContext();
        var completed = new TaskCompletionSource<bool>();

        var pumpThread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(pump);
            pump.Post(async _ =>
            {
                try
                {
                    await tab.RunSingleCellAsync(cell);
                    completed.SetResult(true);
                }
                catch (Exception ex)
                {
                    completed.SetException(ex);
                }
                finally
                {
                    pump.Complete();
                }
            }, null);
            pump.RunOnCurrentThread();
        })
        { IsBackground = true };
        pumpThread.Start();

        var finished = await Task.WhenAny(completed.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.True(ReferenceEquals(finished, completed.Task),
            "RunSingleCellAsync deadlocked on a UI-like single-threaded SynchronizationContext.");
        await completed.Task;

        Assert.Contains("done blocking", cell.OutputText);
    }

    [Fact]
    public async Task RunCodeCommand_DefaultTimeoutSetting_RunsPastTenSecondsWithoutBeingAutoCancelled()
    {
        // The execution timeout defaults to 0 (no automatic limit) — a script/notebook cell now runs
        // until Stop is clicked, matching Jupyter, instead of being silently cut off after a fixed
        // ceiling. Regression guard for that specific default: intentionally runs past the *old*
        // hardcoded 10s default to prove nothing auto-cancels it anymore.
        var script = await _testStorage.CreateNewScriptAsync("Slow But Fine Script");
        script.Code = "System.Threading.Tasks.Task.Delay(10500).Wait();\nConsole.WriteLine(\"FROM_SLOW_BUT_FINE\");";
        await _testStorage.SaveScriptAsync(script);

        // No getTimeoutSeconds passed — exercises the constructor's default fallback.
        var studio = CreateStudio(script);

        await studio.RunCodeCommand.ExecuteAsync(null);

        Assert.Contains("FROM_SLOW_BUT_FINE", studio.ConsoleOutput);
        Assert.DoesNotContain("Timed out", studio.CompilerStatusText, StringComparison.OrdinalIgnoreCase);
    }

    // ── Abandon-and-recover: Stop/timeout must give up promptly, and the kernel must stay usable ──

    [Fact]
    public async Task RunCodeCommand_TrulyStuckScript_AbandonsPromptlyAndKernelRecoversForNextRun()
    {
        var script = await _testStorage.CreateNewScriptAsync("Stuck Script");
        // Blocks far longer than timeout (1s) + grace period (1s) — must be abandoned, not awaited.
        script.Code = "System.Threading.Tasks.Task.Delay(4000).Wait();\nConsole.WriteLine(\"FROM_STUCK_LATE\");";
        await _testStorage.SaveScriptAsync(script);

        var studio = CreateStudio(script, getTimeoutSeconds: () => 1);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await studio.RunCodeCommand.ExecuteAsync(null);
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3),
            $"Should abandon ~1s (timeout) + ~1s (grace) in, took {sw.Elapsed}");
        Assert.Contains("Timed out", studio.CompilerStatusText, StringComparison.OrdinalIgnoreCase);

        // A second, fast run on the same kernel must succeed promptly — proving HardReset() freed
        // the execution lock the abandoned run never released.
        var second = await _testStorage.CreateNewScriptAsync("Fast Script");
        second.Code = "Console.WriteLine(\"FROM_B\");";
        await _testStorage.SaveScriptAsync(second);
        await studio.UpdateActiveScriptAsync(second);

        var secondSw = System.Diagnostics.Stopwatch.StartNew();
        await studio.RunCodeCommand.ExecuteAsync(null);
        secondSw.Stop();

        Assert.True(secondSw.Elapsed < TimeSpan.FromSeconds(2),
            $"Second run should complete quickly if the kernel lock was freed, took {secondSw.Elapsed}");
        Assert.Contains("FROM_B", studio.ConsoleOutput);

        // Give the first (abandoned) run's background thread time to actually reach its late
        // statement, well past the point where the second run already took over.
        await Task.Delay(TimeSpan.FromSeconds(3.5));

        Assert.DoesNotContain("FROM_STUCK_LATE", studio.ConsoleOutput);
    }

    private sealed class SingleThreadSynchronizationContext : SynchronizationContext
    {
        private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();

        // A background continuation from the awaited Task.Run (e.g. a trailing continuation still
        // unwinding after this test's own outcome is already decided) can race with Complete() and
        // try to post afterward — harmless at that point, so drop it instead of crashing the process.
        public override void Post(SendOrPostCallback d, object? state)
        {
            try
            {
                _queue.Add((d, state));
            }
            catch (InvalidOperationException)
            {
            }
        }

        public override void Send(SendOrPostCallback d, object? state) => d(state);

        public void RunOnCurrentThread()
        {
            foreach (var workItem in _queue.GetConsumingEnumerable())
            {
                workItem.Callback(workItem.State);
            }
        }

        public void Complete() => _queue.CompleteAdding();
    }
}
