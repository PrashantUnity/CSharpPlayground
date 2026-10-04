using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Highlighting;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.JavaScript;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class JavaScriptEditorTests
{
    private static string? ColorOf(string code, string token, bool isDark = true, int occurrence = 0)
    {
        var document = new TextDocument(code);
        var engine = new HighlightingEngine(JavaScriptSyntaxHighlighting.Get(isDark).MainRuleSet);
        var offset = -1;
        for (var i = 0; i <= occurrence; i++) offset = code.IndexOf(token, offset + 1, StringComparison.Ordinal);
        var target = document.GetLineByOffset(offset).LineNumber;

        HighlightedLine? highlighted = null;
        for (var number = 1; number <= target; number++)
        {
            highlighted = engine.HighlightLine(document, document.GetLineByNumber(number));
        }

        var section = highlighted!.Sections
            .Where(s => s.Offset <= offset && s.Offset + s.Length >= offset + token.Length && s.Color.Foreground != null)
            .OrderBy(s => s.Length)
            .FirstOrDefault();
        return section?.Color.Foreground?.ToString()?.ToUpperInvariant();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BothThemes_Load(bool isDark) => Assert.NotNull(JavaScriptSyntaxHighlighting.Get(isDark));

    [Fact]
    public void KeywordsStringsCommentsNumbersAndCalls_EachGetTheirColor()
    {
        const string code = "function compute(x) { // TODO check\n    return x > 0 ? 3.14 * x : null;\n}\nconsole.log(compute(10));\n";

        Assert.Equal("#FF569CD6", ColorOf(code, "function"));
        Assert.Equal("#FFDCDCAA", ColorOf(code, "compute"));
        Assert.Equal("#FF6A9955", ColorOf(code, "// TODO"));
        Assert.Equal("#FFC586C0", ColorOf(code, "return"));
        Assert.Equal("#FFB5CEA8", ColorOf(code, "3.14"));
        Assert.Equal("#FF569CD6", ColorOf(code, "null"));
        Assert.Equal("#FF4EC9B0", ColorOf(code, "console"));
        Assert.Equal("#FFDCDCAA", ColorOf(code, "log"));
    }

    [Fact]
    public void TemplateLiterals_WithInterpolation_HighlightCorrectly()
    {
        const string code = "const message = `Hello, ${user.name}!`;\n";

        Assert.Equal("#FF569CD6", ColorOf(code, "const"));
        Assert.Equal("#FFCE9178", ColorOf(code, "Hello,"));
        Assert.Equal("#FF9CDCFE", ColorOf(code, "${"));
        Assert.Equal("#FFCE9178", ColorOf(code, "!`"));
    }

    [Fact]
    public void Indentation_IndentsAfterOpeningBrace()
    {
        var doc = new TextDocument("function test() {\n\n");
        var strategy = new JavaScriptIndentationStrategy(new TextEditorOptions { IndentationSize = 4, ConvertTabsToSpaces = true });
        strategy.IndentLine(doc, doc.GetLineByNumber(2));

        Assert.Equal("    ", doc.GetText(doc.GetLineByNumber(2)));
    }

    [Fact]
    public void Indentation_OutdentsOnClosingBrace()
    {
        var doc = new TextDocument("    function test() {\n    }\n");
        var strategy = new JavaScriptIndentationStrategy(new TextEditorOptions { IndentationSize = 4, ConvertTabsToSpaces = true });
        strategy.IndentLine(doc, doc.GetLineByNumber(2));

        Assert.Equal("}", doc.GetText(doc.GetLineByNumber(2)).Trim());
    }

    // ── Folding ────────────────────────────────────────────────────────────────

    private static List<NewFolding> GetFoldings(string code)
    {
        var doc = new TextDocument(code);
        return new JavaScriptFoldingStrategy().CreateFoldings(doc, out _).ToList();
    }

    [Fact]
    public void Folding_MultilineBraceBlock_IsCollapsible()
    {
        const string code = "function compute(x) {\n    return x * 2;\n}\n";
        var foldings = GetFoldings(code);
        Assert.Single(foldings);
        Assert.Equal("{...}", foldings[0].Name);
    }

    [Fact]
    public void Folding_SingleLineBraceBlock_NotCollapsed()
    {
        const string code = "const obj = { x: 1 };\n";
        var foldings = GetFoldings(code);
        Assert.Empty(foldings);
    }

    [Fact]
    public void Folding_MultilineBlockComment_IsCollapsible()
    {
        const string code = "/* first\n * second\n */\nlet x = 1;\n";
        var foldings = GetFoldings(code);
        Assert.Contains(foldings, f => f.Name == "/* ... */");
    }

    [Fact]
    public void Folding_TemplateLiteralWithBrace_NotFolded()
    {
        // The ${...} inside a template literal must not be treated as a block
        const string code = "const msg = `Hello ${name}!`;\n";
        var foldings = GetFoldings(code);
        Assert.Empty(foldings);
    }
}
