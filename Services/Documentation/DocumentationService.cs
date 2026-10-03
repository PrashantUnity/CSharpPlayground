using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Documentation;

public partial class DocumentationService
{
    private static readonly Lazy<DocumentationService> _instance = new(() => new DocumentationService());
    public static DocumentationService Instance => _instance.Value;

    private readonly List<DocCategory> _categories;
    private readonly Dictionary<string, DocArticle> _articlesById;

    public IReadOnlyList<DocCategory> Categories => _categories;

    public DocumentationService()
    {
        _categories = new List<DocCategory>();
        _articlesById = new Dictionary<string, DocArticle>(StringComparer.OrdinalIgnoreCase);

        InitializeDocumentation();
    }

    private void InitializeDocumentation()
    {
        var appCategory = BuildAppGuideCategory();
        var extensibilityCategory = BuildExtensibilityAndCustomizationCategory();
        var learnCSharpCategory = BuildLearnCSharpCategory();
        var fundamentalsCategory = BuildFundamentalsCategory();
        var oopCategory = BuildOopCategory();
        var memoryManagementCategory = BuildMemoryManagementCategory();
        var collectionsGenericsCategory = BuildCollectionsGenericsCategory();
        var linqCategory = BuildLinqCategory();
        var delegatesEventsCategory = BuildDelegatesEventsCategory();
        var asyncParallelCategory = BuildAsyncParallelCategory();
        var exceptionLoggingCategory = BuildExceptionLoggingCategory();
        var dotNetRuntimeCategory = BuildDotNetRuntimeCategory();
        var fileSerializationCategory = BuildFileSerializationCategory();
        var diCleanCodeCategory = BuildDiCleanCodeCategory();
        var designPatternsCategory = BuildDesignPatternsCategory();
        var aspNetCoreCategory = BuildAspNetCoreCategory();
        var efCoreCategory = BuildEfCoreCategory();
        var testingDebuggingCategory = BuildTestingDebuggingCategory();
        var advancedTopicsCategory = BuildAdvancedTopicsCategory();
        var diagramsCategory = BuildDiagramsAndVisualizationsCategory();
        var visualizerCategory = BuildVisualizersCategory();
        var polyglotVisualsCategory = BuildPolyglotVisualsCategory();
        var displayCategory = BuildDisplayApisCategory();
        var shortcutsCategory = BuildShortcutsCategory();

        _categories.Add(appCategory);
        _categories.Add(extensibilityCategory);
        _categories.Add(learnCSharpCategory);
        _categories.Add(fundamentalsCategory);
        _categories.Add(oopCategory);
        _categories.Add(memoryManagementCategory);
        _categories.Add(collectionsGenericsCategory);
        _categories.Add(linqCategory);
        _categories.Add(delegatesEventsCategory);
        _categories.Add(asyncParallelCategory);
        _categories.Add(exceptionLoggingCategory);
        _categories.Add(dotNetRuntimeCategory);
        _categories.Add(fileSerializationCategory);
        _categories.Add(diCleanCodeCategory);
        _categories.Add(designPatternsCategory);
        _categories.Add(aspNetCoreCategory);
        _categories.Add(efCoreCategory);
        _categories.Add(testingDebuggingCategory);
        _categories.Add(advancedTopicsCategory);
        _categories.Add(diagramsCategory);
        _categories.Add(visualizerCategory);
        _categories.Add(polyglotVisualsCategory);
        _categories.Add(displayCategory);
        _categories.Add(shortcutsCategory);

        foreach (var category in _categories)
        {
            foreach (var article in category.Articles)
            {
                article.CategoryId = category.Id;
                _articlesById[article.Id] = article;
            }
        }
    }

    public event Action? Changed;

