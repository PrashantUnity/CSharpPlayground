using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;
using Xunit;
using CSharpManagerViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Hub.CSharpManagerViewModel;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// The Hub draws a card per workspace item, and every card is a heavy control: it draws the first page of them and offers
/// the rest, while searching still looks through all of them.
/// </summary>
public class HubListCapTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_HubCapTests_" + Guid.NewGuid().ToString("N"));
    private readonly LocalScriptStorageService _storage;

    public HubListCapTests()
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

    private async Task<CSharpManagerViewModel> HubWithScriptsAsync(int count)
    {
        for (var i = 0; i < count; i++) await _storage.CreateNewScriptAsync($"Item {i:D3}");
        var hub = new CSharpManagerViewModel(_storage, openScriptAction: _ => { }, openNotebookAction: _ => { });
        await hub.LoadWorkspaceItemsAsync();
        return hub;
    }

    [Fact]
    public async Task ASmallLibrary_IsDrawnInFull_WithNothingHidden()
    {
        var hub = await HubWithScriptsAsync(30);

        Assert.Equal(30, hub.FilteredItems.OfType<WorkspaceItemSummary>().Count());
        Assert.Equal(0, hub.HiddenItemCount);
        Assert.False(hub.HasHiddenItems);
    }

    [Fact]
    public async Task ABigLibrary_DrawsTheFirstPage_AndShowMoreRevealsTheRestPageByPage()
    {
        var hub = await HubWithScriptsAsync(250);

        Assert.Equal(100, hub.FilteredItems.Count);
        Assert.Equal(150, hub.HiddenItemCount);
        Assert.True(hub.HasHiddenItems);
        Assert.Equal(250, hub.FilteredItemCount); // The count is of everything that matches, drawn or not.

        hub.ShowMoreItemsCommand.Execute(null);
        Assert.Equal(200, hub.FilteredItems.Count);
        Assert.Equal(50, hub.HiddenItemCount);

        hub.ShowMoreItemsCommand.Execute(null);
        Assert.Equal(250, hub.FilteredItems.Count);
        Assert.False(hub.HasHiddenItems);
    }

    [Fact]
    public async Task Searching_LooksThroughTheItemsThatAreNotDrawn_AndStartsAgainFromTheFirstPage()
    {
        var hub = await HubWithScriptsAsync(250);
        hub.ShowMoreItemsCommand.Execute(null);

        hub.SearchQuery = "Item 249";

        var match = Assert.Single(hub.FilteredItems.OfType<WorkspaceItemSummary>());
        Assert.Equal("Item 249", match.Title);
        Assert.Equal(0, hub.HiddenItemCount);

        hub.SearchQuery = string.Empty;
        Assert.Equal(100, hub.FilteredItems.Count); // Back to one page, not the two shown before searching.
    }

    [Fact]
    public async Task FilteringByType_StartsAgainFromTheFirstPage()
    {
        var hub = await HubWithScriptsAsync(250);
        hub.ShowMoreItemsCommand.Execute(null);

        hub.SelectedTypeFilter = "Scripts";

        Assert.Equal(100, hub.FilteredItems.Count);
        Assert.Equal(150, hub.HiddenItemCount);
    }
}
