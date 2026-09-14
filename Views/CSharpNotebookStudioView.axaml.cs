using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;

namespace PdfEditorApp.Plugins.CSharpEditor.Views;

public partial class CSharpNotebookStudioView : UserControl
{
    public CSharpNotebookStudioView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not CSharpNotebookStudioViewModel vm) return;

        // Enter / Escape during inline rename in explorer
        if (e.Source is TextBox tb && tb.DataContext is ExplorerItemViewModel itemVm && itemVm.IsRenaming)
        {
            if (e.Key == Key.Enter)
            {
                itemVm.CommitRenameCommand.Execute(null);
                e.Handled = true;
                return;
            }
            else if (e.Key == Key.Escape)
            {
                itemVm.CancelRenameCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        bool isCmdOrCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);

        // Ctrl+Shift+Enter: Run All Cells
        if (isCmdOrCtrl && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.Enter)
        {
            _ = vm.RunAllCellsAsync();
            e.Handled = true;
            return;
        }

        // Ctrl+S / Cmd+S: Save Notebook
        if (isCmdOrCtrl && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.S)
        {
            _ = vm.SaveAsync();
            e.Handled = true;
            return;
        }

        // Ctrl+Shift+B: Add Code Cell Below
        if (isCmdOrCtrl && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.B)
        {
            vm.AddCodeCell(vm.ActiveTab?.ActiveCell);
            e.Handled = true;
            return;
        }

        // Ctrl+Shift+A: Add Code Cell Above
        if (isCmdOrCtrl && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.A)
        {
            vm.AddCellAbove();
            e.Handled = true;
            return;
        }

        // Ctrl+Shift+D: Delete Active Cell
        if (isCmdOrCtrl && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.D)
        {
            vm.DeleteActiveCell();
            e.Handled = true;
            return;
        }

        // F5: Run All Cells
        if (e.Key == Key.F5 && !e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            _ = vm.RunAllCellsAsync();
            e.Handled = true;
            return;
        }
    }
}
