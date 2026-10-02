using Avalonia.Controls;
using Avalonia.Interactivity;
using PdfEditorApp.Plugins.CSharpEditor.Views;
using CSharpManagerViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Hub.CSharpManagerViewModel;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Hub;

public partial class HubCreateItemDialogControl : UserControl
{
    public HubCreateItemDialogControl()
    {
        InitializeComponent();
    }

    private async void OnBrowseFolderClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CSharpManagerViewModel vm)
        {
            await HubFilePickerActions.BrowseFolderAsync(this, vm);
        }
    }
}
