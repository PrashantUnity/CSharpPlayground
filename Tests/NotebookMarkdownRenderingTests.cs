using System.Linq;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>Running a notebook shows its markdown rendered instead of as source, the way Jupyter does.</summary>
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

    [Fact]
    public async Task RunAll_RendersEveryMarkdownCell()
    {
        var tab = Tab((CellType.Markdown, "# Problem"), (CellType.Code, "var x = 1;"), (CellType.Markdown, "## Notes"));
        Assert.All(tab.Cells.Where(c => c.IsMarkdownCell), c => Assert.False(c.IsMarkdownPreviewMode));

        await tab.RunAllCellsAsync();

        Assert.All(tab.Cells.Where(c => c.IsMarkdownCell), c => Assert.True(c.IsMarkdownPreviewMode));
        Assert.NotNull(tab.Cells[1].ExecutionCount);
    }

    [Fact]
    public async Task RunningAMarkdownCell_RendersItWithoutExecutingAnything()
    {
        var tab = Tab((CellType.Markdown, "**bold**"));

        await tab.RunSingleCellAsync(tab.Cells[0]);

        Assert.True(tab.Cells[0].IsMarkdownPreviewMode);
        Assert.Null(tab.Cells[0].ExecutionCount);
    }

    [Fact]
    public async Task RunAbove_RendersOnlyTheMarkdownItReaches()
    {
        var tab = Tab((CellType.Markdown, "# Above"), (CellType.Code, "var y = 2;"), (CellType.Markdown, "# Below"));

        await tab.RunCellsAboveAsync(tab.Cells[1]);

        Assert.True(tab.Cells[0].IsMarkdownPreviewMode);
        Assert.False(tab.Cells[2].IsMarkdownPreviewMode);
    }

    [Fact]
    public void Blind75Notebooks_OpenWithTheirNotesRendered()
    {
        var notebook = Blind75CatalogService.ConvertToNotebook(Blind75CatalogService.GetProblemByNumber(53)!);

        Assert.All(notebook.Cells.Where(c => c.Type == CellType.Markdown), c => Assert.True(c.IsMarkdownPreviewMode));
    }
}
