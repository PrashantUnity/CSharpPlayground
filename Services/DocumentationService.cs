using System;
using System.Collections.Generic;
using System.Linq;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

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
        var visualizerCategory = BuildVisualizersCategory();
        var displayCategory = BuildDisplayApisCategory();
        var shortcutsCategory = BuildShortcutsCategory();

        _categories.Add(appCategory);
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
        _categories.Add(visualizerCategory);
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
                }

                if (score > 0)
                {
                    results.Add((article, score));
                }
            }
        }

        return results.OrderByDescending(r => r.Score).Select(r => r.Article).ToList();
    }

    public ScriptDocumentItem CreateScriptFromSnippet(DocCodeSnippet snippet)
    {
        return new ScriptDocumentItem
        {
            Title = string.IsNullOrWhiteSpace(snippet.Title) ? "Docs Sample Script" : snippet.Title,
            Code = snippet.Code,
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
}
