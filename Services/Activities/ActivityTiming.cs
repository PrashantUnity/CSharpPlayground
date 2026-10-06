using System;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Activities;

/// <summary>
/// When running work becomes visible. One place, so every zone behaves the same.
/// Work that ends before <see cref="ShowAfter"/> is never shown at all (a tab switch, most file opens): a spinner that
/// flashes for 30 ms reads as a glitch, not as feedback. Once shown, it stays at least <see cref="MinVisible"/> so it
/// does not flicker. These are presentation rules: nothing ever waits on them.
/// </summary>
public static class ActivityTiming
{
    /// <summary>How long work runs before its zone's line and the status bar show it.</summary>
    public static readonly TimeSpan ShowAfter = TimeSpan.FromMilliseconds(250);

    /// <summary>How long something stays on screen once it was shown, even if the work ended sooner.</summary>
    public static readonly TimeSpan MinVisible = TimeSpan.FromMilliseconds(400);

    /// <summary>How long work runs before it also gets a toast (only if it can be cancelled or reports progress).</summary>
    public static readonly TimeSpan ToastAfter = TimeSpan.FromSeconds(2);

    /// <summary>How long blocking work runs before the branded card covers the studio.</summary>
    public static readonly TimeSpan BlockingShowAfter = TimeSpan.FromMilliseconds(400);

    /// <summary>How long an error toast stays before it dismisses itself.</summary>
    public static readonly TimeSpan ErrorToastLifetime = TimeSpan.FromSeconds(8);

    /// <summary>How often the presenter re-evaluates the rules while something is pending or showing.</summary>
    public static readonly TimeSpan PresenterTick = TimeSpan.FromMilliseconds(100);
}
