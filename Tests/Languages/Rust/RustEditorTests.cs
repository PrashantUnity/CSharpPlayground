using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Highlighting;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class RustEditorTests
{
    private static string? ColorOf(string code, string token, bool isDark = true, int occurrence = 0)
    {
        var document = new TextDocument(code);
        var engine = new HighlightingEngine(RustSyntaxHighlighting.Get(isDark).MainRuleSet);
        var offset = -1;
        for (var i = 0; i <= occurrence; i++) offset = code.IndexOf(token, offset + 1, StringComparison.Ordinal);
        Assert.True(offset >= 0, $"'{token}' not in the code");
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

    private const string Keyword = "#FF569CD6";
    private const string Control = "#FFC586C0";
    private const string Comment = "#FF6A9955";
    private const string String = "#FFCE9178";
    private const string Escape = "#FFD7BA7D";
    private const string Type = "#FF4EC9B0";
    private const string Function = "#FFDCDCAA";
    private const string Number = "#FFB5CEA8";
    private const string Attribute = "#FF9CDCFE";

    [Theory]
    [InlineData(true, "Rust Dark")]
    [InlineData(false, "Rust Light")]
    public void BothThemes_Load(bool isDark, string name) => Assert.Equal(name, RustSyntaxHighlighting.Get(isDark).Name);

    [Fact]
    public void KeywordsTypesFunctionsMacrosAndNumbers_EachGetTheirColor()
    {
        const string code = "pub fn main() -> Option<i32> {\n    let mut v: Vec<u8> = Vec::new();\n    println!(\"{}\", 42u8);\n    match v.len() { 0 => None, _ => Some(1_000) }\n}\n";

        Assert.Equal(Keyword, ColorOf(code, "pub"));
        Assert.Equal(Keyword, ColorOf(code, "fn"));
        Assert.Equal(Function, ColorOf(code, "main"));
        Assert.Equal(Keyword, ColorOf(code, "let"));
        Assert.Equal(Keyword, ColorOf(code, "mut"));
        Assert.Equal(Type, ColorOf(code, "i32"));
        Assert.Equal(Type, ColorOf(code, "Option"));
        Assert.Equal(Type, ColorOf(code, "Vec"));
        Assert.Equal(Type, ColorOf(code, "u8"));
        Assert.Equal(Function, ColorOf(code, "new"));
        Assert.Equal(Function, ColorOf(code, "println!"));
        Assert.Equal(Number, ColorOf(code, "42u8"));
        Assert.Equal(Control, ColorOf(code, "match"));
        Assert.Equal(Keyword, ColorOf(code, "None"));
        Assert.Equal(Keyword, ColorOf(code, "Some"));
        Assert.Equal(Number, ColorOf(code, "1_000"));
    }

    [Fact]
    public void Comments_Attributes_AndDocComments_AreColoured()
    {
        const string code = "//! Crate docs\n/// Says hi. TODO later\n#[derive(Debug, Clone)]\nstruct P;\n#![allow(unused)]\n";

        Assert.Equal(Comment, ColorOf(code, "//! Crate docs"));
        Assert.Equal(Comment, ColorOf(code, "/// Says hi."));
        Assert.Equal(Attribute, ColorOf(code, "derive"));
        Assert.Equal(Attribute, ColorOf(code, "#![allow(unused)]"));
        Assert.Equal(Keyword, ColorOf(code, "struct"));
    }

    [Fact]
    public void Strings_Escapes_RawStrings_AndByteStrings_AreColoured()
    {
        const string code = "let a = \"hi\\n there\";\nlet b = r#\"raw \"quoted\" text\"#;\nlet c = b\"bytes\";\nlet d = r\"plain raw\";\n";

        Assert.Equal(String, ColorOf(code, "hi"));
        Assert.Equal(Escape, ColorOf(code, "\\n"));
        Assert.Equal(String, ColorOf(code, "raw \"quoted\" text"));
        Assert.Equal(String, ColorOf(code, "b\"bytes\""));
        Assert.Equal(String, ColorOf(code, "r\"plain raw\""));
    }

    [Fact]
    public void ACharLiteral_IsAString_ButALifetimeIsNot()
    {
        const string code = "let c = 'x';\nlet n = '\\n';\nfn f<'a>(s: &'a str) -> &'static str { s }\n";

        Assert.Equal(String, ColorOf(code, "'x'"));
        Assert.Equal(String, ColorOf(code, "'\\n'"));
        Assert.Equal(Keyword, ColorOf(code, "'a"));
        Assert.Equal(Keyword, ColorOf(code, "'a", occurrence: 1));
        Assert.Equal(Keyword, ColorOf(code, "'static"));
    }

    [Fact]
    public void BlockComments_Nest()
    {
        const string code = "/* outer /* inner */ still comment */ let x = 1;\n";

        Assert.Equal(Comment, ColorOf(code, "still comment"));
        Assert.Equal(Keyword, ColorOf(code, "let"));
    }

    [Fact]
    public void AMultiLineString_KeepsItsColourOnTheNextLine()
    {
        const string code = "let s = \"first\nsecond line\";\nlet t = 1;\n";

        Assert.Equal(String, ColorOf(code, "second line"));
        Assert.Equal(Keyword, ColorOf(code, "let", occurrence: 1));
    }

    [Fact]
    public void UpperCaseConstants_AreNotColouredAsTypes_ButCamelCaseNamesAre()
    {
        const string code = "const MAX_SIZE: usize = 10;\nstruct Point;\nlet p = Point;\n";

        Assert.Null(ColorOf(code, "MAX_SIZE"));
        Assert.Equal(Type, ColorOf(code, "Point"));
        Assert.Equal(Type, ColorOf(code, "Point", occurrence: 1));
        Assert.Equal(Type, ColorOf(code, "usize"));
    }

    [Fact]
    public void ATurbofishCall_IsColouredAsAFunction()
    {
        const string code = "let n = s.parse::<i32>().unwrap();\n";

        Assert.Equal(Function, ColorOf(code, "parse"));
        Assert.Equal(Function, ColorOf(code, "unwrap"));
        Assert.Equal(Type, ColorOf(code, "i32"));
    }

    // ── Indentation ────────────────────────────────────────────────────────────

    private static TextEditorOptions Options => new() { IndentationSize = 4, ConvertTabsToSpaces = true };

    [Fact]
    public void Indentation_IndentsAfterAnOpeningBrace()
    {
        var doc = new TextDocument("fn main() {\n\n");
        new BraceIndentationStrategy(Options).IndentLine(doc, doc.GetLineByNumber(2));

        Assert.Equal("    ", doc.GetText(doc.GetLineByNumber(2)));
    }

    [Fact]
    public void Indentation_KeepsTheLevelAfterAStatement_AndOutdentsOnAClosingBrace()
    {
        var doc = new TextDocument("    let x = 1;\n\n    }\n");
        var strategy = new BraceIndentationStrategy(Options);
        strategy.IndentLine(doc, doc.GetLineByNumber(2));
        strategy.IndentLine(doc, doc.GetLineByNumber(3));

        Assert.Equal("    ", doc.GetText(doc.GetLineByNumber(2)));
        Assert.Equal("}", doc.GetText(doc.GetLineByNumber(3)));
    }

    [Fact]
    public void Indentation_IgnoresATrailingComment_ButNotASlashesInsideAString()
    {
        var withComment = new TextDocument("fn main() { // start\n\n");
        var withUrl = new TextDocument("let url = \"http://x\"; {\n\n");
        var strategy = new BraceIndentationStrategy(Options);
        strategy.IndentLine(withComment, withComment.GetLineByNumber(2));
        strategy.IndentLine(withUrl, withUrl.GetLineByNumber(2));

        Assert.Equal("    ", withComment.GetText(withComment.GetLineByNumber(2)));
        Assert.Equal("    ", withUrl.GetText(withUrl.GetLineByNumber(2)));
    }

    // ── Folding ────────────────────────────────────────────────────────────────

    private static List<NewFolding> Folds(string code) =>
        new RustFoldingStrategy().CreateFoldings(new TextDocument(code), out _).ToList();

    [Fact]
    public void Folding_FoldsBlocksArraysAndCallsThatSpanLines()
    {
        var folds = Folds("""
            fn main() {
                let v = vec![
                    1,
                    2,
                ];
                call(
                    v,
                );
            }
            """);

        Assert.Equal(["{ ... }", "[ ... ]", "( ... )"], folds.Select(f => f.Name));
    }

    [Fact]
    public void Folding_ASingleLineBlock_IsNotFolded()
    {
        Assert.Empty(Folds("fn f() { 1 }\nlet v = [1, 2];\n"));
    }

    [Fact]
    public void Folding_ALifetime_DoesNotHideTheBracesAfterIt()
    {
        var folds = Folds("""
            fn longest<'a>(a: &'a str) -> &'a str {
                a
            }
            impl<'a> S<'a> {
                fn f(&self) {}
            }
            """);

        Assert.Equal(2, folds.Count(f => f.Name == "{ ... }"));
    }

    [Fact]
    public void Folding_ACharLiteralOfABrace_IsNotABlock()
    {
        var folds = Folds("""
            fn f(c: char) -> bool {
                c == '{' || c == '}'
            }
            """);

        Assert.Single(folds);
    }

    [Fact]
    public void Folding_NestedBlockComments_FoldAsOne()
    {
        var folds = Folds("/* outer\n /* inner */\n still */\nfn f() {\n}\n");

        Assert.Equal(["/* ... */", "{ ... }"], folds.Select(f => f.Name));
    }

    [Fact]
    public void Folding_BracesInsideStringsAndComments_AreIgnored()
    {
        var folds = Folds("""
            fn f() {
                let s = "}{ not a block";
                // } nor here {
            }
            """);

        Assert.Single(folds);
    }

    [Fact]
    public void Folding_ARawString_FoldsWhenItSpansLines_AndItsBracesAreIgnored()
    {
        var folds = Folds("let s = r#\"{\nnot a block }\n\"#;\nfn f() {\n}\n");

        Assert.Equal(["r\"...\"", "{ ... }"], folds.Select(f => f.Name));
    }

    [Fact]
    public void Folding_RegionMarkers_FoldTheirLines()
    {
        var folds = Folds("// region: helpers\nfn a() {}\nfn b() {}\n// endregion\nfn main() {}\n");

        var region = Assert.Single(folds);
        Assert.Equal("helpers", region.Name);
    }

    [Fact]
    public void Folding_AnUnclosedBlock_IsSimplyNotFolded()
    {
        Assert.Empty(Folds("fn f() {\n let x = 1;\n"));
    }

    // ── The language wires them up ─────────────────────────────────────────────

    [Fact]
    public void TheLanguage_UsesTheseEditorServices()
    {
        var services = new StudioLanguageServices(Path.Combine(Path.GetTempPath(), "FryPDF_RustEditor_" + Guid.NewGuid().ToString("N")));
        var language = services.Registry.Get(LanguageIds.Rust)!;

        Assert.Equal("Rust Dark", language.GetHighlighting(true)!.Name);
        Assert.Equal("Rust Light", language.GetHighlighting(false)!.Name);
        Assert.IsType<RustFoldingStrategy>(language.Folding);
        Assert.IsType<BraceIndentationStrategy>(language.CreateIndentationStrategy(Options));
    }
}
