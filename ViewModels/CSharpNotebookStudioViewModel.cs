using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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
    private readonly Func<int> _getTimeoutSeconds;

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
    private string _workspaceName = "WORKSPACE";

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

    public string BreadcrumbFolder => ActiveTab?.BreadcrumbFolder ?? "Library";
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
        Action? backToHomeAction = null,
        Func<int>? getTimeoutSeconds = null)
    {
        _notebook = notebook;
        _storageService = storageService;
        _compilerService = compilerService;
        _executionEngine = executionEngine;
        _backToHubAction = backToHubAction;
        _backToHomeAction = backToHomeAction;
        _getTimeoutSeconds = getTimeoutSeconds ?? (() => 10);

        var initialTab = new NotebookTabViewModel(
            _notebook,
            folderName: "Library",
            filePath: $"{notebook.Title}.frynb",
            onSelectTab: SelectTab,
            onCloseTab: CloseTab,
            getTimeoutSeconds: _getTimeoutSeconds);

        Tabs.Add(initialTab);
        SelectTab(initialTab);

        PopulateExplorerTree();
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
            var folder = expItem?.Parent?.Name ?? "Library";
            var newTab = new NotebookTabViewModel(
                notebook,
                folderName: folder,
                filePath: expItem?.FullPath ?? $"{notebook.Title}.frynb",
                onSelectTab: SelectTab,
                onCloseTab: CloseTab,
                getTimeoutSeconds: _getTimeoutSeconds);

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
            existing.Name = fileName;
            if (!string.IsNullOrEmpty(notebook.Id))
            {
                existing.DocumentId = notebook.Id;
            }
            return existing;
        }

        var expItem = CreateFileItem(fileName, notebook.Id, parent: null, fullPath: fileName);
        ExplorerRootItems.Add(expItem);
        return expItem;
    }

    [RelayCommand]
    public void SelectTab(NotebookTabViewModel? tab)
    {
        if (tab == null) return;

        ActiveTab = tab;
        HighlightExplorerItem(tab.Title);
    }

    [RelayCommand]
    public void CloseTab(NotebookTabViewModel? tab)
    {
        if (tab == null) return;

        tab.DisposeAllCellResources();

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
    public async Task NewNotebookTab()
    {
        var timestamp = DateTime.Now.ToString("HHmmss");
        var title = $"Notebook_{timestamp}";

        NotebookDocumentItem newDoc;
        try
        {
            newDoc = await _storageService.CreateNewNotebookAsync(title);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Failed to create new notebook: {ex.Message}");
            newDoc = new NotebookDocumentItem { Id = Guid.NewGuid().ToString("N"), Title = title };
        }

        var newTab = new NotebookTabViewModel(
            newDoc,
            folderName: "Library",
            filePath: $"{newDoc.Title}.frynb",
            onSelectTab: SelectTab,
            onCloseTab: CloseTab,
            getTimeoutSeconds: _getTimeoutSeconds);

        Tabs.Add(newTab);
        SelectTab(newTab);

        var newExpItem = EnsureDocumentInExplorer(newDoc);
        HighlightExplorerItem(newExpItem.Name);
        newExpItem.StartRename();
    }

    private void OnExplorerItemClicked(ExplorerItemViewModel item) => _ = OpenDocumentAsync(item);

    public void OpenDocument(ExplorerItemViewModel item) => _ = OpenDocumentAsync(item);

    public async Task OpenDocumentAsync(ExplorerItemViewModel item)
    {
        if (item.IsDirectory)
        {
            item.IsExpanded = !item.IsExpanded;
            return;
        }

        DeselectAll(ExplorerRootItems);
        item.IsSelected = true;

        var fileName = item.Name;
        var folderName = item.Parent?.Name ?? "Library";
        var filePath = !string.IsNullOrEmpty(item.FullPath) ? item.FullPath : fileName;
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

        NotebookDocumentItem? loadedDoc = null;
        if (!string.IsNullOrEmpty(item.DocumentId))
        {
            loadedDoc = await _storageService.LoadNotebookAsync(item.DocumentId);
        }

        if (loadedDoc == null)
        {
            var summaries = await _storageService.LoadWorkspaceSummariesAsync();
            var match = summaries.FirstOrDefault(s => s.IsNotebook && (
                string.Equals(s.Id, item.DocumentId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s.Title, docTitle, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s.Title, fileName, StringComparison.OrdinalIgnoreCase)));

            if (match != null)
            {
                loadedDoc = await _storageService.LoadNotebookAsync(match.Id);
                if (loadedDoc != null) item.DocumentId = match.Id;
            }
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

            await _storageService.SaveNotebookAsync(loadedDoc);
            item.DocumentId = loadedDoc.Id;
        }

        var newTab = new NotebookTabViewModel(
            loadedDoc,
            folderName: folderName,
            filePath: filePath,
            onSelectTab: SelectTab,
            onCloseTab: CloseTab,
            getTimeoutSeconds: _getTimeoutSeconds);

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
    public void InterruptExecution()
    {
        ActiveTab?.InterruptExecution();
    }

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
        if (ActiveTab != null && ActiveTab.IsModified)
        {
            ActiveTab.Notebook.LastModified = DateTime.UtcNow;
            var saved = await _storageService.SaveNotebookAsync(ActiveTab.Notebook);
            ActiveTab.IsModified = !saved;
            CompilerStatusText = saved ? "Saved" : "⚠️ Save failed — check disk space/permissions";
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
    public async Task DeleteSelectedExplorerItem()
    {
        var selected = FindSelectedItem(ExplorerRootItems);
        if (selected != null)
        {
            await DeleteExplorerItemAsync(selected);
        }
    }

    public void DeleteExplorerItem(ExplorerItemViewModel item) => _ = DeleteExplorerItemAsync(item);

    [RelayCommand]
    public async Task DeleteExplorerItemAsync(ExplorerItemViewModel item)
    {
        if (item == null) return;

        if (item.IsExternalGroup) return;

        if (item.IsDirectory)
        {
            var descendantIds = CollectDescendantDocumentIds(item);
            if (descendantIds.Count > 0)
            {
                foreach (var tab in Tabs.Where(t => descendantIds.Contains(t.Notebook.Id)).ToList())
                {
                    CloseTab(tab);
                }
            }

            try
            {
                await _storageService.DeleteFolderAsync(item.FullPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CSharpEditorPlugin] Failed to delete folder '{item.FullPath}': {ex.Message}");
            }
        }
        else if (!string.IsNullOrEmpty(item.DocumentId))
        {
            try
            {
                await _storageService.DeleteItemAsync(item.DocumentId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CSharpEditorPlugin] Failed to delete '{item.DocumentId}': {ex.Message}");
            }

            var openTab = Tabs.FirstOrDefault(t =>
                string.Equals(t.Notebook.Id, item.DocumentId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(t.Title, item.Name, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(item.FullPath) && string.Equals(t.FilePath, item.FullPath, StringComparison.OrdinalIgnoreCase)));

            if (openTab != null)
            {
                CloseTab(openTab);
            }
        }

        if (item.Parent != null)
        {
            var parent = item.Parent;
            parent.Children.Remove(item);

            if (parent.IsExternalGroup && parent.Children.Count == 0)
            {
                ExplorerRootItems.Remove(parent);
            }
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
                await OpenDocumentAsync(nextFile);
            }
        }
    }

    public void DuplicateExplorerItem(ExplorerItemViewModel item) => _ = DuplicateExplorerItemAsync(item);

    [RelayCommand]
    public async Task DuplicateExplorerItemAsync(ExplorerItemViewModel item)
    {
        if (item == null || item.IsDirectory) return;

        var parent = item.Parent;
        var originalTitle = item.Name.EndsWith(".frynb", StringComparison.OrdinalIgnoreCase)
            ? item.Name.Substring(0, item.Name.Length - 6)
            : item.Name;
        var copyTitle = $"{originalTitle} Copy";
        var copyFileName = $"{copyTitle}.frynb";

        NotebookDocumentItem? origDoc = null;
        if (!string.IsNullOrEmpty(item.DocumentId))
        {
            origDoc = await _storageService.LoadNotebookAsync(item.DocumentId);
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
                    IsMarkdownPreviewMode = cell.IsMarkdownPreviewMode,
                    IsInputCollapsed = cell.IsInputCollapsed,
                    IsOutputCollapsed = cell.IsOutputCollapsed,
                    IsOutputScrolled = cell.IsOutputScrolled
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

        var externalFolderPath = parent?.IsExternalGroup == true ? parent.FullPath : null;
        await _storageService.SaveNotebookAsync(copyDoc, externalFolderPath);

        var copyPath = string.IsNullOrEmpty(parent?.FullPath) ? copyFileName : $"{parent!.FullPath}/{copyFileName}";
        var copyItem = CreateFileItem(copyFileName, copyDoc.Id, parent, copyPath);

        AddToTree(parent, copyItem);
        if (parent != null) parent.IsExpanded = true;

        await OpenDocumentAsync(copyItem);
    }

    [RelayCommand]
    public void CopyItemPath(ExplorerItemViewModel item)
    {
        if (item == null) return;
        var path = !string.IsNullOrEmpty(item.FullPath) ? item.FullPath : item.Name;
        CompilerStatusText = $"Path: {path}";
    }

    [RelayCommand]
    public async Task NewFile()
    {
        var selected = FindSelectedItem(ExplorerRootItems);
        var targetFolder = (selected != null && selected.IsDirectory) ? selected : selected?.Parent;

        if (targetFolder != null)
        {
            await NewFileUnderItemAsync(targetFolder);
        }
        else
        {
            await NewNotebookTab();
        }
    }

    public void NewFileUnderItem(ExplorerItemViewModel target) => _ = NewFileUnderItemAsync(target);

    [RelayCommand]
    public async Task NewFileUnderItemAsync(ExplorerItemViewModel target)
    {
        var folder = target.IsDirectory ? target : target.Parent;
        var timestamp = DateTime.Now.ToString("HHmmss");
        var title = $"Notebook_{timestamp}";
        var fileName = $"{title}.frynb";
        var folderPath = folder?.FullPath;

        NotebookDocumentItem newDoc;
        try
        {
            newDoc = await _storageService.CreateNewNotebookAsync(title, folderPath: folderPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Failed to create notebook: {ex.Message}");
            newDoc = new NotebookDocumentItem { Id = Guid.NewGuid().ToString("N"), Title = title };
        }

        var fullPath = string.IsNullOrEmpty(folderPath) ? fileName : $"{folderPath}/{fileName}";
        var newFile = CreateFileItem(fileName, newDoc.Id, folder, fullPath);

        AddToTree(folder, newFile);
        if (folder != null) folder.IsExpanded = true;

        await OpenDocumentAsync(newFile);
        newFile.StartRename();
    }

    [RelayCommand]
    public async Task NewFolder()
    {
        var selected = FindSelectedItem(ExplorerRootItems);
        var targetFolder = (selected != null && selected.IsDirectory) ? selected : selected?.Parent;
        await CreateFolderCoreAsync(targetFolder);
    }

    public void NewFolderUnderItem(ExplorerItemViewModel target) => _ = NewFolderUnderItemAsync(target);

    [RelayCommand]
    public async Task NewFolderUnderItemAsync(ExplorerItemViewModel target)
    {
        var folder = target.IsDirectory ? target : target.Parent;
        await CreateFolderCoreAsync(folder);
    }

    private async Task CreateFolderCoreAsync(ExplorerItemViewModel? parentFolder)
    {
        string newRelativePath;
        try
        {
            newRelativePath = await _storageService.CreateFolderAsync(parentFolder?.FullPath, "New Folder");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Failed to create folder: {ex.Message}");
            return;
        }

        var name = newRelativePath.Contains('/') ? newRelativePath[(newRelativePath.LastIndexOf('/') + 1)..] : newRelativePath;
        var newFolder = CreateFolderItem(name, newRelativePath, isExpanded: true, parent: parentFolder);
        AddToTree(parentFolder, newFolder);
        if (parentFolder != null) parentFolder.IsExpanded = true;
        newFolder.StartRename();
    }

    [RelayCommand]
    public async Task OpenExternalProjectAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            var result = await _storageService.OpenExternalProjectAsync(path);
            if (!result.Success)
            {
                if (ActiveTab != null)
                {
                    ActiveTab.KernelStatusText = result.Message;
                }
                return;
            }

            await RefreshExplorer();

            if (!string.IsNullOrEmpty(result.PrimaryDocumentId))
            {
                var loaded = await _storageService.LoadNotebookAsync(result.PrimaryDocumentId);
                if (loaded != null)
                {
                    UpdateActiveNotebook(loaded);
                }
            }

            if (ActiveTab != null)
            {
                ActiveTab.KernelStatusText = result.Message;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Failed to open external project '{path}': {ex.Message}");
            if (ActiveTab != null)
            {
                ActiveTab.KernelStatusText = $"Error opening project: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    public async Task RefreshExplorer()
    {
        try
        {
            var folderPaths = await _storageService.LoadFolderPathsAsync();
            var summaries = await _storageService.LoadWorkspaceSummariesAsync();
            RebuildExplorerTree(folderPaths, summaries);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Failed to refresh explorer tree: {ex.Message}");
        }
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
        try
        {
            var folderPaths = _storageService.LoadFolderPathsAsync().GetAwaiter().GetResult();
            var summaries = _storageService.LoadWorkspaceSummariesAsync().GetAwaiter().GetResult();
            RebuildExplorerTree(folderPaths, summaries);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Failed to populate explorer tree: {ex.Message}");
            ExplorerRootItems.Clear();
        }
    }

    private void RebuildExplorerTree(List<string> folderPaths, List<WorkspaceItemSummary> summaries)
    {
        ExplorerRootItems.Clear();
        var folderNodes = new Dictionary<string, ExplorerItemViewModel>(StringComparer.OrdinalIgnoreCase);

        ExplorerItemViewModel? GetOrCreateFolder(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;
            if (folderNodes.TryGetValue(relativePath, out var existing)) return existing;

            var lastSlash = relativePath.LastIndexOf('/');
            var name = lastSlash >= 0 ? relativePath[(lastSlash + 1)..] : relativePath;
            var parentPath = lastSlash >= 0 ? relativePath[..lastSlash] : string.Empty;
            var parent = GetOrCreateFolder(parentPath);

            var node = CreateFolderItem(name, relativePath, isExpanded: false, parent: parent);
            AddToTree(parent, node);
            folderNodes[relativePath] = node;
            return node;
        }

        foreach (var path in folderPaths.OrderBy(p => p.Count(c => c == '/')).ThenBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            GetOrCreateFolder(path);
        }

        var externalGroupNodes = new Dictionary<string, ExplorerItemViewModel>(StringComparer.OrdinalIgnoreCase);

        ExplorerItemViewModel GetOrCreateExternalGroup(string absolutePath)
        {
            if (externalGroupNodes.TryGetValue(absolutePath, out var existing)) return existing;

            var trimmed = absolutePath.TrimEnd('/', '\\');
            var name = Path.GetFileName(trimmed);
            if (string.IsNullOrEmpty(name)) name = trimmed;

            var node = CreateFolderItem(name, absolutePath, isExpanded: false, parent: null, isExternalGroup: true);
            AddToTree(null, node);
            externalGroupNodes[absolutePath] = node;
            return node;
        }

        var openDocumentIds = new HashSet<string>(Tabs.Select(t => t.Notebook.Id), StringComparer.OrdinalIgnoreCase);
        var relevantExternalFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in summaries)
        {
            if (s.IsNotebook && openDocumentIds.Contains(s.Id) && !string.IsNullOrEmpty(s.FolderPath) && Path.IsPathRooted(s.FolderPath))
            {
                relevantExternalFolders.Add(s.FolderPath);
            }
        }

        foreach (var s in summaries.Where(x => x.IsNotebook).OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase))
        {
            var name = s.Title.EndsWith(".frynb", StringComparison.OrdinalIgnoreCase) ? s.Title : $"{s.Title}.frynb";
            var isExternal = !string.IsNullOrEmpty(s.FolderPath) && Path.IsPathRooted(s.FolderPath);
            if (isExternal && !relevantExternalFolders.Contains(s.FolderPath!))
            {
                continue;
            }

            var parent = isExternal ? GetOrCreateExternalGroup(s.FolderPath!) : GetOrCreateFolder(s.FolderPath);
            var fullPath = isExternal
                ? $"{s.FolderPath!.TrimEnd('/', '\\')}/{name}"
                : (string.IsNullOrEmpty(s.FolderPath) ? name : $"{s.FolderPath}/{name}");

            var siblings = parent?.Children ?? (IEnumerable<ExplorerItemViewModel>)ExplorerRootItems;
            if (siblings.Any(c => !c.IsDirectory && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var docItem = CreateFileItem(name, s.Id, parent, fullPath);
            AddToTree(parent, docItem);
        }

        foreach (var tab in Tabs.ToList())
        {
            EnsureDocumentInExplorer(tab.Notebook);
        }

        SortExplorerTree(ExplorerRootItems);

        if (ActiveTab != null)
        {
            HighlightExplorerItem(ActiveTab.Title);
        }
    }

    private void SortExplorerTree(ObservableCollection<ExplorerItemViewModel> items)
    {
        var sorted = items.OrderByDescending(i => i.IsDirectory).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();
        if (!sorted.SequenceEqual(items))
        {
            items.Clear();
            foreach (var item in sorted)
            {
                items.Add(item);
            }
        }

        foreach (var folder in items.Where(i => i.IsDirectory))
        {
            SortExplorerTree(folder.Children);
        }
    }

    private ExplorerItemViewModel CreateFolderItem(string name, string fullPath, bool isExpanded = false, ExplorerItemViewModel? parent = null, bool isExternalGroup = false)
    {
        return new ExplorerItemViewModel
        {
            Name = name,
            IsDirectory = true,
            IsExternalGroup = isExternalGroup,
            IsExpanded = isExpanded,
            Parent = parent,
            Depth = (parent?.Depth ?? -1) + 1,
            FullPath = fullPath,
            OnItemClicked = OnExplorerItemClicked,
            OnDeleteRequested = DeleteExplorerItem,
            OnNewFileRequested = NewFileUnderItem,
            OnNewFolderRequested = NewFolderUnderItem,
            OnRenameCommitted = OnItemRenamed,
            OnDuplicateRequested = DuplicateExplorerItem,
            OnCopyPathRequested = CopyItemPath
        };
    }

    private ExplorerItemViewModel CreateFileItem(string name, string? documentId, ExplorerItemViewModel? parent, string fullPath)
    {
        return new ExplorerItemViewModel
        {
            Name = name,
            DocumentId = documentId,
            IsDirectory = false,
            FileExtension = Path.GetExtension(name),
            Parent = parent,
            Depth = (parent?.Depth ?? -1) + 1,
            FullPath = fullPath,
            OnItemClicked = OnExplorerItemClicked,
            OnDeleteRequested = DeleteExplorerItem,
            OnNewFileRequested = NewFileUnderItem,
            OnNewFolderRequested = NewFolderUnderItem,
            OnRenameCommitted = OnItemRenamed,
            OnDuplicateRequested = DuplicateExplorerItem,
            OnCopyPathRequested = CopyItemPath
        };
    }

    private void AddToTree(ExplorerItemViewModel? parent, ExplorerItemViewModel child)
    {
        if (parent != null)
        {
            parent.Children.Add(child);
        }
        else
        {
            ExplorerRootItems.Add(child);
        }
    }

    private HashSet<string> CollectDescendantDocumentIds(ExplorerItemViewModel item)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Walk(ExplorerItemViewModel node)
        {
            if (!node.IsDirectory && !string.IsNullOrEmpty(node.DocumentId))
            {
                ids.Add(node.DocumentId);
            }
            foreach (var child in node.Children)
            {
                Walk(child);
            }
        }

        Walk(item);
        return ids;
    }

    private void UpdateDescendantFullPaths(ExplorerItemViewModel node, string oldPrefix, string newPrefix)
    {
        foreach (var child in node.Children)
        {
            if (child.FullPath.StartsWith(oldPrefix, StringComparison.Ordinal))
            {
                child.FullPath = newPrefix + child.FullPath[oldPrefix.Length..];
            }
            UpdateDescendantFullPaths(child, oldPrefix, newPrefix);
        }
    }

    private void OnItemRenamed(ExplorerItemViewModel item) => _ = OnItemRenamedAsync(item);

    internal async Task OnItemRenamedAsync(ExplorerItemViewModel item)
    {
        if (item.IsDirectory)
        {
            if (item.IsExternalGroup) return;

            try
            {
                var oldPath = item.FullPath;
                var newPath = await _storageService.RenameFolderAsync(oldPath, item.Name);
                UpdateDescendantFullPaths(item, oldPath, newPath);
                item.FullPath = newPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CSharpEditorPlugin] Failed to rename folder '{item.FullPath}': {ex.Message}");
            }
            return;
        }

        if (string.IsNullOrEmpty(item.DocumentId)) return;

        var newTitle = item.Name.EndsWith(".frynb", StringComparison.OrdinalIgnoreCase)
            ? item.Name.Substring(0, item.Name.Length - 6)
            : item.Name;

        var openTab = Tabs.FirstOrDefault(t => string.Equals(t.Notebook.Id, item.DocumentId, StringComparison.OrdinalIgnoreCase));
        if (openTab != null)
        {
            openTab.Title = item.Name;
            openTab.Notebook.Title = newTitle;
            await _storageService.SaveNotebookAsync(openTab.Notebook);
        }
        else
        {
            var doc = await _storageService.LoadNotebookAsync(item.DocumentId);
            if (doc != null)
            {
                doc.Title = newTitle;
                await _storageService.SaveNotebookAsync(doc);
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
