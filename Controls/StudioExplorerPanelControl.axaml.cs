using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls;

public partial class StudioExplorerPanelControl : UserControl
{
    public event EventHandler<RoutedEventArgs>? OpenProjectRequested;

    public StudioExplorerPanelControl()
    {
        InitializeComponent();
    }

    private void OnRenameTextBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is ExplorerItemViewModel vm && vm.IsRenaming)
        {
            vm.CommitRename();
        }
    }

    private void OnOpenProjectMenuClick(object? sender, RoutedEventArgs e)
    {
        OpenProjectRequested?.Invoke(this, e);
    }
}
