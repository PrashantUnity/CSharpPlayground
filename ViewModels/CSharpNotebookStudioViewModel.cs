using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
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
    private readonly Action _backToHubAction;
    private readonly Action? _backToHomeAction;

    private readonly ObservableCollection<NotebookCellViewModel> _emptyCells = new();
    private readonly ObservableCollection<NotebookVariableInfo> _emptyVariables = new();

    [ObservableProperty]
    private NotebookTabViewModel? _activeTab;

    [ObservableProperty]
    private NotebookDocumentItem _notebook;

    [ObservableProperty]
    private bool _isVariableInspectorOpen = false;

    [ObservableProperty]
    private bool _isOutlineOpen = false;

    [ObservableProperty]
    private string _workspaceName = "SKIASHARP";

    [ObservableProperty]
    private bool _isWorkspaceExpanded = true;

    [ObservableProperty]
    private bool _isExplorerOpen = true;

    [ObservableProperty]
    private bool _isOutlineExpanded = false;

    [ObservableProperty]
    private bool _isTimelineExpanded = false;

    public ObservableCollection<NotebookTabViewModel> Tabs { get; } = new();
    public ObservableCollection<ExplorerItemViewModel> ExplorerRootItems { get; } = new();

    public bool HasActiveTab => ActiveTab != null;
    public bool HasNoTabs => ActiveTab == null;

    public ObservableCollection<NotebookCellViewModel> Cells => ActiveTab?.Cells ?? _emptyCells;
    public ObservableCollection<NotebookVariableInfo> Variables => ActiveTab?.Variables ?? _emptyVariables;

    public NotebookCellViewModel? ActiveCell => ActiveTab?.ActiveCell;
    public bool IsExecuting => ActiveTab?.IsExecuting ?? false;
    public string KernelName => ActiveTab?.KernelName ?? ".NET (C#)";
    public string CompilerStatusText
    {
        get => ActiveTab?.KernelStatusText ?? "Kernel Ready";
        set
        {
            if (ActiveTab != null)
            {
                ActiveTab.KernelStatusText = value;
                OnPropertyChanged(nameof(CompilerStatusText));
            }
        }
    }

    public string WorkspaceExpansionArrow => IsWorkspaceExpanded ? "⌵" : ">";

    public string BreadcrumbFolder => ActiveTab?.BreadcrumbFolder ?? "Code";
    public string BreadcrumbDocument => ActiveTab?.BreadcrumbDocument ?? "Untitled.frynb";
    public string ActiveCellBadgeText => ActiveTab?.ActiveCellBadgeText ?? "Notebook Root";
    public string ActiveCellTypeIcon => ActiveTab?.ActiveCellTypeIcon ?? "CodeBraces";
    public string ActiveCellTypeColor => ActiveTab?.ActiveCellTypeColor ?? "#58A6FF";

    public string BreadcrumbText => $"{BreadcrumbFolder} › {BreadcrumbDocument} › {ActiveCellBadgeText}";
    public string DocumentTabTitle => ActiveTab?.Title ?? "Notebook.frynb";

    partial void OnIsWorkspaceExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(WorkspaceExpansionArrow));
    }

    partial void OnActiveTabChanged(NotebookTabViewModel? oldValue, NotebookTabViewModel? newValue)
    {
        if (oldValue != null)
        {
            oldValue.PropertyChanged -= OnActiveTabPropertyChanged;
            oldValue.IsActive = false;
        }

        if (newValue != null)
        {
            newValue.IsActive = true;
            newValue.PropertyChanged += OnActiveTabPropertyChanged;
            Notebook = newValue.Notebook;
        }

        NotifyActiveTabProperties();
    }

    private void OnActiveTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NotebookTabViewModel.ActiveCell) or
            nameof(NotebookTabViewModel.ActiveCellBadgeText) or
            nameof(NotebookTabViewModel.ActiveCellTypeIcon) or
            nameof(NotebookTabViewModel.ActiveCellTypeColor))
        {
            OnPropertyChanged(nameof(ActiveCell));
            OnPropertyChanged(nameof(ActiveCellBadgeText));
            OnPropertyChanged(nameof(ActiveCellTypeIcon));
            OnPropertyChanged(nameof(ActiveCellTypeColor));
            OnPropertyChanged(nameof(BreadcrumbText));
        }
        else if (e.PropertyName == nameof(NotebookTabViewModel.KernelStatusText))
        {
            OnPropertyChanged(nameof(CompilerStatusText));
        }
        else if (e.PropertyName == nameof(NotebookTabViewModel.IsExecuting))
        {
            OnPropertyChanged(nameof(IsExecuting));
        }
        else if (e.PropertyName == nameof(NotebookTabViewModel.Title))
        {
            OnPropertyChanged(nameof(BreadcrumbDocument));
            OnPropertyChanged(nameof(DocumentTabTitle));
            OnPropertyChanged(nameof(BreadcrumbText));
        }
        else if (e.PropertyName == nameof(NotebookTabViewModel.FolderName))
        {
            OnPropertyChanged(nameof(BreadcrumbFolder));
            OnPropertyChanged(nameof(BreadcrumbText));
        }
        else if (e.PropertyName == nameof(NotebookTabViewModel.Variables))
        {
            OnPropertyChanged(nameof(Variables));
        }
    }

    private void NotifyActiveTabProperties()
    {
        OnPropertyChanged(nameof(HasActiveTab));
        OnPropertyChanged(nameof(HasNoTabs));
        OnPropertyChanged(nameof(Cells));
        OnPropertyChanged(nameof(Variables));
        OnPropertyChanged(nameof(ActiveCell));
        OnPropertyChanged(nameof(IsExecuting));
        OnPropertyChanged(nameof(KernelName));
        OnPropertyChanged(nameof(CompilerStatusText));
        OnPropertyChanged(nameof(BreadcrumbFolder));
        OnPropertyChanged(nameof(BreadcrumbDocument));
        OnPropertyChanged(nameof(ActiveCellBadgeText));
        OnPropertyChanged(nameof(ActiveCellTypeIcon));
        OnPropertyChanged(nameof(ActiveCellTypeColor));
        OnPropertyChanged(nameof(BreadcrumbText));
        OnPropertyChanged(nameof(DocumentTabTitle));
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
        _backToHubAction = backToHubAction;
        _backToHomeAction = backToHomeAction;

        PopulateExplorerTree();

        // Initialize primary open tab
        var initialTab = new NotebookTabViewModel(
            _notebook,
            folderName: "Code",
            filePath: $"Code/{notebook.Title}.frynb",
            onSelectTab: SelectTab,
            onCloseTab: CloseTab);

        Tabs.Add(initialTab);
        SelectTab(initialTab);
    }

    public void UpdateActiveNotebook(NotebookDocumentItem notebook)
    {
        Notebook = notebook;

        var expItem = EnsureDocumentInExplorer(notebook);

        var existingTab = Tabs.FirstOrDefault(t => 
            string.Equals(t.Notebook.Id, notebook.Id, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t.Notebook.Title, notebook.Title, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t.Title, notebook.Title, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t.Title, $"{notebook.Title}.frynb", StringComparison.OrdinalIgnoreCase));

        if (existingTab != null)
        {
            SelectTab(existingTab);
        }
        else
        {
            var folder = expItem?.Parent?.Name ?? "Code";
            var newTab = new NotebookTabViewModel(
                notebook,
                folderName: folder,
                filePath: $"{folder}/{notebook.Title}.frynb",
                onSelectTab: SelectTab,
                onCloseTab: CloseTab);

            Tabs.Add(newTab);
            SelectTab(newTab);
        }
    }

    public ExplorerItemViewModel EnsureDocumentInExplorer(NotebookDocumentItem notebook)
    {
        var fileName = notebook.Title.EndsWith(".frynb", StringComparison.OrdinalIgnoreCase)
            ? notebook.Title
            : $"{notebook.Title}.frynb";

        var existing = FindItemByIdOrName(ExplorerRootItems, notebook.Id, fileName);
        if (existing != null)
        {
            if (!string.IsNullOrEmpty(notebook.Id))
            {
                existing.DocumentId = notebook.Id;
            }
            return existing;
        }

        var codeFolder = ExplorerRootItems.FirstOrDefault(x => x.Name == "Code")
                         ?? ExplorerRootItems.FirstOrDefault(x => x.IsDirectory);

        var expItem = new ExplorerItemViewModel
        {
            Name = fileName,
            DocumentId = notebook.Id,
            IsDirectory = false,
            FileExtension = ".frynb",
            Parent = codeFolder,
            Depth = (codeFolder?.Depth ?? 0) + 1,
            OnItemClicked = OnExplorerItemClicked,
            OnDeleteRequested = DeleteExplorerItem,
            OnNewFileRequested = NewFileUnderItem,
            OnNewFolderRequested = NewFolderUnderItem,
            OnRenameCommitted = OnItemRenamed,
            OnDuplicateRequested = DuplicateExplorerItem,
            OnCopyPathRequested = CopyItemPath
        };

        if (codeFolder != null)
        {
            codeFolder.Children.Add(expItem);
            codeFolder.IsExpanded = true;
        }
        else
        {
            ExplorerRootItems.Add(expItem);
        }

        return expItem;
    }

    [RelayCommand]
    public void SelectTab(NotebookTabViewModel? tab)
    {
        if (tab == null) return;

        ActiveTab = tab;

        // Highlight matching item in explorer
        HighlightExplorerItem(tab.Title);
    }

    [RelayCommand]
    public void CloseTab(NotebookTabViewModel? tab)
    {
        if (tab == null) return;

        var idx = Tabs.IndexOf(tab);
        Tabs.Remove(tab);

        if (ActiveTab == tab)
        {
            if (Tabs.Count > 0)
            {
                var nextIdx = Math.Min(idx, Tabs.Count - 1);
                SelectTab(Tabs[nextIdx]);
            }
            else
            {
                ActiveTab = null;
            }
        }
    }

    [RelayCommand]
    public void NewNotebookTab()
    {
        var timestamp = DateTime.Now.ToString("HHmmss");
        var title = $"Notebook_{timestamp}";
        var fileName = $"{title}.frynb";

        var codeFolder = ExplorerRootItems.FirstOrDefault(x => x.Name == "Code")
                         ?? ExplorerRootItems.FirstOrDefault(x => x.IsDirectory);

        NotebookDocumentItem? newDoc = null;
        try
        {
            newDoc = _storageService.CreateNewNotebookAsync(title).GetAwaiter().GetResult();
        }
        catch
        {
            newDoc = new NotebookDocumentItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = title
            };
        }

        var newTab = new NotebookTabViewModel(
            newDoc,
            folderName: codeFolder?.Name ?? "Code",
            filePath: $"{codeFolder?.Name ?? "Code"}/{fileName}",
            onSelectTab: SelectTab,
            onCloseTab: CloseTab);

        Tabs.Add(newTab);
        SelectTab(newTab);

        if (codeFolder != null)
        {
            var newExpItem = new ExplorerItemViewModel
            {
                Name = fileName,
                DocumentId = newDoc.Id,
                IsDirectory = false,
                FileExtension = ".frynb",
                Parent = codeFolder,
                Depth = codeFolder.Depth + 1,
                OnItemClicked = OnExplorerItemClicked,
                OnDeleteRequested = DeleteExplorerItem,
                OnNewFileRequested = NewFileUnderItem,
                OnNewFolderRequested = NewFolderUnderItem,
                OnRenameCommitted = OnItemRenamed,
                OnDuplicateRequested = DuplicateExplorerItem,
                OnCopyPathRequested = CopyItemPath
            };
            codeFolder.IsExpanded = true;
            codeFolder.Children.Add(newExpItem);
            HighlightExplorerItem(fileName);
            newExpItem.StartRename();
        }
    }

    public void OpenDocument(ExplorerItemViewModel item)
    {
        if (item.IsDirectory)
        {
            item.IsExpanded = !item.IsExpanded;
            return;
        }

        DeselectAll(ExplorerRootItems);
        item.IsSelected = true;

        var fileName = item.Name;
        var folderName = item.Parent?.Name ?? "Code";
        var filePath = item.FullPath;
        var docTitle = fileName.EndsWith(".frynb", StringComparison.OrdinalIgnoreCase)
            ? fileName.Substring(0, fileName.Length - 6)
            : fileName;

        var existingTab = Tabs.FirstOrDefault(t =>
            (!string.IsNullOrEmpty(item.DocumentId) && string.Equals(t.Notebook.Id, item.DocumentId, StringComparison.OrdinalIgnoreCase)) ||
            string.Equals(t.Title, fileName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t.Notebook.Title, docTitle, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(filePath) && string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase)));

        if (existingTab != null)
        {
            SelectTab(existingTab);
            return;
        }

        // Attempt to load from storage synchronously
        NotebookDocumentItem? loadedDoc = null;
        if (!string.IsNullOrEmpty(item.DocumentId))
        {
            try
            {
                loadedDoc = _storageService.LoadNotebookAsync(item.DocumentId).GetAwaiter().GetResult();
            }
            catch { }
        }

        if (loadedDoc == null)
        {
            try
            {
                var summaries = _storageService.LoadWorkspaceSummariesAsync().GetAwaiter().GetResult();
                var match = summaries.FirstOrDefault(s => s.IsNotebook && (
                    string.Equals(s.Id, item.DocumentId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s.Title, docTitle, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s.Title, fileName, StringComparison.OrdinalIgnoreCase)));

                if (match != null)
                {
                    loadedDoc = _storageService.LoadNotebookAsync(match.Id).GetAwaiter().GetResult();
                    if (loadedDoc != null) item.DocumentId = match.Id;
                }
            }
            catch { }
        }

        if (loadedDoc == null)
        {
            loadedDoc = new NotebookDocumentItem
            {
                Id = item.DocumentId ?? Guid.NewGuid().ToString("N"),
                Title = docTitle,
                Category = "Interactive",
                Created = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };

            // Populate sample cells for codefrydev.frynb demo
            if (fileName.Contains("codefrydev", StringComparison.OrdinalIgnoreCase))
            {
                loadedDoc.Cells.Add(new NotebookCellItem
                {
                    Type = CellType.Markdown,
                    Source = "# 🚀 CodeFryDev Polyglot Workspace\nInteractive multi-cell document automation, SkiaSharp rendering, and dynamic C# scripts.",
                    IsMarkdownPreviewMode = true
                });
                loadedDoc.Cells.Add(new NotebookCellItem
                {
                    Type = CellType.Code,
                    Source = "// Compute Fibonacci sequence\nvar fib = new List<int> { 1, 1 };\nfor (int i = 2; i < 10; i++) fib.Add(fib[i - 1] + fib[i - 2]);\nfib"
                });
                loadedDoc.Cells.Add(new NotebookCellItem
                {
                    Type = CellType.Code,
                    Source = "Console.WriteLine($\"Workspace timestamp: {DateTime.Now:T}\");"
                });
            }
            else
            {
                loadedDoc.Cells.Add(new NotebookCellItem
                {
                    Type = CellType.Markdown,
                    Source = $"# 📓 {docTitle}\nWrite documentation or notes in this cell.",
                    IsMarkdownPreviewMode = true
                });
                loadedDoc.Cells.Add(new NotebookCellItem
                {
                    Type = CellType.Code,
                    Source = "// Write C# code here\nConsole.WriteLine(\"Hello from notebook!\");"
                });
            }

            _ = _storageService.SaveNotebookAsync(loadedDoc);
            item.DocumentId = loadedDoc.Id;
        }

        var newTab = new NotebookTabViewModel(
            loadedDoc,
            folderName: folderName,
            filePath: !string.IsNullOrEmpty(filePath) ? filePath : $"{folderName}/{fileName}",
            onSelectTab: SelectTab,
            onCloseTab: CloseTab);

        Tabs.Add(newTab);
        SelectTab(newTab);
    }

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
    public void ClearAllOutputs()
    {
        ActiveTab?.ClearAllOutputs();
    }

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
    public void ToggleOutline()
    {
        IsOutlineOpen = !IsOutlineOpen;
    }

    [RelayCommand]
    public void ToggleVariableInspector()
    {
        IsVariableInspectorOpen = !IsVariableInspectorOpen;
        if (IsVariableInspectorOpen)
        {
            ActiveTab?.UpdateVariables();
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (ActiveTab != null)
        {
            ActiveTab.Notebook.LastModified = DateTime.UtcNow;
            await _storageService.SaveNotebookAsync(ActiveTab.Notebook);
            ActiveTab.IsModified = false;
            CompilerStatusText = "Saved";
        }
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

        // Delete from storage if linked to a real document
        if (!string.IsNullOrEmpty(item.DocumentId))
        {
            try
            {
                _ = _storageService.DeleteItemAsync(item.DocumentId);
            }
            catch { }
        }

        // Close any tab open for this item
        var openTab = Tabs.FirstOrDefault(t =>
            (!string.IsNullOrEmpty(item.DocumentId) && string.Equals(t.Notebook.Id, item.DocumentId, StringComparison.OrdinalIgnoreCase)) ||
            string.Equals(t.Title, item.Name, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(item.FullPath) && string.Equals(t.FilePath, item.FullPath, StringComparison.OrdinalIgnoreCase)));

        if (openTab != null)
        {
            CloseTab(openTab);
        }

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
        }
    }

    [RelayCommand]
    public void DuplicateExplorerItem(ExplorerItemViewModel item)
    {
        if (item == null || item.IsDirectory) return;

        var parent = item.Parent ?? ExplorerRootItems.FirstOrDefault(x => x.Name == "Code") ?? ExplorerRootItems.FirstOrDefault(x => x.IsDirectory);
        var originalTitle = item.Name.EndsWith(".frynb", StringComparison.OrdinalIgnoreCase)
            ? item.Name.Substring(0, item.Name.Length - 6)
            : item.Name;
        var copyTitle = $"{originalTitle} Copy";
        var copyFileName = $"{copyTitle}.frynb";

        // Load original document cells if available
        NotebookDocumentItem? origDoc = null;
        if (!string.IsNullOrEmpty(item.DocumentId))
        {
            try { origDoc = _storageService.LoadNotebookAsync(item.DocumentId).GetAwaiter().GetResult(); } catch { }
        }
        if (origDoc == null)
        {
            var openTab = Tabs.FirstOrDefault(t => string.Equals(t.Title, item.Name, StringComparison.OrdinalIgnoreCase));
            origDoc = openTab?.Notebook;
        }

        var copyDoc = new NotebookDocumentItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = copyTitle,
            Category = origDoc?.Category ?? "Interactive",
            Description = origDoc?.Description ?? "",
            Created = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        if (origDoc != null && origDoc.Cells.Count > 0)
        {
            foreach (var cell in origDoc.Cells)
            {
                copyDoc.Cells.Add(new NotebookCellItem
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Type = cell.Type,
                    Source = cell.Source,
                    IsMarkdownPreviewMode = cell.IsMarkdownPreviewMode
                });
            }
        }
        else
        {
            copyDoc.Cells.Add(new NotebookCellItem
            {
                Type = CellType.Markdown,
                Source = $"# 📓 {copyTitle}\nDuplicated notebook workspace.",
                IsMarkdownPreviewMode = true
            });
            copyDoc.Cells.Add(new NotebookCellItem
            {
                Type = CellType.Code,
                Source = "// Write C# code here\nConsole.WriteLine(\"Hello from duplicate notebook!\");"
            });
        }

        _ = _storageService.SaveNotebookAsync(copyDoc);

        var copyItem = new ExplorerItemViewModel
        {
            Name = copyFileName,
            DocumentId = copyDoc.Id,
            IsDirectory = false,
            FileExtension = ".frynb",
            Parent = parent,
            Depth = (parent?.Depth ?? 0) + 1,
            OnItemClicked = OnExplorerItemClicked,
            OnDeleteRequested = DeleteExplorerItem,
            OnNewFileRequested = NewFileUnderItem,
            OnNewFolderRequested = NewFolderUnderItem,
            OnRenameCommitted = OnItemRenamed,
            OnDuplicateRequested = DuplicateExplorerItem,
            OnCopyPathRequested = CopyItemPath
        };

        if (parent != null)
        {
            parent.Children.Add(copyItem);
            parent.IsExpanded = true;
        }
        else
        {
            ExplorerRootItems.Add(copyItem);
        }

        OpenDocument(copyItem);
    }

    [RelayCommand]
    public void CopyItemPath(ExplorerItemViewModel item)
    {
        if (item == null) return;
        var path = !string.IsNullOrEmpty(item.FullPath) ? item.FullPath : item.Name;
        CompilerStatusText = $"Path: {path}";
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
            NewNotebookTab();
        }
    }

    [RelayCommand]
    public void NewFileUnderItem(ExplorerItemViewModel target)
    {
        var folder = target.IsDirectory ? target : (target.Parent ?? target);
        var timestamp = DateTime.Now.ToString("HHmmss");
        var title = $"Notebook_{timestamp}";
        var fileName = $"{title}.frynb";

        NotebookDocumentItem? newDoc = null;
        try
        {
            newDoc = _storageService.CreateNewNotebookAsync(title).GetAwaiter().GetResult();
        }
        catch
        {
            newDoc = new NotebookDocumentItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = title
            };
        }

        var newFile = new ExplorerItemViewModel
        {
            Name = fileName,
            DocumentId = newDoc.Id,
            IsDirectory = false,
            FileExtension = ".frynb",
            Parent = folder,
            Depth = (folder?.Depth ?? 0) + 1,
            OnItemClicked = OnExplorerItemClicked,
            OnDeleteRequested = DeleteExplorerItem,
            OnNewFileRequested = NewFileUnderItem,
            OnNewFolderRequested = NewFolderUnderItem,
            OnRenameCommitted = OnItemRenamed,
            OnDuplicateRequested = DuplicateExplorerItem,
            OnCopyPathRequested = CopyItemPath
        };

        if (folder != null)
        {
            folder.IsExpanded = true;
            folder.Children.Add(newFile);
        }
        else
        {
            ExplorerRootItems.Add(newFile);
        }

        OpenDocument(newFile);
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
        if (folder != null)
        {
            folder.IsExpanded = true;
            folder.Children.Add(newFolder);
        }
        else
        {
            ExplorerRootItems.Add(newFolder);
        }
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

        var assets = CreateFolderItem("assets", isExpanded: false, depth: 0);
        var code = CreateFolderItem("Code", isExpanded: true, depth: 0);

        var docNb = new ExplorerItemViewModel
        {
            Name = "Document Automation Notebook.frynb",
            DocumentId = (Notebook != null && string.Equals(Notebook.Title, "Document Automation Notebook", StringComparison.OrdinalIgnoreCase)) ? Notebook.Id : "doc_automation",
            IsDirectory = false,
            FileExtension = ".frynb",
            IsSelected = true,
            Parent = code,
            Depth = 1,
            OnItemClicked = OnExplorerItemClicked,
            OnDeleteRequested = DeleteExplorerItem,
            OnNewFileRequested = NewFileUnderItem,
            OnNewFolderRequested = NewFolderUnderItem,
            OnRenameCommitted = OnItemRenamed,
            OnDuplicateRequested = DuplicateExplorerItem,
            OnCopyPathRequested = CopyItemPath
        };
        code.Children.Add(docNb);

        var sampleNb = new ExplorerItemViewModel
        {
            Name = "codefrydev.frynb",
            DocumentId = "codefrydev_sample",
            IsDirectory = false,
            FileExtension = ".frynb",
            IsSelected = false,
            Parent = code,
            Depth = 1,
            OnItemClicked = OnExplorerItemClicked,
            OnDeleteRequested = DeleteExplorerItem,
            OnNewFileRequested = NewFileUnderItem,
            OnNewFolderRequested = NewFolderUnderItem,
            OnRenameCommitted = OnItemRenamed,
            OnDuplicateRequested = DuplicateExplorerItem,
            OnCopyPathRequested = CopyItemPath
        };
        code.Children.Add(sampleNb);

        // Load any existing workspace documents from storage
        try
        {
            var summaries = _storageService.LoadWorkspaceSummariesAsync().GetAwaiter().GetResult();
            foreach (var s in summaries.Where(x => x.IsNotebook))
            {
                var name = s.Title.EndsWith(".frynb", StringComparison.OrdinalIgnoreCase) ? s.Title : $"{s.Title}.frynb";
                if (!code.Children.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    var nbItem = new ExplorerItemViewModel
                    {
                        Name = name,
                        DocumentId = s.Id,
                        IsDirectory = false,
                        FileExtension = ".frynb",
                        Parent = code,
                        Depth = 1,
                        OnItemClicked = OnExplorerItemClicked,
                        OnDeleteRequested = DeleteExplorerItem,
                        OnNewFileRequested = NewFileUnderItem,
                        OnNewFolderRequested = NewFolderUnderItem,
                        OnRenameCommitted = OnItemRenamed,
                        OnDuplicateRequested = DuplicateExplorerItem,
                        OnCopyPathRequested = CopyItemPath
                    };
                    code.Children.Add(nbItem);
                }
            }
        }
        catch { }

        // Also ensure any currently open tabs are in the explorer
        foreach (var tab in Tabs)
        {
            var tabFileName = tab.Title.EndsWith(".frynb", StringComparison.OrdinalIgnoreCase) ? tab.Title : $"{tab.Title}.frynb";
            if (!code.Children.Any(c => string.Equals(c.Name, tabFileName, StringComparison.OrdinalIgnoreCase)))
            {
                var tabItem = new ExplorerItemViewModel
                {
                    Name = tabFileName,
                    DocumentId = tab.Notebook.Id,
                    IsDirectory = false,
                    FileExtension = ".frynb",
                    Parent = code,
                    Depth = 1,
                    OnItemClicked = OnExplorerItemClicked,
                    OnDeleteRequested = DeleteExplorerItem,
                    OnNewFileRequested = NewFileUnderItem,
                    OnNewFolderRequested = NewFolderUnderItem,
                    OnRenameCommitted = OnItemRenamed,
                    OnDuplicateRequested = DuplicateExplorerItem,
                    OnCopyPathRequested = CopyItemPath
                };
                code.Children.Add(tabItem);
            }
        }

        var docs = CreateFolderItem("docs", isExpanded: false, depth: 0);
        var notebooks = CreateFolderItem("notebooks", isExpanded: false, depth: 0);
        var output = CreateFolderItem("output", isExpanded: false, depth: 0);
        var scripts = CreateFolderItem("scripts", isExpanded: false, depth: 0);
        var src = CreateFolderItem("src", isExpanded: false, depth: 0);

        ExplorerRootItems.Add(assets);
        ExplorerRootItems.Add(code);
        ExplorerRootItems.Add(docs);
        ExplorerRootItems.Add(notebooks);
        ExplorerRootItems.Add(output);
        ExplorerRootItems.Add(scripts);
        ExplorerRootItems.Add(src);

        if (ActiveTab != null)
        {
            HighlightExplorerItem(ActiveTab.Title);
        }
    }

    private ExplorerItemViewModel CreateFolderItem(string name, bool isExpanded = false, ExplorerItemViewModel? parent = null, int depth = 0)
    {
        return new ExplorerItemViewModel
        {
            Name = name,
            IsDirectory = true,
            IsExpanded = isExpanded,
            Parent = parent,
            Depth = parent != null ? parent.Depth + 1 : depth,
            OnItemClicked = OnExplorerItemClicked,
            OnDeleteRequested = DeleteExplorerItem,
            OnNewFileRequested = NewFileUnderItem,
            OnNewFolderRequested = NewFolderUnderItem,
            OnRenameCommitted = OnItemRenamed,
            OnDuplicateRequested = DuplicateExplorerItem,
            OnCopyPathRequested = CopyItemPath
        };
    }

    private void OnExplorerItemClicked(ExplorerItemViewModel item)
    {
        OpenDocument(item);
    }

    private void OnItemRenamed(ExplorerItemViewModel item)
    {
        if (!item.IsDirectory)
        {
            var openTab = Tabs.FirstOrDefault(t =>
                (!string.IsNullOrEmpty(item.DocumentId) && string.Equals(t.Notebook.Id, item.DocumentId, StringComparison.OrdinalIgnoreCase)) ||
                string.Equals(t.Title, item.Name, StringComparison.OrdinalIgnoreCase) ||
                t.IsActive);

            if (openTab != null)
            {
                openTab.Title = item.Name;
                openTab.Notebook.Title = item.Name.EndsWith(".frynb", StringComparison.OrdinalIgnoreCase)
                    ? item.Name.Substring(0, item.Name.Length - 6)
                    : item.Name;

                try
                {
                    _ = _storageService.SaveNotebookAsync(openTab.Notebook);
                }
                catch { }
            }
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

    private void HighlightExplorerItem(string fileName)
    {
        DeselectAll(ExplorerRootItems);
        var docId = ActiveTab?.Notebook?.Id;
        var match = FindItemByIdOrName(ExplorerRootItems, docId, fileName);
        if (match != null)
        {
            match.IsSelected = true;
            var parent = match.Parent;
            while (parent != null)
            {
                parent.IsExpanded = true;
                parent = parent.Parent;
            }
        }
    }

    private ExplorerItemViewModel? FindItemByIdOrName(IEnumerable<ExplorerItemViewModel> items, string? docId, string name)
    {
        foreach (var it in items)
        {
            if (!string.IsNullOrEmpty(docId) && string.Equals(it.DocumentId, docId, StringComparison.OrdinalIgnoreCase))
                return it;

            if (string.Equals(it.Name, name, StringComparison.OrdinalIgnoreCase))
                return it;

            var found = FindItemByIdOrName(it.Children, docId, name);
            if (found != null) return found;
        }
        return null;
    }

    private ExplorerItemViewModel? FindItemByName(IEnumerable<ExplorerItemViewModel> items, string name)
    {
        return FindItemByIdOrName(items, null, name);
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
