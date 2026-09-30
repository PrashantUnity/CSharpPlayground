using System.Collections.ObjectModel;
using System.Collections.Specialized;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class ExplorerRowListTests
{
    private static ExplorerItemViewModel Folder(string name, ExplorerItemViewModel? parent = null) =>
        new() { Name = name, IsDirectory = true, Parent = parent, Depth = (parent?.Depth ?? -1) + 1 };

    private static ExplorerItemViewModel File(string name, ExplorerItemViewModel? parent = null) =>
        new() { Name = name, Parent = parent, Depth = (parent?.Depth ?? -1) + 1 };

    private static string Names(ExplorerRowList list) => string.Join(",", list.Rows.Select(r => r.Name));

    // A (folder): a1, N (folder): deep      z
    private static (ObservableCollection<ExplorerItemViewModel> Roots, ExplorerItemViewModel A, ExplorerItemViewModel N) Sample()
    {
        var roots = new ObservableCollection<ExplorerItemViewModel>();
        var a = Folder("A");
        a.Children.Add(File("a1", a));
        var n = Folder("N", a);
        n.Children.Add(File("deep", n));
        a.Children.Add(n);
        roots.Add(a);
        roots.Add(File("z"));
        return (roots, a, n);
    }

    [Fact]
    public void CollapsedFolders_ShowOnlyTheirOwnRow()
    {
        var (roots, _, _) = Sample();

        var list = new ExplorerRowList(roots);

        Assert.Equal("A,z", Names(list));
    }

    [Fact]
    public void ExpandingAFolder_InsertsItsRowsRightAfterIt_AndCollapsingRemovesThemAgain()
    {
        var (roots, a, n) = Sample();
        var list = new ExplorerRowList(roots);

        a.IsExpanded = true;
        Assert.Equal("A,a1,N,z", Names(list));

        n.IsExpanded = true;
        Assert.Equal("A,a1,N,deep,z", Names(list));

        a.IsExpanded = false;
        Assert.Equal("A,z", Names(list));
    }

    [Fact]
    public void ExpandingAFolderHiddenByACollapsedAncestor_ChangesNothing_UntilTheAncestorOpens()
    {
        var (roots, a, n) = Sample();
        var list = new ExplorerRowList(roots);

        n.IsExpanded = true;
        Assert.Equal("A,z", Names(list));

        a.IsExpanded = true;
        Assert.Equal("A,a1,N,deep,z", Names(list));
    }

    [Fact]
    public void ExpandingOrCollapsing_RaisesOneRangeNotification_NotOnePerRow()
    {
        var (roots, a, _) = Sample();
        var list = new ExplorerRowList(roots);
        var events = new List<NotifyCollectionChangedEventArgs>();
        list.Rows.CollectionChanged += (_, e) => events.Add(e);

        a.IsExpanded = true;
        a.IsExpanded = false;

        Assert.Equal(2, events.Count);
        Assert.Equal(NotifyCollectionChangedAction.Add, events[0].Action);
        Assert.Equal(2, events[0].NewItems!.Count);
        Assert.Equal(1, events[0].NewStartingIndex);
        Assert.Equal(NotifyCollectionChangedAction.Remove, events[1].Action);
        Assert.Equal(2, events[1].OldItems!.Count);
    }

    [Fact]
    public void AddingAndRemovingItems_FollowsTheTree()
    {
        var (roots, a, _) = Sample();
        var list = new ExplorerRowList(roots);
        a.IsExpanded = true;

        var added = File("a2", a);
        a.Children.Add(added);
        Assert.Equal("A,a1,N,a2,z", Names(list));

        a.Children.Remove(added);
        roots.Add(File("zz"));
        Assert.Equal("A,a1,N,z,zz", Names(list));
    }

    [Fact]
    public void ANewFolderAddedLater_IsWatchedToo()
    {
        var (roots, _, _) = Sample();
        var list = new ExplorerRowList(roots);

        var late = Folder("Late");
        late.Children.Add(File("l1", late));
        roots.Add(late);
        late.IsExpanded = true;

        Assert.Equal("A,z,Late,l1", Names(list));
    }

    [Fact]
    public void Suspend_TurnsManyChangesIntoOneRebuild()
    {
        var (roots, a, _) = Sample();
        var list = new ExplorerRowList(roots);
        var events = new List<NotifyCollectionChangedEventArgs>();
        list.Rows.CollectionChanged += (_, e) => events.Add(e);

        using (list.Suspend())
        {
            for (var i = 0; i < 50; i++) roots.Add(File($"f{i}"));
            a.IsExpanded = true;
            Assert.Empty(events);
        }

        Assert.Single(events);
        Assert.Equal(NotifyCollectionChangedAction.Reset, events[0].Action);
        Assert.Equal(1 + 2 + 1 + 50, list.Rows.Count); // A, a1, N, z, f0..f49 (A open)
    }

    [Fact]
    public void ANestedSuspend_RebuildsOnlyWhenTheOutermostScopeEnds()
    {
        var (roots, _, _) = Sample();
        var list = new ExplorerRowList(roots);
        var resets = 0;
        list.Rows.CollectionChanged += (_, e) => { if (e.Action == NotifyCollectionChangedAction.Reset) resets++; };

        using (list.Suspend())
        {
            using (list.Suspend())
            {
                roots.Add(File("x"));
            }

            Assert.Equal(0, resets);
        }

        Assert.Equal(1, resets);
    }
}

