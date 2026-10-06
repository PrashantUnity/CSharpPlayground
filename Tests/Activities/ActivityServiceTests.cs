using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Services.Activities;
using Xunit;

namespace CSharpEditorPlugin.Tests.Activities;

public class ActivityServiceTests
{
    [Fact]
    public void StartedActivities_AreListed_UntilDisposed_AndEachChangeIsAnnounced()
    {
        var service = new ActivityService(new ManualTimeProvider());
        int changes = 0;
        service.Changed += () => changes++;

        var outer = service.Start(new ActivityOptions("Opening project", ActivityLocation.Window));
        var inner = service.Start(new ActivityOptions("Loading file", ActivityLocation.Editor) { Detail = "a.cs" });

        Assert.Equal(new[] { "Opening project", "Loading file" }, service.Snapshot().Select(a => a.Title));
        Assert.Equal("a.cs", service.Snapshot()[1].Detail);

        inner.Dispose();
        // The outer one keeps its own title: nothing is "last writer wins".
        Assert.Equal("Opening project", Assert.Single(service.Snapshot()).Title);

        outer.Dispose();
        outer.Dispose();
        Assert.Empty(service.Snapshot());
        Assert.Equal(4, changes);
    }

    [Fact]
    public void Report_UpdatesProgressAndDetail_ClampedToZeroOne()
    {
        var service = new ActivityService(new ManualTimeProvider());
        using var activity = service.Start(new ActivityOptions("Indexing"));

        activity.Report(0.4, "src/");
        Assert.Equal(0.4, service.Snapshot()[0].Fraction);
        Assert.Equal("src/", service.Snapshot()[0].Detail);

        activity.Report(7);
        Assert.Equal(1, service.Snapshot()[0].Fraction);
        activity.Report(double.NaN);
        Assert.Null(service.Snapshot()[0].Fraction);
    }

    [Fact]
    public void Cancel_CancelsTheActivityToken_AndALinkedTokenCancelsItToo()
    {
        var service = new ActivityService(new ManualTimeProvider());
        using var own = service.Start(new ActivityOptions("Opening") { Cancellable = true });
        service.Cancel(own.Id);
        Assert.True(own.Token.IsCancellationRequested);

        using var outside = new CancellationTokenSource();
        using var linked = service.Start(new ActivityOptions("Loading"), outside.Token);
        outside.Cancel();
        Assert.True(linked.Token.IsCancellationRequested);

        // Unknown and finished ids are ignored.
        service.Cancel(12345);
        var ended = service.Start(new ActivityOptions("Done"));
        ended.Dispose();
        service.Cancel(ended.Id);
    }

    [Fact]
    public void Fail_RemovesTheActivity_AndReportsTheError_ButCancellationIsNotAFailure()
    {
        var service = new ActivityService(new ManualTimeProvider());
        var failures = new List<(string Title, string Message)>();
        service.Failed += (snapshot, ex) => failures.Add((snapshot.Title, ex.Message));

        var broken = service.Start(new ActivityOptions("Opening data.csv"));
        broken.Fail(new IOException("access denied"));
        broken.Dispose();

        var cancelled = service.Start(new ActivityOptions("Opening big.csv") { Cancellable = true });
        service.Cancel(cancelled.Id);
        cancelled.Fail(new OperationCanceledException(cancelled.Token));

        Assert.Empty(service.Snapshot());
        Assert.Equal(("Opening data.csv", "access denied"), Assert.Single(failures));
    }

    [Fact]
    public async Task ManyThreads_StartReportAndEnd_WithoutLosingOrLeakingActivities()
    {
        var service = new ActivityService();
        long changes = 0;
        service.Changed += () => Interlocked.Increment(ref changes);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(t => Task.Run(() =>
        {
            for (int i = 0; i < 500; i++)
            {
                using var activity = service.Start(new ActivityOptions($"work {t}.{i}"));
                activity.Report(i / 500.0);
                _ = service.Snapshot();
            }
        })));

