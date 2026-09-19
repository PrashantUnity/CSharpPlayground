using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class CSharpManagerUxEnhancementsTests : IDisposable
{
    private readonly string _baseDir;
    private readonly LocalScriptStorageService _storage;

    public CSharpManagerUxEnhancementsTests()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), "FryPDF_ManagerUxTests_" + Guid.NewGuid().ToString("N"));
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
    public void DashboardViewSwitcher_TogglesBetweenWorkspacesAndTemplates()
    {
        var (vm, _, _, _, _) = CreateHub();

        // Default state is Workspaces
        Assert.Equal("Workspaces", vm.ActiveDashboardView);
        Assert.True(vm.IsWorkspacesTabActive);
        Assert.False(vm.IsTemplatesTabActive);

        // Switch to Templates
        ((ICommand)vm.SetActiveDashboardViewCommand).Execute("Templates");
        Assert.Equal("Templates", vm.ActiveDashboardView);
        Assert.False(vm.IsWorkspacesTabActive);
        Assert.True(vm.IsTemplatesTabActive);

        // Switch back to Workspaces
        ((ICommand)vm.SetActiveDashboardViewCommand).Execute("Workspaces");
        Assert.Equal("Workspaces", vm.ActiveDashboardView);
        Assert.True(vm.IsWorkspacesTabActive);
        Assert.False(vm.IsTemplatesTabActive);
    }

    [Fact]
    public async Task PinnedFilter_FiltersItemsToOnlyPinnedWorkspaces()
    {
        var s1 = await _storage.CreateNewScriptAsync("Script Alpha");
        var s2 = await _storage.CreateNewScriptAsync("Script Beta");
        var n1 = await _storage.CreateNewNotebookAsync("Notebook Gamma");
        var n2 = await _storage.CreateNewNotebookAsync("Notebook Delta");

        var (vm, _, _, _, _) = CreateHub();
        await vm.LoadWorkspaceItemsAsync();

        // Clear any auto-pinned defaults to test cleanly
        foreach (var pinned in vm.PinnedItems.ToList())
        {
            await vm.UnpinItemAsync(pinned);
        }
        Assert.Equal(0, vm.PinnedCount);

        // Pin Alpha and Gamma
        var alpha = vm.AllItems.First(i => i.Id == s1.Id);
        var gamma = vm.AllItems.First(i => i.Id == n1.Id);
        await vm.PinItemAsync(alpha);
        await vm.PinItemAsync(gamma);

        Assert.Equal(2, vm.PinnedCount);

        // Set filter to Pinned
        ((ICommand)vm.SetSelectedTypeFilterCommand).Execute("Pinned");
        Assert.True(vm.IsPinnedFilterActive);

        var filteredSummaries = vm.FilteredItems.OfType<WorkspaceItemSummary>().ToList();
        Assert.Equal(2, filteredSummaries.Count);
        Assert.All(filteredSummaries, item => Assert.True(item.IsPinned));
        Assert.Contains(filteredSummaries, i => i.Id == s1.Id);
        Assert.Contains(filteredSummaries, i => i.Id == n1.Id);

        // Switch back to All
        ((ICommand)vm.SetSelectedTypeFilterCommand).Execute("All");
        Assert.True(vm.IsAllFilterActive);
        var allSummaries = vm.FilteredItems.OfType<WorkspaceItemSummary>().ToList();
        Assert.Equal(vm.AllItems.Count, allSummaries.Count);
    }

    [Fact]
    public async Task SearchQuery_MatchesDisplayLocation()
    {
        var s1 = await _storage.CreateNewScriptAsync("Custom Script");
        var (vm, _, _, _, _) = CreateHub();
        await vm.LoadWorkspaceItemsAsync();

        var item = vm.AllItems.First(i => i.Id == s1.Id);
        item.FolderPath = "FinanceDepartmentConfidentialQ3";

        // Filter by unique folder text present only in DisplayLocation
        vm.SearchQuery = "FinanceDepartmentConfidentialQ3";
        var filteredSummaries = vm.FilteredItems.OfType<WorkspaceItemSummary>().ToList();
        Assert.Single(filteredSummaries);
        Assert.Equal(item.Id, filteredSummaries[0].Id);

        // Clear filter
        vm.SearchQuery = string.Empty;
        filteredSummaries = vm.FilteredItems.OfType<WorkspaceItemSummary>().ToList();
        Assert.Equal(vm.AllItems.Count, filteredSummaries.Count);

        // Non-matching query
        vm.SearchQuery = "NonExistentFolderXYZ";
        filteredSummaries = vm.FilteredItems.OfType<WorkspaceItemSummary>().ToList();
        Assert.Empty(filteredSummaries);
    }

    [Fact]
    public async Task ThreePinnedSlots_BindsPinnedItem3AndDispatchesOpen()
    {
        var s1 = await _storage.CreateNewScriptAsync("Script 1");
        var s2 = await _storage.CreateNewScriptAsync("Script 2");
        var s3 = await _storage.CreateNewScriptAsync("Script 3");

        var scriptOpens = 0;
        ScriptDocumentItem? lastScript = null;

        var vm = new CSharpManagerViewModel(
            _storage,
            openScriptAction: s => { scriptOpens++; lastScript = s; },
            openNotebookAction: _ => { },
            navigateToHomeAction: () => { });

        await vm.LoadWorkspaceItemsAsync();

        // Clear auto-pinned defaults
        foreach (var pinned in vm.PinnedItems.ToList())
        {
            await vm.UnpinItemAsync(pinned);
        }

        var i1 = vm.AllItems.First(i => i.Id == s1.Id);
        var i2 = vm.AllItems.First(i => i.Id == s2.Id);
        var i3 = vm.AllItems.First(i => i.Id == s3.Id);

        await vm.PinItemAsync(i1);
        await vm.PinItemAsync(i2);
        await vm.PinItemAsync(i3);

        Assert.True(vm.HasPinnedItem1);
        Assert.True(vm.HasPinnedItem2);
        Assert.True(vm.HasPinnedItem3);
        Assert.NotNull(vm.PinnedItem1);
        Assert.NotNull(vm.PinnedItem2);
        Assert.NotNull(vm.PinnedItem3);

        // Verify the 3rd pinned slot item matches
        var thirdPinnedItem = vm.PinnedItems[2];
        Assert.Equal(thirdPinnedItem.Id, vm.PinnedItem3!.Id);

        // Test executing OpenPinnedItem3Command directly
        ((ICommand)vm.OpenPinnedItem3Command).Execute(null);
        await Task.Delay(100);

        Assert.Equal(1, scriptOpens);
        Assert.NotNull(lastScript);
        Assert.Equal(thirdPinnedItem.Id, lastScript!.Id);

        // Unpin slot 3
        await vm.UnpinItemAsync(thirdPinnedItem);
        Assert.False(vm.HasPinnedItem3);
        Assert.Null(vm.PinnedItem3);
        Assert.Equal(2, vm.PinnedCount);
    }

    [Fact]
    public async Task DirectRowOpenCommand_LaunchesItemImmediately()
    {
        var sc = await _storage.CreateNewScriptAsync("Direct Launch Script");
        var nb = await _storage.CreateNewNotebookAsync("Direct Launch Notebook");

        var scriptOpens = 0;
        var notebookOpens = 0;
        ScriptDocumentItem? openedScript = null;
        NotebookDocumentItem? openedNotebook = null;

        var vm = new CSharpManagerViewModel(
            _storage,
            openScriptAction: s => { scriptOpens++; openedScript = s; },
            openNotebookAction: n => { notebookOpens++; openedNotebook = n; },
            navigateToHomeAction: () => { });

        await vm.LoadWorkspaceItemsAsync();

        var scriptItem = vm.AllItems.First(i => i.Id == sc.Id);
        var notebookItem = vm.AllItems.First(i => i.Id == nb.Id);

        // Execute OpenItemCommand directly on the script row
        ((ICommand)vm.OpenItemCommand).Execute(scriptItem);
        await Task.Delay(100);

        Assert.Equal(1, scriptOpens);
        Assert.Equal(0, notebookOpens);
        Assert.NotNull(openedScript);
        Assert.Equal(sc.Id, openedScript!.Id);

        // Execute OpenItemCommand directly on the notebook row
        ((ICommand)vm.OpenItemCommand).Execute(notebookItem);
        await Task.Delay(100);

        Assert.Equal(1, scriptOpens);
        Assert.Equal(1, notebookOpens);
        Assert.NotNull(openedNotebook);
        Assert.Equal(nb.Id, openedNotebook!.Id);
    }
}
