using Avalonia.Controls;
using Avalonia.Interactivity;
using PdfEditorApp.Plugins.CSharpEditor.Views;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls;

public partial class HubWelcomeSectionControl : UserControl
{
    public HubWelcomeSectionControl()
    {
        InitializeComponent();
    }

    private async void OnOpenProjectFileClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CSharpManagerViewModel vm)
        {
            await HubFilePickerActions.OpenProjectFileAsync(this, vm);
        }
    }

    private async void OnOpenProjectFolderClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CSharpManagerViewModel vm)
        {
            await HubFilePickerActions.OpenProjectFolderAsync(this, vm);
        }
    }
}
