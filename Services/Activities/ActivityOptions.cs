using System;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Activities;

/// <summary>
/// Where the studio shows a running activity. Each zone that does work has its own thin progress line, so the user sees
/// which part of the window is busy; <see cref="Background"/> work only shows in the status bar.
/// </summary>
public enum ActivityLocation
{
    /// <summary>Status bar only (engine warm-up, extension loading, indexing).</summary>
    Background,

    /// <summary>The whole studio: a line across the top of the host.</summary>
    Window,

    /// <summary>The Code Studio editor: a line under its tab bar.</summary>
    Editor,

    /// <summary>The Notebook Studio: a line under its tab strip.</summary>
    Notebook,

    /// <summary>The Explorer side bar: a line at its top.</summary>
    Explorer,

    /// <summary>The Hub's workspace list: a line above it.</summary>
    Hub,
}

/// <summary>
/// What a producer says about one piece of work it starts. The producer never decides <em>how</em> it is shown: the
/// presentation rules (<see cref="ActivityTiming"/>) show nothing for fast work, a line and a status entry for slow work,
/// a toast for long work that can be cancelled or reports progress, and the full-screen card only for
/// <see cref="Blocking"/> work (cold start, switching workspace).
/// </summary>
/// <param name="Title">Short, user-facing ("Opening data.csv").</param>
/// <param name="Location">The zone that is busy.</param>
public sealed record ActivityOptions(string Title, ActivityLocation Location = ActivityLocation.Background)
{
    /// <summary>A second line, e.g. a file name or the current step.</summary>
    public string? Detail { get; init; }

    /// <summary>The user may cancel it (a Cancel button in its toast or card).</summary>
    public bool Cancellable { get; init; }

    /// <summary>The user cannot sensibly do anything else meanwhile: shown as the branded card over the studio.</summary>
    public bool Blocking { get; init; }

    /// <summary>
    /// For <see cref="Blocking"/> work: after this long the card gives way to the zone's line, so a slow load never
    /// holds the studio hostage. <c>null</c>: the card stays until the work ends.
    /// </summary>
    public TimeSpan? YieldAfter { get; init; }
}
