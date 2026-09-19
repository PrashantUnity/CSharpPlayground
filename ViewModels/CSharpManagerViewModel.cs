using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
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
    private string _selectedTypeFilter = "All";

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

    [ObservableProperty]
    private string _selectedNavSection = "Templates";

    private bool _hasAppliedInitialNavDefault;

    [ObservableProperty]
    private CodeTemplate? _selectedTemplate;

    [ObservableProperty]
    private WorkspaceItemSummary? _selectedWorkspaceItem;

    [ObservableProperty]
    private bool _isLaunching;

    [ObservableProperty]
    private WorkspaceItemKind? _pendingCreateKind;

    [ObservableProperty]
    private string _newItemName = string.Empty;

    [ObservableProperty]
    private string? _selectedFolderPath;

    [ObservableProperty]
    private string? _locationWarning;

    [ObservableProperty]
    private string? _statusBannerMessage;

    [ObservableProperty]
    private bool _hasStatusBannerMessage;

    [ObservableProperty]
    private bool _isStatusBannerError;

    private string? _pendingTemplateId;

    public bool IsCreatePromptOpen => PendingCreateKind.HasValue;
    public string CreatePromptTitle => PendingCreateKind == WorkspaceItemKind.Notebook ? "New Notebook" : "New Script";
    public bool IsCreatingNotebook => PendingCreateKind == WorkspaceItemKind.Notebook;
    public bool IsCreatingScript => PendingCreateKind == WorkspaceItemKind.Script;
    public string SelectedFolderDisplay => string.IsNullOrEmpty(SelectedFolderPath) ? "Workspace root" : SelectedFolderPath;

    public string LibraryRootPath => _storageService.LibraryRootPath;

    public ObservableCollection<WorkspaceItemSummary> AllItems { get; } = new();

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

    public bool IsWorkspaceAllNavActive => IsWorkspaceSectionActive && IsAllFilterActive;
    public bool IsScriptsNavActive => IsWorkspaceSectionActive && IsScriptsFilterActive;
    public bool IsNotebooksNavActive => IsWorkspaceSectionActive && IsNotebooksFilterActive;

    public bool HasSelection => SelectedTemplate != null || SelectedWorkspaceItem != null;
    public bool IsShowingTemplate => SelectedTemplate != null;
    public bool IsShowingItem => SelectedWorkspaceItem != null;

    private readonly HashSet<string> _pinnedItemIds = new(StringComparer.OrdinalIgnoreCase);
    public ObservableCollection<WorkspaceItemSummary> PinnedItems { get; } = new();
    public ObservableCollection<WorkspaceItemSummary> UnpinnedItems { get; } = new();

    private string PinnedWorkspacesFilePath =>
        Path.Combine(Path.GetDirectoryName(LibraryRootPath) ?? LibraryRootPath, "pinned_workspaces.json");

    public WorkspaceItemSummary? PinnedItem1 => PinnedItems.Count > 0 ? PinnedItems[0] : null;
    public WorkspaceItemSummary? PinnedItem2 => PinnedItems.Count > 1 ? PinnedItems[1] : null;
    public bool HasPinnedItem1 => PinnedItem1 != null;
    public bool HasPinnedItem2 => PinnedItem2 != null;
    public bool HasAnyPinnedItems => PinnedItems.Count > 0;
    public bool HasUnpinnedItems => UnpinnedItems.Count > 0;

    public WorkspaceItemSummary? RecentNotebook => PinnedItem1?.IsNotebook == true ? PinnedItem1
        : (PinnedItem2?.IsNotebook == true ? PinnedItem2 : AllItems.FirstOrDefault(i => i.IsNotebook));

    public WorkspaceItemSummary? RecentScript => PinnedItem1?.IsScript == true ? PinnedItem1
        : (PinnedItem2?.IsScript == true ? PinnedItem2 : AllItems.FirstOrDefault(i => i.IsScript));

    public bool HasRecentNotebook => RecentNotebook != null;
    public bool HasRecentScript => RecentScript != null;

    public string RecentNotebookTitle => RecentNotebook?.Title ?? "customer_churn_model.ipynb";
    public string RecentNotebookPath => RecentNotebook?.DisplayLocation ?? "~/library/";
    public string RecentNotebookTime => RecentNotebook?.FormattedLastModified ?? "2 hours ago";

    public string RecentScriptTitle => RecentScript?.Title ?? "migrate_users_v2.linq";
    public string RecentScriptPath => RecentScript?.DisplayLocation ?? "~/library/";
    public string RecentScriptTime => RecentScript?.FormattedLastModified ?? "Yesterday";

    private long _diskStorageBytes;
    private int _diskStorageFileCount;
    private readonly DispatcherTimer? _telemetryTimer;

    public string RoslynEngineTitle => "Local Roslyn Engine";
    public string RoslynEngineStatus => "Ready • Roslyn 4.12 & C# 13";
    public bool IsRoslynEngineActive => true;

    public string StorageEngineTitle => "Document Storage";
    public string StorageEngineStatus => Directory.Exists(LibraryRootPath)
        ? $"Connected • {_diskStorageFileCount} docs on disk"
        : "Connected • Library Active";
    public bool IsStorageEngineActive => true;

    public string MemoryUsageText
    {
        get
        {
            try
            {
                var wsBytes = Process.GetCurrentProcess().WorkingSet64;
                var sysBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
                var wsMb = wsBytes / (1024.0 * 1024.0);
                var sysGb = Math.Max(1.0, sysBytes / (1024.0 * 1024.0 * 1024.0));

                if (wsMb >= 1024.0)
                {
                    return $"{wsMb / 1024.0:F2} GB / {sysGb:F0} GB";
                }
                return $"{wsMb:F0} MB / {sysGb:F0} GB";
            }
            catch
            {
                var bytes = GC.GetTotalMemory(false);
                var mb = bytes / (1024.0 * 1024.0);
                return $"{mb:F0} MB Heap";
            }
        }
    }

    public double MemoryUsagePercent
    {
        get
        {
            try
            {
                var wsBytes = Process.GetCurrentProcess().WorkingSet64;
                var wsMb = wsBytes / (1024.0 * 1024.0);
                return Math.Clamp((wsMb / 2048.0) * 100.0, 4.0, 100.0);
            }
            catch
            {
                return 6.0;
            }
        }
    }

    public string StorageUsageText
    {
        get
        {
            if (_diskStorageBytes <= 0)
            {
                return $"{TotalScripts + TotalNotebooks} docs";
            }
            if (_diskStorageBytes < 1024)
            {
                return $"{_diskStorageBytes} B";
            }
            if (_diskStorageBytes < 1024 * 1024)
            {
                return $"{_diskStorageBytes / 1024.0:F1} KB";
            }
            return $"{_diskStorageBytes / (1024.0 * 1024.0):F2} MB";
        }
    }

    public double StorageUsagePercent
    {
        get
        {
            if (_diskStorageBytes <= 0) return 4.0;
            return Math.Clamp((_diskStorageBytes / (1024.0 * 1024.0 * 50.0)) * 100.0, 4.0, 100.0);
        }
    }

    public bool IsLocalServerRunning => true;

    private async Task UpdateDiskStorageAsync()
    {
        var (bytes, count) = await Task.Run(() =>
        {
            long b = 0;
            int c = 0;
            try
            {
                if (!string.IsNullOrEmpty(LibraryRootPath) && Directory.Exists(LibraryRootPath))
                {
                    var dir = new DirectoryInfo(LibraryRootPath);
                    foreach (var file in dir.EnumerateFiles("*", SearchOption.AllDirectories))
                    {
                        if (file.Name.StartsWith(".")) continue;
                        b += file.Length;
                        c++;
                    }
                }
            }
            catch { }
            return (b, c);
        });

        _diskStorageBytes = bytes;
        _diskStorageFileCount = count;
        OnPropertyChanged(nameof(StorageUsageText));
        OnPropertyChanged(nameof(StorageUsagePercent));
        OnPropertyChanged(nameof(StorageEngineStatus));
    }

    [RelayCommand]
    public async Task OpenRecentNotebookAsync()
    {
        if (RecentNotebook != null)
        {
            await OpenItemAsync(RecentNotebook);
        }
        else
        {
            await CreateNewNotebookAsync();
        }
    }

    [RelayCommand]
    public async Task OpenRecentScriptAsync()
    {
        if (RecentScript != null)
        {
            await OpenItemAsync(RecentScript);
        }
        else
        {
            await CreateNewScriptAsync();
        }
    }

    [RelayCommand]
    public async Task TogglePinAsync(WorkspaceItemSummary? item)
    {
        if (item == null) return;
        if (_pinnedItemIds.Contains(item.Id))
        {
            _pinnedItemIds.Remove(item.Id);
        }
        else
        {
            _pinnedItemIds.Add(item.Id);
        }
        await SavePinnedStateAsync();
        SyncPinnedItems();
    }

    [RelayCommand]
    public async Task PinItemAsync(WorkspaceItemSummary? item)
    {
        if (item == null) return;
        if (_pinnedItemIds.Add(item.Id))
        {
            await SavePinnedStateAsync();
            SyncPinnedItems();
        }
    }

    [RelayCommand]
    public async Task UnpinItemAsync(WorkspaceItemSummary? item)
    {
        if (item == null) return;
        if (_pinnedItemIds.Remove(item.Id))
        {
            await SavePinnedStateAsync();
            SyncPinnedItems();
        }
    }

    [RelayCommand]
    public async Task OpenPinnedItem1Async()
    {
        if (PinnedItem1 != null)
        {
            await OpenItemAsync(PinnedItem1);
        }
        else
        {
            await CreateNewNotebookAsync();
        }
    }

    [RelayCommand]
    public async Task OpenPinnedItem2Async()
    {
        if (PinnedItem2 != null)
        {
            await OpenItemAsync(PinnedItem2);
        }
        else
        {
            await CreateNewScriptAsync();
        }
    }

    [RelayCommand]
    public async Task RefreshWorkspaceAsync()
    {
        await LoadWorkspaceItemsAsync();
    }

    private async Task LoadPinnedStateAsync()
    {
        try
        {
            if (File.Exists(PinnedWorkspacesFilePath))
            {
                var json = await File.ReadAllTextAsync(PinnedWorkspacesFilePath);
                var ids = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
                if (ids != null)
                {
                    _pinnedItemIds.Clear();
                    foreach (var id in ids) _pinnedItemIds.Add(id);
                }
            }
        }
        catch { }

        if (_pinnedItemIds.Count == 0 && AllItems.Count > 0)
        {
            var nb = AllItems.FirstOrDefault(i => i.IsNotebook);
            if (nb != null) _pinnedItemIds.Add(nb.Id);
            var sc = AllItems.FirstOrDefault(i => i.IsScript && i.Id != nb?.Id);
            if (sc != null) _pinnedItemIds.Add(sc.Id);
            await SavePinnedStateAsync();
        }

        SyncPinnedItems();
    }

    private async Task SavePinnedStateAsync()
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(_pinnedItemIds.ToList(), new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(PinnedWorkspacesFilePath, json);
        }
        catch { }
    }

    private void SyncPinnedItems()
    {
        PinnedItems.Clear();
        UnpinnedItems.Clear();

        foreach (var item in AllItems)
        {
            item.IsPinned = _pinnedItemIds.Contains(item.Id);
            if (item.IsPinned)
            {
                PinnedItems.Add(item);
            }
            else
            {
                UnpinnedItems.Add(item);
            }
        }

        OnPropertyChanged(nameof(PinnedItem1));
        OnPropertyChanged(nameof(PinnedItem2));
        OnPropertyChanged(nameof(HasPinnedItem1));
        OnPropertyChanged(nameof(HasPinnedItem2));
        OnPropertyChanged(nameof(HasAnyPinnedItems));
        OnPropertyChanged(nameof(HasUnpinnedItems));
        OnPropertyChanged(nameof(RecentNotebook));
        OnPropertyChanged(nameof(RecentScript));
        OnPropertyChanged(nameof(RecentNotebookTitle));
        OnPropertyChanged(nameof(RecentNotebookPath));
        OnPropertyChanged(nameof(RecentNotebookTime));
        OnPropertyChanged(nameof(RecentScriptTitle));
        OnPropertyChanged(nameof(RecentScriptPath));
        OnPropertyChanged(nameof(RecentScriptTime));
    }

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

        _ = UpdateDiskStorageAsync();

        try
        {
            _telemetryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _telemetryTimer.Tick += (s, e) =>
            {
                OnPropertyChanged(nameof(MemoryUsageText));
                OnPropertyChanged(nameof(MemoryUsagePercent));
            };
            _telemetryTimer.Start();
        }
        catch
        {
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
            await UpdateDiskStorageAsync();
            await LoadPinnedStateAsync();
            ApplyFilter();

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

        OnPropertyChanged(nameof(RecentNotebook));
        OnPropertyChanged(nameof(RecentScript));
        OnPropertyChanged(nameof(HasRecentNotebook));
        OnPropertyChanged(nameof(HasRecentScript));
        OnPropertyChanged(nameof(RecentNotebookTitle));
        OnPropertyChanged(nameof(RecentNotebookPath));
        OnPropertyChanged(nameof(RecentNotebookTime));
        OnPropertyChanged(nameof(RecentScriptTitle));
        OnPropertyChanged(nameof(RecentScriptPath));
        OnPropertyChanged(nameof(RecentScriptTime));
        OnPropertyChanged(nameof(MemoryUsageText));
        OnPropertyChanged(nameof(MemoryUsagePercent));
        OnPropertyChanged(nameof(StorageUsageText));
        OnPropertyChanged(nameof(StorageUsagePercent));
        OnPropertyChanged(nameof(StorageEngineStatus));
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
        OnPropertyChanged(nameof(IsCreatingNotebook));
        OnPropertyChanged(nameof(IsCreatingScript));
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

        matches = SelectedSortOption switch
        {
            "Title (A-Z)" => matches.OrderBy(x => x.Title),
            "Execution Count" => matches.OrderByDescending(x => x.ExecutionCount).ThenByDescending(x => x.LastModified),
            _ => matches.OrderByDescending(x => x.LastModified)
        };

        var matchList = matches.ToList();

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
        if (_pinnedItemIds.Remove(item.Id))
        {
            await SavePinnedStateAsync();
            SyncPinnedItems();
        }

        await _storageService.DeleteItemAsync(item.Id);
        UpdateStats();
        await UpdateDiskStorageAsync();

        ApplyFilter();
    }

    [RelayCommand]
    public void DismissStatusBanner()
    {
        HasStatusBannerMessage = false;
        StatusBannerMessage = null;
    }

    [RelayCommand]
    public async Task OpenExistingProjectAsync(string? path = null)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (IsLaunching || IsLoading) return;

        IsLaunching = true;
        try
        {
            var result = await _storageService.OpenExternalProjectAsync(path);
            if (result.Success)
            {
                await LoadWorkspaceItemsAsync();
                StatusBannerMessage = result.Message;
                IsStatusBannerError = false;
                HasStatusBannerMessage = true;

                if (!string.IsNullOrEmpty(result.PrimaryDocumentId))
                {
                    if (result.PrimaryDocumentKind == WorkspaceItemKind.Notebook)
                    {
                        var nb = await _storageService.LoadNotebookAsync(result.PrimaryDocumentId);
                        if (nb != null)
                        {
                            _openNotebookAction.Invoke(nb);
                        }
                    }
                    else
                    {
                        var sc = await _storageService.LoadScriptAsync(result.PrimaryDocumentId);
                        if (sc != null)
                        {
                            _openScriptAction.Invoke(sc);
                        }
                    }
                }
            }
            else
            {
                StatusBannerMessage = result.Message;
                IsStatusBannerError = true;
                HasStatusBannerMessage = true;
            }
        }
        catch (Exception ex)
        {
            StatusBannerMessage = $"Failed to open project: {ex.Message}";
            IsStatusBannerError = true;
            HasStatusBannerMessage = true;
        }
        finally
        {
            IsLaunching = false;
        }
    }
}
