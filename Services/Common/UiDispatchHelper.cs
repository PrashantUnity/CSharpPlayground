using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Common;

/// <summary>
/// Helper to safely dispatch UI operations. In a live application (desktop or single-view lifetime),
/// cross-thread calls are posted to the Avalonia UI dispatcher. In test runners or headless sessions
/// without an active message pump, callbacks are executed inline to prevent deadlocks and queued-callback starvation.
/// </summary>
public static class UiDispatchHelper
{
    public static bool HasLiveUiLifetime =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime or ISingleViewApplicationLifetime;

    public static void RunOnUi(Action action)
    {
        if (HasLiveUiLifetime && !Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(action);
        }
        else
        {
            action();
        }
    }
}
