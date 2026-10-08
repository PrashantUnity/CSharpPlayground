using System;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;

namespace CSharpEditorPlugin.Tests.TestSupport;

public static class TestHeadlessApp
{
    private static readonly object Sync = new();

    public static void EnsureInitialized()
    {
        lock (Sync)
        {
            if (Application.Current != null) return;
            try
            {
                AppBuilder.Configure<PdfEditorApp.Plugins.CSharpEditor.Runner.App>()
                    .UseSkia()
                    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                    .SetupWithoutStarting();
            }
            catch (InvalidOperationException)
            {
                // App already setup by another test runner
            }
        }
    }

    public static void RunOnUIThread(Action action)
    {
        EnsureInitialized();
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Invoke(action);
        }
    }
}
