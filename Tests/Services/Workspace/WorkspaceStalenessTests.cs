using PdfEditorApp.Plugins.CSharpEditor.Services.Execution;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;
using Xunit;
using CSharpCodeStudioViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio.CSharpCodeStudioViewModel;
using CSharpManagerViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Hub.CSharpManagerViewModel;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// Switching a tab or coming back to a page must not re-walk and re-read the whole workspace; it reloads only when the
/// workspace really changed. The storage service's change stamps and scan counter make that provable.
/// </summary>
public class WorkspaceStalenessTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_StalenessTests_" + Guid.NewGuid().ToString("N"));
    private readonly LocalScriptStorageService _storage;

    public WorkspaceStalenessTests()
    {
        _storage = new LocalScriptStorageService(_dir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }
        catch
        {
            // The OS cleans the temp folder eventually.
        }
    }

    private CSharpCodeStudioViewModel CreateStudio(PdfEditorApp.Plugins.CSharpEditor.Models.ScriptDocumentItem script) => new(
        script,
        _storage,
        new RoslynCompilerService(),
        new ScriptExecutionEngine(),
        backToHubAction: () => { },
        backToHomeAction: () => { });

    [Fact]
    public async Task StructureVersion_MovesWhenItemsAreCreatedRenamedOrDeleted_ButNotOnASave()
    {
        var structure = _storage.StructureVersion;
        var script = await _storage.CreateNewScriptAsync("Versioned");
        Assert.True(_storage.StructureVersion > structure, "creating a script changes the tree");

        structure = _storage.StructureVersion;
        script.Code = "// edited";
        Assert.True(await _storage.SaveScriptAsync(script));
        Assert.Equal(structure, _storage.StructureVersion);

        await _storage.CreateFolderAsync(null, "Folder");
        Assert.True(_storage.StructureVersion > structure, "creating a folder changes the tree");

        structure = _storage.StructureVersion;
        await _storage.RenameFolderAsync("Folder", "Renamed");
        Assert.True(_storage.StructureVersion > structure, "renaming a folder changes the tree");

        structure = _storage.StructureVersion;
        await _storage.DeleteFolderAsync("Renamed");
        Assert.True(_storage.StructureVersion > structure, "deleting a folder changes the tree");

        structure = _storage.StructureVersion;
        await _storage.DeleteItemAsync(script.Id);
        Assert.True(_storage.StructureVersion > structure, "deleting a script changes the tree");
    }

    [Fact]
    public async Task ContentVersion_MovesOnASave_SoTheHubCanNoticeANewModifiedTime()
    {
        var script = await _storage.CreateNewScriptAsync("Saved");
        var content = _storage.ContentVersion;

        script.Code = "// edited";
        await _storage.SaveScriptAsync(script);

        Assert.True(_storage.ContentVersion > content);
    }

    [Fact]
    public async Task SwitchingTabs_DoesNotRescanTheWorkspace()
    {
        var first = await _storage.CreateNewScriptAsync("Alpha");
        var second = await _storage.CreateNewScriptAsync("Beta");
        var studio = CreateStudio(first);

        var scans = _storage.WorkspaceScanCount;
        await studio.UpdateActiveScriptAsync(second);
        await studio.SwitchToTabAsync(studio.OpenTabs.First(t => t.Id == first.Id));
        await studio.SwitchToTabAsync(studio.OpenTabs.First(t => t.Id == second.Id));
        await studio.SwitchToTabAsync(studio.OpenTabs.First(t => t.Id == first.Id));

        Assert.Equal(scans, _storage.WorkspaceScanCount);
        Assert.True((bool)studio.ExplorerRootItems.Single(x => x.DocumentId == first.Id).IsSelected);
    }

    [Fact]
    public async Task SwitchingTabs_AfterAnotherPageCreatedAScript_ShowsTheNewScriptInTheExplorer()
    {
        var first = await _storage.CreateNewScriptAsync("Alpha");
        var second = await _storage.CreateNewScriptAsync("Beta");
        var studio = CreateStudio(first);
        await studio.UpdateActiveScriptAsync(second);

        await _storage.CreateNewScriptAsync("Gamma");
        await studio.SwitchToTabAsync(studio.OpenTabs.First(t => t.Id == first.Id));

        Assert.Contains(studio.ExplorerRootItems, x => x.Name == "Gamma.frycs");
    }

    [Fact]
    public async Task ShowingTheExplorerAgain_WithNothingChanged_DoesNotRescan()
    {
        var script = await _storage.CreateNewScriptAsync("Alpha");
        var studio = CreateStudio(script);

        var scans = _storage.WorkspaceScanCount;
        await studio.RefreshExplorerIfStaleAsync();
        await studio.RefreshExplorerIfStaleAsync();

        Assert.Equal(scans, _storage.WorkspaceScanCount);
    }

    [Fact]
    public async Task ShowingTheExplorerAgain_AfterASaveOnly_DoesNotRescan()
    {
        var script = await _storage.CreateNewScriptAsync("Alpha");
        var studio = CreateStudio(script);

        script.Code = "// edited";
        await _storage.SaveScriptAsync(script);
        var scans = _storage.WorkspaceScanCount;
        await studio.RefreshExplorerIfStaleAsync();

        Assert.Equal(scans, _storage.WorkspaceScanCount);
    }

    [Fact]
    public async Task TheHub_ReloadsOnReturnOnlyWhenTheWorkspaceChanged()
    {
        await _storage.CreateNewScriptAsync("Alpha");
        var hub = new CSharpManagerViewModel(_storage, openScriptAction: _ => { }, openNotebookAction: _ => { });
        await hub.LoadWorkspaceItemsAsync();

        var scans = _storage.WorkspaceScanCount;
        await hub.ReloadIfStaleAsync();
        Assert.Equal(scans, _storage.WorkspaceScanCount);

        await _storage.CreateNewScriptAsync("Beta");
        await hub.ReloadIfStaleAsync();

        Assert.Equal(scans + 1, _storage.WorkspaceScanCount);
        Assert.Contains(hub.AllItems, i => i.Title == "Beta");
    }
}
