using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

public partial class CSharpNotebookStudioViewModel : ObservableObject
{
    private readonly IScriptStorageService _storageService;
    private readonly RoslynCompilerService _compilerService;
    private readonly ScriptExecutionEngine _executionEngine;
    private readonly NotebookExecutionKernel _kernel;
    private readonly Action _backToHubAction;
    private readonly Action? _backToHomeAction;

    private int _globalExecutionCounter = 0;

    [ObservableProperty]
    private NotebookDocumentItem _notebook;

    [ObservableProperty]
    private bool _isExecuting;

    [ObservableProperty]
    private string _compilerStatusText = "Kernel Ready";

    [ObservableProperty]
    private bool _isVariableInspectorOpen = false;

    public ObservableCollection<NotebookCellViewModel> Cells { get; } = new();
    public ObservableCollection<NotebookVariableInfo> Variables { get; } = new();

    public CSharpNotebookStudioViewModel(
        NotebookDocumentItem notebook,
        IScriptStorageService storageService,
        RoslynCompilerService compilerService,
        ScriptExecutionEngine executionEngine,
        Action backToHubAction,
        Action? backToHomeAction = null)
    {
        _notebook = notebook;
        _storageService = storageService;
        _compilerService = compilerService;
        _executionEngine = executionEngine;
        _kernel = new NotebookExecutionKernel();
        _backToHubAction = backToHubAction;
        _backToHomeAction = backToHomeAction;

        PopulateCells();
    }

    public void UpdateActiveNotebook(NotebookDocumentItem notebook)
    {
        Notebook = notebook;
        CompilerStatusText = "Kernel Ready";
        _kernel.ResetSession();
        Variables.Clear();
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
                Source = "// 1. Variable Sharing\nvar m = 10;\nConsole.WriteLine($\"Variable m initialized to: {m}\");"
            });

            Notebook.Cells.Add(new NotebookCellItem
            {
                Type = CellType.Code,
                Source = "// 2. Read from previous cell!\nConsole.WriteLine($\"Reading m from previous cell: {m}\");\nm = m * 5;\nConsole.WriteLine($\"Updated m to: {m}\");"
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
        cell.ClearOutput();

        _globalExecutionCounter++;
        cell.ExecutionCount = _globalExecutionCounter;
        CompilerStatusText = $"Executing Cell [{cell.ExecutionCount}]...";

        try
        {
            var result = await _kernel.ExecuteCellAsync(
                cell.Source,
                onLiveConsole: text =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        cell.OutputText += text;
                    });
                },
                onRichOutput: rich =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        switch (rich.Kind)
                        {
                            case CellOutputKind.Image:
                                if (rich.ImageBytes != null)
                                {
                                    cell.SetImageOutput(rich.ImageBytes, rich.ImageFormat ?? "PNG", rich.ImageWidth, rich.ImageHeight);
                                }
                                break;
                            case CellOutputKind.Control:
                                if (rich.InteractiveControl != null)
                                {
                                    cell.SetInteractiveControl(rich.InteractiveControl);
                                }
                                break;
                            case CellOutputKind.Html:
                                if (!string.IsNullOrEmpty(rich.HtmlContent))
                                {
                                    cell.SetHtmlContent(rich.HtmlContent);
                                }
                                break;
                            case CellOutputKind.Table:
                                if (rich.TableResult != null)
                                {
                                    cell.SetTableOutput(rich.TableResult);
                                }
                                break;
                        }
                    });
                });

            if (!result.Success)
            {
                cell.HasError = true;
            }

            cell.ExecutionTimeText = $"{result.Elapsed.TotalMilliseconds:N0} ms";

            // Update live variables in the inspector
            UpdateVariables();

            CompilerStatusText = result.Success
                ? $"Kernel Ready • {Variables.Count} active variable{(Variables.Count == 1 ? "" : "s")}"
                : "Execution Failed";
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
        CompilerStatusText = "Restarting Kernel & Running Notebook...";

        try
        {
            // Reset state to ensure fresh linear run
            _kernel.ResetSession();
            Variables.Clear();

            foreach (var cell in Cells.Where(c => c.Type == CellType.Code))
            {
                await RunSingleCellAsync(cell);
                if (cell.HasError)
                {
                    CompilerStatusText = "Notebook execution stopped due to error";
                    break;
                }
            }

            if (Cells.All(c => !c.HasError))
            {
                CompilerStatusText = $"Notebook Finished • {Variables.Count} active variable{(Variables.Count == 1 ? "" : "s")}";
            }
        }
        finally
        {
            IsExecuting = false;
        }
    }

    [RelayCommand]
    public void RestartKernel()
    {
        _kernel.ResetSession();
        Variables.Clear();
        CompilerStatusText = "Kernel Restarted • Session Fresh";
    }

    [RelayCommand]
    public void ToggleVariableInspector()
    {
        IsVariableInspectorOpen = !IsVariableInspectorOpen;
        if (IsVariableInspectorOpen)
        {
            UpdateVariables();
        }
    }

    private void UpdateVariables()
    {
        var active = _kernel.GetActiveVariables();
        Dispatcher.UIThread.Post(() =>
        {
            Variables.Clear();
            foreach (var v in active)
            {
                Variables.Add(v);
            }
            OnPropertyChanged(nameof(Variables));
        });
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

    [RelayCommand]
    public void BackToHome()
    {
        _ = SaveAsync();
        _backToHomeAction?.Invoke();
    }
}
