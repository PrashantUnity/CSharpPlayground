using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

public partial class CSharpEditorViewModel : ObservableObject
{
    private readonly IScriptStorageService _storageService;
    private readonly RoslynCompilerService _compilerService;
    public RoslynCompilerService CompilerService => _compilerService;
    private readonly ScriptExecutionEngine _executionEngine;
    private readonly Action _backToManagerAction;

    private CancellationTokenSource? _diagnosticsCts;
    private CancellationTokenSource? _executionCts;
    private int _globalExecutionCounter = 0;

    [ObservableProperty]
    private ScriptProjectItem _currentScript;

    [ObservableProperty]
    private string _sourceCode = string.Empty;

    [ObservableProperty]
    private bool _isNotebookMode;

    [ObservableProperty]
    private int _selectedLanguageModeIndex = 0; // 0 = Statements, 1 = Program, 2 = Expression

    [ObservableProperty]
    private bool _isExecuting;

    [ObservableProperty]
    private string _executionTimeText = string.Empty;

    [ObservableProperty]
    private string _compilerStatusText = "Ready";

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private int _warningCount;

    [ObservableProperty]
    private int _infoCount;

    [ObservableProperty]
    private string _consoleOutput = string.Empty;

    [ObservableProperty]
    private int _selectedToolTabIndex = 1; // Default to Console Output

    [ObservableProperty]
    private bool _isToolDeckExpanded = true;

    [ObservableProperty]
    private int _caretLine = 1;

    [ObservableProperty]
    private int _caretColumn = 1;

    public ObservableCollection<DiagnosticItemViewModel> Diagnostics { get; } = new();
    public ObservableCollection<AssemblyReferenceViewModel> References { get; } = new();
    public ObservableCollection<NotebookCellViewModel> NotebookCells { get; } = new();

    public ObservableCollection<string> LanguageModes { get; } = new()
    {
        "C# Statements",
        "C# Program (Main)",
        "C# Expression"
    };

    public event Action<int, int>? RequestNavigateToCaret;

    public ExecutionLanguageMode CurrentLanguageMode => SelectedLanguageModeIndex switch
    {
        1 => ExecutionLanguageMode.Program,
        2 => ExecutionLanguageMode.Expression,
        _ => ExecutionLanguageMode.Statements
    };

    public CSharpEditorViewModel(
        ScriptProjectItem script,
        IScriptStorageService storageService,
        RoslynCompilerService compilerService,
        ScriptExecutionEngine executionEngine,
        Action backToManagerAction)
    {
        _currentScript = script;
        _storageService = storageService;
        _compilerService = compilerService;
        _executionEngine = executionEngine;
        _backToManagerAction = backToManagerAction;

        _sourceCode = script.Code;
        _isNotebookMode = script.IsNotebook;
        _selectedLanguageModeIndex = script.ExecutionMode switch
        {
            "Program" => 1,
            "Expression" => 2,
            _ => 0
        };

        foreach (var r in _compilerService.AvailableReferences)
        {
            References.Add(new AssemblyReferenceViewModel(r));
        }

        PopulateNotebookCells();
        TriggerDiagnosticsCheck();
    }

