using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

public partial class CSharpNotebookStudioViewModel : ObservableObject
{
    private readonly IScriptStorageService _storageService;
    private readonly RoslynCompilerService _compilerService;
    private readonly ScriptExecutionEngine _executionEngine;
    private readonly Action _backToHubAction;

    private int _globalExecutionCounter = 0;

    [ObservableProperty]
    private NotebookDocumentItem _notebook;

    [ObservableProperty]
    private bool _isExecuting;

    [ObservableProperty]
    private string _compilerStatusText = "Ready";

    public ObservableCollection<NotebookCellViewModel> Cells { get; } = new();

    public CSharpNotebookStudioViewModel(
        NotebookDocumentItem notebook,
        IScriptStorageService storageService,
        RoslynCompilerService compilerService,
        ScriptExecutionEngine executionEngine,
        Action backToHubAction)
    {
        _notebook = notebook;
        _storageService = storageService;
        _compilerService = compilerService;
        _executionEngine = executionEngine;
        _backToHubAction = backToHubAction;

        PopulateCells();
    }

    public void UpdateActiveNotebook(NotebookDocumentItem notebook)
    {
        Notebook = notebook;
        CompilerStatusText = "Ready";
        PopulateCells();
    }

    private void PopulateCells()
    {
        Cells.Clear();

        if (Notebook.Cells.Count == 0)
        {
            Notebook.Cells.Add(new NotebookCellItem
            {
                Type = CellType.Code,
                Source = "// New C# Code Cell\nConsole.WriteLine(\"Welcome to FryPDF Interactive Notebook!\");"
            });
        }

        foreach (var cellItem in Notebook.Cells)
        {
            Cells.Add(CreateCellViewModel(cellItem));
        }
    }

    private NotebookCellViewModel CreateCellViewModel(NotebookCellItem item)
    {
        return new NotebookCellViewModel(
            item,
            runAction: RunSingleCellAsync,
            deleteAction: DeleteCell,
            moveAction: MoveCell,
            addBelowAction: AddCellBelow);
    }

    [RelayCommand]
    public async Task RunSingleCellAsync(NotebookCellViewModel cell)
    {
        if (cell.Type != CellType.Code || string.IsNullOrWhiteSpace(cell.Source))
        {
            return;
        }

        cell.IsExecuting = true;
        cell.HasError = false;
        cell.OutputText = "Executing cell...\n";

        _globalExecutionCounter++;
        cell.ExecutionCount = _globalExecutionCounter;

        try
        {
            var (success, bytes, diagnostics) = await Task.Run(() =>
                _compilerService.CompileToAssembly(cell.Source, ExecutionLanguageMode.Statements));

            if (!success || bytes == null)
            {
                cell.HasError = true;
                cell.OutputText = "❌ Compilation Error:\n";
                foreach (var d in diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
                {
                    cell.OutputText += $"  Line {d.Line}: {d.Message}\n";
                }
                return;
            }

            cell.OutputText = string.Empty;
            var result = await _executionEngine.ExecuteAsync(
                bytes,
                liveText =>
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        cell.OutputText += liveText;
                    });
                });

            if (!result.Success)
            {
                cell.HasError = true;
                cell.OutputText += $"\n❌ Runtime Error: {result.Error}";
            }

            cell.ExecutionTimeText = $"{result.Elapsed.TotalMilliseconds:N0} ms";
        }
        finally
        {
            cell.IsExecuting = false;
        }
    }

    [RelayCommand]
    public async Task RunAllCellsAsync()
    {
        if (IsExecuting) return;

        IsExecuting = true;
        CompilerStatusText = "Running Notebook...";

        try
        {
            foreach (var cell in Cells.Where(c => c.Type == CellType.Code))
            {
                await RunSingleCellAsync(cell);
                if (cell.HasError)
                {
                    CompilerStatusText = "Cell Failed";
                    break;
                }
            }
            CompilerStatusText = "Notebook Finished";
        }
        finally
        {
            IsExecuting = false;
        }
    }

    [RelayCommand]
    public void AddCodeCell(NotebookCellViewModel? afterCell = null)
    {
        AddCellBelow(afterCell, CellType.Code);
    }

    [RelayCommand]
    public void AddMarkdownCell(NotebookCellViewModel? afterCell = null)
    {
        AddCellBelow(afterCell, CellType.Markdown);
    }

    private void AddCellBelow(NotebookCellViewModel? targetCell, CellType type)
    {
        var newCellItem = new NotebookCellItem
        {
            Type = type,
            Source = type == CellType.Code ? "// C# Code Block\n" : "### Markdown Notes\nWrite documentation here."
        };

        var newVm = CreateCellViewModel(newCellItem);

        if (targetCell == null)
        {
            Cells.Add(newVm);
            Notebook.Cells.Add(newCellItem);
        }
        else
        {
            var idx = Cells.IndexOf(targetCell);
            if (idx >= 0 && idx < Cells.Count)
            {
                Cells.Insert(idx + 1, newVm);
                Notebook.Cells.Insert(idx + 1, newCellItem);
            }
            else
            {
                Cells.Add(newVm);
                Notebook.Cells.Add(newCellItem);
            }
        }
    }

    private void DeleteCell(NotebookCellViewModel cell)
    {
        Cells.Remove(cell);
        Notebook.Cells.Remove(cell.Model);

        if (Cells.Count == 0)
        {
            AddCodeCell();
        }
    }

    private void MoveCell(NotebookCellViewModel cell, int delta)
    {
        var oldIdx = Cells.IndexOf(cell);
        var newIdx = oldIdx + delta;

        if (oldIdx >= 0 && newIdx >= 0 && newIdx < Cells.Count)
        {
            Cells.Move(oldIdx, newIdx);
            Notebook.Cells.RemoveAt(oldIdx);
            Notebook.Cells.Insert(newIdx, cell.Model);
        }
    }

    [RelayCommand]
    public void ClearAllOutputs()
    {
        foreach (var cell in Cells)
        {
            cell.ClearOutput();
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        Notebook.LastModified = DateTime.UtcNow;
        await _storageService.SaveNotebookAsync(Notebook);
        CompilerStatusText = "Saved";
    }

    [RelayCommand]
    public void BackToHub()
    {
        _ = SaveAsync();
        _backToHubAction.Invoke();
    }
}
