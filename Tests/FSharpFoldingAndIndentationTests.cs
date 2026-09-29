using AvaloniaEdit;
using AvaloniaEdit.Document;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.FSharp;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class FSharpFoldingAndIndentationTests
{
    [Fact]
    public void CreatesFoldings_ForBlockCommentsAndTripleQuotedStrings()
    {
        var code = """"
            (* This is a
               multiline block
               comment in F# *)

            let text = """
                Line 1
                Line 2
                """
            """";

        var doc = new TextDocument(code);
        var strategy = new FSharpFoldingStrategy();
        var foldings = strategy.CreateFoldings(doc, out var errorOffset).ToList();

        Assert.Equal(-1, errorOffset);
        Assert.Contains(foldings, f => f.Name == "(* ... *)");
        Assert.Contains(foldings, f => f.Name == "\"\"\"...\"\"\"");
    }

    [Fact]
    public void IndentationStrategy_IndentsAfterEquals()
    {
        var code = "let compute x =\n";
        var doc = new TextDocument(code);
        var options = new TextEditorOptions { IndentationSize = 4, ConvertTabsToSpaces = true };
        var strategy = new FSharpIndentationStrategy(options);

        var secondLine = doc.GetLineByNumber(2);
        strategy.IndentLine(doc, secondLine);

        Assert.Equal("    ", doc.GetText(secondLine));
    }

    [Fact]
    public void IndentationStrategy_IndentsAfterArrow()
    {
        var code = "match value with\n| Some x ->\n";
        var doc = new TextDocument(code);
        var options = new TextEditorOptions { IndentationSize = 4, ConvertTabsToSpaces = true };
        var strategy = new FSharpIndentationStrategy(options);

        var thirdLine = doc.GetLineByNumber(3);
        strategy.IndentLine(doc, thirdLine);

        Assert.Equal("    ", doc.GetText(thirdLine));
    }

    [Fact]
    public void IndentationStrategy_PreservesExistingIndent()
    {
        var code = "    let x = 1\n";
        var doc = new TextDocument(code);
        var options = new TextEditorOptions { IndentationSize = 4, ConvertTabsToSpaces = true };
        var strategy = new FSharpIndentationStrategy(options);

        var secondLine = doc.GetLineByNumber(2);
        strategy.IndentLine(doc, secondLine);

        Assert.Equal("    ", doc.GetText(secondLine));
    }
}
