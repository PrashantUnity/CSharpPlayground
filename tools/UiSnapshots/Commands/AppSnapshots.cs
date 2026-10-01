using Avalonia.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Runner;

namespace PdfEditorApp.Plugins.CSharpEditor.Tools.UiSnapshots;

/// <summary>Snapshots for application-level windows: About, Update Dialog, and MainWindow.</summary>
internal static class AppSnapshots
{
    public static void About(Options options)
    {
        var window = new AboutWindow
        {
            Width = options.Int("width", 520),
            Height = options.Int("height", 460)
        };
        window.Show();
        Snapshot.Settle();
        Snapshot.Save(window, options, "about");
    }

    public static void Update(Options options)
    {
        var window = new UpdateDialogWindow
        {
            Width = options.Int("width", 440),
            Height = options.Int("height", 260)
        };
        window.Show();
        Snapshot.Settle();
        Snapshot.Save(window, options, "update");
    }

    public static void MainWindow(Options options)
    {
        var window = new MainWindow
        {
            Width = options.Int("width", 1400),
            Height = options.Int("height", 900)
        };
        window.Show();
        Snapshot.Settle();
        Snapshot.Save(window, options, "mainwindow");
    }
}
