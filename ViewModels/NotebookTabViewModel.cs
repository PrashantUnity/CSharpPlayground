using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

public partial class NotebookTabViewModel : ObservableObject
{
    private readonly Action<NotebookTabViewModel>? _onSelectTab;
    private readonly Action<NotebookTabViewModel>? _onCloseTab;
    private readonly Func<int> _getTimeoutSeconds;
    private CancellationTokenSource? _executionCts;

    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _title = "Untitled.frynb";

    [ObservableProperty]
    private string _folderName = "Library";

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isModified;

    [ObservableProperty]
    private NotebookDocumentItem _notebook;

    [ObservableProperty]
    private NotebookCellViewModel? _activeCell;

    [ObservableProperty]
    private bool _isExecuting;

    [ObservableProperty]
    private string _kernelName = ".NET (C#)";

    [ObservableProperty]
    private string _kernelStatusText = "Kernel Ready";

    public NotebookExecutionKernel Kernel { get; }

    public ObservableCollection<NotebookCellViewModel> Cells { get; } = new();

    public ObservableCollection<NotebookVariableInfo> Variables { get; } = new();

    public string IconKind => "NotebookOutline";

    public string IconColor => "#D97706";

    public int ExecutionCounter { get; set; } = 0;

    public string BreadcrumbFolder => string.IsNullOrWhiteSpace(FolderName) ? "Library" : FolderName;

    public string BreadcrumbDocument => string.IsNullOrWhiteSpace(Title) ? "Untitled.frynb" : Title;

    public string ActiveCellBadgeText
    {
        get
        {
            if (ActiveCell == null) return "Notebook Root";

            if (ActiveCell.Type == CellType.Markdown)
            {
                var line = (ActiveCell.Source ?? string.Empty)
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault()?.Trim() ?? string.Empty;

                line = line.TrimStart('#').Trim();
                if (line.Length > 36) line = line.Substring(0, 33) + "...";
                return string.IsNullOrWhiteSpace(line) ? "Markdown Cell" : $"Markdown: {line}";
            }
            else
            {
                var count = ActiveCell.ExecutionCount.HasValue ? $"[{ActiveCell.ExecutionCount}]" : "[*]";
                var line = (ActiveCell.Source ?? string.Empty)
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault()?.Trim() ?? string.Empty;

                if (line.Length > 32) line = line.Substring(0, 29) + "...";
                return string.IsNullOrWhiteSpace(line) ? $"Cell {count}" : $"Cell {count}: {line}";
            }
        }
    }

    public string ActiveCellTypeIcon => (ActiveCell?.Type == CellType.Markdown) ? "FormatHeaderPound" : "CodeBraces";

    public string ActiveCellTypeColor => (ActiveCell?.Type == CellType.Markdown) ? "#4EC9B0" : "#58A6FF";

    public NotebookTabViewModel(
        NotebookDocumentItem notebook,
        string folderName = "Library",
        string filePath = "",
        Action<NotebookTabViewModel>? onSelectTab = null,
        Action<NotebookTabViewModel>? onCloseTab = null,
        Func<int>? getTimeoutSeconds = null)
    {
        _notebook = notebook;
        _folderName = folderName;
        _filePath = filePath;
        _title = notebook.Title.EndsWith(".frynb", StringComparison.OrdinalIgnoreCase)
            ? notebook.Title
            : $"{notebook.Title}.frynb";

        _onSelectTab = onSelectTab;
        _onCloseTab = onCloseTab;
        _getTimeoutSeconds = getTimeoutSeconds ?? (() => 10);

        Kernel = new NotebookExecutionKernel();

        PopulateCells();
    }

    partial void OnActiveCellChanged(NotebookCellViewModel? value)
    {
        OnPropertyChanged(nameof(ActiveCellBadgeText));
        OnPropertyChanged(nameof(ActiveCellTypeIcon));
        OnPropertyChanged(nameof(ActiveCellTypeColor));
    }

    partial void OnTitleChanged(string value)
    {
        OnPropertyChanged(nameof(BreadcrumbDocument));
    }

    partial void OnFolderNameChanged(string value)
    {
        OnPropertyChanged(nameof(BreadcrumbFolder));
    }

    [RelayCommand]
    public void SelectTab()
    {
        _onSelectTab?.Invoke(this);
    }

    [RelayCommand]
    public void CloseTab()
    {
        _onCloseTab?.Invoke(this);
    }

    public Action<NotebookTabViewModel>? OnCloseOthers { get; set; }
    public Action<NotebookTabViewModel>? OnCloseToTheRight { get; set; }
    public Action<NotebookTabViewModel>? OnCloseAll { get; set; }
    public Action<NotebookTabViewModel>? OnCopyPath { get; set; }
    public Action<NotebookTabViewModel>? OnRevealInExplorer { get; set; }

