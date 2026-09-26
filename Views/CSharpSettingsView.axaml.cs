using Avalonia.Controls;
using Avalonia.Input;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;

namespace PdfEditorApp.Plugins.CSharpEditor.Views;

public partial class CSharpSettingsView : UserControl
{
    public CSharpSettingsView()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (DataContext is CSharpSettingsViewModel vm)
            {
                vm.BackToPrevious();
                e.Handled = true;
            }
        }
    }
}
