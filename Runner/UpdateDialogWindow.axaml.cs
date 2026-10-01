using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PdfEditorApp.Plugins.CSharpEditor.Runner;

public partial class UpdateDialogWindow : Window
{
    public UpdateDialogWindow()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
