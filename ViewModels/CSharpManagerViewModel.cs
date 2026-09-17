using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

public partial class CSharpManagerViewModel : ObservableObject
{
    private readonly IScriptStorageService _storageService;
    private readonly Action<ScriptDocumentItem> _openScriptAction;
    private readonly Action<NotebookDocumentItem> _openNotebookAction;
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedTypeFilter = "All"; // "All", "Scripts", "Notebooks"

    [ObservableProperty]
    private string _selectedSortOption = "Recently Modified";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasFilteredItems = true;

    [ObservableProperty]
    private bool _hasSearchQuery;

    [ObservableProperty]
    private int _totalScripts;

    [ObservableProperty]
    private int _totalNotebooks;

    [ObservableProperty]
    private int _filteredItemCount;

    // "Workspace" or "Templates" — which set the center pane shows. Defaults to "Templates" until
    // the first load completes, then flips to "Workspace" only if there's actually something there
    // (see LoadWorkspaceItemsAsync) — a brand-new install lands on Templates since there's nothing
    // else to show yet; a returning user lands on their own work instead of a full template gallery.
    [ObservableProperty]
    private string _selectedNavSection = "Templates";

    private bool _hasAppliedInitialNavDefault;

    // Selection for the inspector panel — mutually exclusive; selecting one clears the other.
    [ObservableProperty]
    private CodeTemplate? _selectedTemplate;

    [ObservableProperty]
    private WorkspaceItemSummary? _selectedWorkspaceItem;

    // Guards LaunchTemplateAsync/CreateNewScriptAsync/CreateNewNotebookAsync against creating
    // duplicate documents: re-entrant calls while one is already in flight are ignored, and so is a
    // call that arrives before the very first LoadWorkspaceItemsAsync (fired unawaited from the
    // constructor) has finished — both were real ways for AllItems to look emptier than it actually
    // is, making an existing document look like it doesn't exist yet.
    [ObservableProperty]
    private bool _isLaunching;

    // "New Script"/"New Notebook" open this prompt instead of creating immediately, so the user
    // can name the document and pick a destination folder — like a real IDE's "New File" dialog —
    // rather than everything silently landing in the library root. The folder itself is chosen via
    // the OS's own native folder browser (see CSharpManagerView.axaml.cs), scoped to browse within
    // LibraryRootPath, which also lets the user create a new folder using the OS dialog's own "New
    // Folder" affordance — no separate in-app "create folder" UI needed. Launching a template does
    // NOT go through this prompt (that stays a fast, one-click "get started" flow); it still calls
    // the Core create methods directly, unchanged.
    [ObservableProperty]
    private WorkspaceItemKind? _pendingCreateKind;

    [ObservableProperty]
    private string _newItemName = string.Empty;

    // Null/empty means the library root. Set directly by the View's code-behind after a successful
    // native folder-picker round trip (already validated + made relative to LibraryRootPath there).
    [ObservableProperty]
    private string? _selectedFolderPath;

    // Set by the View's code-behind when the user picks a folder outside LibraryRootPath via the
    // native browser — the storage layer can only save under that root, so this surfaces why the
    // pick didn't take instead of silently leaving the display unchanged.
    [ObservableProperty]
    private string? _locationWarning;

    private string? _pendingTemplateId;

    public bool IsCreatePromptOpen => PendingCreateKind.HasValue;
    public string CreatePromptTitle => PendingCreateKind == WorkspaceItemKind.Notebook ? "New Notebook" : "New Script";
    public string SelectedFolderDisplay => string.IsNullOrEmpty(SelectedFolderPath) ? "Workspace root" : SelectedFolderPath;

    // Exposed so the View's code-behind can scope the native folder-picker dialog to this directory.
    public string LibraryRootPath => _storageService.LibraryRootPath;

    public ObservableCollection<WorkspaceItemSummary> AllItems { get; } = new();

    // Holds a mix of WorkspaceItemSummary rows and WorkspaceGroupHeaderViewModel divider rows — see
    // ApplyFilter. The view picks a template per runtime type, so this stays a plain object collection
    // rather than forcing a common base type onto WorkspaceItemSummary just for this one list.
    public ObservableCollection<object> FilteredItems { get; } = new();
    public ObservableCollection<CodeTemplate> StarterTemplates { get; } = new();

    public IEnumerable<CodeTemplate> ScriptTemplates => StarterTemplates.Where(t => !t.IsNotebook);
    public IEnumerable<CodeTemplate> NotebookTemplates => StarterTemplates.Where(t => t.IsNotebook);

    public ObservableCollection<string> TypeFilters { get; } = new()
    {
        "All", "Scripts", "Notebooks"
    };

    public ObservableCollection<string> SortOptions { get; } = new()
    {
        "Recently Modified", "Title (A-Z)", "Execution Count"
    };

