using System.Diagnostics;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Common;

/// <summary>Opens web pages in the user's default browser.</summary>
public static class BrowserLauncher
{
    /// <summary>Hands <paramref name="url"/> to the OS off the UI thread; a failure is logged, never thrown.</summary>
    public static void Open(string url)
    {
        _ = Task.Run(() =>
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BrowserLauncher] Could not open {url}: {ex.Message}");
            }
        });
    }
}
