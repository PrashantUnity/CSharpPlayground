using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;
using PdfEditorApp.Plugins.CSharpEditor.Services.Workspace;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Hub;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class RecentWorkspaceServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _jsonPath;
    private readonly RecentWorkspaceService _service;

    public RecentWorkspaceServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "RecentWsTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _jsonPath = Path.Combine(_testDir, "recent_workspaces.json");
        _service = new RecentWorkspaceService(_jsonPath);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task RecordWorkspaceOpenedAsync_StoresInMruOrder()
    {
        var folder1 = Path.Combine(_testDir, "ProjectOne");
        var folder2 = Path.Combine(_testDir, "ProjectTwo");
        Directory.CreateDirectory(folder1);
        Directory.CreateDirectory(folder2);

        await _service.RecordWorkspaceOpenedAsync(folder1);
        await Task.Delay(10);
        await _service.RecordWorkspaceOpenedAsync(folder2);

        var list = await _service.LoadRecentWorkspacesAsync();
        Assert.Equal(2, list.Count);
        Assert.Equal("ProjectTwo", list[0].Name);
        Assert.Equal("ProjectOne", list[1].Name);
    }

    [Fact]
    public async Task RecordWorkspaceOpenedAsync_DetectsStandaloneNotebookKind()
    {
        var nbPath = Path.Combine(_testDir, "InteractiveData.ipynb");
        await File.WriteAllTextAsync(nbPath, "{}");

        var item = await _service.RecordWorkspaceOpenedAsync(nbPath);
        Assert.Equal(RecentWorkspaceKind.StandaloneNotebook, item.Kind);
        Assert.True(item.IsNotebook);
        Assert.Equal("NOTEBOOK", item.KindBadgeText);
        Assert.Equal("NB", item.Monogram);
    }

    [Fact]
    public async Task RecordWorkspaceOpenedAsync_DetectsGitBranch()
    {
        var gitRepo = Path.Combine(_testDir, "MyGitRepo");
        var gitDir = Path.Combine(gitRepo, ".git");
        Directory.CreateDirectory(gitDir);
        await File.WriteAllTextAsync(Path.Combine(gitDir, "HEAD"), "ref: refs/heads/develop\n");

        var item = await _service.RecordWorkspaceOpenedAsync(gitRepo);
        Assert.True(item.HasGitBranch);
        Assert.Equal("develop", item.GitBranch);
    }

    [Fact]
    public async Task TogglePinAsync_MovesPinnedItemToTop()
    {
        var folder1 = Path.Combine(_testDir, "Alpha");
        var folder2 = Path.Combine(_testDir, "Beta");
        Directory.CreateDirectory(folder1);
        Directory.CreateDirectory(folder2);

        var item1 = await _service.RecordWorkspaceOpenedAsync(folder1);
        await Task.Delay(10);
        await _service.RecordWorkspaceOpenedAsync(folder2);

        // Before pin: Beta is first (most recent)
        var list1 = await _service.LoadRecentWorkspacesAsync();
        Assert.Equal("Beta", list1[0].Name);

        // Pin Alpha
        await _service.TogglePinAsync(item1.Id);

        // After pin: Alpha is pinned so it comes first
        var list2 = await _service.LoadRecentWorkspacesAsync();
        Assert.Equal("Alpha", list2[0].Name);
        Assert.True(list2[0].IsPinned);
    }

    [Fact]
    public async Task RemoveRecentAsync_DeletesFromRecentList()
    {
        var folder = Path.Combine(_testDir, "ToRemove");
        Directory.CreateDirectory(folder);

        var item = await _service.RecordWorkspaceOpenedAsync(folder);
        var before = await _service.LoadRecentWorkspacesAsync();
        Assert.Single(before);

        await _service.RemoveRecentAsync(item.Id);
        var after = await _service.LoadRecentWorkspacesAsync();
        Assert.Empty(after);
    }

    [Fact]
    public async Task EphemeralDocuments_DoNotPersistToDisk()
    {
        var storage = new LocalScriptStorageService(_testDir);
        var script = new ScriptDocumentItem
        {
            Id = "ephemeral_test_script",
            Title = "Ephemeral Starter Problem",
            Code = "// not saved",
            IsEphemeral = true
        };

        var saved = await storage.SaveScriptAsync(script);
        Assert.False(saved);

        var writtenPath = Path.Combine(_testDir, "Ephemeral Starter Problem.cs");
        Assert.False(File.Exists(writtenPath));

        var nb = new NotebookDocumentItem
        {
            Id = "ephemeral_test_nb",
            Title = "Ephemeral Notebook",
            IsEphemeral = true
        };

        var nbSaved = await storage.SaveNotebookAsync(nb);
        Assert.False(nbSaved);
    }

    [Fact]
    public async Task CSharpManagerViewModel_FiltersAndSearchesWorkspaces()
    {
        var storage = new LocalScriptStorageService(_testDir);

        var ws1 = Path.Combine(_testDir, "AlgorithmsWorkspace");
        var ws2 = Path.Combine(_testDir, "WebProject");
        var nb = Path.Combine(_testDir, "DataAnalysis.ipynb");

        Directory.CreateDirectory(ws1);
        Directory.CreateDirectory(ws2);
        await File.WriteAllTextAsync(nb, "{}");

        await storage.RecentWorkspaces.RecordWorkspaceOpenedAsync(ws1);
        await storage.RecentWorkspaces.RecordWorkspaceOpenedAsync(ws2);
        await storage.RecentWorkspaces.RecordWorkspaceOpenedAsync(nb);

        var vm = new CSharpManagerViewModel(
            storage,
            openScriptAction: _ => { },
            openNotebookAction: _ => { });

        await vm.LoadWorkspaceItemsAsync();

        Assert.Equal(3, vm.TotalRecentWorkspaces);
        Assert.Equal(2, vm.RecentWorkspacesCount);
        Assert.Equal(1, vm.RecentNotebooksCount);

        // Filter by Notebooks
        vm.SelectedTypeFilter = "Notebooks";
        Assert.Single(vm.FilteredWorkspaces);
        Assert.Equal("DataAnalysis.ipynb", vm.FilteredWorkspaces[0].Name);

        // Search query
        vm.SelectedTypeFilter = "All";
        vm.SearchQuery = "Algorithms";
        Assert.Single(vm.FilteredWorkspaces);
        Assert.Equal("AlgorithmsWorkspace", vm.FilteredWorkspaces[0].Name);

        // Reset
        vm.ResetRecentSearchAndFilters();
        Assert.Equal(3, vm.FilteredWorkspaces.Count);
    }
}
