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

    [ObservableProperty]
    private bool _isOutlineOpen = false;

    [ObservableProperty]
    private NotebookCellViewModel? _activeCell;

    [ObservableProperty]
    private string _kernelName = ".NET (C#)";

    [ObservableProperty]
    private string _workspaceName = "SKIASHARP";

    [ObservableProperty]
    private bool _isWorkspaceExpanded = true;

    public string WorkspaceExpansionArrow => IsWorkspaceExpanded ? "⌵" : ">";

    partial void OnIsWorkspaceExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(WorkspaceExpansionArrow));
    }

    [ObservableProperty]
    private bool _isExplorerOpen = true;

    [ObservableProperty]
    private bool _isOutlineExpanded = false;

    [ObservableProperty]
    private bool _isTimelineExpanded = false;

    public ObservableCollection<NotebookCellViewModel> Cells { get; } = new();
    public ObservableCollection<NotebookVariableInfo> Variables { get; } = new();
    public ObservableCollection<ExplorerItemViewModel> ExplorerRootItems { get; } = new();

    public string BreadcrumbText
    {
        get
        {
            var title = string.IsNullOrWhiteSpace(Notebook?.Title) ? "codefrydev.frynb" : Notebook.Title;
            if (!title.EndsWith(".frynb", StringComparison.OrdinalIgnoreCase))
            {
                title += ".frynb";
            }
            if (ActiveCell != null && !string.IsNullOrWhiteSpace(ActiveCell.Source))
            {
                var firstLine = ActiveCell.Source.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? string.Empty;
                if (firstLine.Length > 40) firstLine = firstLine.Substring(0, 37) + "...";
                return $"Code > {title} > C# {firstLine}";
            }
            return $"Code > {title}";
        }
    }

    public string DocumentTabTitle
    {
        get
        {
            var title = string.IsNullOrWhiteSpace(Notebook?.Title) ? "codefrydev.frynb" : Notebook.Title;
            if (!title.EndsWith(".frynb", StringComparison.OrdinalIgnoreCase))
            {
                title += ".frynb";
            }
            return title;
        }
    }

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
        PopulateExplorerTree();
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
                Source = "using System;"
            });

            Notebook.Cells.Add(new NotebookCellItem
            {
                Type = CellType.Code,
                Source = "Console.WriteLine(\"test is test\");"
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
    public void SelectCell(NotebookCellViewModel? cell)
    {
        if (cell == null) return;
        foreach (var c in Cells)
        {
            c.IsSelected = (c == cell);
        }
        ActiveCell = cell;
        OnPropertyChanged(nameof(BreadcrumbText));
    }

    [RelayCommand]
    public void ToggleOutline()
    {
        IsOutlineOpen = !IsOutlineOpen;
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

    [RelayCommand]
    public void ToggleWorkspaceExpand()
    {
        IsWorkspaceExpanded = !IsWorkspaceExpanded;
    }

    [RelayCommand]
    public void ToggleExplorer()
    {
        IsExplorerOpen = !IsExplorerOpen;
    }

    [RelayCommand]
    public void ToggleOutlineExpanded()
    {
        IsOutlineExpanded = !IsOutlineExpanded;
        IsOutlineOpen = IsOutlineExpanded;
    }

    [RelayCommand]
    public void ToggleTimelineExpanded()
    {
        IsTimelineExpanded = !IsTimelineExpanded;
    }

    [RelayCommand]
    public void DeleteSelectedExplorerItem()
    {
        var selected = FindSelectedItem(ExplorerRootItems);
        if (selected != null)
        {
            DeleteExplorerItem(selected);
        }
    }

    [RelayCommand]
    public void DeleteExplorerItem(ExplorerItemViewModel item)
    {
        if (item == null) return;

        if (item.Parent != null)
        {
            item.Parent.Children.Remove(item);
        }
        else
        {
            ExplorerRootItems.Remove(item);
        }

        if (item.IsSelected)
        {
            var nextFile = FindFirstFile(ExplorerRootItems);
            if (nextFile != null)
            {
                OnExplorerItemClicked(nextFile);
            }
            else
            {
                Notebook.Title = "Untitled";
                OnPropertyChanged(nameof(BreadcrumbText));
            }
        }
    }

    [RelayCommand]
    public void NewFile()
    {
        var selected = FindSelectedItem(ExplorerRootItems);
        var targetFolder = (selected != null && selected.IsDirectory) ? selected : 
            (selected?.Parent ?? ExplorerRootItems.FirstOrDefault(x => x.Name == "Code") ?? ExplorerRootItems.FirstOrDefault(x => x.IsDirectory));

        if (targetFolder != null)
        {
            NewFileUnderItem(targetFolder);
        }
        else
        {
            var newFile = new ExplorerItemViewModel
            {
                Name = $"Notebook_{DateTime.Now:HHmmss}.frynb",
                IsDirectory = false,
                FileExtension = ".frynb",
                OnItemClicked = OnExplorerItemClicked,
                OnDeleteRequested = DeleteExplorerItem,
                OnNewFileRequested = NewFileUnderItem,
                OnNewFolderRequested = NewFolderUnderItem,
                OnRenameCommitted = OnItemRenamed
            };
            ExplorerRootItems.Add(newFile);
            OnExplorerItemClicked(newFile);
            newFile.StartRename();
        }
    }

    [RelayCommand]
    public void NewFileUnderItem(ExplorerItemViewModel target)
    {
        var folder = target.IsDirectory ? target : (target.Parent ?? target);
        var newFile = new ExplorerItemViewModel
        {
            Name = $"Notebook_{DateTime.Now:HHmmss}.frynb",
            IsDirectory = false,
            FileExtension = ".frynb",
            Parent = folder,
            OnItemClicked = OnExplorerItemClicked,
            OnDeleteRequested = DeleteExplorerItem,
            OnNewFileRequested = NewFileUnderItem,
            OnNewFolderRequested = NewFolderUnderItem,
            OnRenameCommitted = OnItemRenamed
        };

        folder.IsExpanded = true;
        folder.Children.Add(newFile);
        OnExplorerItemClicked(newFile);
        newFile.StartRename();
    }

    [RelayCommand]
    public void NewFolder()
    {
        var selected = FindSelectedItem(ExplorerRootItems);
        var targetFolder = (selected != null && selected.IsDirectory) ? selected : (selected?.Parent);

        if (targetFolder != null)
        {
            NewFolderUnderItem(targetFolder);
        }
        else
        {
            var newFolder = CreateFolderItem($"folder_{DateTime.Now:HHmmss}", isExpanded: true);
            ExplorerRootItems.Add(newFolder);
            newFolder.StartRename();
        }
    }

    [RelayCommand]
    public void NewFolderUnderItem(ExplorerItemViewModel target)
    {
        var folder = target.IsDirectory ? target : (target.Parent ?? target);
        var newFolder = CreateFolderItem($"folder_{DateTime.Now:HHmmss}", isExpanded: true, parent: folder);
        folder.IsExpanded = true;
        folder.Children.Add(newFolder);
        newFolder.StartRename();
    }

    [RelayCommand]
    public void RefreshExplorer()
    {
        PopulateExplorerTree();
    }

    [RelayCommand]
    public void CollapseAllExplorer()
    {
        foreach (var item in ExplorerRootItems)
        {
            CollapseItemRecursive(item);
        }
    }

    private void CollapseItemRecursive(ExplorerItemViewModel item)
    {
        if (item.IsDirectory)
        {
            item.IsExpanded = false;
            foreach (var child in item.Children)
            {
                CollapseItemRecursive(child);
            }
        }
    }

    public void PopulateExplorerTree()
    {
        ExplorerRootItems.Clear();

        var assets = CreateFolderItem("assets", isExpanded: false);
        var code = CreateFolderItem("Code", isExpanded: true);

        var docNb = new ExplorerItemViewModel
        {
            Name = "Document Automation Notebook.frynb",
            IsDirectory = false,
            FileExtension = ".frynb",
            IsSelected = true,
            Parent = code,
            OnItemClicked = OnExplorerItemClicked,
            OnDeleteRequested = DeleteExplorerItem,
            OnNewFileRequested = NewFileUnderItem,
            OnNewFolderRequested = NewFolderUnderItem,
            OnRenameCommitted = OnItemRenamed
        };
        code.Children.Add(docNb);

        var sampleNb = new ExplorerItemViewModel
        {
            Name = "codefrydev.frynb",
            IsDirectory = false,
            FileExtension = ".frynb",
            IsSelected = false,
            Parent = code,
            OnItemClicked = OnExplorerItemClicked,
            OnDeleteRequested = DeleteExplorerItem,
            OnNewFileRequested = NewFileUnderItem,
            OnNewFolderRequested = NewFolderUnderItem,
            OnRenameCommitted = OnItemRenamed
        };
        code.Children.Add(sampleNb);

        var docs = CreateFolderItem("docs", isExpanded: false);
        var notebooks = CreateFolderItem("notebooks", isExpanded: false);
        var output = CreateFolderItem("output", isExpanded: false);
        var scripts = CreateFolderItem("scripts", isExpanded: false);
        var src = CreateFolderItem("src", isExpanded: false);

        ExplorerRootItems.Add(assets);
        ExplorerRootItems.Add(code);
        ExplorerRootItems.Add(docs);
        ExplorerRootItems.Add(notebooks);
        ExplorerRootItems.Add(output);
        ExplorerRootItems.Add(scripts);
        ExplorerRootItems.Add(src);
    }

    private ExplorerItemViewModel CreateFolderItem(string name, bool isExpanded = false, ExplorerItemViewModel? parent = null)
    {
        return new ExplorerItemViewModel
        {
            Name = name,
            IsDirectory = true,
            IsExpanded = isExpanded,
            Parent = parent,
            OnItemClicked = OnExplorerItemClicked,
            OnDeleteRequested = DeleteExplorerItem,
            OnNewFileRequested = NewFileUnderItem,
            OnNewFolderRequested = NewFolderUnderItem,
            OnRenameCommitted = OnItemRenamed
        };
    }

    private void OnExplorerItemClicked(ExplorerItemViewModel item)
    {
        if (item.IsDirectory)
        {
            item.IsExpanded = !item.IsExpanded;
        }
        else
        {
            DeselectAll(ExplorerRootItems);
            item.IsSelected = true;

            if (item.FileExtension.Equals(".frynb", StringComparison.OrdinalIgnoreCase))
            {
                var title = item.Name.EndsWith(".frynb", StringComparison.OrdinalIgnoreCase)
                    ? item.Name.Substring(0, item.Name.Length - 6)
                    : item.Name;
                Notebook.Title = title;
                OnPropertyChanged(nameof(BreadcrumbText));
                OnPropertyChanged(nameof(DocumentTabTitle));
            }
        }
    }

    private void OnItemRenamed(ExplorerItemViewModel item)
    {
        if (item.IsSelected && !item.IsDirectory)
        {
            var title = item.Name.EndsWith(".frynb", StringComparison.OrdinalIgnoreCase)
                ? item.Name.Substring(0, item.Name.Length - 6)
                : item.Name;
            Notebook.Title = title;
            OnPropertyChanged(nameof(BreadcrumbText));
            OnPropertyChanged(nameof(DocumentTabTitle));
        }
    }

    private void DeselectAll(IEnumerable<ExplorerItemViewModel> items)
    {
        foreach (var it in items)
        {
            it.IsSelected = false;
            if (it.Children.Count > 0)
            {
                DeselectAll(it.Children);
            }
        }
    }

    private ExplorerItemViewModel? FindSelectedItem(IEnumerable<ExplorerItemViewModel> items)
    {
        foreach (var it in items)
        {
            if (it.IsSelected) return it;
            var found = FindSelectedItem(it.Children);
            if (found != null) return found;
        }
        return null;
    }

    private ExplorerItemViewModel? FindFirstFile(IEnumerable<ExplorerItemViewModel> items)
    {
        foreach (var it in items)
        {
            if (!it.IsDirectory) return it;
            var found = FindFirstFile(it.Children);
            if (found != null) return found;
        }
        return null;
    }
}
