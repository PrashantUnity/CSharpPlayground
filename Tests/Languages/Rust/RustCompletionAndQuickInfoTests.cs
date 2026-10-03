using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class RustCompletionAndQuickInfoTests
{
    private readonly RustCompletionService _service = new();

    // The | in the code marks the caret.
    private async Task<IReadOnlyList<CSharpCompletionItem>> Complete(string codeWithCaret)
    {
        var caret = codeWithCaret.IndexOf('|');
        var code = codeWithCaret.Remove(caret, 1);
        return await _service.GetCompletionsAsync(code, caret, new EditorAssistantContext(() => string.Empty, null));
    }

    private static IEnumerable<string> Names(IEnumerable<CSharpCompletionItem> items) => items.Select(i => i.DisplayText);

    [Fact]
    public async Task AfterADot_TheEverydayMethodsAreOffered()
    {
        var items = await Complete("let n = v.|");

        var names = Names(items).ToList();
        Assert.Contains("push", names);
        Assert.Contains("iter", names);
        Assert.Contains("len", names);
        Assert.Contains("unwrap", names);
        Assert.Contains("await", names);
        Assert.DoesNotContain("fn", names);
        Assert.Equal(names.Distinct().Count(), names.Count);
    }

    [Fact]
    public async Task AfterADot_TheTypedPrefixFilters()
    {
        var names = Names(await Complete("v.un|")).ToList();

        Assert.Contains("unwrap", names);
        Assert.Contains("unwrap_or", names);
        Assert.DoesNotContain("push", names);
    }

    [Fact]
    public async Task ARangeDots_AreNotAMethodCall()
    {
        var names = Names(await Complete("for i in 0..|")).ToList();

        Assert.DoesNotContain("push", names);
        Assert.Contains("fn", names);
    }

    [Fact]
    public async Task AfterATypeAndColons_ItsAssociatedFunctionsAreOffered()
    {
        var names = Names(await Complete("let s = String::|")).ToList();

        Assert.Equal(["new", "from", "with_capacity"], names);
    }

    [Fact]
    public async Task AfterStdColons_TheModulesAreOffered()
    {
        var items = await Complete("use std::|");

        Assert.Contains("collections", Names(items));
        Assert.Contains("io", Names(items));
        Assert.All(items.Where(i => i.DisplayText == "fs"), i => Assert.Equal(CompletionItemKind.Namespace, i.Kind));
    }

    [Fact]
    public async Task AfterAModulePath_ItsItemsAreOffered()
    {
        Assert.Equal(["HashMap", "HashSet", "BTreeMap", "BTreeSet", "VecDeque", "BinaryHeap"], Names(await Complete("use std::collections::|")));
        Assert.Contains("swap", Names(await Complete("std::mem::|")));
    }

    [Fact]
    public async Task AfterAnEnumsName_ItsVariantsAreOffered()
    {
        var names = Names(await Complete("""
            enum Shape {
                // A round one
                Circle,
                Square(f64),
                Rect { w: f64, h: f64 },
                #[allow(dead_code)]
                Unit = 4,
            }
            fn main() { let s = Shape::| }
            """)).ToList();

        Assert.Equal(["Circle", "Square", "Rect", "Unit"], names);
    }

    [Fact]
    public async Task AfterATypesName_ItsImplFunctionsAndConstantsAreOffered()
    {
        var names = Names(await Complete("""
            struct Point { x: i32 }
            impl Point {
                const ORIGIN: i32 = 0;
                fn new(x: i32) -> Self { Point { x } }
                fn origin() -> Self { Self::new(0) }
            }
            impl Display for Point {
                fn fmt(&self) {}
            }
            fn other() {}
            fn main() { let p = Point::| }
            """)).ToList();

        Assert.Contains("new", names);
        Assert.Contains("origin", names);
        Assert.Contains("ORIGIN", names);
        Assert.Contains("fmt", names);
        Assert.DoesNotContain("other", names);
    }

    [Fact]
    public async Task AfterSelf_TheFilesFunctionsAreOffered()
    {
        var names = Names(await Complete("fn helper() {}\nfn main() { Self::| }")).ToList();

        Assert.Contains("helper", names);
        Assert.Contains("main", names);
    }

    [Fact]
    public async Task InScope_KeywordsTypesMacrosAndSnippetsAreOffered()
    {
        var items = await Complete("|");
        var names = Names(items).ToList();

        Assert.Contains("fn", names);
        Assert.Contains("let", names);
        Assert.Contains("Vec", names);
        Assert.Contains("HashMap", names);
        Assert.Contains("Iterator", names);
        Assert.Contains("println!", names);
        Assert.Contains("std", names);
        Assert.Contains("fn main()", names);
        Assert.Contains("mod tests", names);
        Assert.Equal(CompletionItemKind.Keyword, items.First(i => i.DisplayText == "fn").Kind);
        Assert.Equal(CompletionItemKind.Snippet, items.First(i => i.DisplayText == "fn main()").Kind);
    }

    [Fact]
    public async Task InScope_TheFilesOwnNamesComeFirst()
    {
        var items = await Complete("""
            struct Widget;
            const LIMIT: usize = 3;
            mod util {}
            fn compute(input: i32, mut total: u64) -> i32 { let result = 1; for step in 0..3 {} result }
            fn main() { | }
            """);

        var declared = items.Where(i => i.Priority > 0).ToDictionary(i => i.DisplayText, i => i.Kind);
        Assert.Equal(CompletionItemKind.Struct, declared["Widget"]);
        Assert.Equal(CompletionItemKind.Field, declared["LIMIT"]);
        Assert.Equal(CompletionItemKind.Namespace, declared["util"]);
        Assert.Equal(CompletionItemKind.Method, declared["compute"]);
        Assert.Equal(CompletionItemKind.Variable, declared["input"]);
        Assert.Equal(CompletionItemKind.Variable, declared["total"]);
        Assert.Equal(CompletionItemKind.Variable, declared["result"]);
        Assert.Equal(CompletionItemKind.Variable, declared["step"]);
        Assert.DoesNotContain("self", declared.Keys);
    }

    [Fact]
    public async Task InScope_ThePrefixFilters()
    {
        var names = Names(await Complete("pri|")).ToList();

        Assert.Contains("println!", names);
        Assert.Contains("print!", names);
        Assert.DoesNotContain("vec!", names);
    }

    [Theory]
    [InlineData("println!", "println!(\"\")", -2)]
    [InlineData("vec!", "vec![]", -1)]
    [InlineData("assert_eq!", "assert_eq!(, )", -3)]
    [InlineData("dbg!", "dbg!()", -1)]
    [InlineData("todo!", "todo!()", 0)]
    public async Task AMacro_IsInsertedWithTheCaretWhereYouTypeNext(string name, string insertion, int caretDelta)
    {
        var item = (await Complete("|")).First(i => i.DisplayText == name);

        Assert.Equal(insertion, item.InsertionText);
        Assert.Equal(caretDelta, item.CaretOffsetDelta);
    }

    [Fact]
    public async Task ASnippet_PutsTheCaretInsideItsBody()
    {
        var item = (await Complete("|")).First(i => i.DisplayText == "fn main()");

        Assert.Equal("fn main() {\n    \n}", item.InsertionText);
        Assert.Equal("fn main() {\n    ".Length, item.InsertionText.Length + item.CaretOffsetDelta);
    }

    [Fact]
    public async Task InAnAttribute_TheAttributeNamesAreOffered()
    {
        var items = await Complete("#[|");

        Assert.Contains("derive", Names(items));
        Assert.Contains("test", Names(items));
        Assert.Equal("derive()", items.First(i => i.DisplayText == "derive").InsertionText);
        Assert.Equal(-1, items.First(i => i.DisplayText == "derive").CaretOffsetDelta);
    }

    [Fact]
    public async Task InDerive_TheDerivableTraitsAreOffered()
    {
        var names = Names(await Complete("#[derive(Debug, |")).ToList();

        Assert.Equal(["Debug", "Clone", "Copy", "PartialEq", "Eq", "PartialOrd", "Ord", "Hash", "Default"], names);
    }

    [Theory]
    [InlineData("abc", 99)]
    [InlineData("abc", -1)]
    [InlineData("", 1)]
    public async Task ABadCaret_OffersNothing(string code, int caret)
    {
        var items = await _service.GetCompletionsAsync(code, caret, new EditorAssistantContext(() => string.Empty, null));

        Assert.Empty(items);
    }

    [Fact]
    public async Task ANewEmptyFile_StillOffersTheSnippets()
    {
        var items = await _service.GetCompletionsAsync(string.Empty, 0, new EditorAssistantContext(() => string.Empty, null));

        Assert.Contains("fn main()", Names(items));
    }

    // ── Quick info ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("println!", RustSymbolKind.Macro)]
    [InlineData("fn", RustSymbolKind.Keyword)]
    [InlineData("HashMap", RustSymbolKind.Type)]
    [InlineData("Iterator", RustSymbolKind.Trait)]
    [InlineData("unwrap", RustSymbolKind.Method)]
    [InlineData("String::new", RustSymbolKind.Function)]
    [InlineData("std::mem::swap", RustSymbolKind.Function)]
    [InlineData("mem::swap", RustSymbolKind.Function)]
    [InlineData("Some", RustSymbolKind.Variant)]
    public void TheEverydayNames_HaveHoverText(string symbol, RustSymbolKind kind)
    {
        var info = RustQuickInfoProvider.Lookup(symbol);

        Assert.NotNull(info);
        Assert.Equal(kind, info.Kind);
        Assert.False(string.IsNullOrWhiteSpace(info.Signature));
        Assert.False(string.IsNullOrWhiteSpace(info.Summary));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("my_own_function")]
    public void AnUnknownName_HasNoHoverText(string symbol) => Assert.Null(RustQuickInfoProvider.Lookup(symbol));

    [Fact]
    public void AKeyword_KeepsItsLanguageMeaning_OverAMethodOfTheSameName()
    {
        // "in", "as" and "move" are keywords; "join"/"split" are methods only.
        Assert.Equal(RustSymbolKind.Keyword, RustQuickInfoProvider.Lookup("as")!.Kind);
        Assert.Equal(RustSymbolKind.Method, RustQuickInfoProvider.Lookup("join")!.Kind);
    }

    [Fact]
    public void ExtractSymbol_FindsTheWordUnderThePointer()
    {
        const string code = "let n = value.unwrap();";

        var symbol = RustQuickInfoProvider.ExtractSymbol(code, code.IndexOf("wrap", StringComparison.Ordinal));

        Assert.Equal(new RustQuickInfoProvider.SymbolAt("unwrap", code.IndexOf("unwrap", StringComparison.Ordinal), 6), symbol);
    }

    [Fact]
    public void ExtractSymbol_IncludesTheBangOfAMacro()
    {
        const string code = "    println!(\"x\");";

        var atName = RustQuickInfoProvider.ExtractSymbol(code, code.IndexOf("ntln", StringComparison.Ordinal));
        var atBang = RustQuickInfoProvider.ExtractSymbol(code, code.IndexOf('!'));

        Assert.Equal(new RustQuickInfoProvider.SymbolAt("println!", 4, 8), atName);
        Assert.Equal(atName, atBang);
    }

    [Fact]
    public void ExtractSymbol_JoinsATypePathTheTableKnows()
    {
        const string code = "let v = Vec::with_capacity(4);";

        var atFunction = RustQuickInfoProvider.ExtractSymbol(code, code.IndexOf("with", StringComparison.Ordinal));
        var atType = RustQuickInfoProvider.ExtractSymbol(code, code.IndexOf("Vec", StringComparison.Ordinal));

        Assert.Equal("Vec::with_capacity", atFunction!.Text);
        Assert.Equal(code.IndexOf("Vec", StringComparison.Ordinal), atFunction.Start);
        Assert.Equal("Vec::with_capacity".Length, atFunction.Length);
        Assert.Equal("Vec", atType!.Text);
    }

    [Fact]
    public void ExtractSymbol_JoinsALongPath_AndFallsBackToTheBareNameForAnUnknownPath()
    {
        const string code = "std::mem::swap(&mut a, &mut b); my::module::thing();";

        var swap = RustQuickInfoProvider.ExtractSymbol(code, code.IndexOf("swap", StringComparison.Ordinal));
        var thing = RustQuickInfoProvider.ExtractSymbol(code, code.IndexOf("thing", StringComparison.Ordinal));

        Assert.Equal("std::mem::swap", swap!.Text);
        Assert.Equal("thing", thing!.Text);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("   ", 1)]
    [InlineData("a + b", 2)]
    [InlineData("abc", 99)]
    public void ExtractSymbol_ReturnsNothingWhereThereIsNoWord(string code, int position) =>
        Assert.Null(RustQuickInfoProvider.ExtractSymbol(code, position));

    [Fact]
    public void TheHoverLookup_AnswersForTheFactory()
    {
        const string code = "let m: HashMap<i32, i32> = HashMap::new();";

        var hit = RustEditorAssistantFactory.Resolve(code, code.IndexOf("HashMap", StringComparison.Ordinal));
        var none = RustEditorAssistantFactory.Resolve(code, code.IndexOf("m:", StringComparison.Ordinal));

        Assert.NotNull(hit);
        Assert.Equal("struct HashMap<K, V>", hit.Signature);
        Assert.Equal("std::collections", hit.Container);
        Assert.Equal(code.IndexOf("HashMap", StringComparison.Ordinal), hit.SpanStart);
        Assert.Equal("HashMap".Length, hit.SpanLength);
        Assert.Null(none);
    }

    [Fact]
    public void TheHoverLookup_LabelsAModuleAsOne()
    {
        var hit = RustEditorAssistantFactory.Resolve("std::mem::swap(&mut a, &mut b);", 10);

        Assert.NotNull(hit);
        Assert.Equal("std::mem", hit.Container);
    }

    [Fact]
    public void TheStandardLibraryTable_HasNoDuplicateKeywordsMacrosTypesOrTraits()
    {
        foreach (var list in new[] { RustStandardLibrary.Keywords, RustStandardLibrary.Macros, RustStandardLibrary.Types, RustStandardLibrary.Traits })
        {
            Assert.Equal(list.Count, list.Select(s => s.Name).Distinct().Count());
        }

        Assert.All(RustStandardLibrary.Methods, m => Assert.False(string.IsNullOrWhiteSpace(m.Owner)));
    }

    [Fact]
    public void TheLanguage_UsesTheseAssistants()
    {
        var services = new StudioLanguageServices(Path.Combine(Path.GetTempPath(), "FryPDF_RustAssist_" + Guid.NewGuid().ToString("N")));
        var language = services.Registry.Get(LanguageIds.Rust)!;

        Assert.Same(RustEditorAssistantFactory.Instance, language.EditorAssistants);
        Assert.True(language.Capabilities.HasFlag(LanguageCapabilities.Completion));
        Assert.True(language.Capabilities.HasFlag(LanguageCapabilities.QuickInfo));
    }
}
