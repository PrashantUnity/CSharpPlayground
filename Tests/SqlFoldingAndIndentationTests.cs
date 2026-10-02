using AvaloniaEdit;
using AvaloniaEdit.Document;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Sql;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class SqlFoldingAndIndentationTests
{
    [Fact]
    public void CreatesFoldings_ForBlockComments()
    {
        var code =
            "/* This is a\n" +
            "   multiline block\n" +
            "   comment in SQL */\n" +
            "SELECT * FROM users;";

        var doc = new TextDocument(code);
        var strategy = new SqlFoldingStrategy();
        var foldings = strategy.CreateFoldings(doc, out var errorOffset).ToList();

        Assert.Equal(-1, errorOffset);
        Assert.Contains(foldings, f => f.Name == "/* ... */");
    }

    [Fact]
    public void CreatesFoldings_ForParenthesizedBlocks()
    {
        var code =
            "CREATE TABLE users (\n" +
            "    id INTEGER PRIMARY KEY,\n" +
            "    name TEXT NOT NULL\n" +
            ");";

        var doc = new TextDocument(code);
        var strategy = new SqlFoldingStrategy();
        var foldings = strategy.CreateFoldings(doc, out var errorOffset).ToList();

        Assert.Equal(-1, errorOffset);
        Assert.Contains(foldings, f => f.Name == "(...)");
    }

    [Fact]
    public void CreatesFoldings_ForBeginEnd()
    {
        var code =
            "BEGIN\n" +
            "    INSERT INTO log VALUES (1, 'hello');\n" +
            "    INSERT INTO log VALUES (2, 'world');\n" +
            "END;";

        var doc = new TextDocument(code);
        var strategy = new SqlFoldingStrategy();
        var foldings = strategy.CreateFoldings(doc, out var errorOffset).ToList();

        Assert.Equal(-1, errorOffset);
        Assert.Contains(foldings, f => f.Name == "BEGIN ... END");
    }

    [Fact]
    public void IndentationStrategy_PreservesExistingIndent()
    {
        var code = "    SELECT id FROM users\n";
        var doc = new TextDocument(code);
        var options = new TextEditorOptions { IndentationSize = 4, ConvertTabsToSpaces = true };
        var strategy = new SqlIndentationStrategy(options);

        var secondLine = doc.GetLineByNumber(2);
        strategy.IndentLine(doc, secondLine);

        Assert.Equal("    ", doc.GetText(secondLine));
    }

    [Fact]
    public void IndentationStrategy_IndentsAfterOpenParenthesis()
    {
        var code = "CREATE TABLE users (\n";
        var doc = new TextDocument(code);
        var options = new TextEditorOptions { IndentationSize = 4, ConvertTabsToSpaces = true };
        var strategy = new SqlIndentationStrategy(options);

        var secondLine = doc.GetLineByNumber(2);
        strategy.IndentLine(doc, secondLine);

        Assert.Equal("    ", doc.GetText(secondLine));
    }

    [Fact]
    public void IndentationStrategy_IndentsAfterSelectKeyword()
    {
        var code = "SELECT\n";
        var doc = new TextDocument(code);
        var options = new TextEditorOptions { IndentationSize = 4, ConvertTabsToSpaces = true };
        var strategy = new SqlIndentationStrategy(options);

        var secondLine = doc.GetLineByNumber(2);
        strategy.IndentLine(doc, secondLine);

        Assert.Equal("    ", doc.GetText(secondLine));
    }

    [Fact]
    public void IndentationStrategy_OutdentsBeforeClosingParen()
    {
        var code = "CREATE TABLE (\n    id INTEGER,\n);\n";
        var doc = new TextDocument(code);
        var options = new TextEditorOptions { IndentationSize = 4, ConvertTabsToSpaces = true };
        var strategy = new SqlIndentationStrategy(options);

        // The closing paren line should not be additionally indented
        var closingLine = doc.GetLineByNumber(3);
        strategy.IndentLine(doc, closingLine);

        // Closing paren lines should have base indent (not more than opening)
        var indent = doc.GetText(closingLine);
        Assert.True(indent.Length <= 4);
    }
}
