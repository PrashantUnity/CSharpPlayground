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
    private bool _isLoading;

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

    public CSharpManagerViewModel(
        IScriptStorageService storageService,
        Action<ScriptDocumentItem> openScriptAction,
        Action<NotebookDocumentItem> openNotebookAction)
    {
        _storageService = storageService;
        _openScriptAction = openScriptAction;
        _openNotebookAction = openNotebookAction;

        foreach (var t in CodeTemplateLibrary.GetTemplates())
        {
            StarterTemplates.Add(t);
        }

        _ = LoadWorkspaceItemsAsync();
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
    partial void OnSelectedTypeFilterChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void SetSelectedTypeFilter(string filter)
    {
        SelectedTypeFilter = filter;
    }

    private void ApplyFilter()
    {
        FilteredItems.Clear();

        var query = SearchQuery.Trim().ToLowerInvariant();
        var typeFilter = SelectedTypeFilter;

        var matches = AllItems.Where(item =>
        {
            if (typeFilter == "Scripts" && !item.IsScript) return false;
            if (typeFilter == "Notebooks" && !item.IsNotebook) return false;

            if (string.IsNullOrEmpty(query)) return true;

            return item.Title.ToLowerInvariant().Contains(query) ||
                   item.Description.ToLowerInvariant().Contains(query) ||
                   item.Category.ToLowerInvariant().Contains(query);
        });

        foreach (var match in matches)
        {
            FilteredItems.Add(match);
        }
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
    public async Task DeleteItemAsync(WorkspaceItemSummary item)
    {
        if (item == null) return;

        AllItems.Remove(item);
        FilteredItems.Remove(item);
        await _storageService.DeleteItemAsync(item.Id);
        UpdateStats();
    }
}
