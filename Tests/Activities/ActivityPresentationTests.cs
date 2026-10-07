using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Services.Activities;
using Xunit;

namespace CSharpEditorPlugin.Tests.Activities;

public class ActivityPresentationTests
{
    private readonly ManualTimeProvider _clock = new();
    private readonly ActivityService _service;
    private readonly ActivityPresentationTracker _tracker;

    public ActivityPresentationTests()
    {
        _service = new ActivityService(_clock);
        _tracker = new ActivityPresentationTracker(_clock);
    }

    private ActivityPresentation Now() => _tracker.Compute(_service.Snapshot());

    [Fact]
    public void FastWork_IsNeverShown()
    {
        using (_service.Start(new ActivityOptions("Switching tab", ActivityLocation.Editor)))
        {
            _clock.AdvanceMs(200);
            var during = Now();
            Assert.False(during.LineFor(ActivityLocation.Editor).IsVisible);
            Assert.Null(during.StatusText);
            Assert.True(during.NeedsTick);
        }

        var after = Now();
        Assert.False(after.LineFor(ActivityLocation.Editor).IsVisible);
        Assert.False(after.NeedsTick);
    }

    [Fact]
    public void SlowWork_ShowsALineInItsZone_AndTheStatusBar_ButNoCardOrToast()
    {
        using var open = _service.Start(new ActivityOptions("Opening data.csv", ActivityLocation.Editor) { Detail = "12 MB" });
        _clock.AdvanceMs(300);
        var p = Now();

        Assert.True(p.LineFor(ActivityLocation.Editor).IsVisible);
        Assert.Null(p.LineFor(ActivityLocation.Editor).Fraction);
        Assert.False(p.LineFor(ActivityLocation.Explorer).IsVisible);
        Assert.Equal("Opening data.csv", p.StatusText);
        Assert.Equal("12 MB", p.StatusDetail);
        Assert.Null(p.Card);
        Assert.Empty(p.Toasts);
    }

    [Fact]
    public void ShownWork_StaysAtLeastMinVisible_ThenGoes()
    {
        var open = _service.Start(new ActivityOptions("Opening", ActivityLocation.Explorer));
        _clock.AdvanceMs(260);
        Assert.True(Now().LineFor(ActivityLocation.Explorer).IsVisible);

        open.Dispose();
        _clock.AdvanceMs(100);
        Assert.True(Now().LineFor(ActivityLocation.Explorer).IsVisible);

        _clock.AdvanceMs(400);
        var later = Now();
        Assert.False(later.LineFor(ActivityLocation.Explorer).IsVisible);
        Assert.Null(later.StatusText);
        Assert.False(later.NeedsTick);
    }

    [Fact]
    public void LongCancellableWork_GetsAToast_AndDismissingItKeepsTheWorkRunning()
    {
        using var open = _service.Start(new ActivityOptions("Opening big.csv", ActivityLocation.Editor) { Cancellable = true });
        using var notCancellable = _service.Start(new ActivityOptions("Indexing", ActivityLocation.Background));
        _clock.AdvanceMs(1000);
        Assert.Empty(Now().Toasts);

        _clock.AdvanceMs(1100);
        var toast = Assert.Single(Now().Toasts);
        Assert.Equal(open.Id, toast.Id);
        Assert.True(toast.Cancellable);
        Assert.Equal("Opening big.csv (+1)", Now().StatusText);

        _tracker.Dismiss(toast.Id);
        Assert.Empty(Now().Toasts);
        Assert.True(Now().LineFor(ActivityLocation.Editor).IsVisible);
        Assert.False(open.Token.IsCancellationRequested);
    }

    [Fact]
    public void DeterminateWork_ShowsItsFractionOnTheLine_AndGetsAToast()
    {
        using var index = _service.Start(new ActivityOptions("Reading files", ActivityLocation.Explorer));
        index.Report(0.25);
        _clock.AdvanceMs(2100);
        var p = Now();
        Assert.Equal(0.25, p.LineFor(ActivityLocation.Explorer).Fraction);
        Assert.Equal(0.25, Assert.Single(p.Toasts).Fraction);
    }

    [Fact]
    public void BlockingWork_ShowsTheCardAfterItsDelay_AndYieldsToTheLineWhenAsked()
    {
        using var coldStart = _service.Start(new ActivityOptions("Loading workspace", ActivityLocation.Hub)
        {
            Blocking = true,
            YieldAfter = TimeSpan.FromSeconds(3)
        });

        _clock.AdvanceMs(300);
        Assert.Null(Now().Card);

        _clock.AdvanceMs(200);
        Assert.Equal("Loading workspace", Now().Card?.Title);

        _clock.AdvanceMs(2600);
        var yielded = Now();
        Assert.Null(yielded.Card);
        Assert.True(yielded.LineFor(ActivityLocation.Hub).IsVisible);
    }

    [Fact]
    public void TheCard_GoesAtOnce_WhenBlockingWorkEnds()
    {
        var switchWorkspace = _service.Start(new ActivityOptions("Opening folder", ActivityLocation.Window) { Blocking = true, Cancellable = true });
        _clock.AdvanceMs(450);
        Assert.True(Now().Card?.Cancellable);

        switchWorkspace.Dispose();
        Assert.Null(Now().Card);
    }

    [Fact]
    public void Failures_BecomeErrorToasts_ThatExpire()
    {
        _service.Failed += (s, ex) => _tracker.AddError(s, ex);
        var open = _service.Start(new ActivityOptions("Opening a.png", ActivityLocation.Editor));
        open.Fail(new InvalidDataException("not a PNG"));

        var toast = Assert.Single(Now().Toasts);
        Assert.True(toast.IsError);
        Assert.Equal("Opening a.png failed", toast.Title);
        Assert.Equal("not a PNG", toast.Detail);

        _clock.Advance(ActivityTiming.ErrorToastLifetime);
        Assert.Empty(Now().Toasts);
    }
}
