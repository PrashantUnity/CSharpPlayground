using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>What the UI review found in the view models behind the Hub, Docs, notebook variables and .Dump() tables.</summary>
public class UiPolishTests : IDisposable
{
    private readonly string _baseDir = Path.Combine(Path.GetTempPath(), "FryPDF_UiPolishTests_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_baseDir)) Directory.Delete(_baseDir, recursive: true);
        }
        catch { }
    }

    private CSharpManagerViewModel Hub(LocalScriptStorageService storage) =>
        new(storage, openScriptAction: _ => { }, openNotebookAction: _ => { });

    // ── Docs ──────────────────────────────────────────────────────────────────

    [Fact]
    public void OpeningAnArticleInAnotherCategory_ShowsThatCategoryInTheBreadcrumb()
    {
        var docs = new CSharpDocsViewModel();
        var other = docs.Categories.Skip(1).First(c => c.Articles.Count > 0);
        var article = other.Articles[0];

        docs.SelectArticle(article);

        Assert.Same(other, docs.SelectedCategory);
        Assert.Equal($"{other.Title} > {article.Title}", docs.ActiveBreadcrumb);
    }

    // ── Hub ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AFirstRun_HighlightsHome_AndSaysTheWorkspaceIsEmpty()
    {
        var hub = Hub(new LocalScriptStorageService(_baseDir));
        await hub.LoadWorkspaceItemsAsync();

        Assert.True(hub.IsWorkspacesTabActive);
        Assert.True(hub.IsWorkspaceSectionActive);
        Assert.False(hub.IsTemplatesSectionActive);
        Assert.True(hub.IsWorkspaceEmpty);
        Assert.Equal(0, hub.StorageUsagePercent);
    }

    [Fact]
    public async Task AWorkspaceWithDocuments_IsNotEmpty_AndShowsSomeStorage()
    {
        var storage = new LocalScriptStorageService(_baseDir);
        await storage.CreateNewScriptAsync("Sorting playground");
        var hub = Hub(storage);

        await hub.LoadWorkspaceItemsAsync();

        Assert.False(hub.IsWorkspaceEmpty);
        Assert.True(hub.IsWorkspaceSectionActive);
        Assert.True(hub.StorageUsagePercent > 0);
    }

    // ── .Dump() tables ────────────────────────────────────────────────────────

    [Fact]
    public void ACollectionInACell_ShowsItsItems()
    {
        Assert.Equal("[COBOL, FLOW-MATIC]", DumpTableBuilder.CreateCell(new[] { "COBOL", "FLOW-MATIC" }).DisplayText);
        Assert.Equal("[1, 2, 4]", DumpTableBuilder.CreateCell(new List<int> { 1, 2, 4 }).DisplayText);
        Assert.Equal("[[1, 2], [3]]", DumpTableBuilder.CreateCell(new[] { new[] { 1, 2 }, new[] { 3 } }).DisplayText);
        Assert.Equal("text", DumpTableBuilder.CreateCell("text").DisplayText);
    }

    [Fact]
    public void ALongCollectionInACell_ShowsTheFirstFewAndHowManyMore()
    {
        Assert.Equal("[1, 2, 3, 4, 5, 6, … (+4)]", DumpTableBuilder.CreateCell(Enumerable.Range(1, 10).ToArray()).DisplayText);
    }

    [Fact]
    public void AnEndlessSequenceInACell_DoesNotHang()
    {
        static IEnumerable<int> Forever()
        {
            for (int i = 0; ; i++) yield return i;
        }

        Assert.Equal("[0, 1, 2, 3, 4, 5, …]", DumpTableBuilder.CreateCell(Forever()).DisplayText);
    }

    // ── Notebook variables ────────────────────────────────────────────────────

    [Fact]
    public async Task Variables_ShowTypesAsCSharpWritesThem_AndPlainObjectsByTheirType()
    {
        var kernel = new NotebookExecutionKernel();
        var result = await kernel.ExecuteCellAsync("""
            class Solution { }
            var seen = new Dictionary<int, int> { [1] = 2 };
            var sol = new Solution();
            var label = "hi";
            """);
        Assert.True(result.Success, result.ErrorMessage);

        var variables = kernel.GetActiveVariables().ToDictionary(v => v.Name);

        Assert.Equal("Dictionary<Int32, Int32>", variables["seen"].TypeName);
        Assert.Equal("Solution", variables["sol"].TypeName);
        Assert.Equal("{Solution}", variables["sol"].ValueDisplay);
        Assert.Equal("\"hi\"", variables["label"].ValueDisplay);
    }
}