    [RelayCommand]
    public void CloseOthers() => OnCloseOthers?.Invoke(this);

    [RelayCommand]
    public void CloseToTheRight() => OnCloseToTheRight?.Invoke(this);

    [RelayCommand]
    public void CloseAll() => OnCloseAll?.Invoke(this);

    [RelayCommand]
    public void CopyPath() => OnCopyPath?.Invoke(this);

    [RelayCommand]
    public void RevealInExplorer() => OnRevealInExplorer?.Invoke(this);

    public void PopulateCells()
    {
        Cells.Clear();

        if (Notebook.Cells.Count == 0)
        {
            Notebook.Cells.Add(new NotebookCellItem
            {
                Type = CellType.Code,
                Source = "using System;"
            });

            Notebook.Cells.Add(new NotebookCellItem
            {
                Type = CellType.Code,
                Source = "Console.WriteLine(\"Welcome to FryPDF Interactive Notebook!\");"
            });

            Notebook.Cells.Add(new NotebookCellItem
            {
                Type = CellType.Code,
                Source = "var list = new List<int>()\n{\n    1,3,4,5,6,7,8,10\n};\nlist"
            });

            Notebook.Cells.Add(new NotebookCellItem
            {
                Type = CellType.Code,
                Source = "public class People\n{\n    public string Name { get; set; } = string.Empty;\n    public string Class { get; set; } = string.Empty;\n    public int[] Numbers { get; set; } = [1, 34, 45, 235, 25];\n    public People? Another { get; set; }\n    public void WhoAreYou()\n    {\n        Console.WriteLine($\"My name is {Name}\");\n    }\n}"
            });

            Notebook.Cells.Add(new NotebookCellItem
            {
                Type = CellType.Code,
                Source = "var people = new People();\npeople"
            });

            Notebook.Cells.Add(new NotebookCellItem
            {
                Type = CellType.Code,
                Source = "people.Name = \"Code\";\npeople.Class = \"II\";\npeople.Another = people;\n\npeople"
            });
        }

        foreach (var cellItem in Notebook.Cells)
        {
            Cells.Add(CreateCellViewModel(cellItem));
        }

        if (Cells.Count > 0)
        {
            SelectCell(Cells[0]);
        }
    }

    public NotebookCellViewModel CreateCellViewModel(NotebookCellItem item)
    {
        return new NotebookCellViewModel(
            item,
            runAction: RunSingleCellAsync,
            deleteAction: DeleteCell,
            moveAction: MoveCell,
            addBelowAction: AddCellBelow,
            runAndSelectNextAction: RunCellAndSelectNextAsync);
    }

