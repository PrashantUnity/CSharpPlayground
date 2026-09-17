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
        var libraryRoot = Path.Combine(_baseDir, "library");
        Directory.CreateDirectory(Path.Combine(libraryRoot, "Blocked.frycs"));

        var script = new ScriptDocumentItem { Id = scriptId, Title = "Blocked" };
        var saved = await storage.SaveScriptAsync(script);

        Assert.False(saved);
        var normalScript = new ScriptDocumentItem { Id = Guid.NewGuid().ToString("N"), Title = "Fine" };
        Assert.True(await storage.SaveScriptAsync(normalScript));
    }
    [Fact]
    public async Task LoadNotebookAsync_FileNameDoesNotMatchInternalId_StillFindsAndLoadsIt()
    {
        var storage = new LocalScriptStorageService(_baseDir);
        await storage.LoadWorkspaceSummariesAsync();

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

            var afterRestart = new LocalScriptStorageService(_baseDir);
            var summaries = await afterRestart.LoadWorkspaceSummariesAsync();
            Assert.DoesNotContain(summaries, s => s.Id == script.Id);
        }
        finally
        {
            if (Directory.Exists(externalDir)) Directory.Delete(externalDir, recursive: true);
        }
    }

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

        var script = await storage.CreateNewScriptAsync("Q1/Q2 Report");

        var libraryRoot = Path.Combine(_baseDir, "library");
        Assert.True(File.Exists(Path.Combine(libraryRoot, "Q1_Q2 Report.frycs")));

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

    [Fact]
    public async Task OpenExternalProjectAsync_WithValidFryCsProjFile_RegistersAndLoadsDocuments()
    {
        var externalDir = Path.Combine(Path.GetTempPath(), "FryPDF_OpenProjTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(externalDir);
        try
        {
            var scriptId = Guid.NewGuid().ToString("N");
            var scriptDoc = new ScriptDocumentItem
            {
                Id = scriptId,
                Title = "Downloaded Script",
                Code = "Console.WriteLine(\"Hello\");"
            };
            await File.WriteAllTextAsync(
                Path.Combine(externalDir, "Downloaded Script.frycs"),
                JsonSerializer.Serialize(scriptDoc, new JsonSerializerOptions { WriteIndented = true }));

            var projFilePath = Path.Combine(externalDir, "MyExternalProject.frycsproj");
            var projJson = @"{
                ""Kind"": ""Script"",
                ""Documents"": [
                    { ""Id"": """ + scriptId + @""", ""File"": ""Downloaded Script.frycs"" }
                ]
            }";
            await File.WriteAllTextAsync(projFilePath, projJson);

            var storage = new LocalScriptStorageService(_baseDir);
            var result = await storage.OpenExternalProjectAsync(projFilePath);

            Assert.True(result.Success);
            Assert.Equal(scriptId, result.PrimaryDocumentId);
            Assert.Equal(WorkspaceItemKind.Script, result.PrimaryDocumentKind);
            Assert.Equal(1, result.DocumentsLoadedCount);

            var loadedScript = await storage.LoadScriptAsync(scriptId);
            Assert.NotNull(loadedScript);
            Assert.Equal("Downloaded Script", loadedScript!.Title);

            var summaries = await storage.LoadWorkspaceSummariesAsync();
            Assert.Contains(summaries, s => s.Id == scriptId);
        }
        finally
        {
            if (Directory.Exists(externalDir)) Directory.Delete(externalDir, recursive: true);
        }
    }

    [Fact]
    public async Task OpenExternalProjectAsync_WithWindowsBackslashesInProjectFile_NormalizesAndResolvesOnUnix()
    {
        var externalDir = Path.Combine(Path.GetTempPath(), "FryPDF_OpenProjTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(externalDir);
        try
        {
            var scriptId = Guid.NewGuid().ToString("N");
            var scriptDoc = new ScriptDocumentItem
            {
                Id = scriptId,
                Title = "Windows Script",
                Code = "Console.WriteLine(\"Windows\");"
            };
            await File.WriteAllTextAsync(
                Path.Combine(externalDir, "Windows Script.frycs"),
                JsonSerializer.Serialize(scriptDoc, new JsonSerializerOptions { WriteIndented = true }));

            var projFilePath = Path.Combine(externalDir, "WindowsProject.frycsproj");
            var projJson = @"{
                ""Kind"": ""Script"",
                ""Documents"": [
                    { ""Id"": """ + scriptId + @""", ""File"": ""subdir\\Windows Script.frycs"" }
                ]
            }";
            await File.WriteAllTextAsync(projFilePath, projJson);

            var storage = new LocalScriptStorageService(_baseDir);
            var result = await storage.OpenExternalProjectAsync(projFilePath);

            Assert.True(result.Success);
            Assert.Equal(scriptId, result.PrimaryDocumentId);

            var loadedScript = await storage.LoadScriptAsync(scriptId);
            Assert.NotNull(loadedScript);
            Assert.Equal("Windows Script", loadedScript!.Title);
        }
        finally
        {
            if (Directory.Exists(externalDir)) Directory.Delete(externalDir, recursive: true);
        }
    }

    [Fact]
    public async Task OpenExternalProjectAsync_WithFolderContainingLooseScripts_AutoGeneratesProjectFileAndIndexes()
    {
        var externalDir = Path.Combine(Path.GetTempPath(), "FryPDF_OpenProjTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(externalDir);
        try
        {
            var scriptDoc = new ScriptDocumentItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = "Loose Script",
                Code = "Console.WriteLine(\"Loose\");"
            };
            await File.WriteAllTextAsync(
                Path.Combine(externalDir, "Loose Script.frycs"),
                JsonSerializer.Serialize(scriptDoc, new JsonSerializerOptions { WriteIndented = true }));

            var storage = new LocalScriptStorageService(_baseDir);
            var result = await storage.OpenExternalProjectAsync(externalDir);

            Assert.True(result.Success);
            Assert.Equal(1, result.DocumentsLoadedCount);
            Assert.Equal(scriptDoc.Id, result.PrimaryDocumentId);

            var summaries = await storage.LoadWorkspaceSummariesAsync();
            Assert.Contains(summaries, s => s.Id == scriptDoc.Id);
        }
        finally
        {
            if (Directory.Exists(externalDir)) Directory.Delete(externalDir, recursive: true);
        }
    }

    [Fact]
    public async Task OpenExternalProjectAsync_WithLooseFryNbFile_RegistersFolderAndLoadsNotebook()
    {
        var externalDir = Path.Combine(Path.GetTempPath(), "FryPDF_OpenProjTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(externalDir);
        try
        {
            var nbDoc = new NotebookDocumentItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = "Loose Notebook"
            };
            var nbPath = Path.Combine(externalDir, "Loose Notebook.frynb");
            await File.WriteAllTextAsync(nbPath, JsonSerializer.Serialize(nbDoc, new JsonSerializerOptions { WriteIndented = true }));

            var storage = new LocalScriptStorageService(_baseDir);
            var result = await storage.OpenExternalProjectAsync(nbPath);

            Assert.True(result.Success);
            Assert.Equal(nbDoc.Id, result.PrimaryDocumentId);
            Assert.Equal(WorkspaceItemKind.Notebook, result.PrimaryDocumentKind);

            var loaded = await storage.LoadNotebookAsync(nbDoc.Id);
            Assert.NotNull(loaded);
            Assert.Equal("Loose Notebook", loaded!.Title);
        }
        finally
        {
            if (Directory.Exists(externalDir)) Directory.Delete(externalDir, recursive: true);
        }
    }

    [Fact]
    public async Task OpenExternalProjectAsync_WithRawCsFile_WrapsIntoScriptDocumentAndRegisters()
    {
        var externalDir = Path.Combine(Path.GetTempPath(), "FryPDF_OpenProjTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(externalDir);
        try
        {
            var csPath = Path.Combine(externalDir, "QuickScript.cs");
            await File.WriteAllTextAsync(csPath, "Console.WriteLine(\"From C# file\");");

            var storage = new LocalScriptStorageService(_baseDir);
            var result = await storage.OpenExternalProjectAsync(csPath);

            Assert.True(result.Success);
            Assert.Equal(WorkspaceItemKind.Script, result.PrimaryDocumentKind);
            Assert.NotNull(result.PrimaryDocumentId);

            var loaded = await storage.LoadScriptAsync(result.PrimaryDocumentId!);
            Assert.NotNull(loaded);
            Assert.Equal("QuickScript", loaded!.Title);
            Assert.Contains("From C# file", loaded.Code);
        }
        finally
        {
            if (Directory.Exists(externalDir)) Directory.Delete(externalDir, recursive: true);
        }
    }

    [Fact]
    public async Task OpenExternalProjectAsync_WithNonExistentPath_ReturnsFailureResult()
    {
        var storage = new LocalScriptStorageService(_baseDir);
        var result = await storage.OpenExternalProjectAsync("/path/does/not/exist/anywhere/test.frycsproj");

        Assert.False(result.Success);
        Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
    }
}
