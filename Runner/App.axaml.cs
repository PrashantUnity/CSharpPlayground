using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace PdfEditorApp.Plugins.CSharpEditor.Runner;

public partial class App : Application
{
    public override void Initialize()
    {
        // See the matching handler in the hosted FryPDF app's App.axaml.cs for why this is
        // needed: raw input is dispatched via Dispatcher.Send, which only offers this event
        // as a way to intercept an exception thrown while handling a key/pointer event before
        // it aborts the whole process.
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            Debug.WriteLine($"[Dispatcher.UnhandledException] {e.Exception}");
            e.Handled = true;
        };

        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void AboutMenuItem_OnClick(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            var about = new AboutWindow();
            about.OpenDocumentationAction = () =>
            {
                if (desktop.MainWindow is MainWindow mw && mw.StudioHostVm is { } host)
                {
                    host.NavigateToDocs();
                }
            };
            about.ShowDialog(desktop.MainWindow);
        }
    }

    private void SettingsMenuItem_OnClick(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow is MainWindow mw &&
            mw.StudioHostVm is { } host)
        {
            host.NavigateToSettings();
        }
    }

    private void CustomizationMenuItem_OnClick(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow is MainWindow mw &&
            mw.StudioHostVm is { } host)
        {
            host.NavigateToSettings("Customization");
        }
    }

    private async void CheckForUpdatesMenuItem_OnClick(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            var updateDialog = new UpdateDialogWindow();
            await updateDialog.ShowDialog(desktop.MainWindow);
        }
    }
}
