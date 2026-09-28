using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;

namespace PdfEditorApp.Plugins.CSharpEditor.Views;

public partial class CSharpStudioHostView : UserControl
{
    public CSharpStudioHostView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnHostKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnHostKeyDown(object? sender, KeyEventArgs e)
    {
        var isModifier = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (isModifier && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && (e.Key == Key.OemComma || e.Key == Key.Oem1))
        {
            if (DataContext is CSharpStudioHostViewModel hostVm)
            {
                hostVm.NavigateToSettings();
                e.Handled = true;
            }
        }
    }
}
