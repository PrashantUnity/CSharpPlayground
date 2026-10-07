using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Execution;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;
using Xunit;
using CSharpCodeStudioViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio.CSharpCodeStudioViewModel;
using CSharpNotebookStudioViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Notebooks.CSharpNotebookStudioViewModel;
using ExplorerItemViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio.Explorer.ExplorerItemViewModel;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// A workspace with more files than the listing limit is not cut off: the Explorer lists its top folder and each folder when it
/// is opened. (The limit is 10 files here, so a few dozen files stand in for a huge project.)
/// </summary>
public class LazyExplorerTests : IDisposable
{
    private const int Limit = 10;

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_LazyExplorerTests_" + Guid.NewGuid().ToString("N"));
    private readonly LocalScriptStorageService _storage;

    public LazyExplorerTests()
    {
        _storage = new LocalScriptStorageService(_dir, workspaceFileLimit: Limit);
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

    private string Root => _storage.ActiveWorkspaceRootPath;

    private void Files(string relativeFolder, int count, string prefix = "file")
    {
        var folder = relativeFolder.Length == 0 ? Root : Path.Combine(Root, relativeFolder.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(folder);
        for (int i = 0; i < count; i++) File.WriteAllText(Path.Combine(folder, $"{prefix}{i:D2}.py"), "print(1)\n");
    }

    // 2 files at the top and 5 folders of 5 files: 27 files, so more than the limit of 10.
    private void BigWorkspace()
    {
        Files(string.Empty, 2, "top");
        for (int f = 1; f <= 5; f++) Files($"folder{f}", 5, $"f{f}_");
    }

    private CSharpCodeStudioViewModel CreateStudio(ScriptDocumentItem? script = null) => new(
        script ?? new ScriptDocumentItem { Title = "Open" },
        _storage,
        new RoslynCompilerService(),
        new ScriptExecutionEngine(),
        backToHubAction: () => { },
        backToHomeAction: () => { });

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            try
            {
                if (condition()) break;
            }
            catch (InvalidOperationException)
            {
                // Transient concurrent modification while background explorer tasks update rows
            }
            Assert.True(DateTime.UtcNow < deadline, "the condition never became true");
            await Task.Delay(20);
        }
    }

    [Fact]
    public async Task Storage_ListsTheTopFolderOnly_WhenTheWorkspaceIsBiggerThanTheLimit()
    {
        BigWorkspace();

        var listing = await _storage.LoadExplorerListingAsync();

        Assert.True(listing.IsPartial);
        Assert.Equal(new[] { "folder1", "folder2", "folder3", "folder4", "folder5" }, listing.FolderPaths.OrderBy(p => p));
        Assert.Equal(new[] { "top00", "top01" }, listing.Items.Select(i => i.Title).OrderBy(t => t));
        Assert.False(listing.IsTruncated);
    }

    [Fact]
    public async Task Storage_ListsTheWholeWorkspace_WhenItFitsTheLimit()
    {
        Files(string.Empty, 2, "top");
        Files("only", 3);

        var listing = await _storage.LoadExplorerListingAsync();

        Assert.False(listing.IsPartial);
        Assert.Equal(new[] { "only" }, listing.FolderPaths);
        Assert.Equal(5, listing.Items.Count);
    }

    [Fact]
    public async Task ListFolder_ReturnsOneFoldersDirectContents_AsVsCodeShowsThem()
    {
        Files("src", 2);
        Files("src/models", 3);
        Files("src/node_modules", 4);
        Files("src/.git", 1);

        var listing = await _storage.ListFolderAsync("src");

        // node_modules is shown (its contents listed when it is opened); .git is hidden, as in VS Code.
        Assert.Equal(new[] { "src/models", "src/node_modules" }, listing.FolderPaths.OrderBy(p => p, StringComparer.Ordinal));
        Assert.Equal(2, listing.Items.Count);
        Assert.True(listing.IsPartial);
        Assert.Equal(4, (await _storage.ListFolderAsync("src/node_modules")).Items.Count);
    }

