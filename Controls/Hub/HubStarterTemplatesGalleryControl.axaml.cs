using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using CSharpManagerViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Hub.CSharpManagerViewModel;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Hub;

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
