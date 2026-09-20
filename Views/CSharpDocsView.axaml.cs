using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;

namespace PdfEditorApp.Plugins.CSharpEditor.Views;

public partial class CSharpDocsView : UserControl
{
    public CSharpDocsView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not CSharpDocsViewModel vm) return;

        if (e.Key == Key.Escape)
        {
            if (vm.HasSearchQuery)
            {
                vm.ClearSearch();
                e.Handled = true;
            }
            else
            {
                vm.BackToHub();
                e.Handled = true;
            }
        }
    }
}
