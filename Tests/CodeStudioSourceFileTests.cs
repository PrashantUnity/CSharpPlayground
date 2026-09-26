using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

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

    private static IEnumerable<ExplorerItemViewModel> All(IEnumerable<ExplorerItemViewModel> items) =>
        items.SelectMany(i => new[] { i }.Concat(All(i.Children)));

    [Fact]
    public async Task TheExplorer_ShowsSourceFiles_WithTheirLanguagesIcon()
    {
        WriteSource("main.py", "print('hi')\n");
        var studio = Studio(await _storage.CreateNewScriptAsync("Notes"));

        var item = Assert.Single(All(studio.ExplorerRootItems), i => i.Name == "main.py");

        Assert.True(item.IsSourceFile);
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

        Assert.Equal("main.py", studio.Script.Title);
        Assert.Equal("print('hi')\n", studio.Code);
        Assert.Equal(LanguageIds.Python, studio.ActiveLanguage.Id);
        var tab = studio.OpenTabs.Single(t => t.IsActive);
        Assert.True(tab.HasLanguageIcon);
        Assert.Equal("LanguagePython", tab.LanguageIconKind);
    }

    [Fact]
    public async Task NewFileOptions_OfferEverySourceFileLanguage()
    {
        var studio = Studio(await _storage.CreateNewScriptAsync("Notes"));

        var python = Assert.Single(studio.NewFileOptions);

        Assert.Equal("New Python File", python.Label);
        Assert.Equal("LanguagePython", python.IconKind);
    }

    [Fact]
    public async Task ANewSourceFile_IsCreatedInTheSelectedFolder_OpenedAndReadyToRename()
    {
        Directory.CreateDirectory(Path.Combine(_storage.LibraryRootPath, "tools"));
        var studio = Studio(await _storage.CreateNewScriptAsync("Notes"));
        var folder = All(studio.ExplorerRootItems).Single(i => i.IsDirectory && i.Name == "tools");

        await studio.NewFileOptions.Single().Command.ExecuteAsync(folder);

        Assert.StartsWith(Path.Combine(_storage.LibraryRootPath, "tools", "script_"), studio.Script.SourceFilePath);
        Assert.True(File.Exists(studio.Script.SourceFilePath));
        Assert.Contains("print(", studio.Code);
        var item = All(studio.ExplorerRootItems).Single(i => i.DocumentId == studio.Script.Id);
        Assert.True(item.IsRenaming);
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
        Assert.Equal(Path.GetFullPath(moved), studio.Script.SourceFilePath);
        Assert.Equal(studio.Script.Id, item.DocumentId);
        Assert.Equal("final.py", studio.OpenTabs.Single(t => t.IsActive).Title);
    }

    [Fact]
    public async Task DeletingASourceFile_AsksFirst()
    {
        var path = WriteSource("keep.py", "print('keep')\n");
        var studio = Studio(await _storage.CreateNewScriptAsync("Notes"));
        var item = All(studio.ExplorerRootItems).Single(i => i.Name == "keep.py");

        item.RequestDelete();
        Assert.True(item.IsConfirmingDelete);
        Assert.True(File.Exists(path));

        item.CancelDelete();
        Assert.False(item.IsConfirmingDelete);
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
        Assert.Equal("tool_copy.py", studio.Script.Title);
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
        Assert.False(studio.OpenTabs.Single(t => t.IsActive).IsDirty);
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
