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
        SizeChanged += OnSizeChanged;
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is CSharpDocsViewModel vm && e.NewSize.Width > 0)
        {
            vm.IsOutlineVisible = e.NewSize.Width >= 1400;
        }
    }

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is CSharpDocsViewModel vm && Bounds.Width > 0)
        {
            vm.IsOutlineVisible = Bounds.Width >= 1400;
        }
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
