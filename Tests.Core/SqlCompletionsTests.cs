using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Sql;
using Xunit;

namespace FrySharp.Core.Tests;

/// <summary>SQL completion and hover are text in, items out (no sqlite3 needed).</summary>
public class SqlCompletionsTests
{
    private static string[] Labels(string code, string marker) =>
        SqlCompletions.Get(code, code.IndexOf(marker, StringComparison.Ordinal) + marker.Length).Select(i => i.DisplayText).ToArray();

    [Fact]
    public void AfterTheDot_ThePrefixIsOfTheNameAfterIt()
    {
        Assert.Equal([".tables"], Labels(".ta", ".ta"));
        Assert.Contains(".schema", Labels(".", "."));
        Assert.Contains(".mode", Labels(".m", ".m"));
        Assert.Empty(Labels(".zz", ".zz"));
    }

    [Fact]
    public void AKeywordIsOfferedByItsFirstLetters_AnyCase()
    {
        Assert.Contains("SELECT", Labels("sel", "sel"));
        Assert.Contains("GROUP BY", Labels("gro", "gro"));
    }

    [Fact]
    public void TheTablesAndViewsTheTextCreates_AreOffered_EachOnce()
    {
        var code = "create table people (id integer);\ncreate table if not exists People (id integer);\ncreate view people_names as select 1;\nselect * from peo";

        Assert.Equal(["people", "people_names"], Labels(code, "from peo").Where(l => l.StartsWith("peo", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void NothingAtAnOffsetOutsideTheText()
    {
        Assert.Empty(SqlCompletions.Get("select", 99));
        Assert.Empty(SqlCompletions.Get(string.Empty, 0));
    }

    [Theory]
    [InlineData("select name from people group by name", "group", "GROUP BY")]
    [InlineData("select name from people group by name", "by", "GROUP BY")]
    [InlineData("select name from people order by name", "order", "ORDER BY")]
    [InlineData("insert into t values (1)", "insert", "INSERT INTO")]
    [InlineData("create table t (x)", "table", "CREATE TABLE")]
    [InlineData("select count(*) from t", "count", "COUNT")]
    public void AHoverOnAKeyword_FindsTheClause_OnEitherWordOfIt(string code, string word, string entry)
    {
        var symbol = SqlQuickInfoProvider.ExtractSymbol(code, code.IndexOf(word, StringComparison.Ordinal) + 1);

        Assert.NotNull(symbol);
        Assert.Equal(entry, symbol!.ToUpperInvariant());
        Assert.NotNull(SqlQuickInfoProvider.Lookup(symbol));
    }

    [Fact]
    public void AWordThatIsNotKnown_HasNoHover()
    {
        var code = "select anything from somewhere";

        Assert.Null(SqlQuickInfoProvider.Lookup(SqlQuickInfoProvider.ExtractSymbol(code, code.IndexOf("anything", StringComparison.Ordinal) + 1)!));
    }
}
