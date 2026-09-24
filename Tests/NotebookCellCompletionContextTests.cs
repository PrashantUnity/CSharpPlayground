using System.Linq;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>
/// Regression coverage for cross-cell notebook completion: a cell that only references a variable
/// declared in an earlier cell (which is what NotebookExecutionKernel's chained ScriptState actually
/// exposes at runtime) must still get real member-access completions, not just cells that declare
/// the variable themselves.
/// </summary>
public class NotebookCellCompletionContextTests
{
    // Shared heavy object — RoslynCompilerService takes ~14s to init on a cold static cache;
    // create once per test class, not per test. See CSharpCompletionServiceTests for the same note.
    private static readonly CSharpCompletionService CompletionService = new(new RoslynCompilerService());

    private static NotebookTabViewModel CreateTabWithCells(params (CellType Type, string Source)[] cells)
    {
        var notebook = new NotebookDocumentItem { Title = "Completion Context Test" };
        foreach (var (type, source) in cells)
        {
            notebook.Cells.Add(new NotebookCellItem { Type = type, Source = source });
        }

        return new NotebookTabViewModel(notebook);
    }

    [Fact]
    public void GetPrecedingContext_FirstCell_IsEmpty()
    {
        var tab = CreateTabWithCells((CellType.Code, "var x = 1;"));

        Assert.Equal(string.Empty, tab.Cells[0].GetPrecedingContext());
    }

    [Fact]
    public void GetPrecedingContext_SecondCell_ContainsFirstCellSource()
    {
        var tab = CreateTabWithCells(
            (CellType.Code, "var response = new System.Net.Http.HttpResponseMessage();"),
            (CellType.Code, "response."));

        var context = tab.Cells[1].GetPrecedingContext();

        Assert.Contains("HttpResponseMessage", context);
    }

    [Fact]
    public void GetPrecedingContext_SkipsMarkdownCells()
    {
        var tab = CreateTabWithCells(
            (CellType.Markdown, "# Notes about response"),
            (CellType.Code, "response."));

        var context = tab.Cells[1].GetPrecedingContext();

        Assert.Equal(string.Empty, context);
    }

    [Fact]
    public async Task DotMemberAccess_OnVariableDeclaredInEarlierCell_ReturnsItsMembers()
    {
        var tab = CreateTabWithCells(
            (CellType.Code, "var client = new HttpClient();\nvar url = \"https://example.com/\";\nvar response = await client.GetAsync(url);"),
            (CellType.Code, "response."));

        var laterCell = tab.Cells[1];
        var mergedCode = laterCell.GetPrecedingContext() + "\n" + laterCell.Source;

        var completions = await CompletionService.GetCompletionsAsync(mergedCode, mergedCode.Length, ExecutionLanguageMode.Statements);

        var names = completions.Select(c => c.DisplayText).ToHashSet();
        Assert.Contains("StatusCode", names);
        Assert.Contains("IsSuccessStatusCode", names);
    }
}
