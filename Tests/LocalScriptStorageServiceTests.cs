using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class LocalScriptStorageServiceTests : IDisposable
{
    private readonly string _baseDir;

    public LocalScriptStorageServiceTests()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), "FryPDF_StorageTests_" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_baseDir))
            {
                Directory.Delete(_baseDir, recursive: true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task CreateFolderAsync_NestedFolder_AppearsInLoadFolderPaths()
    {
        var storage = new LocalScriptStorageService(_baseDir);

        var parent = await storage.CreateFolderAsync(null, "Reports");
        var child = await storage.CreateFolderAsync(parent, "Q1");

        Assert.Equal("Reports", parent);
        Assert.Equal("Reports/Q1", child);

        var paths = await storage.LoadFolderPathsAsync();
        Assert.Contains("Reports", paths);
        Assert.Contains("Reports/Q1", paths);
    }

    [Fact]
    public async Task CreateFolderAsync_DuplicateName_AutoSuffixes()
    {
        var storage = new LocalScriptStorageService(_baseDir);

        var first = await storage.CreateFolderAsync(null, "Scratch");
        var second = await storage.CreateFolderAsync(null, "Scratch");

        Assert.Equal("Scratch", first);
        Assert.Equal("Scratch (2)", second);
    }

    [Fact]
    public async Task CreateNewNotebookAsync_WithFolderPath_IsFoundByIdAndByFolder()
    {
        var storage = new LocalScriptStorageService(_baseDir);
        var folder = await storage.CreateFolderAsync(null, "Notebooks");

        var nb = await storage.CreateNewNotebookAsync("My Notebook", folderPath: folder);

        var loaded = await storage.LoadNotebookAsync(nb.Id);
        Assert.NotNull(loaded);
        Assert.Equal("My Notebook", loaded.Title);

        var summaries = await storage.LoadWorkspaceSummariesAsync();
        var summary = summaries.First(s => s.Id == nb.Id);
        Assert.Equal("Notebooks", summary.FolderPath);
    }

    [Fact]
    public async Task RenameFolderAsync_KeepsDocumentsFindableByStorage()
    {
        var storage = new LocalScriptStorageService(_baseDir);
        var folder = await storage.CreateFolderAsync(null, "Old Name");
        var nb = await storage.CreateNewNotebookAsync("Doc", folderPath: folder);

        var newPath = await storage.RenameFolderAsync(folder, "New Name");

        Assert.Equal("New Name", newPath);
        var paths = await storage.LoadFolderPathsAsync();
        Assert.Contains("New Name", paths);
        Assert.DoesNotContain("Old Name", paths);

        // Storage resolves documents by id via a recursive scan, so renaming the folder on disk must
        // not orphan the document.
        var loaded = await storage.LoadNotebookAsync(nb.Id);
        Assert.NotNull(loaded);

        var summaries = await storage.LoadWorkspaceSummariesAsync();
        Assert.Equal("New Name", summaries.First(s => s.Id == nb.Id).FolderPath);
    }

    [Fact]
    public async Task DeleteFolderAsync_RemovesNestedDocumentsToo()
    {
        var storage = new LocalScriptStorageService(_baseDir);
        var folder = await storage.CreateFolderAsync(null, "ToDelete");
        var nb = await storage.CreateNewNotebookAsync("Doomed", folderPath: folder);

        await storage.DeleteFolderAsync(folder);

        var paths = await storage.LoadFolderPathsAsync();
        Assert.DoesNotContain("ToDelete", paths);

        var loaded = await storage.LoadNotebookAsync(nb.Id);
        Assert.Null(loaded);
    }

    [Fact]
    public async Task SaveScriptAsync_ReturnsFalse_WhenPathIsBlockedByDirectory()
    {
        var storage = new LocalScriptStorageService(_baseDir);
        var scriptId = Guid.NewGuid().ToString("N");

        // Force a real write failure: put a DIRECTORY at the exact path the new script would be
        // written to, so File.WriteAllTextAsync throws instead of silently overwriting.
        var libraryRoot = Path.Combine(_baseDir, "library");
        Directory.CreateDirectory(Path.Combine(libraryRoot, $"{scriptId}.frycs"));

        var script = new ScriptDocumentItem { Id = scriptId, Title = "Blocked" };
        var saved = await storage.SaveScriptAsync(script);

        Assert.False(saved);

        // Sanity: a normal save still succeeds (proves the storage instance itself is healthy, and
        // the failure above was specific to the blocked path, not a broken instance).
        var normalScript = new ScriptDocumentItem { Id = Guid.NewGuid().ToString("N"), Title = "Fine" };
        Assert.True(await storage.SaveScriptAsync(normalScript));
    }

    [Fact]
    public async Task LegacyFlatLayout_MigratesIntoLibraryRootOnFirstUse()
    {
        // Simulate a pre-existing install using the old flat "scripts/" + "notebooks/" layout, before
        // any LocalScriptStorageService has touched this directory with the new "library/" scheme.
        var legacyScriptsDir = Path.Combine(_baseDir, "scripts");
        var legacyNotebooksDir = Path.Combine(_baseDir, "notebooks");
        Directory.CreateDirectory(legacyScriptsDir);
        Directory.CreateDirectory(legacyNotebooksDir);

        var scriptId = Guid.NewGuid().ToString("N");
        var script = new ScriptDocumentItem { Id = scriptId, Title = "Legacy Script", Code = "// legacy" };
        await File.WriteAllTextAsync(
            Path.Combine(legacyScriptsDir, $"{scriptId}.frycs"),
            JsonSerializer.Serialize(script, new JsonSerializerOptions { WriteIndented = true }));

        var notebookId = Guid.NewGuid().ToString("N");
        var notebook = new NotebookDocumentItem { Id = notebookId, Title = "Legacy Notebook" };
        await File.WriteAllTextAsync(
            Path.Combine(legacyNotebooksDir, $"{notebookId}.frynb"),
            JsonSerializer.Serialize(notebook, new JsonSerializerOptions { WriteIndented = true }));

        // Now construct the service against the same base dir — this should migrate both legacy files
        // into library/ without needing to re-seed default templates (since real content already exists).
        var storage = new LocalScriptStorageService(_baseDir);
        var summaries = await storage.LoadWorkspaceSummariesAsync();

        Assert.Contains(summaries, s => s.Id == scriptId && s.Title == "Legacy Script");
        Assert.Contains(summaries, s => s.Id == notebookId && s.Title == "Legacy Notebook");

        var libraryRoot = Path.Combine(_baseDir, "library");
        Assert.True(File.Exists(Path.Combine(libraryRoot, $"{scriptId}.frycs")));
        Assert.True(File.Exists(Path.Combine(libraryRoot, $"{notebookId}.frynb")));

        // No default-template seeding should have run, since real content already existed.
        Assert.DoesNotContain(summaries, s => s.Id == "leetcode_two_sum");
    }

    // Reproduces a real bug found in a user's actual library: a notebook whose filename on disk
    // didn't match its own internal Id (e.g. carried over from an older storage layout, or left behind
    // after a duplicate cleanup that removed the id-named copy but kept a differently-named one with
    // real content). LoadWorkspaceSummariesAsync already tolerated this — it reads every file's own
    // content rather than assuming filename == id — but LoadNotebookAsync/SaveNotebookAsync/
    // DeleteItemAsync all funneled through a filename-only lookup and silently failed (no exception,
    // just a null/no-op) for exactly this case. FindExistingFilePath now falls back to scanning file
    // contents when the filename guess misses.
    [Fact]
    public async Task LoadNotebookAsync_FileNameDoesNotMatchInternalId_StillFindsAndLoadsIt()
    {
        var storage = new LocalScriptStorageService(_baseDir);
        await storage.LoadWorkspaceSummariesAsync(); // force initialization/seeding first

        var realId = "mismatched-id-123";
        var notebook = new NotebookDocumentItem { Id = realId, Title = "Renamed Notebook" };
        var libraryRoot = Path.Combine(_baseDir, "library");
        Directory.CreateDirectory(libraryRoot);
        await File.WriteAllTextAsync(
            Path.Combine(libraryRoot, "completely-different-filename.frynb"),
            JsonSerializer.Serialize(notebook, new JsonSerializerOptions { WriteIndented = true }));

        var loaded = await storage.LoadNotebookAsync(realId);

        Assert.NotNull(loaded);
        Assert.Equal("Renamed Notebook", loaded.Title);
    }

    [Fact]
    public async Task SaveNotebookAsync_FileNameDoesNotMatchInternalId_UpdatesExistingFileRatherThanDuplicating()
    {
        var storage = new LocalScriptStorageService(_baseDir);
        await storage.LoadWorkspaceSummariesAsync();

        var realId = "mismatched-id-456";
        var notebook = new NotebookDocumentItem { Id = realId, Title = "Original Title" };
        var libraryRoot = Path.Combine(_baseDir, "library");
        Directory.CreateDirectory(libraryRoot);
        var mismatchedPath = Path.Combine(libraryRoot, "some-other-filename.frynb");
        await File.WriteAllTextAsync(mismatchedPath, JsonSerializer.Serialize(notebook, new JsonSerializerOptions { WriteIndented = true }));

        notebook.Title = "Updated Title";
        var saved = await storage.SaveNotebookAsync(notebook);

        Assert.True(saved);
        // Must still be exactly one file for this Id — a naive "not found by filename, write a new
        // one at {id}.frynb" would have created a second, duplicate file instead of updating this one.
        var matchingFiles = Directory.EnumerateFiles(libraryRoot, "*.frynb", SearchOption.AllDirectories)
            .Where(f => JsonSerializer.Deserialize<NotebookDocumentItem>(File.ReadAllText(f))?.Id == realId)
            .ToList();
        Assert.Single(matchingFiles);
        Assert.Equal(mismatchedPath, matchingFiles[0]);
        var reloaded = await storage.LoadNotebookAsync(realId);
        Assert.Equal("Updated Title", reloaded!.Title);
    }

    [Fact]
    public async Task DeleteItemAsync_FileNameDoesNotMatchInternalId_ActuallyDeletesTheFile()
    {
        var storage = new LocalScriptStorageService(_baseDir);
        await storage.LoadWorkspaceSummariesAsync();

        var realId = "mismatched-id-789";
        var notebook = new NotebookDocumentItem { Id = realId, Title = "To Be Deleted" };
        var libraryRoot = Path.Combine(_baseDir, "library");
        Directory.CreateDirectory(libraryRoot);
        var mismatchedPath = Path.Combine(libraryRoot, "yet-another-filename.frynb");
        await File.WriteAllTextAsync(mismatchedPath, JsonSerializer.Serialize(notebook, new JsonSerializerOptions { WriteIndented = true }));

        await storage.DeleteItemAsync(realId);

        Assert.False(File.Exists(mismatchedPath));
    }
}