    [Fact]
    public async Task ListFolder_OfAFolderWithMoreFilesThanTheLimit_ListsTheFirstOnesAndSaysSo()
    {
        Files("data", Limit + 15);

        var listing = await _storage.ListFolderAsync("data");

        Assert.Equal(Limit, listing.Items.Count);
        Assert.True(listing.IsTruncated);
    }

    [Fact]
    public async Task TheExplorer_ShowsTheTopFolder_WithUnlistedFoldersThatCanBeOpened()
    {
        BigWorkspace();
        var studio = CreateStudio();

        await studio.RefreshExplorerAsync();

        var folders = studio.ExplorerRootItems.Where(i => i.IsDirectory).ToList();
        Assert.Equal(5, folders.Count);
        // The two files of the folder, and the open document that is not part of it (listed at the top).
        // The folder's own files only: the open document ("Open", never saved here) has no row.
        Assert.Equal(new[] { "top00.py", "top01.py" }, studio.ExplorerRootItems.Where(i => !i.IsDirectory).Select<ExplorerItemViewModel, string>(i => i.Name).OrderBy(n => n));
        Assert.All(folders, f =>
        {
            Assert.False(f.ChildrenLoaded);
            Assert.Single(f.Children);
            Assert.True(f.Children[0].IsPlaceholder);
        });
        Assert.Equal(7, studio.ExplorerRows.Rows.Count); // 5 folders, 2 files
        Assert.False((bool)studio.IsExplorerTruncated);
    }

    [Fact]
    public async Task OpeningAFolder_ListsItsContents_AndOnlyThen()
    {
        BigWorkspace();
        var studio = CreateStudio();
        await studio.RefreshExplorerAsync();
        var folder = studio.ExplorerRootItems.First(i => i.Name == "folder3");
        var scansBefore = _storage.WorkspaceScanCount;

        folder.IsExpanded = true;
        await WaitUntil(() => folder.ChildrenLoaded);

        Assert.Equal(5, folder.Children.Count);
        Assert.All(folder.Children, c => Assert.False(c.IsPlaceholder));
        Assert.Equal(new[] { "f3_00.py", "f3_01.py", "f3_02.py", "f3_03.py", "f3_04.py" }, folder.Children.Select<ExplorerItemViewModel, string>(c => c.Name));
        Assert.Equal(5 + 2 + 5, studio.ExplorerRows.Rows.Count); // folders, the top files, the opened folder's files
        Assert.All(folder.Children, c => Assert.Equal("folder3/" + c.Name, (string?)c.FullPath));
        // The other folders were not listed.
        Assert.All(studio.ExplorerRootItems.Where(i => i.IsDirectory && i != folder), f => Assert.False(f.ChildrenLoaded));
        Assert.Equal(scansBefore, _storage.WorkspaceScanCount);
    }

    [Fact]
    public async Task CollapsingAndReopeningAFolder_DoesNotListItAgain()
    {
        BigWorkspace();
        var studio = CreateStudio();
        await studio.RefreshExplorerAsync();
        var folder = studio.ExplorerRootItems.First(i => i.Name == "folder1");
        folder.IsExpanded = true;
        await WaitUntil(() => folder.ChildrenLoaded);
        var children = folder.Children.ToList();

        folder.IsExpanded = false;
        folder.IsExpanded = true;
        await Task.Delay(100);

        Assert.Equal(children, folder.Children.ToList());
        Assert.Equal(5 + 2 + 5, studio.ExplorerRows.Rows.Count); // folders, the top files, the opened folder's files
    }

