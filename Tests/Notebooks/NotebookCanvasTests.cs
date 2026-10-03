using System.Collections.ObjectModel;
using System.Collections.Specialized;
using PdfEditorApp.Plugins.CSharpEditor.Controls.Common;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Execution;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Notebooks;
using Xunit;
using CSharpNotebookStudioViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Notebooks.CSharpNotebookStudioViewModel;
using NotebookCellViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Notebooks.NotebookCellViewModel;

namespace CSharpEditorPlugin.Tests;

public class NotebookCanvasRowsTests
{
    private static NotebookCellViewModel Cell(string source) => new(new NotebookCellItem { Source = source });

    private static string Describe(NotebookCanvasRows rows) => string.Join(",", rows.Rows.Select<object, string>(r => r switch
    {
        NotebookCanvasHeader => "H",
        NotebookCanvasFooter => "F",
        NotebookCellViewModel cell => cell.Source,
        _ => "?"
    }));

    [Fact]
    public void AnEmptyNotebook_IsJustTheHeaderAndTheFooter()
    {
        var rows = new NotebookCanvasRows(new ObservableCollection<NotebookCellViewModel>());

        Assert.Equal("H,F", Describe(rows));
    }

    [Fact]
    public void TheCells_SitBetweenTheHeaderAndTheFooter_InOrder()
    {
        var cells = new ObservableCollection<NotebookCellViewModel> { Cell("a"), Cell("b"), Cell("c") };

        var rows = new NotebookCanvasRows(cells);

        Assert.Equal("H,a,b,c,F", Describe(rows));
    }

    [Fact]
    public void AddingACell_AtTheEndOrInTheMiddle_KeepsTheFooterLast()
    {
        var cells = new ObservableCollection<NotebookCellViewModel> { Cell("a"), Cell("c") };
        var rows = new NotebookCanvasRows(cells);

        cells.Add(Cell("d"));
        cells.Insert(1, Cell("b"));

        Assert.Equal("H,a,b,c,d,F", Describe(rows));
    }

    [Fact]
    public void RemovingAndMovingCells_ChangesJustTheirRows()
    {
        var cells = new ObservableCollection<NotebookCellViewModel> { Cell("a"), Cell("b"), Cell("c"), Cell("d") };
        var rows = new NotebookCanvasRows(cells);

        cells.RemoveAt(1);
        Assert.Equal("H,a,c,d,F", Describe(rows));

        cells.Move(0, 2);
        Assert.Equal("H,c,d,a,F", Describe(rows));
    }

    [Fact]
    public void ReplacingOrClearingTheCells_RebuildsTheRows()
    {
        var cells = new ObservableCollection<NotebookCellViewModel> { Cell("a"), Cell("b") };
        var rows = new NotebookCanvasRows(cells);

        cells[0] = Cell("z");
        Assert.Equal("H,z,b,F", Describe(rows));

        cells.Clear();
        Assert.Equal("H,F", Describe(rows));
    }

    [Fact]
    public void AddingManyCellsOneByOne_RaisesOneNotificationEach_NotARebuildEachTime()
    {
        var cells = new ObservableCollection<NotebookCellViewModel>();
        var rows = new NotebookCanvasRows(cells);
        var actions = new List<NotifyCollectionChangedAction>();
        rows.Rows.CollectionChanged += (_, e) => actions.Add(e.Action);

        for (var i = 0; i < 20; i++) cells.Add(Cell($"c{i}"));

        Assert.All(actions, a => Assert.Equal(NotifyCollectionChangedAction.Add, a));
        Assert.Equal(20, actions.Count);
    }
}

public class LazyContentTests
{
    private sealed class Data;

    [Fact]
    public void NothingIsBuilt_UntilItIsFirstNeeded()
    {
        var host = new LazyContent { DataContext = new Data() };

        Assert.Null(host.Content);
    }

    [Fact]
    public void WhenItBecomesActive_ItsContentIsTheDataContext_SoTheTemplateBuilds()
    {
        var data = new Data();
        var host = new LazyContent { DataContext = data };

        host.IsActive = true;

        Assert.Same(data, host.Content);
    }

    [Fact]
    public void OnceBuilt_ItStaysBuilt_EvenWhenItIsNoLongerActive()
    {
        var data = new Data();
        var host = new LazyContent { DataContext = data, IsActive = true };

        host.IsActive = false;

        Assert.Same(data, host.Content);
    }

    [Fact]
    public void ARecycledHost_FollowsItsNewDataContext_SoItNeverShowsTheOldItem()
    {
        var first = new Data();
        var second = new Data();
        var host = new LazyContent { DataContext = first, IsActive = true };

        host.DataContext = second;

        Assert.Same(second, host.Content);
    }

    [Fact]
    public void AHostThatWasNeverActive_StaysEmpty_WhenItsDataContextChanges()
    {
        var host = new LazyContent { DataContext = new Data() };

        host.DataContext = new Data();

        Assert.Null(host.Content);
    }
}

public class NotebookSidePanelListTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_NotebookPanels_" + Guid.NewGuid().ToString("N"));
    private readonly LocalScriptStorageService _storage;

    public NotebookSidePanelListTests()
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

    private CSharpNotebookStudioViewModel CreateStudio(int cells)
    {
        var notebook = new NotebookDocumentItem { Title = "Panels" };
        for (var i = 0; i < cells; i++) notebook.Cells.Add(new NotebookCellItem { Source = $"var x{i} = {i};" });

        return new CSharpNotebookStudioViewModel(
            notebook,
            _storage,
            new RoslynCompilerService(),
            new ScriptExecutionEngine(),
            backToHubAction: () => { },
            backToHomeAction: () => { });
    }

    [Fact]
    public void TheOutlineAndSearchPanels_ListNoCells_WhileTheExplorerIsShowing()
    {
        var studio = CreateStudio(12);

        Assert.True(studio.IsExplorerActive);
        Assert.Empty(studio.OutlineCells);
        Assert.Empty(studio.SearchPanelCells);
    }

    [Fact]
    public void OpeningTheOutline_ListsTheCells_AndLeavingItTakesThemAway()
    {
        var studio = CreateStudio(12);
        var changed = new List<string?>();
        studio.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        studio.SelectedActivityBarIndex = 1;

        Assert.Equal(12, studio.OutlineCells.Count());
        Assert.Contains(nameof(CSharpNotebookStudioViewModel.OutlineCells), changed);

        studio.SelectedActivityBarIndex = 0;
        Assert.Empty(studio.OutlineCells);
    }

    [Fact]
    public void OpeningSearch_ListsTheMatchingCells()
    {
        var studio = CreateStudio(12);
        studio.SelectedActivityBarIndex = 3;

        Assert.Equal(12, studio.SearchPanelCells.Count());

        studio.SearchText = "x7 ";
        Assert.Single(studio.SearchPanelCells);
    }

    [Fact]
    public void TheCanvasRows_AreTheActiveNotebooksCells_BetweenTheHeaderAndTheFooter()
    {
        var studio = CreateStudio(5);

        var rows = studio.CanvasRows;

        Assert.Equal(7, rows.Count);
        Assert.IsType<NotebookCanvasHeader>(rows[0]);
        Assert.IsType<NotebookCanvasFooter>(rows[^1]);
        Assert.Equal(studio.Cells, rows.OfType<NotebookCellViewModel>());
        Assert.Same(rows, studio.CanvasRows); // One list per notebook, not a new one on every read.
    }
}
