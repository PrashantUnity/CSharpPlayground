using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls;

public partial class QuickOpenOverlayControl : UserControl
{
    public QuickOpenOverlayControl()
    {
        InitializeComponent();

        PropertyChanged += (s, e) =>
        {
            if (e.Property == IsVisibleProperty && IsVisible)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    var box = this.FindControl<TextBox>("SearchBox");
                    if (box != null)
                    {
                        box.Focus();
                        box.SelectAll();
                    }
                }, DispatcherPriority.Input);
            }
        };
    }

    private void OnBackdropPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is QuickOpenViewModel vm)
        {
            vm.Close();
            e.Handled = true;
        }
    }

    private void OnPaletteCardPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
    }

    private void OnSearchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not QuickOpenViewModel vm) return;

        if (e.Key == Key.Down)
        {
            vm.SelectNext();
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            vm.SelectPrevious();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            _ = vm.ExecuteSelectedAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.Close();
            e.Handled = true;
        }
    }

    private void OnListBoxDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (DataContext is QuickOpenViewModel vm)
        {
            _ = vm.ExecuteSelectedAsync();
            e.Handled = true;
        }
    }

    private void OnListBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not QuickOpenViewModel vm) return;

        if (e.Key == Key.Enter)
        {
            _ = vm.ExecuteSelectedAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.Close();
            e.Handled = true;
        }
    }
}
