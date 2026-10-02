using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Documentation;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;
using PdfEditorApp.Plugins.CSharpEditor.Services.Templates;
using Xunit;
using CSharpDocsViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Docs.CSharpDocsViewModel;

namespace CSharpEditorPlugin.Tests;

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
    public void ThePythonGuide_OpensItsSnippetsAsPythonCells_AndCSharpSnippetsStayCSharp()
    {
        var service = DocumentationService.Instance;
        var guide = service.Categories.Single(c => c.Id == "app_guide");
        var python = guide.Articles.Single(a => a.Id == "languages_python");
        var welcome = guide.Articles.Single(a => a.Id == "welcome_guide");

        Assert.All(python.CodeSnippets, snippet =>
        {
            Assert.Equal(WorkspaceItemKind.Notebook, snippet.TargetKind);
            Assert.Equal("python", service.CreateNotebookFromSnippet(snippet).Cells.Single(c => c.Type == CellType.Code).Language);
        });
        Assert.Null(service.CreateNotebookFromSnippet(welcome.CodeSnippets[0]).Cells.Single(c => c.Type == CellType.Code).Language);
        Assert.Contains(service.SearchArticles("pandas"), a => a.Id == "languages_python");
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
        Assert.Equal((string?)"dump_api", (string?)vm.SelectedArticle.Id);
        Assert.Equal((string?)"display_apis", (string?)vm.SelectedCategory.Id);
        Assert.Contains((string)"dump", (string?)vm.ActiveBreadcrumb.ToLowerInvariant());

        // Test search
        vm.SearchQuery = "matrix";
        Assert.True((bool)vm.HasSearchQuery);
        Assert.NotEmpty(vm.FilteredArticles);

        vm.ClearSearch();
        Assert.False((bool)vm.HasSearchQuery);
        Assert.Empty(vm.FilteredArticles);

        // Next / Previous article
        var currentId = vm.SelectedArticle.Id;
        if (vm.HasNextArticle)
        {
            vm.NextArticle();
            Assert.NotEqual(currentId, vm.SelectedArticle.Id);
            vm.PreviousArticle();
            Assert.Equal((string?)currentId, (string?)vm.SelectedArticle.Id);
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

    [Fact]
    public void DocumentationService_DiagramsCategory_ShouldHaveComprehensiveMultiLanguageArticles()
    {
        var service = DocumentationService.Instance;
        var diagramsCat = service.Categories.FirstOrDefault(c => c.Id == "diagrams_and_visualizations");

        Assert.NotNull(diagramsCat);
        Assert.True(diagramsCat.Articles.Count >= 7, "Expected at least 7 diagram articles.");

        var articleIds = diagramsCat.Articles.Select(a => a.Id).ToList();
        Assert.Contains("diagrams_quickstart", articleIds);
        Assert.Contains("diagrams_trees", articleIds);
        Assert.Contains("diagrams_graphs", articleIds);
        Assert.Contains("diagrams_matrices", articleIds);
        Assert.Contains("diagrams_3d_matrices", articleIds);
        Assert.Contains("diagrams_vector_canvas", articleIds);
        Assert.Contains("diagrams_3d_surfaces", articleIds);

        foreach (var article in diagramsCat.Articles)
        {
            Assert.NotEmpty(article.CodeSnippets);
            var snippet = article.CodeSnippets[0];
            Assert.True(snippet.HasVariants, $"Snippet {snippet.Id} should have multi-language variants.");
            Assert.True(snippet.Variants.Count >= 4, $"Snippet {snippet.Id} should have at least 4 languages (C++, Java, Python, C#).");

            var langs = snippet.Variants.Select(v => v.Language).ToList();
            Assert.Contains("cpp", langs);
            Assert.Contains("java", langs);
            Assert.Contains("python", langs);
            Assert.Contains("csharp", langs);

            // Default selection should be active
            Assert.NotNull(snippet.SelectedVariant);
            Assert.False(string.IsNullOrWhiteSpace(snippet.Code));
        }

        // Test search
        var searchResults = service.SearchArticles("diagram");
        Assert.NotEmpty(searchResults);
        Assert.Contains(searchResults, a => a.CategoryId == "diagrams_and_visualizations");
    }

    [Fact]
    public void DocCodeSnippet_MultiLanguageVariants_ShouldSwitchLanguageAndCodeCorrectly()
    {
        var service = DocumentationService.Instance;
        var snippet = new DocCodeSnippet
        {
            Id = "test_multi",
            Title = "Codes [C++ | Java | Python3 | C#]",
            Description = "Multi-language test snippet"
        }
        .AddVariant("cpp", "C++", "std::cout << \"Hello C++\";")
        .AddVariant("java", "Java", "System.out.println(\"Hello Java\");")
        .AddVariant("python", "Python3", "print(\"Hello Python\")")
        .AddVariant("csharp", "C#", "Console.WriteLine(\"Hello C#\");");

        Assert.True(snippet.HasVariants);
        Assert.Equal(4, snippet.Variants.Count);
        Assert.Equal("cpp", snippet.Language);
        Assert.Contains("Hello C++", snippet.Code);

        // Switch to Java
        var javaVariant = snippet.Variants.First(v => v.Language == "java");
        snippet.SelectVariant(javaVariant);
        Assert.True(javaVariant.IsSelected);
        Assert.False(snippet.Variants[0].IsSelected);
        Assert.Equal("java", snippet.Language);
        Assert.Contains("Hello Java", snippet.Code);

        // Check script creation with Java
        var script = service.CreateScriptFromSnippet(snippet);
        Assert.Equal("java", script.LanguageId);
        Assert.Contains("Hello Java", script.Code);

        // Switch to Python
        var pyVariant = snippet.Variants.First(v => v.Language == "python");
        snippet.SelectVariant(pyVariant);
        Assert.Equal("python", snippet.Language);
        Assert.Contains("Hello Python", snippet.Code);

        var pyScript = service.CreateScriptFromSnippet(snippet);
        Assert.Equal("python", pyScript.LanguageId);
    }

    [Fact]
    public void Plot3DSpecBuilder_VoxelBarWith2DMatrix_ShouldCreateVoxelPoints()
    {
        int[,] matrix = {
            { 10, 20 },
            { 30, 40 }
        };

        var spec = PdfEditorApp.Plugins.CSharpEditor.Visuals.Building.Plot3DSpecBuilder.From(matrix, PdfEditorApp.Plugins.CSharpEditor.Visuals.Spec.Plot3DType.VoxelBar);
        Assert.Equal(PdfEditorApp.Plugins.CSharpEditor.Visuals.Spec.Plot3DType.VoxelBar, spec.Kind);
        Assert.Single(spec.Series);
        Assert.Equal(4, spec.Series[0].X.Count);
        Assert.Equal(4, spec.Series[0].Z.Count);
        Assert.Equal(10, spec.Series[0].Z[0]);
        Assert.Equal(20, spec.Series[0].Z[1]);
        Assert.Equal(30, spec.Series[0].Z[2]);
        Assert.Equal(40, spec.Series[0].Z[3]);
    }

    [Fact]
    public void DocumentationService_CreateNotebookFromArticle_ShouldContainMarkdownSectionsAndCodeSnippets()
    {
        var service = DocumentationService.Instance;
        var diagramsCat = service.Categories.First(c => c.Id == "diagrams_and_visualizations");
        var article = diagramsCat.Articles.First(a => a.Id == "diagrams_3d_matrices");

        var notebook = service.CreateNotebookFromArticle(article);
        Assert.NotNull(notebook);
        Assert.Equal("3D Matrix & Voxel Grid Diagrams", notebook.Title);
        Assert.True(notebook.Cells.Count >= 4, "Expected markdown intro, section notes, and code cells.");

        var mdCells = notebook.Cells.Where(c => c.Type == CellType.Markdown).ToList();
        var codeCells = notebook.Cells.Where(c => c.Type == CellType.Code).ToList();

        Assert.NotEmpty(mdCells);
        Assert.NotEmpty(codeCells);
        Assert.Contains(mdCells, c => c.Source.Contains("3D Spatial Grid & BFS Pathfinding"));
        Assert.Contains(codeCells, c => c.Source.Contains("VoxelBar3D") || c.Source.Contains("voxel_bars") || c.Source.Contains("voxelBars"));
    }

    [Fact]
    public void CodeTemplateLibrary_3DVoxelMatrixTemplate_ShouldExistAsNotebookWithCells()
    {
        var templates = CodeTemplateLibrary.GetTemplates();
        var template = templates.FirstOrDefault(t => t.Id == "charting3d_voxel_matrix_and_spatial_grid");

        Assert.NotNull(template);
        Assert.Equal(WorkspaceItemKind.Notebook, template.Kind);
        Assert.True(template.IsNotebook);
        Assert.NotEmpty(template.Cells);
        Assert.Contains(template.Cells, c => c.Type == CellType.Markdown && c.Source.Contains("3D Spatial Grid & BFS Pathfinding"));
        Assert.Contains(template.Cells, c => c.Type == CellType.Code && c.Source.Contains("VoxelBar3D"));
        Assert.Contains(template.Cells, c => c.Type == CellType.Code && c.Source.Contains("Scatter3D"));
    }

    [Fact]
    public void DocumentationService_CleanSnippetTitle_CleansCodesPrefix()
    {
        Assert.Equal("3D Voxel Matrix Topography", DocumentationService.CleanSnippetTitle("Codes [C++ | Java | Python3 | C#] : 3D Voxel Matrix Topography"));
        Assert.Equal("Grid & Island Diagram", DocumentationService.CleanSnippetTitle("Codes [C++ | Java | Python3 | C#] : Grid & Island Diagram"));
        Assert.Equal("Matrix Algorithm", DocumentationService.CleanSnippetTitle("Matrix Algorithm"));
        Assert.Equal("Codes [C++ | Java | Python3 | C#]", DocumentationService.CleanSnippetTitle("Codes [C++ | Java | Python3 | C#]"));
    }

    [Fact]
    public void CreateScriptFromSnippet_WithCppVariant_CreatesCppExtensionNotFrycs()
    {
        var service = DocumentationService.Instance;
        var snippet = new DocCodeSnippet
        {
            Id = "snip_voxel",
            Title = "Codes [C++ | Java | Python3 | C#] : 3D Voxel Matrix Topography",
            Description = "3D Voxel Topography",
            TargetKind = WorkspaceItemKind.Script
        }
        .AddVariant("cpp", "C++", "#include <fry_display.hpp>\nint main() { return 0; }")
        .AddVariant("python", "Python3", "import fry_display\nprint('hello')");

        // C++ variant is selected by default (first variant)
        Assert.Equal("cpp", snippet.Language);
        var cppScript = service.CreateScriptFromSnippet(snippet);
        Assert.Equal("cpp", cppScript.LanguageId);
        Assert.Equal("3D Voxel Matrix Topography.cpp", cppScript.Title);
        Assert.False(cppScript.Title.EndsWith(".frycs", StringComparison.OrdinalIgnoreCase));
        Assert.EndsWith(".cpp", cppScript.Title, StringComparison.OrdinalIgnoreCase);

        // Switch to Python variant
        var pyVariant = snippet.Variants.First(v => v.Language == "python");
        snippet.SelectVariant(pyVariant);
        Assert.Equal("python", snippet.Language);
        var pyScript = service.CreateScriptFromSnippet(snippet);
        Assert.Equal("python", pyScript.LanguageId);
        Assert.Equal("3D Voxel Matrix Topography.py", pyScript.Title);
        Assert.False(pyScript.Title.EndsWith(".frycs", StringComparison.OrdinalIgnoreCase));
        Assert.EndsWith(".py", pyScript.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryInStudio_WithMultiLanguageSnippetAndStorage_CreatesLanguageSpecificSourceFileNotFrycs()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"frydocs_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var languages = new StudioLanguageServices(tempDir);
            var storage = new LocalScriptStorageService(tempDir, languages.Registry);
            ScriptDocumentItem? launchedScript = null;

            var vm = new CSharpDocsViewModel(
                docService: DocumentationService.Instance,
                openScriptAction: s => launchedScript = s,
                storageService: storage,
                languages: languages);

            var snippet = new DocCodeSnippet
            {
                Id = "snip_3d_voxel",
                Title = "Codes [C++ | Java | Python3 | C#] : 3D Voxel Matrix Topography",
                Description = "Render a 3D matrix as an interactive voxel bar grid",
                TargetKind = WorkspaceItemKind.Script
            }
            .AddVariant("cpp", "C++", "#include <fry_display.hpp>\nint main() { return 0; }")
            .AddVariant("python", "Python3", "import fry_display\nprint('py')");

            // Select C++
            snippet.SelectVariant(snippet.Variants.First(v => v.Language == "cpp"));
            await vm.TryInStudioAsync(snippet);

            Assert.NotNull(launchedScript);
            Assert.Equal("cpp", launchedScript.LanguageId);
            Assert.NotNull(launchedScript.SourceFilePath);
            Assert.True(File.Exists(launchedScript.SourceFilePath));
            Assert.EndsWith(".cpp", launchedScript.SourceFilePath, StringComparison.OrdinalIgnoreCase);
            Assert.False(launchedScript.SourceFilePath.EndsWith(".frycs", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("#include <fry_display.hpp>", File.ReadAllText(launchedScript.SourceFilePath));

            // Verify no .frycs file was created
            var files = Directory.GetFiles(tempDir, "*.*", SearchOption.AllDirectories);
            Assert.DoesNotContain(files, f => f.EndsWith(".frycs", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(files, f => f.EndsWith(".cpp", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }
}