        Assert.Empty(service.Snapshot());
        Assert.Equal(8 * 500 * 3, Interlocked.Read(ref changes));
        Assert.Equal(8 * 500 * 3, service.Version);
    }

    [Fact]
    public void AThrowingSubscriber_DoesNotBreakTheWorkThatReported()
    {
        var service = new ActivityService(new ManualTimeProvider());
        service.Changed += () => throw new InvalidOperationException("presenter bug");
        using var activity = service.Start(new ActivityOptions("Opening"));
        activity.Report(0.5);
        Assert.Single(service.Snapshot());
    }

    [Fact]
    public async Task RunAsync_ReportsFailures_ReturnsFalse_AndEndsCancellationQuietly()
    {
        var service = new ActivityService(new ManualTimeProvider());
        var failures = new List<string>();
        service.Failed += (s, _) => failures.Add(s.Title);

        Assert.True(await service.RunAsync(new ActivityOptions("ok"), _ => Task.CompletedTask));
        Assert.False(await service.RunAsync(new ActivityOptions("broken"), _ => throw new InvalidOperationException("boom")));
        Assert.False(await service.RunAsync(new ActivityOptions("cancelled") { Cancellable = true }, async a =>
        {
            service.Cancel(a.Id);
            await Task.Delay(Timeout.Infinite, a.Token);
        }));
        Assert.Equal(42, await service.RunAsync(new ActivityOptions("value"), _ => Task.FromResult(42)));

        Assert.Equal(new[] { "broken" }, failures);
        Assert.Empty(service.Snapshot());
    }

    [Fact]
    public async Task FireAndForget_ReportsAFailure_InsteadOfLosingIt()
    {
        var service = new ActivityService(new ManualTimeProvider());
        var failed = new TaskCompletionSource<string>();
        service.Failed += (s, ex) => failed.TrySetResult($"{s.Title}: {ex.Message}");

        Task.Run(new Action(() => throw new InvalidOperationException("no engine"))).FireAndForget(service, "Loading extensions");

        Assert.Equal("Loading extensions: no engine", await failed.Task.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void NullActivityService_ReportsNothing_ButLinkedCancellationStillWorks()
    {
        using var outside = new CancellationTokenSource();
        using var activity = NullActivityService.Instance.Start(new ActivityOptions("x"), outside.Token);
        activity.Report(0.5, "y");
        outside.Cancel();
        Assert.True(activity.Token.IsCancellationRequested);
        Assert.Empty(NullActivityService.Instance.Snapshot());
    }
}

public class LatestOperationTests
{
    [Fact]
    public void Begin_CancelsThePreviousRequest_AndOnlyTheNewestIsLatest()
    {
        var latest = new LatestOperation();
        var first = latest.Begin();
        Assert.True(latest.IsLatest(first));

        var second = latest.Begin();
        Assert.True(first.IsCancellationRequested);
        Assert.False(latest.IsLatest(first));
        Assert.True(latest.IsLatest(second));

        latest.CancelCurrent();
        Assert.True(second.IsCancellationRequested);
        Assert.False(latest.IsLatest(second));
    }

    [Fact]
    public async Task ASlowEarlierRequest_CannotWinOverALaterOne()
    {
        var latest = new LatestOperation();
        var shown = new List<string>();
        var slowGate = new TaskCompletionSource();

        async Task Open(string name, Task load)
        {
            var token = latest.Begin();
            await load;
            if (token.IsCancellationRequested) return;
            shown.Add(name);
        }

        var a = Open("A", slowGate.Task);
        var b = Open("B", Task.CompletedTask);
        slowGate.SetResult();
        await Task.WhenAll(a, b);

        Assert.Equal(new[] { "B" }, shown);
    }
}

public class SingleFlightTests
{
    [Fact]
    public async Task ConcurrentLoadsOfOneKey_ShareOneLoad_AndTheNextLoadRunsAfresh()
    {
        var flight = new SingleFlight<string, int>(StringComparer.OrdinalIgnoreCase);
        int loads = 0;
        var gate = new TaskCompletionSource();

        async Task<int> Load()
        {
            Interlocked.Increment(ref loads);
            await gate.Task;
            return 7;
        }

        var first = flight.RunAsync("doc", Load);
        var second = flight.RunAsync("DOC", Load);
        var other = flight.RunAsync("other", Load);
        Assert.Equal(2, flight.InFlightCount);
        gate.SetResult();

        Assert.Equal(new[] { 7, 7, 7 }, await Task.WhenAll(first, second, other));
        Assert.Equal(2, loads);
        Assert.Equal(0, flight.InFlightCount);

        Assert.Equal(7, await flight.RunAsync("doc", Load));
        Assert.Equal(3, loads);
    }

    [Fact]
    public async Task AFailedLoad_FailsEveryJoinedCaller_AndIsForgotten()
    {
        var flight = new SingleFlight<string, int>();
        var gate = new TaskCompletionSource();
        async Task<int> Broken()
        {
            await gate.Task;
            throw new IOException("gone");
        }

        var first = flight.RunAsync("doc", Broken);
        var second = flight.RunAsync("doc", Broken);
        gate.SetResult();

        await Assert.ThrowsAsync<IOException>(() => first);
        await Assert.ThrowsAsync<IOException>(() => second);
        Assert.Equal(0, flight.InFlightCount);
    }
}
