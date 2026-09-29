using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Core;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.Sql;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class SqlProblemLanguageAdapterTests
{
    [Fact]
    public void Adapter_IsRegisteredInProblemLanguageRegistry()
    {
        var adapter = ProblemLanguageRegistry.GetAdapter(LanguageIds.Sql);
        Assert.NotNull(adapter);
        Assert.Equal("sql", adapter.LanguageId);
        Assert.Equal(".sql", adapter.DefaultFileExtension);
    }

    [Fact]
    public void LanguageId_IsSql()
    {
        Assert.Equal("sql", SqlProblemLanguageAdapter.Instance.LanguageId);
    }

    [Fact]
    public void BuildScript_GeneratesValidSqlScriptWithComments()
    {
        // Use a well-known Blind 75 DB problem (problem 1 = Two Sum, which has C# default;
        // the SQL adapter will still wrap it in SQL scaffold comments).
        var problem = Blind75CatalogService.GetProblemByNumber(1);
        Assert.NotNull(problem);

        var adapter = SqlProblemLanguageAdapter.Instance;
        var script = adapter.BuildScript(problem);

        Assert.NotNull(script);
        Assert.Equal(LanguageIds.Sql, script.LanguageId);
        Assert.EndsWith(".sql", script.Title);
        Assert.False(string.IsNullOrWhiteSpace(script.Code));
        Assert.Contains("SELECT", script.Code, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildNotebook_GeneratesSqlCells()
    {
        var problem = Blind75CatalogService.GetProblemByNumber(1);
        Assert.NotNull(problem);

        var adapter = SqlProblemLanguageAdapter.Instance;
        var notebook = adapter.BuildNotebook(problem);

        Assert.NotNull(notebook);
        Assert.NotEmpty(notebook.Cells);

        // At least one code cell must be SQL
        Assert.Contains(notebook.Cells, c =>
            c.Type == PdfEditorApp.Plugins.CSharpEditor.Models.CellType.Code &&
            c.Language == LanguageIds.Sql);
    }
}
