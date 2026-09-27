using Avalonia.Controls;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;

namespace PdfEditorApp.Plugins.CSharpEditor.Views;

public partial class CSharpBlindProblemsView : UserControl
{
    public CSharpBlindProblemsView()
    {
        InitializeComponent();
        SizeChanged += OnSizeChanged;
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is CSharpBlindProblemsViewModel vm && e.NewSize.Width > 0)
        {
            vm.UpdateAdaptiveFlyoutWidth(e.NewSize.Width);
        }
    }

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is CSharpBlindProblemsViewModel vm && Bounds.Width > 0)
        {
            vm.UpdateAdaptiveFlyoutWidth(Bounds.Width);
        }
    }
}
