using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.Notebooks;

public partial class CSharpNotebookStudioViewModel
{
    // ── Cell execution commands ───────────────────────────────────────────────

    [RelayCommand]
    public async Task RunSingleCellAsync(NotebookCellViewModel cell)
    {
        if (ActiveTab != null)
        {
            await ActiveTab.RunSingleCellAsync(cell);
        }
    }

    [RelayCommand]
    public async Task RunCellAndSelectNextAsync(NotebookCellViewModel? cell = null)
    {
        if (ActiveTab != null)
        {
            await ActiveTab.RunCellAndSelectNextAsync(cell);
        }
    }

    [RelayCommand]
    public async Task RunAllCellsAsync()
    {
        if (ActiveTab != null)
        {
            await ActiveTab.RunAllCellsAsync();
        }
    }

    [RelayCommand]
    public void RestartKernel()
    {
        ActiveTab?.RestartKernel();
    }

    [RelayCommand]
    public void InterruptExecution()
    {
        ActiveTab?.InterruptExecution();
    }

    // ── Cell management commands ──────────────────────────────────────────────

    [RelayCommand]
    public void SelectCell(NotebookCellViewModel? cell)
    {
        ActiveTab?.SelectCell(cell);
    }

    [RelayCommand]
    public void AddCodeCell(NotebookCellViewModel? afterCell = null)
    {
        ActiveTab?.AddCodeCell(afterCell);
    }

    [RelayCommand]
    public void AddMarkdownCell(NotebookCellViewModel? afterCell = null)
    {
        ActiveTab?.AddMarkdownCell(afterCell);
    }

    [RelayCommand]
    public void InsertCodeCellAfter(NotebookCellViewModel? cell)
    {
        ActiveTab?.AddCodeCell(cell);
    }

    [RelayCommand]
    public void InsertMarkdownCellAfter(NotebookCellViewModel? cell)
    {
        ActiveTab?.AddMarkdownCell(cell);
    }

    [RelayCommand]
    public void AddCellAbove()
    {
        ActiveTab?.AddCellAbove(ActiveTab.ActiveCell, CellType.Code);
    }

    [RelayCommand]
    public void DeleteActiveCell()
    {
        if (ActiveTab?.ActiveCell != null)
        {
            ActiveTab.DeleteCell(ActiveTab.ActiveCell);
        }
    }

    [RelayCommand]
    public void SelectCellFromOutline(NotebookCellViewModel? cell)
    {
        if (cell == null || ActiveTab == null) return;
        ActiveTab.SelectCell(cell);
        RequestScrollToCell?.Invoke(cell);
    }

    // ── Cell visibility commands ──────────────────────────────────────────────

    [RelayCommand]
    public void ClearAllOutputs()
    {
        ActiveTab?.ClearAllOutputs();
    }

    [RelayCommand]
    public void CollapseAllInputs()
    {
        ActiveTab?.CollapseAllInputs();
    }

    [RelayCommand]
    public void ExpandAllInputs()
    {
        ActiveTab?.ExpandAllInputs();
    }

    [RelayCommand]
    public void CollapseAllOutputs()
    {
        ActiveTab?.CollapseAllOutputs();
    }

    [RelayCommand]
    public void ExpandAllOutputs()
    {
        ActiveTab?.ExpandAllOutputs();
    }

    [RelayCommand]
    public void CollapseAllCells()
    {
        ActiveTab?.CollapseAllCells();
    }

    [RelayCommand]
    public void ExpandAllCells()
    {
        ActiveTab?.ExpandAllCells();
    }

    [RelayCommand]
    public void FoldAllCodeBlocks()
    {
        ActiveTab?.FoldAllCodeBlocks();
    }

    [RelayCommand]
    public void UnfoldAllCodeBlocks()
    {
        ActiveTab?.UnfoldAllCodeBlocks();
    }

    [RelayCommand]
    public void FormatAllCodeCells()
    {
        ActiveTab?.FormatAllCodeCells();
    }

    // ── Save ─────────────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (ActiveTab == null) return;

        // An ephemeral notebook must be saved even when not yet modified (user pressed Ctrl+S on a fresh tab).
        if (ActiveTab.IsModified || ActiveTab.Notebook.IsEphemeral)
        {
            ActiveTab.Notebook.IsEphemeral = false;
            ActiveTab.Notebook.LastModified = DateTime.UtcNow;
            var saved = await _storageService.SaveNotebookAsync(ActiveTab.Notebook);
            ActiveTab.IsModified = !saved;
            CompilerStatusText = saved ? "Saved" : "⚠️ Save failed — check disk space/permissions";
        }
    }
}
