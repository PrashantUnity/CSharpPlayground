using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class CSharpCodeStudioExplorerTests : IDisposable
{
    private readonly string _testBaseDir;
    private readonly LocalScriptStorageService _testStorage;

    public CSharpCodeStudioExplorerTests()
    {
        _testBaseDir = Path.Combine(Path.GetTempPath(), "FryPDF_CodeStudioExplorerTests_" + Guid.NewGuid().ToString("N"));
        _testStorage = new LocalScriptStorageService(_testBaseDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testBaseDir))
            {
                Directory.Delete(_testBaseDir, recursive: true);
            }
        }
        catch { }
    }

    private CSharpCodeStudioViewModel CreateStudio(ScriptDocumentItem script)
    {
        return new CSharpCodeStudioViewModel(
            script,
            _testStorage,
            new RoslynCompilerService(),
            new ScriptExecutionEngine(),
            backToHubAction: () => { },
            backToHomeAction: () => { });
    }

    [Fact]
    public async Task PopulateExplorerTree_WithLibraryScripts_ListsThemAtRoot()
    {
        var script = await _testStorage.CreateNewScriptAsync("Main Script");
        await _testStorage.CreateNewScriptAsync("Sibling Script");

        var studio = CreateStudio(script);

        Assert.Contains(studio.ExplorerRootItems, x => !x.IsDirectory && x.Name == "Main Script.frycs");
        Assert.Contains(studio.ExplorerRootItems, x => !x.IsDirectory && x.Name == "Sibling Script.frycs");
    }

    [Fact]
    public async Task PopulateExplorerTree_ForScriptOutsideActiveRoot_ShowsItAsOrphanTopLevelFile()
    {
        // A document saved outside the active workspace root (here: the still-empty internal library)
        // isn't part of the tree walk, but the currently open document always surfaces via the
        // orphan-node fallback so the active tab is never invisible in its own Explorer.
        var externalDir = Path.Combine(Path.GetTempPath(), "FryPDF_CodeStudioExternalTests_" + Guid.NewGuid().ToString("N"), "MyExternalFolder");
        try
        {
            var script = await _testStorage.CreateNewScriptAsync("External Script", folderPath: externalDir);
            var studio = CreateStudio(script);

            var item = Assert.Single(studio.ExplorerRootItems);
            Assert.False(item.IsDirectory);
            Assert.Equal("External Script.frycs", item.Name);
        }
        finally
        {
            var root = Path.GetDirectoryName(externalDir)!;
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PopulateExplorerTree_WithUnrelatedExternalScript_DoesNotShowIt()
    {
        var relevantDir = Path.Combine(Path.GetTempPath(), "FryPDF_CodeStudioExternalTests_" + Guid.NewGuid().ToString("N"), "hello");
        var unrelatedDir = Path.Combine(Path.GetTempPath(), "FryPDF_CodeStudioExternalTests_" + Guid.NewGuid().ToString("N"), "unrelated");
        try
        {
            var openScript = await _testStorage.CreateNewScriptAsync("Open One", folderPath: relevantDir);
            await _testStorage.CreateNewScriptAsync("Untouched", folderPath: unrelatedDir);

            var studio = CreateStudio(openScript);

            // Neither external folder is the active workspace root, so only the currently open
            // document shows (via the orphan-node fallback) — the unrelated one must not leak in.
            var item = Assert.Single(studio.ExplorerRootItems);
            Assert.False(item.IsDirectory);
            Assert.Equal("Open One.frycs", item.Name);
        }
        finally
        {
            foreach (var dir in new[] { relevantDir, unrelatedDir })
            {
                var root = Path.GetDirectoryName(dir)!;
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SwitchToScriptAsync_ToDifferentScript_UpdatesScriptAndCodeAndFiresReloadEvent()
    {
        var first = await _testStorage.CreateNewScriptAsync("First Script");
        first.Code = "// first script code";
        await _testStorage.SaveScriptAsync(first);

        var second = await _testStorage.CreateNewScriptAsync("Second Script");
        second.Code = "// second script code";
        await _testStorage.SaveScriptAsync(second);

        var studio = CreateStudio(first);
        studio.Code = "// edited in memory, unsaved";

        var reloadFired = false;
        studio.RequestReloadEditorText += () => reloadFired = true;

        var secondItem = studio.ExplorerRootItems.Single(x => x.Name == "Second Script.frycs");
        await studio.SwitchToScriptAsync(secondItem);

        Assert.Equal(second.Id, studio.Script.Id);
        Assert.Equal("// second script code", studio.Code);
        Assert.True(reloadFired);

        var reloadedFirst = await _testStorage.LoadScriptAsync(first.Id);
        Assert.Equal("// edited in memory, unsaved", reloadedFirst!.Code);
    }

    [Fact]
    public async Task SwitchToScriptAsync_ToAlreadyOpenScript_DoesNothingAndDoesNotFireReload()
    {
        var script = await _testStorage.CreateNewScriptAsync("Solo Script");
        var studio = CreateStudio(script);

        var reloadFired = false;
        studio.RequestReloadEditorText += () => reloadFired = true;

        var sameItem = studio.ExplorerRootItems.Single(x => x.Name == "Solo Script.frycs");
        await studio.SwitchToScriptAsync(sameItem);

        Assert.False(reloadFired);
    }

    [Fact]
    public async Task NewScriptCommand_WithNoSelection_CreatesAtLibraryRootAndSwitchesToIt()
    {
        var initial = await _testStorage.CreateNewScriptAsync("Initial Script");
        var studio = CreateStudio(initial);

        await studio.NewScript();

        Assert.NotEqual(initial.Id, studio.Script.Id);
        Assert.Contains(studio.ExplorerRootItems, x => x.DocumentId == studio.Script.Id);

        var summaries = await _testStorage.LoadWorkspaceSummariesAsync();
        var created = summaries.Single(s => s.Id == studio.Script.Id);
        Assert.True(string.IsNullOrEmpty(created.FolderPath));
    }

    [Fact]
    public async Task DeleteExplorerItemAsync_OnFolderInsideOpenedExternalRoot_DeletesItLikeAnyOtherFolder()
    {
        // Once a folder is opened as the active workspace root, every real folder inside it is fully
        // manageable — there's no synthetic "external wrapper" node left to protect from deletion.
        var externalDir = Path.Combine(Path.GetTempPath(), "FryPDF_CodeStudioExternalTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(externalDir);
        try
        {
            await _testStorage.OpenExternalProjectAsync(externalDir);
            var subFolder = await _testStorage.CreateFolderAsync(null, "SubFolder");
            var script = await _testStorage.CreateNewScriptAsync("Inside", folderPath: subFolder);
            var studio = CreateStudio(script);

            var folderNode = studio.ExplorerRootItems.Single(x => x.IsDirectory);
            Assert.True(folderNode.IsManageableDirectory);

            await studio.DeleteExplorerItemAsync(folderNode);

            Assert.False(Directory.Exists(Path.Combine(externalDir, "SubFolder")));
            Assert.DoesNotContain(studio.ExplorerRootItems, x => ReferenceEquals(x, folderNode));
        }
        finally
        {
            if (Directory.Exists(externalDir)) Directory.Delete(externalDir, recursive: true);
        }
    }

    [Fact]
    public async Task DeleteExplorerItemAsync_LastScriptInFolder_FolderStaysVisibleAndEmpty()
    {
        // Folders now always reflect a real directory on disk, so — unlike the old synthetic external
        // group node — an emptied folder doesn't disappear from the tree.
        var openScript = await _testStorage.CreateNewScriptAsync("Kept Open");
        var folder = await _testStorage.CreateFolderAsync(null, "SoloFolder");
        await _testStorage.CreateNewScriptAsync("Only One", folderPath: folder);
        var studio = CreateStudio(openScript);

        var summaries = await _testStorage.LoadWorkspaceSummariesAsync();
        var docId = summaries.Single(s => s.Title == "Only One").Id;
        var tempItem = new ExplorerItemViewModel { DocumentId = docId, IsDirectory = false };
        await studio.SwitchToScriptAsync(tempItem);

        var folderNode = studio.ExplorerRootItems.Single(x => x.IsDirectory && x.Name == "SoloFolder");
        var docItem = folderNode.Children.Single();

        await studio.DeleteExplorerItemAsync(docItem);

        Assert.Contains(studio.ExplorerRootItems, x => x.IsDirectory && x.Name == "SoloFolder");
    }

    [Fact]
    public async Task DuplicateExplorerItemAsync_ForDocumentInOpenedExternalRoot_KeepsCopyInSameFolder()
    {
        var externalDir = Path.Combine(Path.GetTempPath(), "FryPDF_CodeStudioExternalTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(externalDir);
        try
        {
            await _testStorage.OpenExternalProjectAsync(externalDir);
            var script = await _testStorage.CreateNewScriptAsync("Original");
            var studio = CreateStudio(script);
            var originalItem = studio.ExplorerRootItems.Single(x => !x.IsDirectory);

            await studio.DuplicateExplorerItemAsync(originalItem);

            var summaries = await _testStorage.LoadWorkspaceSummariesAsync();
            var copy = Assert.Single(summaries, s => s.Title == "Original Copy");
            Assert.Equal(string.Empty, copy.FolderPath);
            Assert.True(File.Exists(Path.Combine(externalDir, "Original Copy.frycs")));
        }
        finally
        {
            if (Directory.Exists(externalDir)) Directory.Delete(externalDir, recursive: true);
        }
    }

    [Fact]
    public async Task OnItemRenamedAsync_ForCurrentlyOpenScript_UpdatesScriptTitleAndPersists()
    {
        var script = await _testStorage.CreateNewScriptAsync("Old Name");
        var studio = CreateStudio(script);
        var item = studio.ExplorerRootItems.Single(x => x.DocumentId == script.Id);

        item.Name = "New Name.frycs";
        await studio.OnItemRenamedAsync(item);

        Assert.Equal("New Name", studio.Script.Title);

        var reloaded = await _testStorage.LoadScriptAsync(script.Id);
        Assert.Equal("New Name", reloaded!.Title);
    }

    [Fact]
    public async Task UpdateActiveScriptAsync_OnSingleThreadedUiLikeContext_DoesNotDeadlock()
    {
        var externalDir = Path.Combine(Path.GetTempPath(), "FryPDF_CodeStudioDeadlockTests_" + Guid.NewGuid().ToString("N"), "ExtFolder");
        try
        {
            var first = await _testStorage.CreateNewScriptAsync("First", folderPath: externalDir);
            var second = await _testStorage.CreateNewScriptAsync("Second", folderPath: externalDir);
            var studio = CreateStudio(first);

            var pump = new SingleThreadSynchronizationContext();
            var completed = new TaskCompletionSource<bool>();

            var pumpThread = new Thread(() =>
            {
                SynchronizationContext.SetSynchronizationContext(pump);
                pump.Post(async _ =>
                {
                    try
                    {
                        await studio.UpdateActiveScriptAsync(second);
                        completed.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        completed.SetException(ex);
                    }
                    finally
                    {
                        pump.Complete();
                    }
                }, null);
                pump.RunOnCurrentThread();
            })
            { IsBackground = true };
            pumpThread.Start();

            var finished = await Task.WhenAny(completed.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.True(ReferenceEquals(finished, completed.Task),
                "UpdateActiveScriptAsync deadlocked on a UI-like single-threaded SynchronizationContext.");
            await completed.Task;

            Assert.Equal(second.Id, studio.Script.Id);
        }
        finally
        {
            var root = Path.GetDirectoryName(externalDir)!;
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task OpenExternalProjectAsync_ValidScriptFile_UpdatesActiveScriptAndExplorer()
    {
        var externalDir = Path.Combine(Path.GetTempPath(), "FryPDF_CodeStudioOpenTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(externalDir);
        try
        {
            var externalFile = Path.Combine(externalDir, "ImportedScript.cs");
            await File.WriteAllTextAsync(externalFile, "System.Console.WriteLine(\"Imported!\");");

            var initialScript = await _testStorage.CreateNewScriptAsync("Initial");
            var studio = CreateStudio(initialScript);

            await studio.OpenExternalProjectAsync(externalFile);

            Assert.Equal("ImportedScript", studio.Script.Title);
            Assert.Contains("Imported!", studio.Code);
        }
        finally
        {
            if (Directory.Exists(externalDir)) Directory.Delete(externalDir, recursive: true);
        }
    }

    private sealed class SingleThreadSynchronizationContext : SynchronizationContext
    {
        private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();

        public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));

        public override void Send(SendOrPostCallback d, object? state) => d(state);

        public void RunOnCurrentThread()
        {
            foreach (var workItem in _queue.GetConsumingEnumerable())
            {
                workItem.Callback(workItem.State);
            }
        }

        public void Complete() => _queue.CompleteAdding();
    }
}
