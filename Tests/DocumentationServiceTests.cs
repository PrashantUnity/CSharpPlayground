using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class DocumentationServiceTests
{
    [Fact]
    public void DocumentationService_ShouldInitializeAllCategoriesAndArticles()
    {
        var service = DocumentationService.Instance;

        Assert.NotNull(service.Categories);
        Assert.True(service.Categories.Count >= 4, "Expected at least 4 documentation categories.");

        var categoryIds = service.Categories.Select(c => c.Id).ToList();
        Assert.Contains("app_guide", categoryIds);
        Assert.Contains("visualizer_recorder", categoryIds);
        Assert.Contains("display_apis", categoryIds);
        Assert.Contains("shortcuts_category", categoryIds);

        foreach (var cat in service.Categories)
        {
            Assert.False(string.IsNullOrWhiteSpace(cat.Title));
            Assert.False(string.IsNullOrWhiteSpace(cat.AccentColor));
            Assert.NotEmpty(cat.Articles);

            foreach (var art in cat.Articles)
            {
                Assert.False(string.IsNullOrWhiteSpace(art.Id));
                Assert.False(string.IsNullOrWhiteSpace(art.Title));
                Assert.False(string.IsNullOrWhiteSpace(art.Summary));
                Assert.NotEmpty(art.Sections);
                Assert.Equal(cat.Id, art.CategoryId);
            }
        }
    }

    [Fact]
    public void DocumentationService_VisualizerRecorderCategory_ShouldHaveCompleteCoverage()
    {
        var service = DocumentationService.Instance;
        var visCat = service.Categories.FirstOrDefault(c => c.Id == "visualizer_recorder");

        Assert.NotNull(visCat);
        Assert.True(visCat.Articles.Count >= 5, "Expected comprehensive visualizer articles.");

        var articleIds = visCat.Articles.Select(a => a.Id).ToList();
        Assert.Contains("visualizer_overview", articleIds);
        Assert.Contains("arrays_and_bars", articleIds);
        Assert.Contains("matrix_and_board", articleIds);
        Assert.Contains("trees_and_graphs", articleIds);
        Assert.Contains("canvas_recorder", articleIds);
        Assert.Contains("visualizer_helpers", articleIds);

        // Check code snippets
        var allSnippets = visCat.Articles.SelectMany(a => a.CodeSnippets).ToList();
        Assert.NotEmpty(allSnippets);
        Assert.Contains(allSnippets, s => s.Id == "snip_binary_search");
        Assert.Contains(allSnippets, s => s.Id == "snip_quicksort_bars");
        Assert.Contains(allSnippets, s => s.Id == "snip_matrix_bfs");
        Assert.Contains(allSnippets, s => s.Id == "snip_tree_recorder");
        Assert.Contains(allSnippets, s => s.Id == "snip_stack_canvas");

        foreach (var snip in allSnippets)
        {
            Assert.False(string.IsNullOrWhiteSpace(snip.Code));
            Assert.True(snip.Code.Contains("Visualizer") || snip.Code.Contains("Display"));
        }
    }

    [Fact]
    public void DocumentationService_LearnCSharpCategory_ShouldHaveAllChapters()
    {
        var service = DocumentationService.Instance;
        var learnCat = service.Categories.FirstOrDefault(c => c.Id == "learn_csharp");

        Assert.NotNull(learnCat);
        Assert.Equal(5, learnCat.Articles.Count);

        var articleIds = learnCat.Articles.Select(a => a.Id).ToList();
        Assert.Contains("learn_httpclient", articleIds);
        Assert.Contains("learn_json_serialization", articleIds);
        Assert.Contains("learn_sync_async", articleIds);
        Assert.Contains("learn_file_io", articleIds);
        Assert.Contains("learn_threading", articleIds);

        foreach (var article in learnCat.Articles)
        {
            Assert.NotEmpty(article.CodeSnippets);
            Assert.All(article.CodeSnippets, s => Assert.False(string.IsNullOrWhiteSpace(s.Code)));
        }
    }

    [Fact]
    public void DocumentationService_LearnCSharpSnippets_ShouldCompileWithoutErrors()
    {
        var service = DocumentationService.Instance;
        var learnCat = service.Categories.FirstOrDefault(c => c.Id == "learn_csharp");
        Assert.NotNull(learnCat);

        var compiler = new RoslynCompilerService();

        foreach (var article in learnCat.Articles)
        {
            foreach (var snippet in article.CodeSnippets)
            {
                var diagnostics = compiler.CheckDiagnostics(snippet.Code, ExecutionLanguageMode.Statements);
                var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

                Assert.True(errors.Count == 0,
                    $"Snippet '{snippet.Id}' in article '{article.Id}' has compile errors: " +
                    string.Join("; ", errors.Select(e => $"{e.Id}: {e.Message}")));
            }
        }
    }

    [Fact]
    public void DocumentationService_VisualizerSnippets_ShouldCompileAsScripts()
    {
        var service = DocumentationService.Instance;
        var visCat = service.Categories.FirstOrDefault(c => c.Id == "visualizer_recorder");
        Assert.NotNull(visCat);

        var compiler = new RoslynCompilerService();

        foreach (var snippet in visCat.Articles.SelectMany(a => a.CodeSnippets))
        {
            var diagnostics = compiler.CheckDiagnostics(snippet.Code, ExecutionLanguageMode.Statements);
            var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

            Assert.True(errors.Count == 0,
                $"Snippet '{snippet.Id}' has compile errors: " +
                string.Join("; ", errors.Select(e => $"{e.Id}: {e.Message}")));
        }
    }

    [Fact]
    public void DocumentationService_Search_ShouldReturnRelevantResults()
    {
        var service = DocumentationService.Instance;

        // Search for recorder
        var recorderResults = service.SearchArticles("recorder");
        Assert.NotEmpty(recorderResults);
        Assert.Contains(recorderResults, a => a.Id == "visualizer_overview" || a.Id == "canvas_recorder");

        // Search for dump
        var dumpResults = service.SearchArticles("dump");
        Assert.NotEmpty(dumpResults);
        Assert.Equal("dump_api", dumpResults[0].Id);

        // Search for shortcuts
        var shortcutResults = service.SearchArticles("shortcuts");
        Assert.NotEmpty(shortcutResults);
        Assert.Contains(shortcutResults, a => a.Id == "shortcuts_reference");

        // Search within the new Learn C# chapters
        var deadlockResults = service.SearchArticles("deadlock");
        Assert.NotEmpty(deadlockResults);
        Assert.Contains(deadlockResults, a => a.Id == "learn_sync_async");

        var interlockedResults = service.SearchArticles("interlocked");
        Assert.NotEmpty(interlockedResults);
        Assert.Contains(interlockedResults, a => a.Id == "learn_threading");

        // Search for non-existent term
        var emptyResults = service.SearchArticles("xyznonexistentterm999");
        Assert.Empty(emptyResults);
    }

    [Fact]
    public void DocumentationService_CreateScriptAndNotebookFromSnippet_ShouldProduceValidDocuments()
    {
        var service = DocumentationService.Instance;
        var snippet = new DocCodeSnippet
        {
            Id = "test_snip",
            Title = "Matrix Algorithm",
            Description = "Traverses a grid with BFS",
            Code = "int[,] grid = { { 1, 2 } };\nDisplay.Matrix(grid);",
            TargetKind = WorkspaceItemKind.Script
        };

        var script = service.CreateScriptFromSnippet(snippet);
        Assert.NotNull(script);
        Assert.Equal("Matrix Algorithm", script.Title);
        Assert.Equal(snippet.Code, script.Code);
        Assert.Contains("Matrix Algorithm", script.Notes);

        var notebook = service.CreateNotebookFromSnippet(snippet);
        Assert.NotNull(notebook);
        Assert.Equal("Matrix Algorithm", notebook.Title);
        Assert.Equal(2, notebook.Cells.Count);
        Assert.Equal(CellType.Markdown, notebook.Cells[0].Type);
        Assert.Equal(CellType.Code, notebook.Cells[1].Type);
        Assert.Equal(snippet.Code, notebook.Cells[1].Source);
    }

    [Fact]
    public void CSharpDocsViewModel_StateAndNavigation_ShouldFunctionCorrectly()
    {
        ScriptDocumentItem? launchedScript = null;
        NotebookDocumentItem? launchedNotebook = null;
        bool backToHubCalled = false;

        var vm = new CSharpDocsViewModel(
            docService: DocumentationService.Instance,
            backToHubAction: () => backToHubCalled = true,
            openScriptAction: s => launchedScript = s,
            openNotebookAction: n => launchedNotebook = n);

        Assert.NotEmpty(vm.Categories);
        Assert.NotNull(vm.SelectedCategory);
        Assert.NotNull(vm.SelectedArticle);

        // Navigate to a specific topic
        vm.SelectTopic("dump_api");
        Assert.Equal("dump_api", vm.SelectedArticle.Id);
        Assert.Equal("display_apis", vm.SelectedCategory.Id);
        Assert.Contains("dump", vm.ActiveBreadcrumb.ToLowerInvariant());

        // Test search
        vm.SearchQuery = "matrix";
        Assert.True(vm.HasSearchQuery);
        Assert.NotEmpty(vm.FilteredArticles);

        vm.ClearSearch();
        Assert.False(vm.HasSearchQuery);
        Assert.Empty(vm.FilteredArticles);

        // Next / Previous article
        var currentId = vm.SelectedArticle.Id;
        if (vm.HasNextArticle)
        {
            vm.NextArticle();
            Assert.NotEqual(currentId, vm.SelectedArticle.Id);
            vm.PreviousArticle();
            Assert.Equal(currentId, vm.SelectedArticle.Id);
        }

        // Try in Studio command
        var testSnippet = new DocCodeSnippet
        {
            Id = "s1",
            Title = "Test Run Snippet",
            Code = "Console.WriteLine(123);",
            TargetKind = WorkspaceItemKind.Script
        };
        vm.TryInStudio(testSnippet);
        Assert.NotNull(launchedScript);
        Assert.Equal("Test Run Snippet", launchedScript.Title);

        testSnippet.TargetKind = WorkspaceItemKind.Notebook;
        vm.TryInStudio(testSnippet);
        Assert.NotNull(launchedNotebook);
        Assert.Equal("Test Run Snippet", launchedNotebook.Title);

        // Back to hub
        vm.BackToHub();
        Assert.True(backToHubCalled);
    }
}
