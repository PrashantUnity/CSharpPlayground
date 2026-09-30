using AvaloniaEdit;
using AvaloniaEdit.Document;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class GoFoldingAndIndentationTests
{
    [Fact]
    public void CreatesFoldings_ForCurlyBraces()
    {
        var code = """
            package main

            func main() {
                println("hello")
            }
            """;

        var doc = new TextDocument(code);
        var strategy = new GoFoldingStrategy();
        var foldings = strategy.CreateFoldings(doc, out var errorOffset).ToList();

        Assert.Equal(-1, errorOffset);
        Assert.Single(foldings);
        Assert.Equal("{ ... }", foldings[0].Name);
    }

    [Fact]
    public void CreatesFoldings_ForParenthesizedBlocks()
    {
        var code = """
            package main

            import (
                "fmt"
                "strings"
            )

            const (
                A = 1
                B = 2
            )
            """;

        var doc = new TextDocument(code);
        var strategy = new GoFoldingStrategy();
        var foldings = strategy.CreateFoldings(doc, out var errorOffset).ToList();

        Assert.Equal(-1, errorOffset);
        Assert.Equal(2, foldings.Count);
        Assert.All(foldings, f => Assert.Equal("( ... )", f.Name));
    }

    [Fact]
    public void CreatesFoldings_ForBlockCommentsAndRawStrings()
    {
        var code = """
            /*
             * Multi-line
             * comment
             */
            package main

            var text = `
                line 1
                line 2
            `
            """;

        var doc = new TextDocument(code);
        var strategy = new GoFoldingStrategy();
        var foldings = strategy.CreateFoldings(doc, out var errorOffset).ToList();

        Assert.Equal(-1, errorOffset);
        Assert.Equal(2, foldings.Count);
        Assert.Contains(foldings, f => f.Name == "/* ... */");
        Assert.Contains(foldings, f => f.Name == "`...`");
    }

    [Fact]
    public void IndentationStrategy_IndentsAfterOpeningBrace()
    {
        var options = new TextEditorOptions { IndentationSize = 4, ConvertTabsToSpaces = true };
        var strategy = new GoIndentationStrategy(options);

        var doc = new TextDocument("func main() {\n\n");
        var line = doc.GetLineByNumber(2);

        strategy.IndentLine(doc, line);

        Assert.Equal("    ", doc.GetText(line));
    }

    [Fact]
    public void IndentationStrategy_OutdentsOnClosingBrace()
    {
        var options = new TextEditorOptions { IndentationSize = 4, ConvertTabsToSpaces = true };
        var strategy = new GoIndentationStrategy(options);

        var doc = new TextDocument("    if true {\n        println()\n    }");
        var line = doc.GetLineByNumber(3);

        strategy.IndentLine(doc, line);

        var lineText = doc.GetText(line);
        Assert.StartsWith("    }", lineText);
    }
}