    [RelayCommand]
    public void InterruptExecution()
    {
        _executionCts?.Cancel();
        KernelStatusText = "Interrupting...";
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

        ExecutionCounter++;
        cell.ExecutionCount = ExecutionCounter;
        KernelStatusText = $"Executing Cell [{cell.ExecutionCount}]...";

        _executionCts?.Cancel();
        _executionCts?.Dispose();
        _executionCts = new CancellationTokenSource();
        var executionCts = _executionCts;

        var timeoutSeconds = Math.Max(1, _getTimeoutSeconds());
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(executionCts.Token, timeoutCts.Token);

        try
        {
            var result = await Kernel.ExecuteCellAsync(
                cell.Source,
                ct: linkedCts.Token,
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
                            case CellOutputKind.ObjectInspector:
                                if (rich.InspectorNode != null)
                                {
                                    cell.SetInspectorOutput(rich.InspectorNode);
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

            UpdateVariables();

            if (result.WasCancelled)
            {
                KernelStatusText = timeoutCts.IsCancellationRequested
                    ? $"⏱️ Cell timed out after {timeoutSeconds}s"
                    : "🛑 Cell execution interrupted";
            }
            else
            {
                KernelStatusText = result.Success
                    ? $"Kernel Ready • {Variables.Count} active variable{(Variables.Count == 1 ? "" : "s")}"
                    : "Execution Failed";
            }
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
        KernelStatusText = "Restarting Kernel & Running Notebook...";

        try
        {
            Kernel.ResetSession();
            Variables.Clear();

            foreach (var cell in Cells.Where(c => c.Type == CellType.Code))
            {
                await RunSingleCellAsync(cell);
                if (cell.HasError)
                {
                    KernelStatusText = "Notebook execution stopped due to error";
                    break;
                }
            }

            if (Cells.All(c => !c.HasError))
            {
                KernelStatusText = $"Notebook Finished • {Variables.Count} active variable{(Variables.Count == 1 ? "" : "s")}";
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
        _executionCts?.Cancel();
        Kernel.ResetSession();
        Variables.Clear();
        KernelStatusText = "Kernel Restarted • Session Fresh";
    }

    public void UpdateVariables()
    {
        var active = Kernel.GetActiveVariables();
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
    public void SelectCell(NotebookCellViewModel? cell)
    {
        if (cell == null) return;
        foreach (var c in Cells)
        {
            c.IsSelected = (c == cell);
        }
        ActiveCell = cell;
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

    public void AddCellAbove(NotebookCellViewModel? targetCell, CellType type)
    {
        var newCellItem = new NotebookCellItem
        {
            Type = type,
            Source = type == CellType.Code ? "// C# Code Block\n" : "### Markdown Notes\nWrite documentation here."
        };
        var newVm = CreateCellViewModel(newCellItem);

        if (targetCell == null || Cells.Count == 0)
        {
            Cells.Insert(0, newVm);
            Notebook.Cells.Insert(0, newCellItem);
        }
        else
        {
            var idx = Cells.IndexOf(targetCell);
            if (idx >= 0)
            {
                Cells.Insert(idx, newVm);
                Notebook.Cells.Insert(idx, newCellItem);
            }
            else
            {
                Cells.Insert(0, newVm);
                Notebook.Cells.Insert(0, newCellItem);
            }
        }

        IsModified = true;
        SelectCell(newVm);
    }

    [RelayCommand]
    public async Task RunCellAndSelectNextAsync(NotebookCellViewModel? targetCell = null)
    {
        var cell = targetCell ?? ActiveCell ?? Cells.FirstOrDefault();
        if (cell == null) return;

        await RunSingleCellAsync(cell);

        var idx = Cells.IndexOf(cell);
        if (idx >= 0 && idx < Cells.Count - 1)
        {
            SelectCell(Cells[idx + 1]);
        }
        else
        {
            AddCodeCell(cell);
        }
    }

    public void AddCellBelow(NotebookCellViewModel? targetCell, CellType type)
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

        IsModified = true;
        SelectCell(newVm);
    }

    public void DeleteCell(NotebookCellViewModel cell)
    {
        cell.DisposeLiveResources();
        Cells.Remove(cell);
        Notebook.Cells.Remove(cell.Model);

        if (Cells.Count == 0)
        {
            AddCodeCell();
        }

        IsModified = true;
        if (ActiveCell == cell)
        {
            SelectCell(Cells.LastOrDefault());
        }
    }

    public void DisposeAllCellResources()
    {
        foreach (var cell in Cells)
        {
            cell.DisposeLiveResources();
        }
    }

    public void MoveCell(NotebookCellViewModel cell, int delta)
    {
        var oldIdx = Cells.IndexOf(cell);
        var newIdx = oldIdx + delta;

        if (oldIdx >= 0 && newIdx >= 0 && newIdx < Cells.Count)
        {
            Cells.Move(oldIdx, newIdx);
            Notebook.Cells.RemoveAt(oldIdx);
            Notebook.Cells.Insert(newIdx, cell.Model);
            IsModified = true;
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
    public void CollapseAllInputs()
    {
        foreach (var cell in Cells)
        {
            cell.IsInputCollapsed = true;
        }
        IsModified = true;
    }

    [RelayCommand]
    public void ExpandAllInputs()
    {
        foreach (var cell in Cells)
        {
            cell.IsInputCollapsed = false;
        }
        IsModified = true;
    }

    [RelayCommand]
    public void CollapseAllOutputs()
    {
        foreach (var cell in Cells)
        {
            if (cell.HasOutput)
            {
                cell.IsOutputCollapsed = true;
            }
        }
        IsModified = true;
    }

    [RelayCommand]
    public void ExpandAllOutputs()
    {
        foreach (var cell in Cells)
        {
            cell.IsOutputCollapsed = false;
        }
        IsModified = true;
    }

    [RelayCommand]
    public void CollapseAllCells()
    {
        foreach (var cell in Cells)
        {
            cell.IsInputCollapsed = true;
            if (cell.HasOutput)
            {
                cell.IsOutputCollapsed = true;
            }
        }
        IsModified = true;
    }

    [RelayCommand]
    public void ExpandAllCells()
    {
        foreach (var cell in Cells)
        {
            cell.IsInputCollapsed = false;
            cell.IsOutputCollapsed = false;
        }
        IsModified = true;
    }

    [RelayCommand]
    public void FoldAllCodeBlocks()
    {
        foreach (var cell in Cells.Where(c => c.IsCodeCell))
        {
            cell.FoldAllCode();
        }
    }

    [RelayCommand]
    public void UnfoldAllCodeBlocks()
    {
        foreach (var cell in Cells.Where(c => c.IsCodeCell))
        {
            cell.UnfoldAllCode();
        }
    }

    [RelayCommand]
    public void FormatAllCodeCells()
    {
        foreach (var cell in Cells.Where(c => c.IsCodeCell))
        {
            cell.FormatCode();
        }
    }
}
