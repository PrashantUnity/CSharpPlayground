using Avalonia;
using Avalonia.Controls;
using CSharpManagerViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Hub.CSharpManagerViewModel;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Hub;

public partial class HubSystemStatusPanelControl : UserControl
{
    public HubSystemStatusPanelControl()
    {
        InitializeComponent();
    }

    // The toolchain rows are filled when the Hub is on screen (in the background), and again each time it comes back,
    // since Python may have been installed meanwhile.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RefreshToolchains();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (TopLevel.GetTopLevel(this) != null) RefreshToolchains();
    }

    private void RefreshToolchains()
    {
        if (DataContext is CSharpManagerViewModel vm) _ = vm.RefreshToolchainStatusesAsync();
    }
}
