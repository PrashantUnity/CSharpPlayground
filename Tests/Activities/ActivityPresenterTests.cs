using Avalonia.Controls;
using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Controls.Activities;
using PdfEditorApp.Plugins.CSharpEditor.Services.Activities;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Common;
using Xunit;

namespace CSharpEditorPlugin.Tests.Activities;

public class ActivityPresenterTests
{
    private readonly ManualTimeProvider _clock = new();
    private readonly ActivityService _service;
    private readonly ActivityPresenterViewModel _presenter;

    public ActivityPresenterTests()
    {
        _service = new ActivityService(_clock);
        _presenter = new ActivityPresenterViewModel(_service, action => action());
    }

    private void After(double milliseconds)
    {
        _clock.AdvanceMs(milliseconds);
        _presenter.Refresh();
    }

    [Fact]
    public void ALine_ShowsOnlyInItsZone_AfterTheDelay_AndTheTimerStopsWhenIdle()
    {
        Assert.False(_presenter.IsTicking);
        var open = _service.Start(new ActivityOptions("Opening data.csv", ActivityLocation.Editor) { Detail = "12 MB" });
        Assert.False(_presenter.Editor.IsVisible);
        Assert.True(_presenter.IsTicking);

        After(300);
        Assert.True(_presenter.Editor.IsVisible);
        Assert.True(_presenter.Editor.IsIndeterminate);
        Assert.False(_presenter.Explorer.IsVisible);
        Assert.Equal("Opening data.csv", _presenter.StatusText);
        Assert.Equal("12 MB", _presenter.StatusDetail);
        Assert.True(_presenter.HasStatus);
        Assert.False(_presenter.HasCard);

        open.Report(0.5);
        Assert.False(_presenter.Editor.IsIndeterminate);
        Assert.Equal(50, _presenter.Editor.Value);

        open.Dispose();
        After(500);
        Assert.False(_presenter.Editor.IsVisible);
        Assert.False(_presenter.HasStatus);
        Assert.False(_presenter.IsTicking);
    }

    [Fact]
    public void LongCancellableWork_GetsAToast_WhoseCancelStopsTheWork()
    {
        using var open = _service.Start(new ActivityOptions("Opening big.csv", ActivityLocation.Editor) { Cancellable = true });
        After(2100);

        var toast = Assert.Single(_presenter.Toasts);
        Assert.True(_presenter.HasToasts);
        Assert.True(toast.IsCancellable);
        toast.CancelCommand.Execute(null);
        Assert.True(open.Token.IsCancellationRequested);

        open.Dispose();
        After(10);
        Assert.Empty(_presenter.Toasts);
        Assert.False(_presenter.HasToasts);
    }

    [Fact]
    public void AFailure_ShowsAnErrorToast_ThatCanBeDismissed()
    {
        _service.Start(new ActivityOptions("Opening a.png", ActivityLocation.Editor)).Fail(new InvalidDataException("not a PNG"));

        var toast = Assert.Single(_presenter.Toasts);
        Assert.True(toast.IsError);
        Assert.Equal("not a PNG", toast.Detail);
        toast.DismissCommand.Execute(null);
        Assert.Empty(_presenter.Toasts);
    }

    [Fact]
    public void BlockingWork_ShowsTheCard_WhichCanCancelIt()
    {
        using var switchFolder = _service.Start(new ActivityOptions("Opening folder", ActivityLocation.Window) { Blocking = true, Cancellable = true, Detail = "repo" });
        After(300);
        Assert.False(_presenter.HasCard);

        After(200);
        Assert.True(_presenter.HasCard);
        Assert.Equal("Opening folder", _presenter.Card?.Title);
        Assert.Equal("repo", _presenter.Card?.Detail);
        _presenter.CancelCardCommand.Execute(null);
        Assert.True(switchFolder.Token.IsCancellationRequested);
    }

    [Fact]
    public void Dispose_Unsubscribes_AndStopsTheTimer()
    {
        _service.Start(new ActivityOptions("Indexing"));
        Assert.True(_presenter.IsTicking);
        _presenter.Dispose();
        Assert.False(_presenter.IsTicking);
        _service.Start(new ActivityOptions("More"));
        Assert.False(_presenter.IsTicking);
    }

    [Fact]
    public void AProgressLine_FollowsThePresenterHandedDownByItsScope_AndOnlyForItsZone()
    {
        var root = new Panel();
        var editorLine = new ActivityProgressLine { Location = ActivityLocation.Editor };
        var hubLine = new ActivityProgressLine { Location = ActivityLocation.Hub };
        var status = new ActivityStatusItem();
        root.Children.Add(editorLine);
        root.Children.Add(hubLine);
        root.Children.Add(status);
        ActivityScope.SetPresenter(root, _presenter);
        Assert.Same(_presenter, ActivityScope.GetPresenter(editorLine));

        Assert.False(editorLine.IsVisible);
        Assert.False(status.IsVisible);
        using var open = _service.Start(new ActivityOptions("Opening data.csv", ActivityLocation.Editor));
        After(300);

        Assert.True(editorLine.IsVisible);
        Assert.False(hubLine.IsVisible);
        Assert.True(status.IsVisible);
        // Not on screen (no window): nothing animates.
        Assert.False(editorLine.IsAnimationRunning);
        Assert.False(status.IsAnimationRunning);

        open.Dispose();
        After(500);
        Assert.False(editorLine.IsVisible);
        Assert.False(status.IsVisible);
    }

    [Fact]
    public void AProgressLine_WithoutAScope_StaysHidden()
    {
        var line = new ActivityProgressLine { Location = ActivityLocation.Editor };
        new Panel().Children.Add(line);
        Assert.False(line.IsVisible);
    }
}
