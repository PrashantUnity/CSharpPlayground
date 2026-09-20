using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

public partial class CSharpDocsViewModel : ObservableObject
{
    private readonly DocumentationService _docService;
    private readonly Action? _backToHubAction;
    private readonly Action<ScriptDocumentItem>? _openScriptAction;
    private readonly Action<NotebookDocumentItem>? _openNotebookAction;

    [ObservableProperty]
    private DocCategory? _selectedCategory;

    [ObservableProperty]
    private DocArticle? _selectedArticle;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _hasSearchQuery;

    [ObservableProperty]
    private string? _copiedSnippetId;

    [ObservableProperty]
    private string _activeBreadcrumb = "Overview";

    public ObservableCollection<DocCategory> Categories { get; } = new();
    public ObservableCollection<DocArticle> FilteredArticles { get; } = new();

    public bool HasNextArticle => GetNextArticle() != null;
    public bool HasPreviousArticle => GetPreviousArticle() != null;

    public CSharpDocsViewModel(
        DocumentationService? docService = null,
        Action? backToHubAction = null,
        Action<ScriptDocumentItem>? openScriptAction = null,
        Action<NotebookDocumentItem>? openNotebookAction = null)
    {
        _docService = docService ?? DocumentationService.Instance;
        _backToHubAction = backToHubAction;
        _openScriptAction = openScriptAction;
        _openNotebookAction = openNotebookAction;

        LoadDocumentation();
    }

    private void LoadDocumentation()
    {
        Categories.Clear();
        foreach (var category in _docService.Categories)
        {
            Categories.Add(category);
        }

        if (Categories.Count > 0)
        {
            SelectedCategory = Categories[0];
            if (SelectedCategory.Articles.Count > 0)
            {
                SelectedArticle = SelectedCategory.Articles[0];
            }
        }

        UpdateBreadcrumb();
    }

    partial void OnSelectedArticleChanged(DocArticle? value)
    {
        UpdateBreadcrumb();
        OnPropertyChanged(nameof(HasNextArticle));
        OnPropertyChanged(nameof(HasPreviousArticle));
    }

    partial void OnSearchQueryChanged(string value)
    {
        HasSearchQuery = !string.IsNullOrWhiteSpace(value);
        FilteredArticles.Clear();

        if (HasSearchQuery)
        {
            var matches = _docService.SearchArticles(value);
            foreach (var match in matches)
            {
                FilteredArticles.Add(match);
            }
        }
    }

    private void UpdateBreadcrumb()
    {
        if (SelectedArticle != null && SelectedCategory != null)
        {
            ActiveBreadcrumb = $"{SelectedCategory.Title} > {SelectedArticle.Title}";
        }
        else if (SelectedArticle != null)
        {
            ActiveBreadcrumb = SelectedArticle.Title;
        }
        else
        {
            ActiveBreadcrumb = "Documentation";
        }
    }

    [RelayCommand]
    public void SelectCategory(DocCategory? category)
    {
        if (category == null) return;
        SelectedCategory = category;
        if (category.Articles.Count > 0)
        {
            SelectedArticle = category.Articles[0];
        }
    }

    [RelayCommand]
    public void SelectArticle(DocArticle? article)
    {
        if (article == null) return;
        SelectedArticle = article;

        var parentCategory = Categories.FirstOrDefault(c => c.Id == article.CategoryId);
        if (parentCategory != null)
        {
            SelectedCategory = parentCategory;
        }

        if (HasSearchQuery)
        {
            SearchQuery = string.Empty;
        }
    }

    public void SelectTopic(string topicId)
    {
        var article = _docService.GetArticle(topicId);
        if (article != null)
        {
            SelectArticle(article);
        }
    }

    [RelayCommand]
    public void ClearSearch()
    {
        SearchQuery = string.Empty;
    }

    [RelayCommand]
    public async Task CopySnippetAsync(DocCodeSnippet? snippet)
    {
        if (snippet == null || string.IsNullOrWhiteSpace(snippet.Code)) return;

        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow?.Clipboard != null)
            {
                await desktop.MainWindow.Clipboard.SetTextAsync(snippet.Code);
            }
        }
        catch
        {
            // Fallback
        }

        CopiedSnippetId = snippet.Id;
        _ = Task.Delay(2000).ContinueWith(_ =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (CopiedSnippetId == snippet.Id)
                {
                    CopiedSnippetId = null;
                }
            });
        });
    }

    [RelayCommand]
    public void TryInStudio(DocCodeSnippet? snippet)
    {
        if (snippet == null) return;

        if (snippet.TargetKind == WorkspaceItemKind.Notebook)
        {
            var notebook = _docService.CreateNotebookFromSnippet(snippet);
            _openNotebookAction?.Invoke(notebook);
        }
        else
        {
            var script = _docService.CreateScriptFromSnippet(snippet);
            _openScriptAction?.Invoke(script);
        }
    }

    [RelayCommand]
    public void TryInScriptStudio(DocCodeSnippet? snippet)
    {
        if (snippet == null) return;
        var script = _docService.CreateScriptFromSnippet(snippet);
        _openScriptAction?.Invoke(script);
    }

    [RelayCommand]
    public void TryInNotebookStudio(DocCodeSnippet? snippet)
    {
        if (snippet == null) return;
        var notebook = _docService.CreateNotebookFromSnippet(snippet);
        _openNotebookAction?.Invoke(notebook);
    }

    [RelayCommand]
    public void OpenCodeStudio()
    {
        var initialScript = new ScriptDocumentItem
        {
            Title = "Algorithm Workspace",
            Code = CodeTemplateLibrary.GetTemplates()[0].InitialCode,
            Notes = CodeTemplateLibrary.GetTemplates()[0].Notes
        };
        _openScriptAction?.Invoke(initialScript);
    }

    [RelayCommand]
    public void OpenNotebookStudio()
    {
        var initialNotebook = new NotebookDocumentItem
        {
            Title = "Interactive C# Notebook"
        };
        _openNotebookAction?.Invoke(initialNotebook);
    }

    [RelayCommand]
    public void BackToHub()
    {
        _backToHubAction?.Invoke();
    }

    [RelayCommand]
    public void NextArticle()
    {
        var next = GetNextArticle();
        if (next != null)
        {
            SelectArticle(next);
        }
    }

    [RelayCommand]
    public void PreviousArticle()
    {
        var prev = GetPreviousArticle();
        if (prev != null)
        {
            SelectArticle(prev);
        }
    }

    private DocArticle? GetNextArticle()
    {
        if (SelectedArticle == null) return null;
        var allArticles = Categories.SelectMany(c => c.Articles).ToList();
        int idx = allArticles.FindIndex(a => a.Id == SelectedArticle.Id);
        if (idx >= 0 && idx < allArticles.Count - 1)
        {
            return allArticles[idx + 1];
        }
        return null;
    }

    private DocArticle? GetPreviousArticle()
    {
        if (SelectedArticle == null) return null;
        var allArticles = Categories.SelectMany(c => c.Articles).ToList();
        int idx = allArticles.FindIndex(a => a.Id == SelectedArticle.Id);
        if (idx > 0)
        {
            return allArticles[idx - 1];
        }
        return null;
    }
}
