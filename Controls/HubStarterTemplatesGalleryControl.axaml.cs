using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls;

public partial class HubStarterTemplatesGalleryControl : UserControl
{
    public HubStarterTemplatesGalleryControl()
    {
        InitializeComponent();
    }

    private void OnCardDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Visual visual && visual.DataContext is CodeTemplate template && DataContext is CSharpManagerViewModel vm)
        {
            _ = vm.LaunchTemplateAsync(template);
            e.Handled = true;
        }
    }
}
