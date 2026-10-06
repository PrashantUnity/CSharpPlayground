using PdfEditorApp.Plugins.CSharpEditor.Services.Activities;
using PdfEditorApp.Plugins.CSharpEditor.Services.Startup;
using Xunit;

namespace CSharpEditorPlugin.Tests.Activities;

public class EngineReadinessTests
{
    [Fact]
    public async Task CoreIsReady_BeforeTheWarmUpFinishes_AndBothAreReportedAsActivities()
    {
        var activities = new ActivityService();
        var titles = new List<string>();
        activities.Changed += () =>
        {
            lock (titles) titles.AddRange(activities.Snapshot().Select(a => a.Title));
        };
        var warmGate = new TaskCompletionSource();
        var engine = new EngineReadiness(activities, () => Task.CompletedTask, () => warmGate.Task);

        Assert.Equal(EngineState.Starting, engine.State);
        await engine.Start().WaitAsync(TimeSpan.FromSeconds(10));

        // Opening a document can go ahead while the first compilation still runs.
        Assert.False(engine.WarmReady.IsCompleted);
        Assert.Equal(EngineState.Starting, engine.State);
        Assert.StartsWith("Starting", engine.StatusText);

        warmGate.SetResult();
        await engine.WarmReady.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(EngineState.Ready, engine.State);
        Assert.StartsWith("Ready", engine.StatusText);
        Assert.Contains("Starting C# engine", titles);
        Assert.Contains("Warming up C# engine", titles);
        Assert.Empty(activities.Snapshot());
    }

    [Fact]
    public async Task AFailedCore_IsReported_AndStartedAgainByTheNextOpen()
    {
        var activities = new ActivityService();
        var failures = new List<string>();
        activities.Failed += (s, ex) => failures.Add($"{s.Title}: {ex.Message}");
        int attempts = 0;
        var engine = new EngineReadiness(activities, () =>
        {
            if (Interlocked.Increment(ref attempts) == 1) throw new FileNotFoundException("System.Runtime.dll");
            return Task.CompletedTask;
        }, () => Task.CompletedTask);

        await Assert.ThrowsAsync<FileNotFoundException>(() => engine.Start());
        Assert.Equal(EngineState.Failed, engine.State);
        Assert.Contains("System.Runtime.dll", engine.StatusText);
        Assert.Equal(new[] { "Starting C# engine: System.Runtime.dll" }, failures);

        await engine.WhenCoreReadyAsync().WaitAsync(TimeSpan.FromSeconds(10));
        await engine.WarmReady.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(2, attempts);
        Assert.Equal(EngineState.Ready, engine.State);
    }

    [Fact]
    public async Task AFailedWarmUp_DoesNotRebuildTheCore()
    {
        var activities = new ActivityService();
        int coreRuns = 0;
        var engine = new EngineReadiness(activities, () =>
        {
            Interlocked.Increment(ref coreRuns);
            return Task.CompletedTask;
        }, () => throw new InvalidOperationException("warm-up broke"));

        await engine.Start();
        await engine.WarmReady.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(EngineState.Failed, engine.State);

        await engine.WhenCoreReadyAsync();
        Assert.Equal(1, coreRuns);
    }
}
