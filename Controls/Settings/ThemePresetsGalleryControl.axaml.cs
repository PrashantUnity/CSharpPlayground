using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Settings;

public partial class ThemePresetsGalleryControl : UserControl
{
    static ThemePresetsGalleryControl()
    {
        // A card's name box takes the keyboard as soon as Rename shows it, with the old name selected.
        IsVisibleProperty.Changed.AddClassHandler<Border>((border, e) =>
        {
            if (e.NewValue is not true || border.Child is not Grid grid) return;
            foreach (var child in grid.Children)
            {
                if (child is TextBox box && box.Classes.Contains("theme-rename-box"))
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        box.Focus();
                        box.SelectAll();
                    }, DispatcherPriority.Input);
                    return;
                }
            }
        });
    }

    public ThemePresetsGalleryControl()
    {
        InitializeComponent();
    }

    private CSharpSettingsViewModel? Settings => DataContext as CSharpSettingsViewModel;

    private void OnSaveAsBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Settings is not { } settings) return;
        settings.SaveCurrentThemeAs();
        e.Handled = true;
    }

    private void OnRenameBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (Settings is not { } settings || (sender as Control)?.DataContext is not ThemePresetItemViewModel item) return;
        switch (e.Key)
        {
            case Key.Enter:
                settings.RenameUserTheme(item);
                e.Handled = true;
                break;
            case Key.Escape:
                settings.CancelRenameUserTheme(item);
                e.Handled = true;
                break;
        }
    }
}
