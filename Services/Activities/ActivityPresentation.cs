using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Activities;

/// <summary>A zone's progress line. <see cref="Fraction"/> is <c>null</c> for "busy, amount unknown".</summary>
public sealed record ActivityLine(bool IsVisible, double? Fraction)
{
    public static readonly ActivityLine Hidden = new(false, null);
}

/// <summary>A toast: long work that can be cancelled or reports progress, or work that failed.</summary>
public sealed record ActivityToast(long Id, string Title, string? Detail, double? Fraction, bool Cancellable, bool IsError);

/// <summary>The branded card over the studio, for blocking work.</summary>
public sealed record ActivityCard(long Id, string Title, string? Detail, bool Cancellable);

/// <summary>Everything the screen should show about running work at one moment.</summary>
public sealed record ActivityPresentation(
    IReadOnlyDictionary<ActivityLocation, ActivityLine> Lines,
    string? StatusText,
    string? StatusDetail,
    IReadOnlyList<ActivityToast> Toasts,
    ActivityCard? Card,
    bool NeedsTick)
{
    public ActivityLine LineFor(ActivityLocation location) => Lines.TryGetValue(location, out var line) ? line : ActivityLine.Hidden;
}

/// <summary>
/// The presentation rules of <see cref="ActivityTiming"/> as a state machine, without any UI: given the running
/// activities and the time, what should be on screen. It remembers what it showed, so something that was shown stays for
/// <see cref="ActivityTiming.MinVisible"/> after its work ended, and failures stay as error toasts for a while.
/// Not thread-safe: one presenter drives it from one thread.
/// </summary>
public sealed class ActivityPresentationTracker
{
    private readonly TimeProvider _time;
    private readonly Dictionary<long, long> _shownAt = new();
    private readonly Dictionary<long, ActivitySnapshot> _lastSeen = new();
    private readonly Dictionary<long, (ActivitySnapshot Snapshot, long ShownAt)> _lingering = new();
    private readonly List<(ActivityToast Toast, long PostedAt)> _errors = new();
    private readonly HashSet<long> _dismissed = new();

    public ActivityPresentationTracker(TimeProvider time)
    {
        _time = time;
    }

    /// <summary>Adds an error toast for a failed activity.</summary>
    public void AddError(ActivitySnapshot failed, Exception error)
    {
        _errors.RemoveAll(e => e.Toast.Id == failed.Id);
        var message = string.IsNullOrWhiteSpace(error.Message) ? error.GetType().Name : error.Message;
        _errors.Add((new ActivityToast(failed.Id, $"{failed.Title} failed", message, null, Cancellable: false, IsError: true), _time.GetTimestamp()));
    }

    /// <summary>Hides a toast (an error goes away; running work keeps running, only its toast is gone).</summary>
    public void Dismiss(long id)
    {
        if (_errors.RemoveAll(e => e.Toast.Id == id) == 0) _dismissed.Add(id);
    }

    public ActivityPresentation Compute(IReadOnlyList<ActivitySnapshot> running)
    {
        long now = _time.GetTimestamp();
        var visible = new List<ActivitySnapshot>();
        var toasts = new List<ActivityToast>();
        ActivityCard? card = null;
        var runningIds = new HashSet<long>();

        foreach (var activity in running)
        {
            runningIds.Add(activity.Id);
            var elapsed = _time.GetElapsedTime(activity.StartedTimestamp, now);
            bool blockingNow = IsBlockingNow(activity, elapsed);
            var threshold = blockingNow ? ActivityTiming.BlockingShowAfter : ActivityTiming.ShowAfter;
            if (elapsed < threshold) continue;

            _shownAt.TryAdd(activity.Id, now);
            _lastSeen[activity.Id] = activity;
            visible.Add(activity);

            if (blockingNow)
            {
                card ??= new ActivityCard(activity.Id, activity.Title, activity.Detail, activity.Cancellable);
            }
            else if (elapsed >= ActivityTiming.ToastAfter && (activity.Cancellable || activity.Fraction.HasValue) && !_dismissed.Contains(activity.Id))
            {
                toasts.Add(new ActivityToast(activity.Id, activity.Title, activity.Detail, activity.Fraction, activity.Cancellable, IsError: false));
            }
        }

        // Shown work that just ended stays a moment, so a line that appeared does not vanish the next instant.
        foreach (var id in _shownAt.Keys.Where(id => !runningIds.Contains(id)).ToList())
        {
            if (_lastSeen.TryGetValue(id, out var last)) _lingering[id] = (last, _shownAt[id]);
            _shownAt.Remove(id);
            _lastSeen.Remove(id);
            _dismissed.Remove(id);
        }

        foreach (var (id, entry) in _lingering.ToList())
        {
            if (_time.GetElapsedTime(entry.ShownAt, now) >= ActivityTiming.MinVisible) _lingering.Remove(id);
        }

        _errors.RemoveAll(e => _time.GetElapsedTime(e.PostedAt, now) >= ActivityTiming.ErrorToastLifetime);
        toasts.AddRange(_errors.Select(e => e.Toast));

        // The card never lingers: once blocking work is over the studio is usable again at once.
        var onScreen = visible.Concat(_lingering.Values.Select(l => l.Snapshot)).OrderBy(a => a.StartedTimestamp).ToList();
        var lines = new Dictionary<ActivityLocation, ActivityLine>();
        foreach (var group in onScreen.Where(a => a.Location != ActivityLocation.Background).GroupBy(a => a.Location))
        {
            var items = group.ToList();
            double? fraction = items.Count == 1 ? items[0].Fraction : null;
            lines[group.Key] = new ActivityLine(true, fraction);
        }

        string? statusText = null;
        string? statusDetail = null;
        if (onScreen.Count > 0)
        {
            var first = onScreen[0];
            statusText = onScreen.Count > 1 ? $"{first.Title} (+{onScreen.Count - 1})" : first.Title;
            statusDetail = first.Detail;
        }

        bool needsTick = running.Count > 0 || _lingering.Count > 0 || _errors.Count > 0;
        return new ActivityPresentation(lines, statusText, statusDetail, toasts, card, needsTick);
    }

    private static bool IsBlockingNow(ActivitySnapshot activity, TimeSpan elapsed) =>
        activity.Blocking && (activity.YieldAfter is not { } yieldAfter || elapsed < yieldAfter);
}