    [Fact]
    public async Task RefreshingTheExplorer_OpensTheFoldersThatWereOpen_AgainDownToTheDeepestOne()
    {
        BigWorkspace();
        Files("folder2/inner/deeper", 3, "deep");
        var studio = CreateStudio();
        await studio.RefreshExplorerAsync();
        var folder2 = studio.ExplorerRootItems.First(i => i.Name == "folder2");
        folder2.IsExpanded = true;
        await WaitUntil(() => folder2.ChildrenLoaded);
        var inner = folder2.Children.First(c => c.Name == "inner");
        inner.IsExpanded = true;
        await WaitUntil(() => inner.ChildrenLoaded);
        var deeper = inner.Children.First(c => c.Name == "deeper");
        deeper.IsExpanded = true;
        await WaitUntil(() => deeper.ChildrenLoaded);

        await studio.RefreshExplorerAsync();

        var newFolder2 = studio.ExplorerRootItems.First(i => i.Name == "folder2");
        Assert.NotSame(folder2, newFolder2);
        await WaitUntil(() => newFolder2.IsExpanded && newFolder2.ChildrenLoaded
            && newFolder2.Children.FirstOrDefault(c => c.Name == "inner") is { IsExpanded: true, ChildrenLoaded: true } newInner
            && newInner.Children.FirstOrDefault(c => c.Name == "deeper") is { IsExpanded: true, ChildrenLoaded: true });
        Assert.Contains(studio.ExplorerRows.Rows, r => r.Name == "deep00.py");
        Assert.False((bool)studio.ExplorerRootItems.First(i => i.Name == "folder4").IsExpanded);
    }

    [Fact]
    public async Task TheOpenDocument_IsRevealed_ByOpeningTheFoldersDownToIt()
    {
        BigWorkspace();
        var nested = await _storage.CreateFolderAsync("folder4", "inner");
        var doc = await _storage.CreateNewScriptAsync("Buried", folderPath: nested);
        var studio = CreateStudio();
        await studio.RefreshExplorerAsync();
        Assert.Null(studio.ExplorerRootItems.SelectMany(Flatten).FirstOrDefault(i => i.DocumentId == doc.Id));

        await studio.UpdateActiveScriptAsync(doc);

        await WaitUntil(() => studio.ExplorerRootItems.SelectMany(Flatten).FirstOrDefault(i => i.DocumentId == doc.Id) is { IsSelected: true });
        var item = studio.ExplorerRootItems.SelectMany(Flatten).First(i => i.DocumentId == doc.Id);
        Assert.Equal((string?)"folder4/inner/Buried.frycs", (string?)item.FullPath);
        Assert.True((bool)item.Parent!.IsExpanded);
        Assert.True((bool)item.Parent.Parent!.IsExpanded);
        Assert.Contains(item, studio.ExplorerRows.Rows);
        // An open document inside the workspace is not listed as an outsider at the top.
        Assert.DoesNotContain(studio.ExplorerRootItems, i => i.DocumentId == doc.Id);
    }

    [Fact]
    public async Task ANewFolderUnderAnUnlistedFolder_AppearsOnce_AndIsBeingRenamed()
    {
        BigWorkspace();
        var studio = CreateStudio();
        await studio.RefreshExplorerAsync();
        var folder = studio.ExplorerRootItems.First(i => i.Name == "folder5");

        await studio.NewFolderUnderItemAsync(folder);

        Assert.True(folder.ChildrenLoaded);
        Assert.True((bool)folder.IsExpanded);
        var created = folder.Children.Where(c => c.IsDirectory).ToList();
        Assert.Single(created);
        Assert.True((bool)created[0].IsRenaming);
        Assert.Equal(5, folder.Children.Count(c => !c.IsDirectory));
    }

    [Fact]
    public async Task ANewScriptUnderAnUnlistedFolder_IsListedAndBeingRenamed()
    {
        BigWorkspace();
        var studio = CreateStudio();
        await studio.RefreshExplorerAsync();
        var folder = studio.ExplorerRootItems.First(i => i.Name == "folder2");

        await studio.NewScriptUnderItemAsync(folder);

        // Creating the script moved the workspace on, so the Explorer rebuilt its tree: look at the folder as it is now.
        folder = studio.ExplorerRootItems.First(i => i.Name == "folder2");
        await WaitUntil(() => folder.ChildrenLoaded);
        var created = folder.Children.Single(c => c.FullPath.EndsWith(".frycs", StringComparison.OrdinalIgnoreCase));
        Assert.True((bool)created.IsRenaming);
        Assert.Equal((string?)studio.Script.Id, (string?)created.DocumentId);
    }

