using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Execution;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Catalogs.Blind75;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;
using Xunit;
using CSharpCodeStudioViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio.CSharpCodeStudioViewModel;
using CSharpNotebookStudioViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Notebooks.CSharpNotebookStudioViewModel;
using ExplorerItemViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio.Explorer.ExplorerItemViewModel;

namespace CSharpEditorPlugin.Tests;

/// <summary>Source files (main.py) in the Code Studio's Explorer and tabs.</summary>
public class CodeStudioSourceFileTests : IDisposable
{
    private readonly string _baseDir = Path.Combine(Path.GetTempPath(), "FryPDF_CodeStudioSourceFiles_" + Guid.NewGuid().ToString("N"));
    private readonly StudioLanguageServices _languages;
    private readonly LocalScriptStorageService _storage;

    public CodeStudioSourceFileTests()
    {
        _languages = new StudioLanguageServices(Path.Combine(_baseDir, "services"));
        _storage = new LocalScriptStorageService(Path.Combine(_baseDir, "storage"), _languages.Registry);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_baseDir)) Directory.Delete(_baseDir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private CSharpCodeStudioViewModel Studio(ScriptDocumentItem script) =>
        new(script, _storage, new RoslynCompilerService(), new ScriptExecutionEngine(),
            backToHubAction: () => { },
            blindProgress: new LocalBlindProgressService(Path.Combine(_baseDir, "progress")),
            languages: _languages);

    private string WriteSource(string relativePath, string text)
    {
        var path = Path.Combine(_storage.LibraryRootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    private string CopyOrWriteImage(string relativePath)
    {
        var path = Path.Combine(_storage.LibraryRootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var sample = Path.Combine(AppContext.BaseDirectory, "Assets", "app-logo.png");
        if (!File.Exists(sample))
        {
            sample = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../..", "Assets", "app-logo.png"));
        }
        if (File.Exists(sample))
        {
            File.Copy(sample, path, overwrite: true);
        }
        else
        {
            var pngBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
            File.WriteAllBytes(path, pngBytes);
        }
        return path;
    }

    private static IEnumerable<ExplorerItemViewModel> All(IEnumerable<ExplorerItemViewModel> items) =>
        items.SelectMany(i => new[] { i }.Concat(All(i.Children)));

    [Fact]
    public async Task TheExplorer_ShowsSourceFiles_WithTheirLanguagesIcon()
    {
        WriteSource("main.py", "print('hi')\n");
        var studio = Studio(await _storage.CreateNewScriptAsync("Notes"));

        var item = Assert.Single(All(studio.ExplorerRootItems), i => i.Name == "main.py");

        Assert.True((bool)item.IsSourceFile);
        Assert.Equal("LanguagePython", item.IconKind);
        Assert.Contains(All(studio.ExplorerRootItems), i => i.Name == "Notes.frycs" && !i.IsSourceFile);
    }

    [Fact]
    public async Task OpeningASourceFile_GivesItATabWithItsLanguage()
    {
        WriteSource("main.py", "print('hi')\n");
        var studio = Studio(await _storage.CreateNewScriptAsync("Notes"));
        var item = All(studio.ExplorerRootItems).Single(i => i.Name == "main.py");

        await studio.SwitchToScriptAsync(item);

        Assert.Equal((string?)"main.py", (string?)studio.Script.Title);
        Assert.Equal((string?)"print('hi')\n", (string?)studio.Code);
        Assert.Equal(LanguageIds.Python, studio.ActiveLanguage.Id);
        var tab = studio.OpenTabs.Single(t => t.IsActive);
        Assert.True(tab.HasLanguageIcon);
        Assert.Equal("LanguagePython", tab.LanguageIconKind);
    }

    [Fact]
    public async Task NewFileOptions_OfferEverySourceFileLanguage()
    {
        var studio = Studio(await _storage.CreateNewScriptAsync("Notes"));

        Assert.Equal(8, studio.NewFileOptions.Count);
        var python = Assert.Single(studio.NewFileOptions, o => o.LanguageId == LanguageIds.Python);
        var js = Assert.Single(studio.NewFileOptions, o => o.LanguageId == LanguageIds.JavaScript);
        var java = Assert.Single(studio.NewFileOptions, o => o.LanguageId == LanguageIds.Java);
        var cpp = Assert.Single(studio.NewFileOptions, o => o.LanguageId == LanguageIds.Cpp);
        var go = Assert.Single(studio.NewFileOptions, o => o.LanguageId == LanguageIds.Go);
        var fsharp = Assert.Single(studio.NewFileOptions, o => o.LanguageId == LanguageIds.FSharp);
        var sql = Assert.Single(studio.NewFileOptions, o => o.LanguageId == LanguageIds.Sql);
        var rust = Assert.Single(studio.NewFileOptions, o => o.LanguageId == LanguageIds.Rust);

        Assert.Equal("New Python File", python.Label);
        Assert.Equal("LanguagePython", python.IconKind);
        Assert.Equal("New JavaScript File", js.Label);
        Assert.Equal("LanguageJavascript", js.IconKind);
        Assert.Equal("New Java File", java.Label);
        Assert.Equal("LanguageJava", java.IconKind);
        Assert.Equal("New C++ File", cpp.Label);
        Assert.Equal("LanguageCpp", cpp.IconKind);
        Assert.Equal("New Go File", go.Label);
        Assert.Equal("LanguageGo", go.IconKind);
        Assert.Equal("New F# File", fsharp.Label);
        Assert.Equal("FunctionVariant", fsharp.IconKind);
        Assert.Equal("New SQL File", sql.Label);
        Assert.Equal("Database", sql.IconKind);
        Assert.Equal("New Rust File", rust.Label);
        Assert.Equal("LanguageRust", rust.IconKind);
    }

    [Fact]
    public async Task ANewSourceFile_IsCreatedInTheSelectedFolder_OpenedAndReadyToRename()
    {
        Directory.CreateDirectory(Path.Combine(_storage.LibraryRootPath, "tools"));
        var studio = Studio(await _storage.CreateNewScriptAsync("Notes"));
        var folder = All(studio.ExplorerRootItems).Single(i => i.IsDirectory && i.Name == "tools");

        await studio.NewFileOptions.First(o => o.LanguageId == LanguageIds.Python).Command.ExecuteAsync(folder);

        Assert.StartsWith(Path.Combine(_storage.LibraryRootPath, "tools", "script_"), (string?)studio.Script.SourceFilePath);
        Assert.True(File.Exists(studio.Script.SourceFilePath));
        Assert.Contains((string)"print(", (string?)studio.Code);
        var item = All(studio.ExplorerRootItems).Single(i => i.DocumentId == studio.Script.Id);
        Assert.True((bool)item.IsRenaming);
    }

    [Fact]
    public async Task RenamingInTheExplorer_MovesTheFile_AndTheOpenTabFollows()
    {
        var path = WriteSource("draft.py", "print('draft')\n");
        var studio = Studio(await _storage.CreateNewScriptAsync("Notes"));
        var item = All(studio.ExplorerRootItems).Single(i => i.Name == "draft.py");
        await studio.SwitchToScriptAsync(item);

        item.StartRename();
        item.EditName = "final";
        item.CommitRename();
        await WaitUntil(() => studio.Script.Title == "final.py");

        var moved = Path.Combine(_storage.LibraryRootPath, "final.py");
        Assert.True(File.Exists(moved));
        Assert.False(File.Exists(path));
        Assert.Equal(Path.GetFullPath(moved), (string?)studio.Script.SourceFilePath);
        Assert.Equal((string?)studio.Script.Id, (string?)item.DocumentId);
        Assert.Equal("final.py", studio.OpenTabs.Single(t => t.IsActive).Title);
    }

    [Fact]
    public async Task DeletingASourceFile_AsksFirst()
    {
        var path = WriteSource("keep.py", "print('keep')\n");
        var studio = Studio(await _storage.CreateNewScriptAsync("Notes"));
        var item = All(studio.ExplorerRootItems).Single(i => i.Name == "keep.py");

        item.RequestDelete();
        Assert.True((bool)item.IsConfirmingDelete);
        Assert.True(File.Exists(path));

        item.CancelDelete();
        Assert.False((bool)item.IsConfirmingDelete);
        Assert.True(File.Exists(path));

        item.RequestDelete();
        item.ConfirmDelete();
        await WaitUntil(() => !File.Exists(path));
        Assert.DoesNotContain(All(studio.ExplorerRootItems), i => i.Name == "keep.py");
    }

    [Fact]
    public async Task Duplicating_CopiesTheFile_WithTheEditorsText()
    {
        WriteSource("tool.py", "print('v1')\n");
        var studio = Studio(await _storage.CreateNewScriptAsync("Notes"));
        await studio.SwitchToScriptAsync(All(studio.ExplorerRootItems).Single(i => i.Name == "tool.py"));
        studio.Code = "print('v2')\n";

        await studio.DuplicateExplorerItemAsync(All(studio.ExplorerRootItems).Single(i => i.Name == "tool.py"));

        var copy = Path.Combine(_storage.LibraryRootPath, "tool_copy.py");
        Assert.Equal("print('v2')\n", File.ReadAllText(copy));
        Assert.Equal((string?)"tool_copy.py", (string?)studio.Script.Title);
    }

    [Fact]
    public async Task SwitchingAway_DoesntSaveOverAnotherEditorsChange()
    {
        var path = WriteSource("shared.py", "print('original')\n");
        var studio = Studio(await _storage.CreateNewScriptAsync("Notes"));
        await studio.SwitchToScriptAsync(All(studio.ExplorerRootItems).Single(i => i.Name == "shared.py"));
        studio.Code = "print('studio')\n";
        File.WriteAllText(path, "print('other editor')\n");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));

        await studio.SwitchToScriptAsync(All(studio.ExplorerRootItems).Single(i => i.Name == "Notes.frycs"));

        Assert.Equal("print('other editor')\n", File.ReadAllText(path));
    }

    [Fact]
    public async Task CtrlS_SavesTheEditorsText_OverAnotherEditorsChange()
    {
        var path = WriteSource("shared.py", "print('original')\n");
        var studio = Studio(await _storage.CreateNewScriptAsync("Notes"));
        await studio.SwitchToScriptAsync(All(studio.ExplorerRootItems).Single(i => i.Name == "shared.py"));
        studio.Code = "print('studio')\n";
        File.WriteAllText(path, "print('other editor')\n");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));

        await studio.SaveCommand.ExecuteAsync(null);

        Assert.Equal("print('studio')\n", File.ReadAllText(path));
        Assert.False((bool)studio.OpenTabs.Single(t => t.IsActive).IsDirty);
    }

    [Fact]
    public async Task TheNotebookExplorer_OpensSourceFilesInTheCodeStudio()
    {
        WriteSource("main.py", "print('hi')\n");
        ScriptDocumentItem? opened = null;
        var notebooks = new CSharpNotebookStudioViewModel(
            new NotebookDocumentItem { Title = "Scratch" }, _storage, new RoslynCompilerService(), new ScriptExecutionEngine(),
            backToHubAction: () => { }, openScriptAction: doc => opened = doc);
        await notebooks.RefreshExplorer();
        var item = All(notebooks.ExplorerRootItems).Single(i => i.Name == "main.py");

        await notebooks.OpenDocumentAsync(item);

        Assert.Equal("main.py", opened?.Title);
        Assert.Equal(LanguageIds.Python, opened?.LanguageId);
        Assert.DoesNotContain(notebooks.Tabs, t => t.Title.Contains("main"));
    }

    [Fact]
    public async Task OpeningAnImageFile_ActivatesVisualImageViewer_WithAccurateMetadata()
    {
        CopyOrWriteImage("assets/app-logo.png");
        var studio = Studio(await _storage.CreateNewScriptAsync("Notes"));
        var item = Assert.Single(All(studio.ExplorerRootItems), i => i.Name == "app-logo.png");

        await studio.SwitchToScriptAsync(item);

        Assert.True(studio.IsActiveDocumentImage);
        Assert.False(studio.ShowCodeEditor);
        Assert.Equal("PNG Image", studio.ImageFormatText);
        Assert.False(string.IsNullOrEmpty(studio.ImageFileSizeText));
        Assert.Contains("PNG Image", studio.RuntimeLabel);

        // Zoom commands
        Assert.True(studio.ImageFitToWindow);
        studio.ImageZoomInCommand.Execute(null);
        Assert.False(studio.ImageFitToWindow);
        Assert.True(studio.ImageZoomFactor > 1.0);

        studio.ImageResetZoomCommand.Execute(null);
        Assert.Equal(1.0, studio.ImageZoomFactor);

        studio.ImageToggleFitCommand.Execute(null);
        Assert.True(studio.ImageFitToWindow);

        // Code drawer toggle
        Assert.False(studio.ShowImageCodeDrawer);
        studio.ToggleImageCodeDrawerCommand.Execute(null);
        Assert.True(studio.ShowImageCodeDrawer);
    }

    [Fact]
    public async Task OpeningAMarkdownFile_TogglesBetweenPreviewAndEditModes()
    {
        var mdPath = WriteSource("docs/guide.md", "# Guide\n\nWelcome to **C# Studio**.\n");
        var studio = Studio(await _storage.CreateNewScriptAsync("Notes"));
        var item = Assert.Single(All(studio.ExplorerRootItems), i => i.Name == "guide.md");

        await studio.SwitchToScriptAsync(item);

        Assert.True(studio.IsActiveDocumentMarkdown);
        Assert.True(studio.HasPreviewMode);
        Assert.True(studio.IsDocumentPreviewMode);
        Assert.True(studio.ShowMarkdownPreview);
        Assert.False(studio.ShowTextEditor);
        Assert.Equal("Edit Raw", studio.DocumentPreviewToggleText);
        Assert.Equal("Markdown (Preview)", studio.RuntimeLabel);

        // Toggle to Edit mode
        studio.ToggleDocumentPreviewModeCommand.Execute(null);

        Assert.False(studio.IsDocumentPreviewMode);
        Assert.False(studio.ShowMarkdownPreview);
        Assert.True(studio.ShowTextEditor);
        Assert.Equal("Preview", studio.DocumentPreviewToggleText);
        Assert.Equal("Markdown (Source)", studio.RuntimeLabel);

        // Toggle back to Preview mode
        studio.ToggleDocumentPreviewModeCommand.Execute(null);

        Assert.True(studio.IsDocumentPreviewMode);
        Assert.True(studio.ShowMarkdownPreview);
        Assert.False(studio.ShowTextEditor);
        Assert.Equal("Edit Raw", studio.DocumentPreviewToggleText);
        Assert.Equal("Markdown (Preview)", studio.RuntimeLabel);
    }

    [Fact]
    public async Task OpeningACsvFile_TogglesBetweenTablePreviewAndRawTextModes()
    {
        var csvContent = """
            Id,City,Score,Active
            1,"San Francisco, CA",98.5,true
            2,"New York, NY",94.2,false
            3,"Austin, TX",89.0,true
            """;
        WriteSource("data/cities.csv", csvContent);
        var studio = Studio(await _storage.CreateNewScriptAsync("Notes"));
        var item = Assert.Single(All(studio.ExplorerRootItems), i => i.Name == "cities.csv");

        await studio.SwitchToScriptAsync(item);

        Assert.True(studio.IsActiveDocumentCsv);
        Assert.True(studio.HasPreviewMode);
        Assert.True(studio.IsDocumentPreviewMode);
        Assert.True(studio.ShowCsvPreview);
        Assert.False(studio.ShowTextEditor);
        Assert.Equal("Edit Raw CSV", studio.DocumentPreviewToggleText);
        Assert.Contains("3 rows × 4 cols", studio.RuntimeLabel);

        // Verify parsed table structure
        Assert.NotNull(studio.ActiveCsvTable);
        Assert.Equal(4, studio.ActiveCsvTable.Columns.Count);
        Assert.Equal(3, studio.ActiveCsvTable.Rows.Count);
        Assert.Equal("City", studio.ActiveCsvTable.Columns[1].Header);
        Assert.False(studio.ActiveCsvTable.Columns[1].IsNumeric);
        Assert.Equal("Score", studio.ActiveCsvTable.Columns[2].Header);
        Assert.True(studio.ActiveCsvTable.Columns[2].IsNumeric);

        // Quoted cell with comma correctly parsed without splitting
        var row0 = studio.ActiveCsvTable.Rows[0];
        Assert.Equal("San Francisco, CA", row0.Cells[1].DisplayText);

        // Toggle to Edit mode
        studio.ToggleDocumentPreviewModeCommand.Execute(null);

        Assert.False(studio.IsDocumentPreviewMode);
        Assert.False(studio.ShowCsvPreview);
        Assert.True(studio.ShowTextEditor);
        Assert.Equal("Preview Table", studio.DocumentPreviewToggleText);
        Assert.Equal("CSV (Source Text)", studio.RuntimeLabel);

        // Toggle back to Preview mode
        studio.ToggleDocumentPreviewModeCommand.Execute(null);

        Assert.True(studio.IsDocumentPreviewMode);
        Assert.True(studio.ShowCsvPreview);
        Assert.False(studio.ShowTextEditor);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition never became true.");
            await Task.Delay(10);
        }
    }
}