public class ExplorerRowListStudioTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_ExplorerRowsTests_" + Guid.NewGuid().ToString("N"));
    private readonly LocalScriptStorageService _storage;

    public ExplorerRowListStudioTests()
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

    private CSharpCodeStudioViewModel CreateStudio(Models.ScriptDocumentItem script) => new(
        script,
        _storage,
        new RoslynCompilerService(),
        new ScriptExecutionEngine(),
        backToHubAction: () => { },
        backToHomeAction: () => { });

    [Fact]
    public async Task TheStudiosRows_ListFoldersCollapsed_AndOpeningOneRevealsItsFiles()
    {
        var folder = await _storage.CreateFolderAsync(null, "Algorithms");
        await _storage.CreateNewScriptAsync("Inside One", folderPath: folder);
        await _storage.CreateNewScriptAsync("Inside Two", folderPath: folder);
        var loose = await _storage.CreateNewScriptAsync("Loose");

        var studio = CreateStudio(loose);

        Assert.Equal(new[] { "Algorithms", "Loose.frycs" }, studio.ExplorerRows.Rows.Select(r => r.Name));

        studio.ExplorerRootItems.Single(x => x.IsDirectory).IsExpanded = true;

        Assert.Equal(new[] { "Algorithms", "Inside One.frycs", "Inside Two.frycs", "Loose.frycs" }, studio.ExplorerRows.Rows.Select(r => r.Name));
    }

    [Fact]
    public async Task OpeningAScriptInsideACollapsedFolder_ExpandsItSoItsRowIsThere()
    {
        var folder = await _storage.CreateFolderAsync(null, "Deep");
        var inside = await _storage.CreateNewScriptAsync("Buried", folderPath: folder);

        var studio = CreateStudio(inside);

        Assert.Contains(studio.ExplorerRows.Rows, r => r.DocumentId == inside.Id && r.IsSelected);
    }

    [Fact]
    public async Task RefreshingTheExplorer_KeepsTheRowsInStepWithTheTree()
    {
        var first = await _storage.CreateNewScriptAsync("One");
        var studio = CreateStudio(first);

        await _storage.CreateNewScriptAsync("Two");
        await studio.RefreshExplorerAsync();

        Assert.Equal(new[] { "One.frycs", "Two.frycs" }, studio.ExplorerRows.Rows.Select(r => r.Name));
        Assert.Equal(studio.ExplorerRootItems.Count, studio.ExplorerRows.Rows.Count);
    }
}
