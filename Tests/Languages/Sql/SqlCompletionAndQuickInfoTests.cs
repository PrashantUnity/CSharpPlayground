using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Sql;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class SqlCompletionAndQuickInfoTests
{
    private readonly SqlCompletionService _completion = new();
    // EditorAssistantContext is a record: EditorAssistantContext(Func<string>? PrecedingCode, Func<bool>? IsSuppressed)
    private readonly EditorAssistantContext _context = new();

    [Fact]
    public async Task Completion_OffersDotCommandsAfterDot()
    {
        var code = ".";
        var items = await _completion.GetCompletionsAsync(code, caretOffset: 1, _context);

        Assert.NotEmpty(items);
        Assert.Contains(items, i => i.DisplayText == ".tables");
        Assert.Contains(items, i => i.DisplayText == ".schema");
        Assert.Contains(items, i => i.DisplayText == ".mode");
        Assert.Contains(items, i => i.DisplayText == ".output" || i.DisplayText == ".nullvalue");
    }

    [Fact]
    public async Task Completion_OffersSqlKeywords()
    {
        var code = "SEL";
        var items = await _completion.GetCompletionsAsync(code, caretOffset: 3, _context);

        Assert.NotEmpty(items);
        Assert.Contains(items, i => i.DisplayText == "SELECT");
    }

    [Fact]
    public async Task Completion_OffersDataTypes()
    {
        var code = "INT";
        var items = await _completion.GetCompletionsAsync(code, caretOffset: 3, _context);

        Assert.NotEmpty(items);
        Assert.Contains(items, i => i.DisplayText == "INTEGER" || i.DisplayText.StartsWith("INT"));
    }

    [Fact]
    public async Task Completion_OffersBuiltinFunctions()
    {
        var code = "COUN";
        var items = await _completion.GetCompletionsAsync(code, caretOffset: 4, _context);

        Assert.NotEmpty(items);
        Assert.Contains(items, i => i.DisplayText == "COUNT");
    }

    [Fact]
    public async Task Completion_OffersLocalTableNamesDefinedInCode()
    {
        // The completion service extracts table names from CREATE TABLE in the code itself
        var code = "CREATE TABLE users (id INTEGER, name TEXT);\nSELECT * FROM us";
        var items = await _completion.GetCompletionsAsync(code, caretOffset: code.Length, _context);

        Assert.Contains(items, i => i.DisplayText == "users");
    }

    [Fact]
    public void QuickInfo_FindsSqlKeywords()
    {
        var info = SqlQuickInfoProvider.Lookup("SELECT");
        Assert.NotNull(info);
        Assert.Contains("SELECT", info.Signature);
        Assert.False(string.IsNullOrWhiteSpace(info.Summary));
    }

    [Fact]
    public void QuickInfo_FindsBuiltinFunctions()
    {
        var countInfo = SqlQuickInfoProvider.Lookup("COUNT");
        Assert.NotNull(countInfo);
        Assert.Contains("COUNT", countInfo.Signature);

        var sumInfo = SqlQuickInfoProvider.Lookup("SUM");
        Assert.NotNull(sumInfo);
        Assert.Contains("SUM", sumInfo.Signature);

        var dateInfo = SqlQuickInfoProvider.Lookup("DATE");
        Assert.NotNull(dateInfo);
        Assert.Contains("DATE", dateInfo.Signature);
    }

    [Fact]
    public void QuickInfo_FindsPragmas()
    {
        var info = SqlQuickInfoProvider.Lookup("PRAGMA");
        Assert.NotNull(info);
        Assert.Contains("PRAGMA", info.Signature);
    }

    [Fact]
    public void QuickInfo_DotCommandsAreNotInQuickInfoDictionary()
    {
        // Dot-commands are handled by completion, not quick info hover cards.
        // The QuickInfo provider covers SQL language constructs only.
        var info = SqlQuickInfoProvider.Lookup(".tables");
        Assert.Null(info);
    }

    [Fact]
    public void QuickInfo_UnknownSymbol_ReturnsNull()
    {
        var info = SqlQuickInfoProvider.Lookup("XYZNOTASQLKEYWORD");
        Assert.Null(info);
    }
}