    private readonly Action? _navigateToHomeAction;

    public bool IsAllFilterActive => SelectedTypeFilter == "All";
    public bool IsScriptsFilterActive => SelectedTypeFilter == "Scripts";
    public bool IsNotebooksFilterActive => SelectedTypeFilter == "Notebooks";

    public bool IsWorkspaceSectionActive => SelectedNavSection == "Workspace";
    public bool IsTemplatesSectionActive => SelectedNavSection == "Templates";

    // Nav-rail highlighting for the two quick-filter entries: only "active" while actually viewing
    // the Workspace section under that filter, not just whenever SelectedTypeFilter happens to still
    // hold that value from before the user navigated to Templates.
    public bool IsWorkspaceAllNavActive => IsWorkspaceSectionActive && IsAllFilterActive;
    public bool IsScriptsNavActive => IsWorkspaceSectionActive && IsScriptsFilterActive;
    public bool IsNotebooksNavActive => IsWorkspaceSectionActive && IsNotebooksFilterActive;

    public bool HasSelection => SelectedTemplate != null || SelectedWorkspaceItem != null;
    public bool IsShowingTemplate => SelectedTemplate != null;
    public bool IsShowingItem => SelectedWorkspaceItem != null;

    public CSharpManagerViewModel(
        IScriptStorageService storageService,
        Action<ScriptDocumentItem> openScriptAction,
        Action<NotebookDocumentItem> openNotebookAction,
        Action? navigateToHomeAction = null)
    {
        _storageService = storageService;
        _openScriptAction = openScriptAction;
        _openNotebookAction = openNotebookAction;
        _navigateToHomeAction = navigateToHomeAction;

        foreach (var t in CodeTemplateLibrary.GetTemplates())
        {
            StarterTemplates.Add(t);
        }

        _ = LoadWorkspaceItemsAsync();
    }

    [RelayCommand]
    private void NavigateToHome()
    {
        _navigateToHomeAction?.Invoke();
    }

    public async Task LoadWorkspaceItemsAsync()
    {
        // The constructor fires this off without awaiting it, and CreateNewScriptAsync/
        // CreateNewNotebookAsync/etc. call it again afterward — without this lock, two concurrent
        // calls interleave Clear()/Add() on the same ObservableCollection (not thread-safe for
        // concurrent mutation), which can throw mid-mutation and silently corrupt AllItems, since
        // every caller here is itself fire-and-forget from an ICommand.Execute(). Reproduced directly
        // in CSharpManagerViewModelTests: concurrent loads threw IndexOutOfRangeException and left
        // AllItems with duplicated entries.
        await _loadLock.WaitAsync();
        try
        {
            IsLoading = true;
            var list = await _storageService.LoadWorkspaceSummariesAsync();
            AllItems.Clear();
            foreach (var item in list)
            {
                AllItems.Add(item);
            }

            UpdateStats();
            ApplyFilter();

            // One-time derived default (see SelectedNavSection's declaration): only steer the user
            // away from Templates on the very first load, never on a later reload (e.g. after
            // creating a new item), so we don't yank them out of whatever section they're already on.
            if (!_hasAppliedInitialNavDefault)
            {
                _hasAppliedInitialNavDefault = true;
                if (AllItems.Count > 0)
                {
                    SelectedNavSection = "Workspace";
                }
            }
        }
        finally
        {
            IsLoading = false;
            _loadLock.Release();
        }
    }

    private void UpdateStats()
    {
        TotalScripts = AllItems.Count(i => i.IsScript);
        TotalNotebooks = AllItems.Count(i => i.IsNotebook);
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilter();
    partial void OnSelectedTypeFilterChanged(string value)
    {
        OnPropertyChanged(nameof(IsAllFilterActive));
        OnPropertyChanged(nameof(IsScriptsFilterActive));
        OnPropertyChanged(nameof(IsNotebooksFilterActive));
        OnPropertyChanged(nameof(IsWorkspaceAllNavActive));
        OnPropertyChanged(nameof(IsScriptsNavActive));
        OnPropertyChanged(nameof(IsNotebooksNavActive));
        ApplyFilter();
    }
    partial void OnSelectedSortOptionChanged(string value) => ApplyFilter();

    partial void OnSelectedNavSectionChanged(string value)
    {
        OnPropertyChanged(nameof(IsWorkspaceSectionActive));
        OnPropertyChanged(nameof(IsTemplatesSectionActive));
        OnPropertyChanged(nameof(IsWorkspaceAllNavActive));
        OnPropertyChanged(nameof(IsScriptsNavActive));
        OnPropertyChanged(nameof(IsNotebooksNavActive));
    }

    partial void OnSelectedTemplateChanged(CodeTemplate? value)
    {
        if (value != null) SelectedWorkspaceItem = null;
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(IsShowingTemplate));
    }

