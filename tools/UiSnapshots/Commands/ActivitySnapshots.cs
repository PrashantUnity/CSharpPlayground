using Avalonia.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Controls.Activities;
using PdfEditorApp.Plugins.CSharpEditor.Services.Activities;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Common;

namespace PdfEditorApp.Plugins.CSharpEditor.Tools.UiSnapshots.Commands;

/// <summary>A clock a snapshot moves by hand, so "work that has run for 3 s" can be drawn without waiting 3 s.</summary>
internal sealed class SnapshotClock : TimeProvider
{
    private long _ticks;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => Interlocked.Read(ref _ticks);
    public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
}

/// <summary>
/// <c>--loading</c> for the studio snapshots: puts running work on a view the way the studio host would, so the
/// progress line, the status entry and (with a host) the toasts can be checked without a real slow operation.
/// </summary>
internal static class ActivitySnapshots
{
    public static ActivityPresenterViewModel? BusyIfAsked(Control view, Options options, ActivityLocation location, string defaultTitle)
    {
        if (!options.Flag("loading")) return null;
        var clock = new SnapshotClock();
        var activities = new ActivityService(clock);
        var presenter = new ActivityPresenterViewModel(activities, action => action());
        var activity = activities.Start(new ActivityOptions(options.Value("loading-title") ?? defaultTitle, location)
        {
            Detail = options.Value("loading-sub"),
            Cancellable = true,
        });
        if (options.Value("loading-progress") is { } progress && double.TryParse(progress, System.Globalization.CultureInfo.InvariantCulture, out var fraction))
        {
            activity.Report(fraction);
        }

        clock.Advance(TimeSpan.FromSeconds(options.Int("loading-seconds", 3)));
        presenter.Refresh();
        ActivityScope.SetPresenter(view, presenter);
        return presenter;
    }
}
