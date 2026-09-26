using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Highlighting;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Python;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>Python in the editor: its colors in both themes, and indentation after Enter.</summary>
public class PythonEditorTests
{
    // The color the highlighter gives a token (the occurrence-th one). The engine runs line by line from the top, as the
    // editor does, so constructs spanning lines (triple-quoted strings) are seen; it's used directly because the editor's
    // DocumentHighlighter insists on Avalonia's UI thread, which another test may own.
    private static string? ColorOf(string code, string token, bool isDark = true, int occurrence = 0)
    {
        var document = new TextDocument(code);
        var engine = new HighlightingEngine(PythonSyntaxHighlighting.Get(isDark).MainRuleSet);
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
    public void BothThemes_Load(bool isDark) => Assert.NotNull(PythonSyntaxHighlighting.Get(isDark));

    [Fact]
    public void KeywordsStringsCommentsNumbersAndCalls_EachGetTheirColor()
    {
        const string code = "def area(r):  # TODO check\n    return 3.14_15 * r ** 2 if r else None\n\nprint(f\"{area(2)=}\", 'x', b\"raw\")\n";

        Assert.Equal("#FF569CD6", ColorOf(code, "def"));
        Assert.Equal("#FFDCDCAA", ColorOf(code, "area"));             // the name after def
        Assert.Equal("#FF6A9955", ColorOf(code, "# TODO"));
        Assert.Equal("#FFC586C0", ColorOf(code, "return"));
        Assert.Equal("#FFB5CEA8", ColorOf(code, "3.14_15"));
        Assert.Equal("#FF569CD6", ColorOf(code, "None"));
        Assert.Equal("#FFDCDCAA", ColorOf(code, "print"));
        Assert.Equal("#FFCE9178", ColorOf(code, "'x'"));
        Assert.Equal("#FFCE9178", ColorOf(code, "b\"raw\""));
        Assert.Equal("#FF9CDCFE", ColorOf(code, "area(2)=", occurrence: 0)); // inside an f-string's replacement field
    }

    [Fact]
    public void TripleQuotedStrings_SpanLines()
    {
        const string code = "text = \"\"\"first\nsecond line\n\"\"\"\nx = 1\n";

        Assert.Equal("#FFCE9178", ColorOf(code, "second line"));
        Assert.Equal("#FFB5CEA8", ColorOf(code, "1"));
    }

    [Fact]
    public void DecoratorsAndClassNames_AreColored()
    {
        const string code = "@dataclass\nclass Point:\n    x: int\n";

        Assert.Equal("#FFDCDCAA", ColorOf(code, "@dataclass"));
        Assert.Equal("#FF4EC9B0", ColorOf(code, "Point"));
        Assert.Equal("#FF4EC9B0", ColorOf(code, "int"));
    }

    [Fact]
    public void TheLightTheme_HasLightThemeColors() =>
        Assert.Equal("#FFAF00DB", ColorOf("return 1\n", "return", isDark: false));

    private static string Enter(string text, int indentationSize = 4)
    {
        var document = new TextDocument(text + "\n");
        var options = new TextEditorOptions { IndentationSize = indentationSize, ConvertTabsToSpaces = true };
        new PythonIndentationStrategy(options).IndentLine(document, document.GetLineByNumber(document.LineCount));
        return document.GetText(document.GetLineByNumber(document.LineCount));
    }

    [Theory]
    [InlineData("def f():", "    ")]
    [InlineData("    if x > 1:  # a comment", "        ")]
    [InlineData("    value = 1", "    ")]
    [InlineData("        return value", "    ")]
    [InlineData("    pass", "")]
    [InlineData("    raise ValueError('no')", "")]
    [InlineData("    print('a:')", "    ")]
    [InlineData("x = {'a': 1}", "")]
    public void Enter_IndentsLikePython(string previousLine, string expectedIndentation) =>
        Assert.Equal(expectedIndentation, Enter(previousLine));
}