    public IDisposable RegisterCategory(DocCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);
        lock (_categories)
        {
            var existing = _categories.FirstOrDefault(c => string.Equals(c.Id, category.Id, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                _categories.Remove(existing);
                foreach (var art in existing.Articles)
                {
                    _articlesById.Remove(art.Id);
                }
            }

            _categories.Add(category);
            foreach (var article in category.Articles)
            {
                article.CategoryId = category.Id;
                _articlesById[article.Id] = article;
            }
        }

        Changed?.Invoke();
        return new RegistrationToken(() => UnregisterCategory(category.Id));
    }

    public bool UnregisterCategory(string categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId)) return false;
        bool removed = false;
        lock (_categories)
        {
            var existing = _categories.FirstOrDefault(c => string.Equals(c.Id, categoryId, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                _categories.Remove(existing);
                foreach (var art in existing.Articles)
                {
                    _articlesById.Remove(art.Id);
                }
                removed = true;
            }
        }

        if (removed)
        {
            Changed?.Invoke();
        }
        return removed;
    }

    private sealed class RegistrationToken : IDisposable
    {
        private Action? _dispose;
        public RegistrationToken(Action dispose) => _dispose = dispose;
        public void Dispose() => System.Threading.Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }

    public DocArticle? GetArticle(string articleId)
    {
        if (string.IsNullOrWhiteSpace(articleId)) return null;
        _articlesById.TryGetValue(articleId, out var article);
        return article;
    }

    public List<DocArticle> SearchArticles(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return _categories.SelectMany(c => c.Articles).ToList();
        }

        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var results = new List<(DocArticle Article, int Score)>();

        foreach (var category in _categories)
        {
            foreach (var article in category.Articles)
            {
                int score = 0;
                foreach (var term in terms)
                {
                    if (article.Title.Contains(term, StringComparison.OrdinalIgnoreCase))
                        score += 20;
                    if (article.Subtitle.Contains(term, StringComparison.OrdinalIgnoreCase))
                        score += 10;
                    if (article.Keywords.Any(k => k.Contains(term, StringComparison.OrdinalIgnoreCase)))
                        score += 15;
                    if (article.Summary.Contains(term, StringComparison.OrdinalIgnoreCase))
                        score += 5;
                    if (category.Title.Contains(term, StringComparison.OrdinalIgnoreCase))
                        score += 8;

                    foreach (var sig in article.ApiSignatures)
                    {
                        if (sig.MethodName.Contains(term, StringComparison.OrdinalIgnoreCase))
                            score += 25;
                        if (sig.Description.Contains(term, StringComparison.OrdinalIgnoreCase))
                            score += 5;
                    }

                    foreach (var snip in article.CodeSnippets)
                    {
                        if (snip.Title.Contains(term, StringComparison.OrdinalIgnoreCase))
                            score += 10;
                        if (snip.Code.Contains(term, StringComparison.OrdinalIgnoreCase))
                            score += 5;
                    }

                    foreach (var sc in article.Shortcuts)
                    {
                        if (sc.Action.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                            sc.MacKey.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                            sc.WinKey.Contains(term, StringComparison.OrdinalIgnoreCase))
                            score += 15;
                    }

                    foreach (var sec in article.Sections)
                    {
                        if (sec.Heading.Contains(term, StringComparison.OrdinalIgnoreCase))
                            score += 10;
                        if (sec.Content.Contains(term, StringComparison.OrdinalIgnoreCase))
                            score += 5;
                    }
                }

                if (score > 0)
                {
                    results.Add((article, score));
                }
            }
        }

        return results.OrderByDescending(r => r.Score).Select(r => r.Article).ToList();
    }

    public static string CleanSnippetTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return string.Empty;
        var trimmed = title.Trim();
        if (trimmed.StartsWith("Codes [", StringComparison.OrdinalIgnoreCase))
        {
            var colonIndex = trimmed.IndexOf(" : ", StringComparison.Ordinal);
            if (colonIndex >= 0)
            {
                return trimmed[(colonIndex + 3)..].Trim();
            }
        }
        return trimmed;
    }

    public ScriptDocumentItem CreateScriptFromSnippet(DocCodeSnippet snippet)
    {
        var lang = Languages.StudioLanguageServices.Default.Registry.Get(snippet.Language);
        var cleanTitle = CleanSnippetTitle(snippet.Title);
        var isSourceFile = lang != null && lang.Storage == Languages.LanguageStorageKind.SourceFile;
        var ext = isSourceFile ? lang!.DefaultExtension() : string.Empty;
        var title = isSourceFile && !cleanTitle.EndsWith(ext, StringComparison.OrdinalIgnoreCase)
            ? $"{cleanTitle}{ext}"
            : cleanTitle;

        return new ScriptDocumentItem
        {
            Title = string.IsNullOrWhiteSpace(title) ? (isSourceFile ? $"Docs Sample Script{ext}" : "Docs Sample Script") : title,
            Code = snippet.Code,
            LanguageId = lang?.Id ?? snippet.Language ?? Languages.LanguageIds.CSharp,
            Notes = $"# {snippet.Title}\n\n{snippet.Description}\n\nGenerated from C# Code Studio Documentation."
        };
    }

    public NotebookDocumentItem CreateNotebookFromSnippet(DocCodeSnippet snippet)
    {
        var notebook = new NotebookDocumentItem
        {
            Title = string.IsNullOrWhiteSpace(snippet.Title) ? "Docs Sample Notebook" : snippet.Title
        };

        notebook.Cells.Add(new NotebookCellItem
        {
            Type = CellType.Markdown,
            Source = $"# 📘 {snippet.Title}\n\n{snippet.Description}\n\n*Created from C# Code Studio Documentation.*",
            IsMarkdownPreviewMode = true
        });

        notebook.Cells.Add(new NotebookCellItem
        {
            Type = CellType.Code,
            Source = snippet.Code,
            // A Python snippet is a Python cell; C# is the notebook's own language.
            Language = Languages.StudioLanguageServices.Default.Registry.Get(snippet.Language) is { Id: not Languages.LanguageIds.CSharp } language ? language.Id : null
        });

        return notebook;
    }

    public NotebookDocumentItem CreateNotebookFromArticle(DocArticle article)
    {
        var notebook = new NotebookDocumentItem
        {
            Title = string.IsNullOrWhiteSpace(article.Title) ? "Docs Sample Notebook" : article.Title
        };

        var mdIntro = $"# 📘 {article.Title}\n\n{article.Summary}";
        if (!string.IsNullOrWhiteSpace(article.Subtitle))
            mdIntro += $"\n\n*{article.Subtitle}*";

        notebook.Cells.Add(new NotebookCellItem
        {
            Type = CellType.Markdown,
            Source = mdIntro,
            IsMarkdownPreviewMode = true
        });

        foreach (var section in article.Sections)
        {
            var secMd = $"### {section.Heading}\n\n{section.Content}";
            if (!string.IsNullOrWhiteSpace(section.CalloutText))
                secMd += $"\n\n> **Tip**: {section.CalloutText}";

            notebook.Cells.Add(new NotebookCellItem
            {
                Type = CellType.Markdown,
                Source = secMd,
                IsMarkdownPreviewMode = true
            });
        }

        foreach (var snippet in article.CodeSnippets)
        {
            notebook.Cells.Add(new NotebookCellItem
            {
                Type = CellType.Markdown,
                Source = $"#### {snippet.Title}\n{snippet.Description}",
                IsMarkdownPreviewMode = true
            });

            notebook.Cells.Add(new NotebookCellItem
            {
                Type = CellType.Code,
                Source = snippet.Code,
                Language = Languages.StudioLanguageServices.Default.Registry.Get(snippet.Language) is { Id: not Languages.LanguageIds.CSharp } language ? language.Id : null
            });
        }

        return notebook;
    }
}
