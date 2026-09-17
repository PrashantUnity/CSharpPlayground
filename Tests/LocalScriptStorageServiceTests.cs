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
        // written to (title-based filename, not the id), so File.WriteAllTextAsync throws instead of
        // silently overwriting.
        var libraryRoot = Path.Combine(_baseDir, "library");
        Directory.CreateDirectory(Path.Combine(libraryRoot, "Blocked.frycs"));

        var script = new ScriptDocumentItem { Id = scriptId, Title = "Blocked" };
        var saved = await storage.SaveScriptAsync(script);

        Assert.False(saved);

        // Sanity: a normal save still succeeds (proves the storage instance itself is healthy, and
        // the failure above was specific to the blocked path, not a broken instance).
        var normalScript = new ScriptDocumentItem { Id = Guid.NewGuid().ToString("N"), Title = "Fine" };
        Assert.True(await storage.SaveScriptAsync(normalScript));
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

    // A rooted folderPath means "save exactly here, outside the library" (used by the Manager's
    // native folder-browser flow) — these verify a document saved that way is still fully usable:
    // found by id, listed in the workspace, editable, and deletable, exactly like an internal one.
    [Fact]
    public async Task CreateNewScriptAsync_WithAbsoluteFolderPath_SavesFileAtExactlyThatLocation()
    {
        var externalDir = Path.Combine(Path.GetTempPath(), "FryPDF_ExternalDocTests_" + Guid.NewGuid().ToString("N"));
        try
        {
            var storage = new LocalScriptStorageService(_baseDir);

            var script = await storage.CreateNewScriptAsync("Desktop Script", folderPath: externalDir);

            var expectedFile = Path.Combine(externalDir, $"{script.Title}.frycs");
            Assert.True(File.Exists(expectedFile));
        }
        finally
        {
            if (Directory.Exists(externalDir)) Directory.Delete(externalDir, recursive: true);
        }
    }

    [Fact]
    public async Task CreateNewScriptAsync_WithAbsoluteFolderPath_AppearsInWorkspaceSummariesWithThatFolder()
    {
        var externalDir = Path.Combine(Path.GetTempPath(), "FryPDF_ExternalDocTests_" + Guid.NewGuid().ToString("N"));
        try
        {
            var storage = new LocalScriptStorageService(_baseDir);
            var script = await storage.CreateNewScriptAsync("Desktop Script", folderPath: externalDir);

            var summary = (await storage.LoadWorkspaceSummariesAsync()).Single(s => s.Id == script.Id);

            Assert.Equal(externalDir, summary.FolderPath);
        }
        finally
        {
            if (Directory.Exists(externalDir)) Directory.Delete(externalDir, recursive: true);
        }
    }

    [Fact]
    public async Task ExternalDocument_PersistsAcrossServiceInstances()
    {
        // Simulates an app restart: a brand-new LocalScriptStorageService pointed at the same
        // _baseDir must still find a document that was saved outside the library by a previous
        // instance — proving the external-document index is actually persisted to disk, not just
        // held in memory for the lifetime of the service that created it.
        var externalDir = Path.Combine(Path.GetTempPath(), "FryPDF_ExternalDocTests_" + Guid.NewGuid().ToString("N"));
        try
        {
            var firstInstance = new LocalScriptStorageService(_baseDir);
            var notebook = await firstInstance.CreateNewNotebookAsync("Restart Test", folderPath: externalDir);

            var secondInstance = new LocalScriptStorageService(_baseDir);

            var loaded = await secondInstance.LoadNotebookAsync(notebook.Id);
            Assert.NotNull(loaded);
            Assert.Equal("Restart Test", loaded!.Title);

            var summary = (await secondInstance.LoadWorkspaceSummariesAsync()).SingleOrDefault(s => s.Id == notebook.Id);
            Assert.NotNull(summary);
        }
        finally
        {
            if (Directory.Exists(externalDir)) Directory.Delete(externalDir, recursive: true);
        }
    }

    [Fact]
    public async Task SaveScriptAsync_ForExternalDocument_OverwritesAtItsExternalLocation()
    {
        var externalDir = Path.Combine(Path.GetTempPath(), "FryPDF_ExternalDocTests_" + Guid.NewGuid().ToString("N"));
        try
        {
            var storage = new LocalScriptStorageService(_baseDir);
            var script = await storage.CreateNewScriptAsync("Desktop Script", folderPath: externalDir);

            var loaded = await storage.LoadScriptAsync(script.Id);
            Assert.NotNull(loaded);
            loaded!.Code = "Console.WriteLine(\"edited\");";
            await storage.SaveScriptAsync(loaded);

            var reloaded = await storage.LoadScriptAsync(script.Id);
            Assert.Equal("Console.WriteLine(\"edited\");", reloaded!.Code);

            // Only the one file at the external location — no stray copy landed in the library root.
            var expectedFile = Path.Combine(externalDir, $"{script.Title}.frycs");
            Assert.True(File.Exists(expectedFile));
            Assert.False(File.Exists(Path.Combine(_baseDir, "library", $"{script.Title}.frycs")));
        }
        finally
        {
            if (Directory.Exists(externalDir)) Directory.Delete(externalDir, recursive: true);
        }
    }

    [Fact]
    public async Task DeleteItemAsync_ForExternalDocument_DeletesFileAndForgetsItAcrossRestart()
    {
        var externalDir = Path.Combine(Path.GetTempPath(), "FryPDF_ExternalDocTests_" + Guid.NewGuid().ToString("N"));
        try
        {
            var storage = new LocalScriptStorageService(_baseDir);
            var script = await storage.CreateNewScriptAsync("Desktop Script", folderPath: externalDir);
            var file = Path.Combine(externalDir, $"{script.Title}.frycs");
            Assert.True(File.Exists(file));

            await storage.DeleteItemAsync(script.Id);
            Assert.False(File.Exists(file));

            // Deleting must also prune the persisted index — otherwise a restart would resurrect a
            // dangling entry pointing at a file that no longer exists.
            var afterRestart = new LocalScriptStorageService(_baseDir);
            var summaries = await afterRestart.LoadWorkspaceSummariesAsync();
            Assert.DoesNotContain(summaries, s => s.Id == script.Id);
        }
        finally
        {
            if (Directory.Exists(externalDir)) Directory.Delete(externalDir, recursive: true);
        }
    }

    // Verifies the actual artifact, not just the resulting behavior: an external save produces a real,
    // visible ".frycsproj"/".frynbproj" file sitting in the chosen folder — named after that folder,
    // like a real IDE's project file — rather than only a hidden entry in the plugin's own data
    // directory. This is what makes the folder self-describing/portable, unlike a purely internal index.
    [Fact]
    public async Task CreateNewScriptAsync_WithAbsoluteFolderPath_CreatesNamedProjectFileInThatFolder()
    {
        var externalDir = Path.Combine(Path.GetTempPath(), "FryPDF_ExternalDocTests_" + Guid.NewGuid().ToString("N"), "MyProject");
        try
        {
            var storage = new LocalScriptStorageService(_baseDir);

            await storage.CreateNewScriptAsync("Desktop Script", folderPath: externalDir);

            var expectedProjectFile = Path.Combine(externalDir, "MyProject.frycsproj");
            Assert.True(File.Exists(expectedProjectFile));

            var json = await File.ReadAllTextAsync(expectedProjectFile);
            Assert.Contains("Script", json);
        }
        finally
        {
            var root = Path.GetDirectoryName(externalDir)!;
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    // The core benefit of a per-folder project file over a per-document index: two documents saved to
    // the same external folder share ONE project file (and one tracked entry), not two independent ones.
    [Fact]
    public async Task CreateNewNotebookAsync_TwiceInSameExternalFolder_SharesOneProjectFileWithBothDocuments()
    {
        var externalDir = Path.Combine(Path.GetTempPath(), "FryPDF_ExternalDocTests_" + Guid.NewGuid().ToString("N"), "SharedProject");
        try
        {
            var storage = new LocalScriptStorageService(_baseDir);

            var first = await storage.CreateNewNotebookAsync("First Note", folderPath: externalDir);
            var second = await storage.CreateNewNotebookAsync("Second Note", folderPath: externalDir);

            var projectFiles = Directory.GetFiles(externalDir, "*.frynbproj");
            var projectFile = Assert.Single(projectFiles);

            var json = await File.ReadAllTextAsync(projectFile);
            Assert.Contains(first.Id, json);
            Assert.Contains(second.Id, json);

            var summaries = await storage.LoadWorkspaceSummariesAsync();
            Assert.Contains(summaries, s => s.Id == first.Id);
            Assert.Contains(summaries, s => s.Id == second.Id);
        }
        finally
        {
            var root = Path.GetDirectoryName(externalDir)!;
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    // Regression coverage for a real user report: new documents were saved as "<guid>.frycs" instead
    // of a name reflecting the title the user actually typed, because WriteNewDocumentAsync named the
    // file after the document's internal Id. Lookups key off the Id stored inside the JSON (or, for
    // external docs, the project file's Documents entry) rather than the filename, so switching to a
    // title-based name is purely cosmetic for every existing code path — these tests pin that filename.
    [Fact]
    public async Task CreateNewScriptAsync_NamesFileAfterTitle_NotTheGuidId()
    {
        var storage = new LocalScriptStorageService(_baseDir);

        var script = await storage.CreateNewScriptAsync("My Cool Script");

        var libraryRoot = Path.Combine(_baseDir, "library");
        Assert.True(File.Exists(Path.Combine(libraryRoot, "My Cool Script.frycs")));
        Assert.False(File.Exists(Path.Combine(libraryRoot, $"{script.Id}.frycs")));
    }

    [Fact]
    public async Task CreateNewNotebookAsync_NamesFileAfterTitle_NotTheGuidId()
    {
        var storage = new LocalScriptStorageService(_baseDir);

        var notebook = await storage.CreateNewNotebookAsync("My Cool Notebook");

        var libraryRoot = Path.Combine(_baseDir, "library");
        Assert.True(File.Exists(Path.Combine(libraryRoot, "My Cool Notebook.frynb")));
        Assert.False(File.Exists(Path.Combine(libraryRoot, $"{notebook.Id}.frynb")));
    }

    [Fact]
    public async Task CreateNewScriptAsync_WithInvalidFileNameCharsInTitle_SanitizesTheFileName()
    {
        var storage = new LocalScriptStorageService(_baseDir);

        // "/" is a path separator (an invalid filename char on every platform this runs on) — a
        // realistic case a user could actually type, e.g. titling a script "Q1/Q2 Report".
        var script = await storage.CreateNewScriptAsync("Q1/Q2 Report");

        var libraryRoot = Path.Combine(_baseDir, "library");
        Assert.True(File.Exists(Path.Combine(libraryRoot, "Q1_Q2 Report.frycs")));

        // Sanitization must only affect the on-disk filename — the stored title stays exactly as typed.
        var loaded = await storage.LoadScriptAsync(script.Id);
        Assert.Equal("Q1/Q2 Report", loaded!.Title);
    }

    [Fact]
    public async Task CreateNewScriptAsync_TwiceWithSameTitle_SuffixesTheSecondFileName()
    {
        var storage = new LocalScriptStorageService(_baseDir);

        var first = await storage.CreateNewScriptAsync("Duplicate Title");
        var second = await storage.CreateNewScriptAsync("Duplicate Title");

        var libraryRoot = Path.Combine(_baseDir, "library");
        Assert.True(File.Exists(Path.Combine(libraryRoot, "Duplicate Title.frycs")));
        Assert.True(File.Exists(Path.Combine(libraryRoot, "Duplicate Title (2).frycs")));

        // Both documents must still independently resolve by Id despite the similar filenames.
        Assert.Equal(first.Id, (await storage.LoadScriptAsync(first.Id))!.Id);
        Assert.Equal(second.Id, (await storage.LoadScriptAsync(second.Id))!.Id);
    }

    [Fact]
    public async Task CreateNewScriptAsync_WithAbsoluteFolderPath_NamesFileAfterTitleInThatFolder()
    {
        var externalDir = Path.Combine(Path.GetTempPath(), "FryPDF_ExternalDocTests_" + Guid.NewGuid().ToString("N"));
        try
        {
            var storage = new LocalScriptStorageService(_baseDir);

            var script = await storage.CreateNewScriptAsync("External Named Script", folderPath: externalDir);

            Assert.True(File.Exists(Path.Combine(externalDir, "External Named Script.frycs")));
            Assert.False(File.Exists(Path.Combine(externalDir, $"{script.Id}.frycs")));
        }
        finally
        {
            if (Directory.Exists(externalDir)) Directory.Delete(externalDir, recursive: true);
        }
    }
}