    private void PopulateNotebookCells()
    {
        NotebookCells.Clear();

        if (CurrentScript.Cells.Count == 0)
        {
            CurrentScript.Cells.Add(new NotebookCellItem
            {
                Type = CellType.Code,
                Source = CurrentScript.Code
            });
        }

        foreach (var cellItem in CurrentScript.Cells)
        {
            NotebookCells.Add(CreateCellViewModel(cellItem));
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

    public void UpdateActiveScript(ScriptProjectItem script)
    {
        CurrentScript = script;
        SourceCode = script.Code;
        IsNotebookMode = script.IsNotebook;
        SelectedLanguageModeIndex = script.ExecutionMode switch
        {
            "Program" => 1,
            "Expression" => 2,
            _ => 0
        };

        ConsoleOutput = string.Empty;
        ExecutionTimeText = string.Empty;

        PopulateNotebookCells();
        TriggerDiagnosticsCheck();
    }

    partial void OnSourceCodeChanged(string value)
    {
        CurrentScript.Code = value;
        CurrentScript.LastModified = DateTime.UtcNow;
        TriggerDiagnosticsCheck();
    }

    partial void OnSelectedLanguageModeIndexChanged(int value)
    {
        CurrentScript.ExecutionMode = value switch
        {
            1 => "Program",
            2 => "Expression",
            _ => "Statements"
        };
        TriggerDiagnosticsCheck();
    }

    partial void OnIsNotebookModeChanged(bool value)
    {
        CurrentScript.IsNotebook = value;
    }

    private void TriggerDiagnosticsCheck()
    {
        _diagnosticsCts?.Cancel();
        _diagnosticsCts = new CancellationTokenSource();
        var token = _diagnosticsCts.Token;

        var codeSnapshot = SourceCode;
        var mode = CurrentLanguageMode;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(350, token);
                if (token.IsCancellationRequested) return;

                CompilerStatusText = "Analyzing...";
                var items = _compilerService.CheckDiagnostics(codeSnapshot, mode);

                if (token.IsCancellationRequested) return;

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    Diagnostics.Clear();
                    foreach (var item in items)
                    {
                        Diagnostics.Add(new DiagnosticItemViewModel(item, (l, c) =>
                        {
                            RequestNavigateToCaret?.Invoke(l, c);
                        }));
                    }

                    ErrorCount = items.Count(i => i.Severity == DiagnosticSeverity.Error);
                    WarningCount = items.Count(i => i.Severity == DiagnosticSeverity.Warning);
                    InfoCount = items.Count(i => i.Severity == DiagnosticSeverity.Info);

                    if (ErrorCount > 0)
                    {
                        CompilerStatusText = $"{ErrorCount} Error{(ErrorCount > 1 ? "s" : "")}";
                    }
                    else if (WarningCount > 0)
                    {
                        CompilerStatusText = $"{WarningCount} Warning{(WarningCount > 1 ? "s" : "")}";
                    }
                    else
                    {
                        CompilerStatusText = "Ready";
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // Expected on rapid typing
            }
        }, token);
    }

    [RelayCommand]
    private async Task RunScriptAsync()
    {
        if (IsExecuting) return;

        if (IsNotebookMode)
        {
            await RunAllCellsAsync();
            return;
        }

        SelectedToolTabIndex = 1; // Switch to Console Output
        IsToolDeckExpanded = true;
        ConsoleOutput = "🚀 Compiling script via Roslyn (.Dump enabled)...\n";
        CompilerStatusText = "Compiling...";
        IsExecuting = true;

        _executionCts?.Cancel();
        _executionCts = new CancellationTokenSource();
        var token = _executionCts.Token;

        try
        {
            var (success, bytes, diagnostics) = await Task.Run(() =>
                _compilerService.CompileToAssembly(SourceCode, CurrentLanguageMode));

            if (!success || bytes == null)
            {
                ConsoleOutput += "❌ Compilation failed. Check the Problems tab for details.\n";
                foreach (var diag in diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
                {
                    ConsoleOutput += $"  • {diag.LocationString}: {diag.Id} {diag.Message}\n";
                }
                CompilerStatusText = "Build Failed";
                SelectedToolTabIndex = 0; // Jump to Problems
                return;
            }

            ConsoleOutput += "✨ Build succeeded! Executing in-memory...\n";
            ConsoleOutput += "--------------------------------------------------\n";
            CompilerStatusText = "Running...";

            var result = await _executionEngine.ExecuteAsync(
                bytes,
                liveText =>
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        ConsoleOutput += liveText;
                    });
                },
                token);

