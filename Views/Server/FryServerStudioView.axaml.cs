using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Server;

namespace PdfEditorApp.Plugins.CSharpEditor.Views.Server;

public partial class FryServerStudioView : UserControl
{
    public FryServerStudioView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not FryServerStudioViewModel vm) return;

        var isCmdOrCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);

        // VS Code Shortcut: Ctrl+B / Cmd+B -> Toggle Primary SideBar
        if (isCmdOrCtrl && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.B)
        {
            vm.ToggleSideBarCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // VS Code Shortcut: Ctrl+J / Cmd+J -> Toggle Bottom Panel
        if (isCmdOrCtrl && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.J)
        {
            vm.ToggleBottomPanelCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // VS Code Shortcut: Ctrl+S / Cmd+S -> Save Document
        if (isCmdOrCtrl && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.S)
        {
            vm.SaveDocumentCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // F5 -> Start Server
        if (e.Key == Key.F5 && !isCmdOrCtrl && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            vm.StartServerCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Shift+F5 -> Stop Server
        if (e.Key == Key.F5 && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            vm.StopServerCommand.Execute(null);
            e.Handled = true;
            return;
        }
    }
}