    partial void OnSelectedWorkspaceItemChanged(WorkspaceItemSummary? value)
    {
        if (value != null) SelectedTemplate = null;
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(IsShowingItem));
    }

    partial void OnPendingCreateKindChanged(WorkspaceItemKind? value)
    {
        OnPropertyChanged(nameof(IsCreatePromptOpen));
        OnPropertyChanged(nameof(CreatePromptTitle));
    }

    partial void OnSelectedFolderPathChanged(string? value) => OnPropertyChanged(nameof(SelectedFolderDisplay));

    [RelayCommand]
    private void SetSelectedTypeFilter(string filter)
    {
        SelectedTypeFilter = filter;
        SelectedNavSection = "Workspace";
    }

    [RelayCommand]
    private void SetSelectedNavSection(string section)
    {
        SelectedNavSection = section;
    }

    [RelayCommand]
    private void SelectTemplate(CodeTemplate template)
    {
        SelectedTemplate = template;
    }

    [RelayCommand]
    private void SelectWorkspaceItem(WorkspaceItemSummary item)
    {
        SelectedWorkspaceItem = item;
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchQuery = string.Empty;
    }

    [RelayCommand]
    private void ResetFilters()
    {
        SearchQuery = string.Empty;
        SelectedTypeFilter = "All";
    }

    private void ApplyFilter()
    {
        FilteredItems.Clear();

        var query = SearchQuery.Trim().ToLowerInvariant();
        HasSearchQuery = !string.IsNullOrEmpty(query);
        var typeFilter = SelectedTypeFilter;

        var matches = AllItems.Where(item =>
        {
            if (typeFilter == "Scripts" && !item.IsScript) return false;
            if (typeFilter == "Notebooks" && !item.IsNotebook) return false;

            if (string.IsNullOrEmpty(query)) return true;

            return item.Title.ToLowerInvariant().Contains(query) ||
                   item.Description.ToLowerInvariant().Contains(query) ||
                   item.Category.ToLowerInvariant().Contains(query) ||
                   item.ExecutionMode.ToLowerInvariant().Contains(query);
        });

        // Apply sorting
        matches = SelectedSortOption switch
        {
            "Title (A-Z)" => matches.OrderBy(x => x.Title),
            "Execution Count" => matches.OrderByDescending(x => x.ExecutionCount).ThenByDescending(x => x.LastModified),
            _ => matches.OrderByDescending(x => x.LastModified)
        };

        var matchList = matches.ToList();

        // Documents saved outside the library share one .frynbproj/.frycsproj project file per folder
        // (see LocalScriptStorageService) — surface that as a workspace header instead of letting them
        // sit in the list looking like unrelated loose files. The header is a label only: every document
        // still appears right beneath it, individually, and is opened exactly the same way as any other
        // row — grouping never hides or replaces access to the real .frynb/.frycs files.
        var emittedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var match in matchList)
        {
            if (!match.IsExternal)
            {
                FilteredItems.Add(match);
                continue;
            }

            if (!emittedGroups.Add(match.FolderPath)) continue;

            var groupMembers = matchList
                .Where(m => m.IsExternal && string.Equals(m.FolderPath, match.FolderPath, StringComparison.OrdinalIgnoreCase))
                .ToList();

            FilteredItems.Add(new WorkspaceGroupHeaderViewModel
            {
                Title = match.ExternalWorkspaceName,
                FolderPath = match.FolderPath,
                ItemCount = groupMembers.Count
            });

            foreach (var member in groupMembers)
            {
                FilteredItems.Add(member);
            }
        }

        HasFilteredItems = matchList.Count > 0;
        FilteredItemCount = matchList.Count;
    }

    [RelayCommand]
    public async Task OpenItemAsync(WorkspaceItemSummary item)
    {
        if (item == null) return;

        if (item.IsNotebook)
        {
            var nb = await _storageService.LoadNotebookAsync(item.Id);
            if (nb != null)
            {
                _openNotebookAction.Invoke(nb);
            }
        }
        else
        {
            var sc = await _storageService.LoadScriptAsync(item.Id);
            if (sc != null)
            {
                _openScriptAction.Invoke(sc);
            }
        }
    }

    [RelayCommand]
    public async Task CreateNewScriptAsync(string? templateId = null)
    {
        // Guards against creating two documents from one accidental double-click: re-entrant calls
        // while a launch is already in flight are ignored, and so is a call that arrives before the
        // very first LoadWorkspaceItemsAsync (fired unawaited from the constructor) has populated
        // AllItems — both were real ways for an existing/about-to-exist document to look absent.
        if (IsLaunching || IsLoading) return;
        await OpenCreatePromptAsync(WorkspaceItemKind.Script, templateId, "New Automation Script");
    }

    [RelayCommand]
    public async Task CreateNewNotebookAsync(string? templateId = null)
    {
        if (IsLaunching || IsLoading) return;
        await OpenCreatePromptAsync(WorkspaceItemKind.Notebook, templateId, "New Interactive Notebook");
    }

    private Task OpenCreatePromptAsync(WorkspaceItemKind kind, string? templateId, string defaultName)
    {
        _pendingTemplateId = templateId;
        var template = StarterTemplates.FirstOrDefault(t => t.Id == templateId);
        NewItemName = template?.Title ?? defaultName;
        SelectedFolderPath = null;
        LocationWarning = null;
        PendingCreateKind = kind;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void CancelCreatePrompt()
    {
        PendingCreateKind = null;
    }

    [RelayCommand]
    public async Task ConfirmCreateAsync()
    {
        if (PendingCreateKind is not { } kind) return;
        if (IsLaunching || IsLoading) return;

        IsLaunching = true;
        try
        {
            var folderPath = SelectedFolderPath;
            var title = string.IsNullOrWhiteSpace(NewItemName)
                ? (kind == WorkspaceItemKind.Notebook ? "New Interactive Notebook" : "New Automation Script")
                : NewItemName.Trim();

            if (kind == WorkspaceItemKind.Notebook)
            {
                await CreateNewNotebookCoreAsync(_pendingTemplateId, folderPath, title);
            }
            else
            {
                await CreateNewScriptCoreAsync(_pendingTemplateId, folderPath, title);
            }
        }
        finally
        {
            IsLaunching = false;
            PendingCreateKind = null;
        }
    }

    private async Task CreateNewScriptCoreAsync(string? templateId, string? folderPath = null, string? explicitTitle = null)
    {
        var template = StarterTemplates.FirstOrDefault(t => t.Id == templateId);
        var title = explicitTitle ?? template?.Title ?? "New Automation Script";

        if (AllItems.Any(i => i.IsScript && string.Equals(i.Title, title, StringComparison.OrdinalIgnoreCase)))
        {
            title = $"{title} (Copy)";
        }

        var newScript = await _storageService.CreateNewScriptAsync(title, templateId, folderPath);
        await LoadWorkspaceItemsAsync();
        _openScriptAction.Invoke(newScript);
    }

    private async Task CreateNewNotebookCoreAsync(string? templateId, string? folderPath = null, string? explicitTitle = null)
    {
        var template = StarterTemplates.FirstOrDefault(t => t.Id == templateId);
        var title = explicitTitle ?? template?.Title ?? "New Interactive Notebook";

        if (AllItems.Any(i => i.IsNotebook && string.Equals(i.Title, title, StringComparison.OrdinalIgnoreCase)))
        {
            title = $"{title} (Copy)";
        }

        var newNb = await _storageService.CreateNewNotebookAsync(title, templateId, folderPath);
        await LoadWorkspaceItemsAsync();
        _openNotebookAction.Invoke(newNb);
    }

    [RelayCommand]
    public async Task LaunchTemplateAsync(CodeTemplate template)
    {
        if (template == null) return;
        if (IsLaunching || IsLoading) return;

        IsLaunching = true;
        try
        {
            // If an existing workspace item matches this template, open it directly rather than generating duplicate copies
            var existing = AllItems.FirstOrDefault(i =>
                (template.Kind == WorkspaceItemKind.Notebook && i.IsNotebook || template.Kind == WorkspaceItemKind.Script && i.IsScript) &&
                (string.Equals(i.Id, template.Id, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(i.Title, template.Title, StringComparison.OrdinalIgnoreCase)));

            if (existing != null)
            {
                await OpenItemAsync(existing);
                return;
            }

            if (template.Kind == WorkspaceItemKind.Notebook)
            {
                await CreateNewNotebookCoreAsync(template.Id);
            }
            else
            {
                await CreateNewScriptCoreAsync(template.Id);
            }
        }
        finally
        {
            IsLaunching = false;
        }
    }

    [RelayCommand]
    public async Task DeleteItemAsync(WorkspaceItemSummary item)
    {
        if (item == null) return;

        if (ReferenceEquals(SelectedWorkspaceItem, item))
        {
            SelectedWorkspaceItem = null;
        }

        AllItems.Remove(item);
        await _storageService.DeleteItemAsync(item.Id);
        UpdateStats();

        // A full re-filter (rather than a direct FilteredItems.Remove) is required now that the list
        // can contain workspace group headers: deleting the last document in an external folder must
        // also drop its now-empty header, which only ApplyFilter's grouping logic knows how to do.
        ApplyFilter();
    }
}