    [Fact]
    public async Task ABigFolderThatIsOpened_SaysThatItHasMoreFilesThanAreListed()
    {
        Files("data", Limit + 15);
        Files("other", Limit + 1);
        var studio = CreateStudio();
        await studio.RefreshExplorerAsync();
        var data = studio.ExplorerRootItems.First(i => i.Name == "data");

        data.IsExpanded = true;
        await WaitUntil(() => data.ChildrenLoaded);

        Assert.Equal(Limit, data.Children.Count(c => !c.IsPlaceholder));
        var note = Assert.Single(data.Children, c => c.IsPlaceholder);
        Assert.Contains((string)"More files", (string?)note.Name);
    }

    [Fact]
    public async Task ASmallWorkspace_IsStillListedWhole()
    {
        Files(string.Empty, 2, "top");
        Files("only", 3);
        Files("only/nested", 2);
        var studio = CreateStudio();

        await studio.RefreshExplorerAsync();

        var only = studio.ExplorerRootItems.Single(i => i.IsDirectory);
        Assert.True(only.ChildrenLoaded);
        Assert.Equal(3 + 1, only.Children.Count);
        Assert.All(studio.ExplorerRootItems.SelectMany(Flatten), i => Assert.False(i.IsPlaceholder));
    }

    private CSharpNotebookStudioViewModel CreateNotebookStudio(NotebookDocumentItem? notebook = null) => new(
        notebook ?? new NotebookDocumentItem { Title = "Open notebook" },
        _storage,
        new RoslynCompilerService(),
        new ScriptExecutionEngine(),
        backToHubAction: () => { },
        backToHomeAction: () => { });

    [Fact]
    public async Task TheNotebookStudiosExplorer_AlsoListsOneFolderAtATime()
    {
        BigWorkspace();
        var studio = CreateNotebookStudio();

        await studio.RefreshExplorer();

        var folder = studio.ExplorerRootItems.First(i => i.Name == "folder3");
        Assert.False(folder.ChildrenLoaded);
        Assert.Equal(5, studio.ExplorerRootItems.Count(i => i.IsDirectory));

        folder.IsExpanded = true;
        await WaitUntil(() => folder.ChildrenLoaded);

        Assert.Equal(5, folder.Children.Count);
        Assert.False((bool)studio.IsExplorerTruncated);
    }

    [Fact]
    public async Task ANotebookOfTheWorkspace_IsRevealedInTheNotebookStudio_AndGetsNoRowAtTheTop()
    {
        BigWorkspace();
        var nested = await _storage.CreateFolderAsync("folder2", "notebooks");
        var notebook = await _storage.CreateNewNotebookAsync("Analysis", folderPath: nested);
        var studio = CreateNotebookStudio();
        await studio.RefreshExplorer();

        studio.UpdateActiveNotebook(notebook);

        await WaitUntil(() => studio.ExplorerRootItems.SelectMany(Flatten).FirstOrDefault(i => i.DocumentId == notebook.Id) is { IsSelected: true });
        var item = studio.ExplorerRootItems.SelectMany(Flatten).First(i => i.DocumentId == notebook.Id);
        Assert.Equal((string?)"folder2/notebooks/Analysis.frynb", (string?)item.FullPath);
        Assert.True((bool)item.Parent!.IsExpanded);
        Assert.DoesNotContain(studio.ExplorerRootItems, i => i.DocumentId == notebook.Id);
    }

    [Fact]
    public async Task ANewNotebookUnderAnUnlistedFolder_AppearsOnce_AndIsBeingRenamed()
    {
        BigWorkspace();
        var studio = CreateNotebookStudio();
        await studio.RefreshExplorer();
        var folder = studio.ExplorerRootItems.First(i => i.Name == "folder4");

        await studio.NewFileUnderItemAsync(folder);

        folder = studio.ExplorerRootItems.First(i => i.Name == "folder4");
        await WaitUntil(() => folder.ChildrenLoaded);
        var created = folder.Children.Where(c => c.FullPath.EndsWith(".frynb", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Single(created);
        Assert.True((bool)created[0].IsRenaming);
    }

    private static IEnumerable<ExplorerItemViewModel> Flatten(ExplorerItemViewModel item) =>
        new[] { item }.Concat(item.Children.SelectMany(Flatten));
}
