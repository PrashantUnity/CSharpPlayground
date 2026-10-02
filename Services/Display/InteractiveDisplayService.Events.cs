using System;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Interaction;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

// Events: a visual's callbacks (DisplayHandle.On, OnClick…) run when the kernel is free; running code that wants them
// sooner, or wants to wait for them, says so here.
public static partial class Display
{
    /// <summary>Runs the callbacks whose events have come, here and now, and says how many ran.</summary>
    public static int ProcessEvents() => VisualEventLoop.Current?.RunPending() ?? 0;

    /// <summary>
    /// Waits for events and runs their callbacks as they come, until Stop (or <paramref name="timeout"/>): a program that
    /// has shown an interactive visual and has nothing else to do.
    /// </summary>
    public static void Wait(TimeSpan? timeout = null) =>
        (VisualEventLoop.Current ?? throw new InvalidOperationException("Nothing listens to a visual yet: add a callback with On or OnClick first."))
            .Wait(InteractiveCancellationContext.Current, timeout);
}