            ConsoleOutput += "\n--------------------------------------------------\n";
            if (result.Success)
            {
                ConsoleOutput += $"✅ Execution finished in {result.Elapsed.TotalMilliseconds:N0} ms\n";
                ExecutionTimeText = $"{result.Elapsed.TotalMilliseconds:N0} ms";
                CompilerStatusText = "Completed";
                CurrentScript.ExecutionCount++;
                _ = _storageService.SaveScriptsAsync([CurrentScript]);
            }
            else if (result.WasCancelled)
            {
                ConsoleOutput += "⚠️ Execution was cancelled.\n";
                CompilerStatusText = "Cancelled";
            }
            else
            {
                ConsoleOutput += $"❌ Execution failed: {result.Error}\n";
                CompilerStatusText = "Runtime Error";
            }
        }
        finally
        {
            IsExecuting = false;
        }
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
            foreach (var cell in NotebookCells.Where(c => c.Type == CellType.Code))
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
            Source = type == CellType.Code ? "// New C# Code Block\n" : "### Markdown Notes\nWrite documentation here."
        };

        var newVm = CreateCellViewModel(newCellItem);

        if (targetCell == null)
        {
            NotebookCells.Add(newVm);
            CurrentScript.Cells.Add(newCellItem);
        }
        else
        {
            var idx = NotebookCells.IndexOf(targetCell);
            if (idx >= 0 && idx < NotebookCells.Count)
            {
                NotebookCells.Insert(idx + 1, newVm);
                CurrentScript.Cells.Insert(idx + 1, newCellItem);
            }
            else
            {
                NotebookCells.Add(newVm);
                CurrentScript.Cells.Add(newCellItem);
            }
        }
    }

    private void DeleteCell(NotebookCellViewModel cell)
    {
        NotebookCells.Remove(cell);
        CurrentScript.Cells.Remove(cell.Model);

        if (NotebookCells.Count == 0)
        {
            AddCodeCell();
        }
    }

    private void MoveCell(NotebookCellViewModel cell, int delta)
    {
        var oldIdx = NotebookCells.IndexOf(cell);
        var newIdx = oldIdx + delta;

        if (oldIdx >= 0 && newIdx >= 0 && newIdx < NotebookCells.Count)
        {
            NotebookCells.Move(oldIdx, newIdx);
            CurrentScript.Cells.RemoveAt(oldIdx);
            CurrentScript.Cells.Insert(newIdx, cell.Model);
        }
    }

    [RelayCommand]
    public void ClearAllCellOutputs()
    {
        foreach (var cell in NotebookCells)
        {
            cell.ClearOutput();
        }
    }

    [RelayCommand]
    private void StopScript()
    {
        if (!IsExecuting) return;
        _executionCts?.Cancel();
        ConsoleOutput += "\n🛑 Cancellation requested by user...\n";
    }

    [RelayCommand]
    private async Task SaveScriptAsync()
    {
        CurrentScript.Code = SourceCode;
        CurrentScript.IsNotebook = IsNotebookMode;
        CurrentScript.LastModified = DateTime.UtcNow;
        await _storageService.SaveScriptsAsync([CurrentScript]);
        CompilerStatusText = "Saved";
    }

    [RelayCommand]
    private void BackToManager()
    {
        _ = SaveScriptAsync();
        _backToManagerAction.Invoke();
    }

    [RelayCommand]
    private void ClearConsole()
    {
        ConsoleOutput = string.Empty;
    }

    [RelayCommand]
    private void SetToolTab(string index)
    {
        if (int.TryParse(index, out var idx))
        {
            SelectedToolTabIndex = idx;
            IsToolDeckExpanded = true;
        }
    }

    [RelayCommand]
    private void ToggleToolDeck()
    {
        IsToolDeckExpanded = !IsToolDeckExpanded;
    }

    public void SetCaretPosition(int line, int column)
    {
        CaretLine = line;
        CaretColumn = column;
    }
}
