using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Highlighting;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class JavaEditorTests
{
    private static string? ColorOf(string code, string token, bool isDark = true, int occurrence = 0)
    {
        var document = new TextDocument(code);
        var engine = new HighlightingEngine(JavaSyntaxHighlighting.Get(isDark).MainRuleSet);
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
    public void BothThemes_Load(bool isDark) => Assert.NotNull(JavaSyntaxHighlighting.Get(isDark));

    [Fact]
    public void KeywordsStringsCommentsNumbersAndCalls_EachGetTheirColor()
    {
        const string code = "public class Main { // TODO check\n    public static void main(String[] args) {\n        System.out.println(42);\n    }\n}\n";

        Assert.Equal("#FF569CD6", ColorOf(code, "public"));
        Assert.Equal("#FF569CD6", ColorOf(code, "class"));
        Assert.Equal("#FF6A9955", ColorOf(code, "// TODO"));
        Assert.Equal("#FF4EC9B0", ColorOf(code, "void"));
        Assert.Equal("#FF4EC9B0", ColorOf(code, "String"));
        Assert.Equal("#FF4EC9B0", ColorOf(code, "System"));
        Assert.Equal("#FFDCDCAA", ColorOf(code, "println"));
        Assert.Equal("#FFB5CEA8", ColorOf(code, "42"));
    }

    [Fact]
    public void Indentation_IndentsAfterOpeningBrace()
    {
        var doc = new TextDocument("public class Main {\n\n");
        var strategy = new JavaIndentationStrategy(new TextEditorOptions { IndentationSize = 4, ConvertTabsToSpaces = true });
        strategy.IndentLine(doc, doc.GetLineByNumber(2));

        Assert.Equal("    ", doc.GetText(doc.GetLineByNumber(2)));
    }

    [Fact]
    public void Indentation_OutdentsOnClosingBrace()
    {
        var doc = new TextDocument("    public void test() {\n    }\n");
        var strategy = new JavaIndentationStrategy(new TextEditorOptions { IndentationSize = 4, ConvertTabsToSpaces = true });
        strategy.IndentLine(doc, doc.GetLineByNumber(2));

        Assert.Equal("}", doc.GetText(doc.GetLineByNumber(2)).Trim());
    }

    // ── Folding ────────────────────────────────────────────────────────────────

    private static List<NewFolding> GetFoldings(string code)
    {
        var doc = new TextDocument(code);
        return new JavaFoldingStrategy().CreateFoldings(doc, out _).ToList();
    }

    [Fact]
    public void Folding_MultilineBraceBlock_IsCollapsible()
    {
        const string code = "public class Main {\n    public static void main(String[] args) {\n        System.out.println(42);\n    }\n}\n";
        var foldings = GetFoldings(code);
        Assert.True(foldings.Count >= 2, $"Expected at least 2 foldings, got {foldings.Count}");
        Assert.Contains(foldings, f => f.Name == "{...}");
    }

    [Fact]
    public void Folding_SingleLineBraceBlock_NotCollapsed()
    {
        const string code = "int x = 0;\n";
        var foldings = GetFoldings(code);
        Assert.Empty(foldings);
    }

    [Fact]
    public void Folding_MultilineBlockComment_IsCollapsible()
    {
        const string code = "/* first line\n * second line\n */\npublic class A {}\n";
        var foldings = GetFoldings(code);
        Assert.Contains(foldings, f => f.Name == "/* ... */");
    }

    [Fact]
    public void Folding_BracesInsideStrings_NotFolded()
    {
        const string code = "String s = \"{not a block}\";\n";
        var foldings = GetFoldings(code);
        Assert.Empty(foldings);
    }
}
