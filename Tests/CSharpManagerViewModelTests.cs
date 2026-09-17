using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class CSharpManagerViewModelTests : IDisposable
{
    private readonly string _baseDir;
    private readonly LocalScriptStorageService _storage;

    public CSharpManagerViewModelTests()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), "FryPDF_HubTests_" + Guid.NewGuid().ToString("N"));
        _storage = new LocalScriptStorageService(_baseDir);
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

    private (CSharpManagerViewModel vm, int scriptOpens, int notebookOpens, ScriptDocumentItem? lastScript, NotebookDocumentItem? lastNotebook) CreateHub()
    {
        var scriptOpens = 0;
        var notebookOpens = 0;
        ScriptDocumentItem? lastScript = null;
        NotebookDocumentItem? lastNotebook = null;

        var vm = new CSharpManagerViewModel(
            _storage,
            openScriptAction: s => { scriptOpens++; lastScript = s; },
            openNotebookAction: n => { notebookOpens++; lastNotebook = n; },
            navigateToHomeAction: () => { });

        return (vm, scriptOpens, notebookOpens, lastScript, lastNotebook);
    }

    [Fact]
    public async Task LoadWorkspaceItemsAsync_ConcurrentCallFromConstructor_DoesNotCorruptOrDuplicateItems()
    {
        await _storage.CreateNewScriptAsync("C# Interactive Scratchpad");
        await _storage.CreateNewScriptAsync("1. Two Sum (Algorithm Workspace)");
        await _storage.CreateNewScriptAsync("PDF Document Automation");
        await _storage.CreateNewScriptAsync("C# Interactive Scratchpad");
        await _storage.CreateNewNotebookAsync("SkiaSharp Graphics & Image Generation");
        await _storage.CreateNewNotebookAsync("New Interactive Notebook");
        await _storage.CreateNewNotebookAsync("SkiaSharp Graphics & Image Generation");
        await _storage.CreateNewNotebookAsync("Document Automation Notebook");

        var expectedTotal = (await _storage.LoadWorkspaceSummariesAsync()).Count;

        var vm = new CSharpManagerViewModel(
            _storage,
            openScriptAction: _ => { },
            openNotebookAction: _ => { },
            navigateToHomeAction: () => { });
        await vm.LoadWorkspaceItemsAsync();

        Assert.Equal(expectedTotal, vm.AllItems.Count);
        Assert.Equal(vm.AllItems.Count, vm.TotalScripts + vm.TotalNotebooks);
        Assert.Equal(vm.AllItems.Count, vm.FilteredItems.Count);
        Assert.Equal(vm.AllItems.Select(i => i.Id).Distinct().Count(), vm.AllItems.Count);
    }

    [Fact]
    public async Task CreateNewScriptCommand_NoParameter_OpensLocationPromptThenConfirmInvokesOpenCallback()
    {
        var scriptOpens = 0;
        ScriptDocumentItem? opened = null;
        var vm = new CSharpManagerViewModel(
            _storage,
            openScriptAction: s => { scriptOpens++; opened = s; },
            openNotebookAction: _ => { },
            navigateToHomeAction: () => { });
        await vm.LoadWorkspaceItemsAsync();

        ((System.Windows.Input.ICommand)vm.CreateNewScriptCommand).Execute(null);
        await Task.Delay(200);

        Assert.True(vm.IsCreatePromptOpen);
        Assert.Equal(0, scriptOpens);
        Assert.Null(opened);

        ((System.Windows.Input.ICommand)vm.ConfirmCreateCommand).Execute(null);
        await Task.Delay(200);

        Assert.Equal(1, scriptOpens);
        Assert.NotNull(opened);
        Assert.False(vm.IsCreatePromptOpen);
    }

    [Fact]
    public async Task CreateNewNotebookCommand_NoParameter_OpensLocationPromptThenConfirmInvokesOpenCallback()
    {
        var notebookOpens = 0;
        NotebookDocumentItem? opened = null;
        var vm = new CSharpManagerViewModel(
            _storage,
            openScriptAction: _ => { },
            openNotebookAction: n => { notebookOpens++; opened = n; },
            navigateToHomeAction: () => { });
        await vm.LoadWorkspaceItemsAsync();

        ((System.Windows.Input.ICommand)vm.CreateNewNotebookCommand).Execute(null);
        await Task.Delay(200);

        Assert.True(vm.IsCreatePromptOpen);
        Assert.Equal(0, notebookOpens);
        Assert.Null(opened);

        ((System.Windows.Input.ICommand)vm.ConfirmCreateCommand).Execute(null);
        await Task.Delay(200);

        Assert.Equal(1, notebookOpens);
        Assert.NotNull(opened);
        Assert.False(vm.IsCreatePromptOpen);
    }

    [Fact]
    public async Task ConfirmCreateCommand_WithSelectedFolderPath_CreatesScriptInsideThatFolder()
    {
        var vm = new CSharpManagerViewModel(
            _storage,
            openScriptAction: _ => { },
            openNotebookAction: _ => { },
            navigateToHomeAction: () => { });
        await vm.LoadWorkspaceItemsAsync();

        await vm.CreateNewScriptAsync();
        vm.SelectedFolderPath = "Reports";
        vm.NewItemName = "Quarterly Export";

        await vm.ConfirmCreateAsync();

        var created = (await _storage.LoadWorkspaceSummariesAsync()).Single(i => i.Title == "Quarterly Export");
        Assert.Equal("Reports", created.FolderPath);
    }

    [Fact]
    public async Task OpenItemCommand_ForEachExistingItem_InvokesCorrectCallbackTypeWithoutThrowing()
    {
        await _storage.CreateNewScriptAsync("Some Script");
        await _storage.CreateNewNotebookAsync("Some Notebook");

        var scriptOpens = 0;
        var notebookOpens = 0;
        var vm = new CSharpManagerViewModel(
            _storage,
            openScriptAction: _ => scriptOpens++,
            openNotebookAction: _ => notebookOpens++,
            navigateToHomeAction: () => { });
        await vm.LoadWorkspaceItemsAsync();

        var expectedScripts = vm.AllItems.Count(i => i.IsScript);
        var expectedNotebooks = vm.AllItems.Count(i => i.IsNotebook);
        Assert.True(expectedScripts >= 1);
        Assert.True(expectedNotebooks >= 1);

        foreach (var item in vm.AllItems.ToList())
        {
            await vm.OpenItemAsync(item);
        }

        Assert.Equal(expectedScripts, scriptOpens);
        Assert.Equal(expectedNotebooks, notebookOpens);
    }

    [Fact]
    public async Task LaunchTemplateCommand_ForEveryRealStarterTemplate_OpensSomethingWithoutThrowing()
    {
        var (vm, _, _, _, _) = CreateHub();
        var scriptOpens = 0;
        var notebookOpens = 0;
        vm = new CSharpManagerViewModel(
            _storage,
            openScriptAction: _ => scriptOpens++,
            openNotebookAction: _ => notebookOpens++,
            navigateToHomeAction: () => { });
        await vm.LoadWorkspaceItemsAsync();

        foreach (var template in vm.StarterTemplates.ToList())
        {
            await vm.LaunchTemplateAsync(template);
        }

        Assert.Equal(vm.StarterTemplates.Count, scriptOpens + notebookOpens);
    }

    [Fact]
    public async Task LaunchTemplateCommand_CalledTwiceBackToBack_CreatesExactlyOneDocument()
    {
        var notebookOpens = 0;
        var vm = new CSharpManagerViewModel(
            _storage,
            openScriptAction: _ => { },
            openNotebookAction: _ => notebookOpens++,
            navigateToHomeAction: () => { });
        await vm.LoadWorkspaceItemsAsync();

        var template = vm.StarterTemplates.First(t => t.Kind == WorkspaceItemKind.Notebook);

        var firstCall = vm.LaunchTemplateAsync(template);
        var secondCall = vm.LaunchTemplateAsync(template);
        await Task.WhenAll(firstCall, secondCall);

        var matches = (await _storage.LoadWorkspaceSummariesAsync())
            .Count(i => string.Equals(i.Title, template.Title, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, matches);
        Assert.Equal(1, notebookOpens);
    }

    [Fact]
    public async Task ConfirmCreateCommand_CalledTwiceBackToBackAfterOpeningNotebookPrompt_CreatesExactlyOneDocument()
    {
        var notebookOpens = 0;
        var vm = new CSharpManagerViewModel(
            _storage,
            openScriptAction: _ => { },
            openNotebookAction: _ => notebookOpens++,
            navigateToHomeAction: () => { });
        await vm.LoadWorkspaceItemsAsync();

        await vm.CreateNewNotebookAsync();
        Assert.True(vm.IsCreatePromptOpen);

        var firstCall = vm.ConfirmCreateAsync();
        var secondCall = vm.ConfirmCreateAsync();
        await Task.WhenAll(firstCall, secondCall);

        var matches = (await _storage.LoadWorkspaceSummariesAsync())
            .Count(i => i.Title.StartsWith("New Interactive Notebook", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, matches);
        Assert.Equal(1, notebookOpens);
    }

    [Fact]
    public async Task LoadWorkspaceItemsAsync_WithMultipleDocsInSameExternalFolder_GroupsThemUnderOneHeader()
    {
        var externalDir = Path.Combine(Path.GetTempPath(), "FryPDF_HubExternalTests_" + Guid.NewGuid().ToString("N"), "SharedFolder");
        try
        {
            await _storage.CreateNewScriptAsync("External One", folderPath: externalDir);
            await _storage.CreateNewNotebookAsync("External Two", folderPath: externalDir);

            var (vm, _, _, _, _) = CreateHub();
            await vm.LoadWorkspaceItemsAsync();

            var header = Assert.Single(vm.FilteredItems.OfType<WorkspaceGroupHeaderViewModel>());
            Assert.Equal("SharedFolder", header.Title);
            Assert.Equal(2, header.ItemCount);

            var items = vm.FilteredItems.OfType<WorkspaceItemSummary>().ToList();
            Assert.Contains(items, i => i.Title == "External One");
            Assert.Contains(items, i => i.Title == "External Two");

            var headerIndex = vm.FilteredItems.IndexOf(header);
            Assert.IsType<WorkspaceItemSummary>(vm.FilteredItems[headerIndex + 1]);
            Assert.IsType<WorkspaceItemSummary>(vm.FilteredItems[headerIndex + 2]);
        }
        finally
        {
            var root = Path.GetDirectoryName(externalDir)!;
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task LoadWorkspaceItemsAsync_WithInternalDocumentsOnly_NeverAddsGroupHeaders()
    {
        await _storage.CreateNewScriptAsync("Local Script");
        await _storage.CreateNewNotebookAsync("Local Notebook");

        var (vm, _, _, _, _) = CreateHub();
        await vm.LoadWorkspaceItemsAsync();

        Assert.Empty(vm.FilteredItems.OfType<WorkspaceGroupHeaderViewModel>());
        Assert.Equal(vm.AllItems.Count, vm.FilteredItems.OfType<WorkspaceItemSummary>().Count());
    }

    [Fact]
    public async Task DeleteItemAsync_LastDocumentInExternalFolder_RemovesTheNowEmptyGroupHeaderToo()
    {
        var externalDir = Path.Combine(Path.GetTempPath(), "FryPDF_HubExternalTests_" + Guid.NewGuid().ToString("N"), "SoloFolder");
        try
        {
            var script = await _storage.CreateNewScriptAsync("Only External", folderPath: externalDir);

            var (vm, _, _, _, _) = CreateHub();
            await vm.LoadWorkspaceItemsAsync();

            var toDelete = vm.AllItems.Single(i => i.Id == script.Id);
            await vm.DeleteItemAsync(toDelete);

            Assert.Empty(vm.FilteredItems.OfType<WorkspaceGroupHeaderViewModel>());
            Assert.DoesNotContain(vm.FilteredItems.OfType<WorkspaceItemSummary>(), i => i.Id == script.Id);
        }
        finally
        {
            var root = Path.GetDirectoryName(externalDir)!;
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DeleteItemAsync_OneOfSeveralDocumentsInExternalFolder_KeepsHeaderWithUpdatedCount()
    {
        var externalDir = Path.Combine(Path.GetTempPath(), "FryPDF_HubExternalTests_" + Guid.NewGuid().ToString("N"), "SharedFolder");
        try
        {
            var first = await _storage.CreateNewScriptAsync("Keep Me", folderPath: externalDir);
            var second = await _storage.CreateNewScriptAsync("Delete Me", folderPath: externalDir);

            var (vm, _, _, _, _) = CreateHub();
            await vm.LoadWorkspaceItemsAsync();

            var toDelete = vm.AllItems.Single(i => i.Id == second.Id);
            await vm.DeleteItemAsync(toDelete);

            var header = Assert.Single(vm.FilteredItems.OfType<WorkspaceGroupHeaderViewModel>());
            Assert.Equal(1, header.ItemCount);
            Assert.Contains(vm.FilteredItems.OfType<WorkspaceItemSummary>(), i => i.Id == first.Id);
        }
        finally
        {
            var root = Path.GetDirectoryName(externalDir)!;
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
    [Fact]
    public async Task PinningAndUnpinning_ToggleAndCommands_PersistsAndSyncsCorrectly()
    {
        var sc1 = await _storage.CreateNewScriptAsync("Script Alpha");
        var sc2 = await _storage.CreateNewScriptAsync("Script Beta");
        var nb1 = await _storage.CreateNewNotebookAsync("Notebook Gamma");

        var (vm, _, _, _, _) = CreateHub();
        await vm.LoadWorkspaceItemsAsync();

        Assert.True(vm.AllItems.Count >= 3);
        Assert.True(vm.HasAnyPinnedItems);

        var itemAlpha = vm.AllItems.First(i => i.Id == sc1.Id);

        await vm.UnpinItemAsync(itemAlpha);
        Assert.False(itemAlpha.IsPinned);
        Assert.DoesNotContain(vm.PinnedItems, i => i.Id == itemAlpha.Id);
        Assert.Contains(vm.UnpinnedItems, i => i.Id == itemAlpha.Id);

        await vm.PinItemAsync(itemAlpha);
        Assert.True(itemAlpha.IsPinned);
        Assert.Contains(vm.PinnedItems, i => i.Id == itemAlpha.Id);
        Assert.DoesNotContain(vm.UnpinnedItems, i => i.Id == itemAlpha.Id);

        await vm.TogglePinAsync(itemAlpha);
        Assert.False(itemAlpha.IsPinned);
        Assert.DoesNotContain(vm.PinnedItems, i => i.Id == itemAlpha.Id);

        await vm.TogglePinAsync(itemAlpha);
        Assert.True(itemAlpha.IsPinned);
        Assert.Contains(vm.PinnedItems, i => i.Id == itemAlpha.Id);

        var (vm2, _, _, _, _) = CreateHub();
        await vm2.LoadWorkspaceItemsAsync();

        var reloadedAlpha = vm2.AllItems.First(i => i.Id == itemAlpha.Id);
        Assert.True(reloadedAlpha.IsPinned);
        Assert.Contains(vm2.PinnedItems, i => i.Id == itemAlpha.Id);
    }

    [Fact]
    public async Task DeleteItemAsync_PinnedItem_RemovesFromPinnedItemsAndPersists()
    {
        var sc = await _storage.CreateNewScriptAsync("To Be Deleted Pinned Script");

        var (vm, _, _, _, _) = CreateHub();
        await vm.LoadWorkspaceItemsAsync();

        var item = vm.AllItems.First(i => i.Id == sc.Id);
        await vm.PinItemAsync(item);
        Assert.True(item.IsPinned);
        Assert.Contains(vm.PinnedItems, i => i.Id == item.Id);

        await vm.DeleteItemAsync(item);
        Assert.DoesNotContain(vm.PinnedItems, i => i.Id == item.Id);
        Assert.DoesNotContain(vm.AllItems, i => i.Id == item.Id);

        var (vm2, _, _, _, _) = CreateHub();
        await vm2.LoadWorkspaceItemsAsync();
        Assert.DoesNotContain(vm2.PinnedItems, i => i.Id == item.Id);
    }

    [Fact]
    public async Task PinAndToggleAsync_ExecutesAsynchronouslyWithoutBlocking()
    {
        var sc = await _storage.CreateNewScriptAsync("Async Test Script");
        var (vm, _, _, _, _) = CreateHub();
        await vm.LoadWorkspaceItemsAsync();

        var item = vm.AllItems.First(i => i.Id == sc.Id);

        await vm.PinItemAsync(item);
        Assert.True(item.IsPinned);
        Assert.Contains(vm.PinnedItems, i => i.Id == item.Id);

        await vm.TogglePinAsync(item);
        Assert.False(item.IsPinned);
        Assert.DoesNotContain(vm.PinnedItems, i => i.Id == item.Id);

        await vm.TogglePinAsync(item);
        Assert.True(item.IsPinned);
        Assert.Contains(vm.PinnedItems, i => i.Id == item.Id);

        await vm.UnpinItemAsync(item);
        Assert.False(item.IsPinned);
        Assert.DoesNotContain(vm.PinnedItems, i => i.Id == item.Id);
    }

    [Fact]
    public async Task OpenExistingProjectAsync_ValidScriptFile_LoadsWorkspaceAndTriggersOpenAction()
    {
        var externalDir = Path.Combine(Path.GetTempPath(), "FryPDF_HubOpenTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(externalDir);
        try
        {
            var scriptId = Guid.NewGuid().ToString("N");
            var scriptDoc = new ScriptDocumentItem
            {
                Id = scriptId,
                Title = "Downloaded External Script",
                Code = "Console.WriteLine(\"Opened from Hub\");"
            };
            var scriptPath = Path.Combine(externalDir, "Downloaded External Script.frycs");
            await File.WriteAllTextAsync(
                scriptPath,
                System.Text.Json.JsonSerializer.Serialize(scriptDoc, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

            var scriptOpens = 0;
            ScriptDocumentItem? lastScript = null;
            var vm = new CSharpManagerViewModel(
                _storage,
                openScriptAction: s => { scriptOpens++; lastScript = s; },
                openNotebookAction: _ => { },
                navigateToHomeAction: () => { });
            await vm.LoadWorkspaceItemsAsync();

            await vm.OpenExistingProjectAsync(scriptPath);

            Assert.True(vm.HasStatusBannerMessage);
            Assert.False(vm.IsStatusBannerError);
            Assert.Contains("Loaded script", vm.StatusBannerMessage);

            Assert.True(scriptOpens > 0);
            Assert.NotNull(lastScript);
            Assert.Equal("Downloaded External Script", lastScript!.Title);

            Assert.Contains(vm.AllItems, i => i.Id == scriptId);
        }
        finally
        {
            if (Directory.Exists(externalDir)) Directory.Delete(externalDir, recursive: true);
        }
    }
}
