using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Documentation;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;
using PdfEditorApp.Plugins.CSharpEditor.Services.Templates;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.Docs;

public partial class CSharpDocsViewModel : ObservableObject, IDisposable
{
    private readonly DocumentationService _docService;
    private readonly Action? _backToHubAction;
    private readonly Action<ScriptDocumentItem>? _openScriptAction;
    private readonly Action<NotebookDocumentItem>? _openNotebookAction;
    private readonly IScriptStorageService? _storageService;
    private readonly StudioLanguageServices? _languages;
    private readonly Lazy<SnippetRunService> _runService;

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

    [ObservableProperty]
    private bool _isOutlineVisible = true;

    public ObservableCollection<DocCategory> Categories { get; } = new();
    public ObservableCollection<DocArticle> FilteredArticles { get; } = new();

    public bool HasNextArticle => GetNextArticle() != null;
    public bool HasPreviousArticle => GetPreviousArticle() != null;

    public CSharpDocsViewModel(
        DocumentationService? docService = null,
        Action? backToHubAction = null,
        Action<ScriptDocumentItem>? openScriptAction = null,
        Action<NotebookDocumentItem>? openNotebookAction = null,
        IScriptStorageService? storageService = null,
        StudioLanguageServices? languages = null)
    {
        _docService = docService ?? DocumentationService.Instance;
        _backToHubAction = backToHubAction;
        _openScriptAction = openScriptAction;
        _openNotebookAction = openNotebookAction;
        _storageService = storageService;
        _languages = languages;
        // Holds no kernel until a sample is run.
        _runService = new Lazy<SnippetRunService>(() => new SnippetRunService((_languages ?? StudioLanguageServices.Default).Registry));

        LoadDocumentation();
        _docService.Changed += OnDocumentationChanged;
    }

    private void OnDocumentationChanged()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var curCatId = SelectedCategory?.Id;
            var curArtId = SelectedArticle?.Id;

            Categories.Clear();
            foreach (var category in _docService.Categories)
            {
                Categories.Add(category);
            }

            if (curCatId != null)
            {
                SelectedCategory = Categories.FirstOrDefault(c => string.Equals(c.Id, curCatId, StringComparison.OrdinalIgnoreCase));
            }
            SelectedCategory ??= Categories.FirstOrDefault();

            if (curArtId != null && SelectedCategory != null)
            {
                SelectedArticle = SelectedCategory.Articles.FirstOrDefault(a => string.Equals(a.Id, curArtId, StringComparison.OrdinalIgnoreCase));
            }
            SelectedArticle ??= SelectedCategory?.Articles.FirstOrDefault();

            UpdateBreadcrumb();
        });
    }

    private void LoadDocumentation()
    {
        Categories.Clear();
        foreach (var category in _docService.Categories.ToList())
        {
            Categories.Add(category);
        }

        if (Categories.Count > 0)
        {
            SelectedCategory = Categories[0];
            SelectedCategory.IsExpanded = true;
            if (SelectedCategory.Articles.Count > 0)
            {
                SelectedArticle = SelectedCategory.Articles[0];
            }
        }

        UpdateBreadcrumb();
    }

    partial void OnSelectedCategoryChanged(DocCategory? oldValue, DocCategory? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
        UpdateBreadcrumb();
    }

    partial void OnSelectedArticleChanged(DocArticle? oldValue, DocArticle? newValue)
    {
        if (oldValue != null)
        {
            oldValue.IsSelected = false;
            StopRuns(oldValue);
        }

        if (newValue != null)
        {
            newValue.IsSelected = true;
            AttachRunners(newValue);
        }

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

    // Every sample of the article gets its Run button and output (a small object; nothing runs until it is pressed).
    private void AttachRunners(DocArticle article)
    {
        foreach (var snippet in article.CodeSnippets)
        {
            snippet.Run ??= new SnippetRunViewModel(_runService.Value, snippet);
        }
    }

    // A sample doesn't keep running (or drawing) in an article nobody is reading.
    private static void StopRuns(DocArticle article)
    {
        foreach (var snippet in article.CodeSnippets) snippet.Run?.Stop();
    }

    public void Dispose()
    {
        _docService.Changed -= OnDocumentationChanged;
        if (SelectedArticle != null) StopRuns(SelectedArticle);
        if (_runService.IsValueCreated) _runService.Value.Dispose();
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
        category.IsExpanded = true;
        if (category.Articles.Count > 0)
        {
            SelectedArticle = category.Articles[0];
        }
    }

    [RelayCommand]
    public void ToggleCategory(DocCategory? category)
    {
        if (category == null) return;
        category.IsExpanded = !category.IsExpanded;
    }

    [RelayCommand]
    public void SelectArticle(DocArticle? article)
    {
        if (article == null) return;

        // The category first, so the breadcrumb and badge never show the previous one next to this article.
        var parentCategory = Categories.FirstOrDefault(c => c.Id == article.CategoryId);
        if (parentCategory != null)
        {
            SelectedCategory = parentCategory;
            parentCategory.IsExpanded = true;
        }
        SelectedArticle = article;

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
    public async Task TryInStudioAsync(DocCodeSnippet? snippet)
    {
        if (snippet == null) return;

        if (snippet.TargetKind == WorkspaceItemKind.Notebook)
        {
            var notebook = _docService.CreateNotebookFromSnippet(snippet);
            _openNotebookAction?.Invoke(notebook);
        }
        else
        {
            var script = await PrepareScriptFromSnippetAsync(snippet);
            _openScriptAction?.Invoke(script);
        }
    }

    public void TryInStudio(DocCodeSnippet? snippet)
    {
        _ = TryInStudioAsync(snippet);
    }

    [RelayCommand]
    public async Task TryInScriptStudioAsync(DocCodeSnippet? snippet)
    {
        if (snippet == null) return;
        var script = await PrepareScriptFromSnippetAsync(snippet);
        _openScriptAction?.Invoke(script);
    }

    public void TryInScriptStudio(DocCodeSnippet? snippet)
    {
        _ = TryInScriptStudioAsync(snippet);
    }

    private async Task<ScriptDocumentItem> PrepareScriptFromSnippetAsync(DocCodeSnippet snippet)
    {
        if (_storageService != null)
        {
            var registry = _languages?.Registry ?? StudioLanguageServices.Default.Registry;
            var lang = registry.Get(snippet.Language);
            var isSourceFileLang = lang != null && lang.Storage == LanguageStorageKind.SourceFile;
            var cleanTitle = DocumentationService.CleanSnippetTitle(snippet.Title);

            if (isSourceFileLang)
            {
                var sourceFile = await _storageService.CreateNewSourceFileAsync(
                    lang!.Id,
                    cleanTitle,
                    folderPath: null,
                    initialContent: snippet.Code);
                if (sourceFile != null)
                {
                    return sourceFile;
                }
            }
            else
            {
                var script = _docService.CreateScriptFromSnippet(snippet);
                await _storageService.SaveScriptAsync(script);
                return script;
            }
        }

        return _docService.CreateScriptFromSnippet(snippet);
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
        if (SelectedArticle != null)
        {
            var articleNotebook = _docService.CreateNotebookFromArticle(SelectedArticle);
            _openNotebookAction?.Invoke(articleNotebook);
            return;
        }

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
