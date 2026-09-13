using System;
using System.Collections.ObjectModel;
using System.Linq;
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

    public ObservableCollection<WorkspaceItemSummary> AllItems { get; } = new();
    public ObservableCollection<WorkspaceItemSummary> FilteredItems { get; } = new();
    public ObservableCollection<CodeTemplate> StarterTemplates { get; } = new();

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
        IsLoading = true;
        try
        {
            var list = await _storageService.LoadWorkspaceSummariesAsync();
            AllItems.Clear();
            foreach (var item in list)
            {
                AllItems.Add(item);
            }

            UpdateStats();
            ApplyFilter();
        }
        finally
        {
            IsLoading = false;
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
        ApplyFilter();
    }
    partial void OnSelectedSortOptionChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void SetSelectedTypeFilter(string filter)
    {
        SelectedTypeFilter = filter;
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

        foreach (var match in matches)
        {
            FilteredItems.Add(match);
        }

        HasFilteredItems = FilteredItems.Count > 0;
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
        var template = StarterTemplates.FirstOrDefault(t => t.Id == templateId);
        var title = template != null ? $"{template.Title} Copy" : "New Automation Script";

        var newScript = await _storageService.CreateNewScriptAsync(title, templateId);
        await LoadWorkspaceItemsAsync();
        _openScriptAction.Invoke(newScript);
    }

    [RelayCommand]
    public async Task CreateNewNotebookAsync(string? templateId = null)
    {
        var template = StarterTemplates.FirstOrDefault(t => t.Id == templateId);
        var title = template != null ? $"{template.Title} Copy" : "New Interactive Notebook";

        var newNb = await _storageService.CreateNewNotebookAsync(title, templateId);
        await LoadWorkspaceItemsAsync();
        _openNotebookAction.Invoke(newNb);
    }

    [RelayCommand]
    public async Task LaunchTemplateAsync(CodeTemplate template)
    {
        if (template == null) return;
        if (template.Kind == WorkspaceItemKind.Notebook)
        {
            await CreateNewNotebookAsync(template.Id);
        }
        else
        {
            await CreateNewScriptAsync(template.Id);
        }
    }

    [RelayCommand]
    public async Task DeleteItemAsync(WorkspaceItemSummary item)
    {
        if (item == null) return;

        AllItems.Remove(item);
        FilteredItems.Remove(item);
        await _storageService.DeleteItemAsync(item.Id);
        UpdateStats();
    }
}
