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
    public async Task PopulateExplorerTree_ForExternallySavedScript_GroupsUnderSingleParentFolderNode()
    {
        var externalDir = Path.Combine(Path.GetTempPath(), "FryPDF_CodeStudioExternalTests_" + Guid.NewGuid().ToString("N"), "MyExternalFolder");
        try
        {
            var script = await _testStorage.CreateNewScriptAsync("External Script", folderPath: externalDir);
            var studio = CreateStudio(script);

            var groupNode = Assert.Single(studio.ExplorerRootItems, x => x.IsDirectory);
            Assert.Equal("MyExternalFolder", groupNode.Name);
            Assert.True(groupNode.IsExternalGroup);
            Assert.False(groupNode.IsManageableDirectory);

            var docItem = Assert.Single(groupNode.Children);
            Assert.Equal("External Script.frycs", docItem.Name);
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

            var externalGroups = studio.ExplorerRootItems.Where(x => x.IsExternalGroup).ToList();
            var visible = Assert.Single(externalGroups);
            Assert.Equal("hello", visible.Name);
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
    public async Task DeleteExplorerItemAsync_OnExternalGroupNode_LeavesRealFolderAndTreeNodeIntact()
    {
        var externalDir = Path.Combine(Path.GetTempPath(), "FryPDF_CodeStudioExternalTests_" + Guid.NewGuid().ToString("N"), "MyExternalFolder");
        try
        {
            var script = await _testStorage.CreateNewScriptAsync("External Script", folderPath: externalDir);
            var studio = CreateStudio(script);
            var groupNode = studio.ExplorerRootItems.Single(x => x.IsDirectory && x.IsExternalGroup);

            await studio.DeleteExplorerItemAsync(groupNode);

            Assert.True(Directory.Exists(externalDir));
            Assert.Contains(studio.ExplorerRootItems, x => ReferenceEquals(x, groupNode));
        }
        finally
        {
            var root = Path.GetDirectoryName(externalDir)!;
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DeleteExplorerItemAsync_LastScriptInExternalGroup_RemovesTheNowEmptyGroupNode()
    {
        var externalDir = Path.Combine(Path.GetTempPath(), "FryPDF_CodeStudioExternalTests_" + Guid.NewGuid().ToString("N"), "SoloFolder");
        try
        {
            var openScript = await _testStorage.CreateNewScriptAsync("Kept Open");
            await _testStorage.CreateNewScriptAsync("Only External", folderPath: externalDir);
            var studio = CreateStudio(openScript);

            var summaries = await _testStorage.LoadWorkspaceSummariesAsync();
            var externalDocId = summaries.Single(s => s.Title == "Only External").Id;
            var tempItem = new ExplorerItemViewModel { DocumentId = externalDocId, IsDirectory = false };
            await studio.SwitchToScriptAsync(tempItem);

            var groupNode = studio.ExplorerRootItems.Single(x => x.IsDirectory && x.IsExternalGroup);
            var docItem = groupNode.Children.Single();

            await studio.DeleteExplorerItemAsync(docItem);

            Assert.DoesNotContain(studio.ExplorerRootItems, x => x.IsExternalGroup);
        }
        finally
        {
            var root = Path.GetDirectoryName(externalDir)!;
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DuplicateExplorerItemAsync_ForExternallySavedScript_KeepsCopyInSameExternalFolder()
    {
        var externalDir = Path.Combine(Path.GetTempPath(), "FryPDF_CodeStudioExternalTests_" + Guid.NewGuid().ToString("N"), "DupFolder");
        try
        {
            var script = await _testStorage.CreateNewScriptAsync("Original", folderPath: externalDir);
            var studio = CreateStudio(script);
            var groupNode = studio.ExplorerRootItems.Single(x => x.IsDirectory && x.IsExternalGroup);
            var originalItem = groupNode.Children.Single();

            await studio.DuplicateExplorerItemAsync(originalItem);

            var summaries = await _testStorage.LoadWorkspaceSummariesAsync();
            var copy = Assert.Single(summaries, s => s.Title == "Original Copy");
            Assert.Equal(externalDir, copy.FolderPath);
        }
        finally
        {
            var root = Path.GetDirectoryName(externalDir)!;
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
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
