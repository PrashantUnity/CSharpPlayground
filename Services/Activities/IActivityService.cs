using System;
using System.Collections.Generic;
using System.Threading;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Activities;

/// <summary>
/// One piece of running work. Dispose it when the work ends (a <c>using</c> block): that is what removes it from the
/// screen. Safe to use from any thread.
/// </summary>
public interface IActivity : IDisposable
{
    long Id { get; }

    /// <summary>Cancelled when the user presses Cancel, or when the token passed to <see cref="IActivityService.Start"/> is.</summary>
    CancellationToken Token { get; }

    /// <summary>Progress so far (0..1, <c>null</c> for "unknown") and/or a new detail line.</summary>
    void Report(double? fraction = null, string? detail = null);

    /// <summary>Ends the activity as failed: the user is told (an error toast). Cancellation is not a failure.</summary>
    void Fail(Exception error);
}

/// <summary>An immutable picture of one running activity.</summary>
public sealed record ActivitySnapshot(
    long Id,
    string Title,
    string? Detail,
    ActivityLocation Location,
    double? Fraction,
    bool Cancellable,
    bool Blocking,
    TimeSpan? YieldAfter,
    long StartedTimestamp);

/// <summary>
/// Where every part of the studio reports work the user may be waiting for. Producers (view models, storage, the
/// engine) only report; presenters decide whether, where and when to show it (<see cref="ActivityTiming"/>). It holds no
/// UI types, so a web or sidecar host can stream the same activities.
/// </summary>
public interface IActivityService
{
    /// <summary>The clock activities are timed with (a fake one in tests).</summary>
    TimeProvider Time { get; }

    /// <summary>Starts an activity. <paramref name="linked"/> cancels it too.</summary>
    IActivity Start(ActivityOptions options, CancellationToken linked = default);

    /// <summary>The activities running now, oldest first.</summary>
    IReadOnlyList<ActivitySnapshot> Snapshot();

    /// <summary>Moves on every change; a presenter can skip work when it has not moved.</summary>
    long Version { get; }

    /// <summary>Cancels a running activity (the Cancel button). Unknown or finished ids are ignored.</summary>
    void Cancel(long id);

    /// <summary>Something started, reported or ended. Raised on the thread that made the change.</summary>
    event Action? Changed;

    /// <summary>An activity failed. Raised on the thread that reported it.</summary>
    event Action<ActivitySnapshot, Exception>? Failed;
}
