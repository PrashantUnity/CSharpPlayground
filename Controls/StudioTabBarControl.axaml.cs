using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls;

public partial class StudioTabBarControl : UserControl
{
    public StudioTabBarControl()
    {
        InitializeComponent();
    }

    public void OnTabPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed)
        {
            if (sender is Visual v && v.DataContext is StudioTabItemViewModel tabVm)
            {
                tabVm.Close();
                e.Handled = true;
            }
        }
    }
}
