using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Catalogs.Blind75;
using Xunit;
using NotebookTabViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Notebooks.NotebookTabViewModel;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// A notebook opens with its markdown rendered, and running shows markdown that was being edited rendered again, the
/// way Jupyter and VS Code do.
/// </summary>
public class NotebookMarkdownRenderingTests
{
    private static NotebookTabViewModel Tab(params (CellType Type, string Source)[] cells)
    {
        var notebook = new NotebookDocumentItem { Title = "Markdown rendering" };
        foreach (var (type, source) in cells)
        {
            notebook.Cells.Add(new NotebookCellItem { Type = type, Source = source });
        }
        return new NotebookTabViewModel(notebook);
    }

    // The notes as the user left them after editing each one.
    private static NotebookTabViewModel Editing(params (CellType Type, string Source)[] cells)
    {
        var tab = Tab(cells);
        foreach (var cell in tab.Cells.Where(c => c.IsMarkdownCell)) cell.IsMarkdownPreviewMode = false;
        return tab;
    }

    [Fact]
    public void ANotebook_OpensWithItsNotesRendered_AndAnEmptyNoteOpensForTyping()
    {
        var tab = Tab((CellType.Markdown, "# Problem"), (CellType.Code, "var x = 1;"), (CellType.Markdown, ""));

        Assert.True((bool)tab.Cells[0].IsViewingMarkdown);
        Assert.True((bool)tab.Cells[2].IsEditingMarkdown);
        Assert.True(tab.Cells[0].Model.IsMarkdownPreviewMode); // saved as shown
    }

    [Fact]
    public async Task RunAll_RendersEveryMarkdownCell()
    {
        var tab = Editing((CellType.Markdown, "# Problem"), (CellType.Code, "var x = 1;"), (CellType.Markdown, "## Notes"));
        Assert.All(tab.Cells.Where(c => c.IsMarkdownCell), c => Assert.False((bool)c.IsMarkdownPreviewMode));

        await tab.RunAllCellsAsync();

        Assert.All(tab.Cells.Where(c => c.IsMarkdownCell), c => Assert.True((bool)c.IsMarkdownPreviewMode));
        Assert.NotNull<int>(tab.Cells[1].ExecutionCount);
    }

    [Fact]
    public async Task RunningAMarkdownCell_RendersItWithoutExecutingAnything()
    {
        var tab = Editing((CellType.Markdown, "**bold**"));

        await tab.RunSingleCellAsync(tab.Cells[0]);

        Assert.True((bool)tab.Cells[0].IsMarkdownPreviewMode);
        Assert.Null<int>(tab.Cells[0].ExecutionCount);
    }

    [Fact]
    public async Task RunAbove_RendersOnlyTheMarkdownItReaches()
    {
        var tab = Editing((CellType.Markdown, "# Above"), (CellType.Code, "var y = 2;"), (CellType.Markdown, "# Below"));

        await tab.RunCellsAboveAsync(tab.Cells[1]);

        Assert.True((bool)tab.Cells[0].IsMarkdownPreviewMode);
        Assert.False((bool)tab.Cells[2].IsMarkdownPreviewMode);
    }

    [Fact]
    public void Blind75Notebooks_OpenWithTheirNotesRendered()
    {
        var notebook = Blind75CatalogService.ConvertToNotebook(Blind75CatalogService.GetProblemByNumber(53)!);

        Assert.All(notebook.Cells.Where(c => c.Type == CellType.Markdown), c => Assert.True(c.IsMarkdownPreviewMode));
    }
}
