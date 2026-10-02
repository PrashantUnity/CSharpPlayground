using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>A notebook cell taken apart into the items later cells keep and the statements that run once.</summary>
public class RustCellSplitterTests
{
    private static RustCell Split(string code) => RustCellSplitter.Split(code);

    [Fact]
    public void ItemsAndStatements_AreToldApart_InTheOrderTheyAppear()
    {
        var cell = Split("""
            use std::collections::HashMap;

            #[derive(Debug)]
            struct Point { x: i32, y: i32 }

            fn add(a: i32, b: i32) -> i32 { a + b }

            let p = Point { x: 1, y: 2 };
            println!("{:?}", p);
            add(1, 2)
            """);

        Assert.Equal(["use", "struct", "fn"], cell.Items.Select(i => i.ItemKind));
        Assert.Equal([null, "Point", "add"], cell.Items.Select(i => i.Name));
        Assert.Equal(3, cell.Statements.Count());
        Assert.True(cell.Statements.Last().CanBeShownValue);
        Assert.False(cell.Statements.First().CanBeShownValue);
        Assert.Contains("#[derive(Debug)]", cell.Items.ElementAt(1).Text);
    }

    [Fact]
    public void EachPiece_KnowsItsLineAndColumn()
    {
        var cell = Split("let a = 1; let b = 2;\n\n  fn f() {}\nb");

        var chunks = cell.Chunks.ToList();
        Assert.Equal((1, 0), (chunks[0].StartLine, chunks[0].StartColumn));
        Assert.Equal((1, 11), (chunks[1].StartLine, chunks[1].StartColumn));
        Assert.Equal((3, 2), (chunks[2].StartLine, chunks[2].StartColumn));
        Assert.Equal((4, 0), (chunks[3].StartLine, chunks[3].StartColumn));
    }

    [Fact]
    public void ABraceOrSemicolon_InsideAStringCharOrComment_IsNotTheEndOfAPiece()
    {
        var cell = Split("""
            let s = "a; b } {";
            let c = ';';
            let q = '\'';
            let raw = r#"end "; } here"#;
            // a comment with } and ;
            /* a /* nested } */ ; comment */
            let last = 1;
            """);

        Assert.Equal(5, cell.Statements.Count());
        Assert.Empty(cell.Items);
        Assert.Contains("/* a /* nested } */ ; comment */", cell.Statements.ElementAt(4).Text);
    }

    [Fact]
    public void ALifetime_IsNotACharacterLiteral()
    {
        var cell = Split("fn longest<'a>(x: &'a str, y: &'a str) -> &'a str { if x.len() > y.len() { x } else { y } }\nlet c = 'a';\nlongest(\"ab\", \"c\")");

        Assert.Equal("longest", Assert.Single(cell.Items).Name);
        Assert.Equal(2, cell.Statements.Count());
    }

    [Fact]
    public void AnIfElseChain_IsOneStatement_AndCanBeTheShownValue()
    {
        var cell = Split("let x = 3;\nif x > 2 { \"big\" } else if x > 1 { \"mid\" } else { \"small\" }");

        Assert.Equal(2, cell.Statements.Count());
        Assert.True(cell.Statements.Last().CanBeShownValue);
        Assert.StartsWith("if x > 2", cell.Statements.Last().Text);
    }

    [Fact]
    public void ABlockLikeStatement_EndsAtItsClosingBrace_WithoutASemicolon()
    {
        var cell = Split("for i in 0..3 { println!(\"{}\", i); }\nlet total = 5;\nmatch total { 5 => println!(\"five\"), _ => {} }\nprintln!(\"done\");");

        Assert.Equal(4, cell.Statements.Count());
        Assert.False(cell.Statements.Last().CanBeShownValue);
    }

    [Theory]
    [InlineData("fn f() {}", "fn", "f")]
    [InlineData("pub fn f() -> i32 { 1 }", "fn", "f")]
    [InlineData("pub(crate) fn f() {}", "fn", "f")]
    [InlineData("const fn f() -> i32 { 1 }", "fn", "f")]
    [InlineData("unsafe fn f() {}", "fn", "f")]
    [InlineData("async fn f() {}", "fn", "f")]
    [InlineData("extern \"C\" fn f() {}", "fn", "f")]
    [InlineData("struct S { a: i32 }", "struct", "S")]
    [InlineData("struct T(i32);", "struct", "T")]
    [InlineData("struct U;", "struct", "U")]
    [InlineData("enum E { A, B(i32) }", "enum", "E")]
    [InlineData("trait Shape { fn area(&self) -> f64; }", "trait", "Shape")]
    [InlineData("union U2 { a: u32, b: f32 }", "union", "U2")]
    [InlineData("mod inner { pub fn f() {} }", "mod", "inner")]
    [InlineData("type Pair = (i32, i32);", "type", "Pair")]
    [InlineData("const MAX: usize = 10;", "const", "MAX")]
    [InlineData("const TABLE: [i32; 3] = [1, 2, 3];", "const", "TABLE")]
    [InlineData("static NAME: &str = \"x\";", "static", "NAME")]
    [InlineData("static mut COUNT: u32 = 0;", "static", "COUNT")]
    [InlineData("macro_rules! twice { ($e:expr) => { $e * 2 }; }", "macro_rules", "twice")]
    [InlineData("extern crate rand;", "extern crate", "rand")]
    [InlineData("use std::{fmt, io::Read};", "use", null)]
    [InlineData("impl S { fn m(&self) {} }", "impl", null)]
    [InlineData("impl<T: Clone> Wrapper<T> where T: Sized { fn get(&self) -> T { todo!() } }", "impl", null)]
    [InlineData("unsafe impl Send for S {}", "impl", null)]
    public void EveryKindOfItem_IsRecognised_WithItsName(string code, string kind, string? name)
    {
        var item = Assert.Single(Split(code).Chunks);

        Assert.Equal(RustChunkKind.Item, item.Kind);
        Assert.Equal(kind, item.ItemKind);
        Assert.Equal(name, item.Name);
    }

    [Theory]
    [InlineData("let x = 1;")]
    [InlineData("x + 1")]
    [InlineData("unsafe { std::ptr::null::<i32>(); }")]
    [InlineData("{ let y = 2; }")]
    [InlineData("const { 1 + 1 };")]
    [InlineData("vec![1, 2, 3]")]
    [InlineData("Point { x: 1, y: 2 }")]
    [InlineData("println!(\"hi\")")]
    [InlineData("async { 1 };")]
    public void Statements_AreNotMistakenForItems(string code)
    {
        var chunk = Assert.Single(Split(code).Chunks);

        Assert.Equal(RustChunkKind.Statement, chunk.Kind);
    }

    [Fact]
    public void ADocCommentAndAttributes_BelongToTheItemTheyPrecede()
    {
        var cell = Split("/// Adds.\n#[inline]\n#[allow(unused)]\npub fn add(a: i32, b: i32) -> i32 { a + b }\nadd(1, 2)");

        var item = Assert.Single(cell.Items);
        Assert.StartsWith("/// Adds.", item.Text);
        Assert.Equal((1, 0), (item.StartLine, item.StartColumn));
        Assert.Single(cell.Statements);
    }

    [Fact]
    public void AnInnerAttribute_IsItsOwnPiece()
    {
        var cell = Split("#![allow(dead_code)]\nfn main() {}");

        Assert.Equal(RustChunkKind.InnerAttribute, cell.Chunks[0].Kind);
        Assert.True(cell.DefinesMain);
    }

    [Fact]
    public void TheKeyOfAType_IsItsName_WhateverItsKind()
    {
        Assert.Equal(Split("struct Foo;").Items.Single().Key, Split("enum Foo { A }").Items.Single().Key);
        Assert.NotEqual(Split("struct Foo;").Items.Single().Key, Split("struct Bar;").Items.Single().Key);
        Assert.Equal(Split("fn go() {}").Items.Single().Key, Split("const go: i32 = 1;").Items.Single().Key);
    }

    [Fact]
    public void AMacroWithBraces_IsAStatementOfItsOwn_AndNeverTheShownValue()
    {
        var cell = Split("thread_local! { static X: i32 = 1; }\nlet a = 2;");

        Assert.Equal(2, cell.Statements.Count());
        Assert.True(cell.Statements.First().IsBraceMacro);
        Assert.False(cell.Statements.First().CanBeShownValue);
    }

    [Theory]
    [InlineData("println!(\"hi\");")]
    [InlineData("vec![1, 2, 3]")]
    [InlineData("dbg!(x);")]
    [InlineData("assert_eq!(1, 1);")]
    [InlineData("format!(\"{}\", 1)")]
    [InlineData("fry::dump!(value);")]
    [InlineData("std::println!(\"qualified\");")]
    public void ACodeMacro_IsAStatement_EvenWhenUnknownMacrosAreTriedAsItems(string code)
    {
        var chunk = Assert.Single(RustCellSplitter.Split(code, MacroPlacement.ItemsWhenUnknown).Chunks);

        Assert.Equal(RustChunkKind.Statement, chunk.Kind);
    }

    [Theory]
    [InlineData("impl_answer!(i32, u8);")]
    [InlineData("thread_local! { static COUNTER: RefCell<i32> = RefCell::new(0); }")]
    [InlineData("lazy_static! { static ref TABLE: Vec<i32> = vec![1]; }")]
    [InlineData("my_crate::make_types![A, B];")]
    public void AnUnknownMacroCall_IsAStatementByDefault_AndAnItemWhenTriedAsOne(string code)
    {
        var asStatement = Assert.Single(RustCellSplitter.Split(code).Chunks);
        var asItem = RustCellSplitter.Split(code, MacroPlacement.ItemsWhenUnknown);

        Assert.Equal(RustChunkKind.Statement, asStatement.Kind);
        var chunk = Assert.Single(asItem.Chunks);
        Assert.Equal(RustChunkKind.Item, chunk.Kind);
        Assert.Equal("macro call", chunk.ItemKind);
        Assert.True(asItem.HasUncertainMacros);
        Assert.False(RustCellSplitter.Split(code).HasUncertainMacros);
    }

    [Fact]
    public void AMacroCallItem_EndsWhereItsBracesOrItsSemicolonDo()
    {
        var cell = RustCellSplitter.Split("impl_answer!(i32);\nthread_local! { static X: i32 = 1; }\nlet a = 1;\nmake!(A, B);", MacroPlacement.ItemsWhenUnknown);

        Assert.Equal(["macro call", "macro call", null, "macro call"], cell.Chunks.Select(c => c.ItemKind));
        Assert.Equal(RustChunkKind.Statement, cell.Chunks[2].Kind);
    }

    [Theory]
    [InlineData("if !(a && b) { run(); }")]
    [InlineData("while !(done) { step(); }")]
    [InlineData("let ok = x != y;")]
    [InlineData("return !(a);")]
    public void ANegationThatLooksLikeAMacroCall_IsNot(string code)
    {
        var cell = RustCellSplitter.Split(code, MacroPlacement.ItemsWhenUnknown);

        Assert.False(cell.HasUncertainMacros);
        Assert.All(cell.Chunks, c => Assert.Equal(RustChunkKind.Statement, c.Kind));
    }

    [Fact]
    public void TheSameMacroCall_TwiceIsOneItem()
    {
        var store = new RustItemStore();
        store.Apply(RustCellSplitter.Split("impl_answer!(i32);", MacroPlacement.ItemsWhenUnknown).Items);
        store.Apply(RustCellSplitter.Split("impl_answer!( i32 );", MacroPlacement.ItemsWhenUnknown).Items);

        Assert.Equal(2, store.Items.Count);
        store.Apply(RustCellSplitter.Split("impl_answer!(i32);", MacroPlacement.ItemsWhenUnknown).Items);
        Assert.Equal(2, store.Items.Count);
    }

    [Fact]
    public void ALetWithoutItsSemicolon_IsRecognised()
    {
        var chunk = Assert.Single(Split("let x = 5").Chunks);

        Assert.True(chunk.StartsWithLet);
        Assert.False(chunk.EndsWithSemicolon);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n\n  ")]
    [InlineData("// only a comment\n/* and another */")]
    public void ACellWithNoCode_HasNoPieces(string code)
    {
        Assert.Empty(Split(code).Chunks);
    }

    [Fact]
    public void WindowsLineEndings_AreTheSameAsUnix()
    {
        var unix = Split("let a = 1;\nlet b = 2;\nfn f() {}");
        var windows = Split("let a = 1;\r\nlet b = 2;\r\nfn f() {}");

        Assert.Equal(unix.Chunks.Select(c => (c.Kind, c.StartLine, c.StartColumn, c.Text)), windows.Chunks.Select(c => (c.Kind, c.StartLine, c.StartColumn, c.Text)));
    }

    [Fact]
    public void AnUnfinishedCell_DoesNotHangOrThrow()
    {
        foreach (var code in new[] { "fn f() {", "let s = \"never closed", "let c = 'x", "r#\"raw", "impl X {", "if x {" })
        {
            var cell = Split(code);
            Assert.NotEmpty(cell.Chunks);
        }

        Assert.Empty(Split("/* never closed").Chunks); // only a comment
    }
}

/// <summary>A cell laid out as a program: what goes where, and the way back to the cell's own lines.</summary>
public class RustCellProgramTests
{
    private static RustCellProgram Build(string code, IReadOnlyList<RustStoredItem>? earlier = null, IReadOnlyList<string>? shares = null, IReadOnlyList<string>? persist = null) =>
        RustCellProgramBuilder.Build(RustCellSplitter.Split(code), earlier ?? [], shares ?? [], persist);

    private static string[] Lines(RustCellProgram program) => program.Source.Split('\n');

    [Fact]
    public void TheProgram_HasItsHeader_ThePreludeTheItemsAndAMain()
    {
        var program = Build("fn add(a: i32, b: i32) -> i32 { a + b }\nlet x = add(1, 2);\nprintln!(\"{}\", x);");

        var lines = Lines(program);
        Assert.Equal(RustCellProgramBuilder.Header, lines[0]);
        Assert.Contains("use fry::prelude::*;", lines);
        Assert.Contains("fn add(a: i32, b: i32) -> i32 { a + b }", lines);
        Assert.Contains(lines, l => l.StartsWith("fn main() -> ::std::result::Result<(), ::std::boxed::Box<dyn ::std::error::Error>> {", StringComparison.Ordinal));
        Assert.Contains("    ::std::result::Result::Ok(())", lines);
        Assert.True(Array.IndexOf(lines, "fn add(a: i32, b: i32) -> i32 { a + b }") < Array.FindIndex(lines, l => l.StartsWith("fn main", StringComparison.Ordinal)));
    }

    [Fact]
    public void ALineOfTheCell_IsTheSameLineAndColumnInTheProgram()
    {
        const string code = "fn helper() -> i32 {\n    7\n}\n\nlet a = helper();\n    let b = a + 1; let c = b + 1;\nc";
        var program = Build(code);
        var cellLines = code.Split('\n');
        var lines = Lines(program);

        // Line 6 holds two statements, which become two lines of the program, and line 7 is the shown value (see the next test).
        foreach (var cellLine in new[] { 1, 2, 3, 5 })
        {
            var generatedLine = Enumerable.Range(1, lines.Length).First(g => program.CellLineOf(g) == cellLine);
            Assert.Contains(cellLines[cellLine - 1].TrimStart(), lines[generatedLine - 1]);
        }

        // "let c" starts at column 19 of cell line 6, and at column 19 of its program line
        var c = Enumerable.Range(1, lines.Length).First(g => lines[g - 1].Contains("let c = b + 1;", StringComparison.Ordinal));
        Assert.Equal(19, lines[c - 1].IndexOf("let c", StringComparison.Ordinal));
        Assert.Equal(6, program.CellLineOf(c));
    }

    [Fact]
    public void TheLinesOfTheScaffolding_AreNotTheCells()
    {
        var program = Build("let x = 1;");

        Assert.Null(program.CellLineOf(1)); // the header
        Assert.Null(program.CellLineOf(Lines(program).Length + 5));
    }

    [Fact]
    public void ALastExpressionWithoutASemicolon_IsShown()
    {
        var program = Build("let x = 6;\nx * 7");

        var lines = Lines(program);
        var open = Array.FindIndex(lines, l => l.Contains("let __fry_last = {", StringComparison.Ordinal));
        Assert.True(open > 0);
        Assert.Equal("x * 7", lines[open + 1]);
        Assert.Equal("    };", lines[open + 2]);
        Assert.Equal("    fry::__auto!(__fry_last);", lines[open + 3]);
        Assert.Equal(2, program.CellLineOf(open + 2));
    }

    [Fact]
    public void AStatementWithASemicolon_IsNotShown()
    {
        Assert.DoesNotContain("__auto", Build("let x = 6;\nx * 7;").Source);
    }

    [Fact]
    public void ALetWithoutASemicolon_GetsOne_AndIsNotShown()
    {
        var program = Build("let x = 5");

        Assert.DoesNotContain("__auto", program.Source);
        Assert.Contains("let x = 5\n;", program.Source);
    }

    [Fact]
    public void TheSharedValues_AreDeclaredFirstInMain()
    {
        var program = Build("nums.len()", shares: ["let nums: Vec<i64> = vec![1, 2, 3];"]);

        var lines = Lines(program);
        var main = Array.FindIndex(lines, l => l.StartsWith("fn main", StringComparison.Ordinal));
        Assert.Equal("    let nums: Vec<i64> = vec![1, 2, 3];", lines[main + 1]);
    }

    [Fact]
    public void EarlierItems_ComeFirst_ButNotThoseTheCellDefinesAgain()
    {
        var earlier = new[]
        {
            new RustStoredItem("value:old", "fn", "old", "fn old() {}"),
            new RustStoredItem("value:redefined", "fn", "redefined", "fn redefined() { /* the old one */ }")
        };

        var program = Build("fn redefined() { /* the new one */ }\nredefined()", earlier);

        Assert.Contains("fn old() {}", program.Source);
        Assert.DoesNotContain("the old one", program.Source);
        Assert.Contains("the new one", program.Source);
    }

    [Fact]
    public void ACellThatDefinesMain_IsAWholeProgram_AndCantAlsoHaveStatements()
    {
        var whole = Build("fn main() { println!(\"hi\"); }");
        Assert.Null(whole.Error);
        Assert.DoesNotContain("fn main() ->", whole.Source);

        var mixed = Build("let x = 1;\nfn main() { }");
        Assert.NotNull(mixed.Error);
        Assert.Contains("fn main", mixed.Error);
    }

    [Fact]
    public void ACellOfOnlyItems_StillHasAnEmptyMain()
    {
        var program = Build("fn only() {}");

        Assert.Contains("fn main()", program.Source);
    }

    [Fact]
    public void InnerAttributes_GoAboveEverything()
    {
        var lines = Lines(Build("#![allow(dead_code)]\nlet x = 1;"));

        Assert.Equal(RustCellProgramBuilder.Header, lines[0]);
        Assert.Equal("#![allow(dead_code)]", lines[1]);
        Assert.True(Array.IndexOf(lines, "use fry::prelude::*;") > 1);
    }

    [Fact]
    public void AnInnerDocComment_IsMadeAnOrdinaryOne_WithTheSameColumns()
    {
        var program = Build("//! about this cell\nlet x = 1;");

        Assert.Contains("//  about this cell", program.Source); // "//!" became "// ": three characters, so columns hold
        Assert.DoesNotContain("//!", program.Source);
    }
}

/// <summary>What the notebook remembers between cells.</summary>
public class RustItemStoreTests
{
    private static IEnumerable<RustCellChunk> Items(string code) => RustCellSplitter.Split(code).Items;

    private static RustItemStore StoreWith(string code)
    {
        var store = new RustItemStore();
        store.Apply(Items(code));
        return store;
    }

    [Fact]
    public void AnItem_IsKeptForLaterCells_InTheOrderItWasDefined()
    {
        var store = StoreWith("fn a() {}\nstruct B;");
        store.Apply(Items("fn c() {}"));

        Assert.Equal(["a", "B", "c"], store.Items.Select(i => i.Name));
    }

    [Fact]
    public void ADefinitionMadeAgain_ReplacesTheOldOneWhereItStood()
    {
        var store = StoreWith("fn a() { 1 }\nfn b() {}");
        store.Apply(Items("fn a() { 2 }"));

        Assert.Equal(["a", "b"], store.Items.Select(i => i.Name));
        Assert.Contains("2", store.Items.First().Text);
    }

    [Fact]
    public void ARedefinedType_DropsTheOlderImplsThatNameIt_ButNotOtherImpls()
    {
        var store = StoreWith("struct Point { x: i32 }\nimpl Point { fn x(&self) -> i32 { self.x } }\nimpl Display for Point {}\nstruct Other;\nimpl Other {}");
        store.Apply(Items("struct Point { x: i32, y: i32 }"));

        Assert.Equal(["Point", "Other"], store.Items.Where(i => i.Kind != "impl").Select(i => i.Name));
        Assert.DoesNotContain(store.Items, i => i.Kind == "impl" && i.Text.Contains("Point", StringComparison.Ordinal));
        Assert.Contains(store.Items, i => i.Kind == "impl" && i.Text.Contains("Other", StringComparison.Ordinal));
    }

    [Fact]
    public void ATypeRunAgainUnchanged_KeepsItsImpls()
    {
        var store = StoreWith("struct Point { x: i32 }\nimpl Point { fn x(&self) -> i32 { self.x } }");
        store.Apply(Items("struct   Point {\n    x: i32\n}"));

        Assert.Contains(store.Items, i => i.Kind == "impl");
    }

    [Fact]
    public void ATypeAndItsImpls_InOneCell_AreKept_WhateverOrderTheyAreWrittenIn()
    {
        var store = StoreWith("struct Point { x: i32 }");
        store.Apply(Items("impl Point { fn new() -> Point { Point { x: 0 } } }\nstruct Point { x: i32, y: i32 }"));

        Assert.Contains(store.Items, i => i.Kind == "impl");
        Assert.Contains(store.Items, i => i.Kind == "struct" && i.Text.Contains("y: i32", StringComparison.Ordinal));
    }

    [Fact]
    public void TheSameUseOrImpl_TwiceIsOneDefinition()
    {
        var store = StoreWith("use std::fmt;\nimpl Foo {}");
        store.Apply(Items("use   std::fmt;\nimpl Foo {}"));

        Assert.Equal(2, store.Items.Count);
    }

    [Fact]
    public void FnMain_IsNeverKept()
    {
        var store = StoreWith("fn main() {}\nfn helper() {}");

        Assert.Equal(["helper"], store.Items.Select(i => i.Name));
    }

    [Fact]
    public void ACloneIsIndependent()
    {
        var store = StoreWith("fn a() {}");
        var copy = store.Clone();
        copy.Apply(Items("fn b() {}"));

        Assert.Single(store.Items);
        Assert.Equal(2, copy.Items.Count);
    }
}

/// <summary>A shared value written as Rust.</summary>
public class RustValueLiteralsTests
{
    [Theory]
    [InlineData("true", "let v: bool = true;")]
    [InlineData("42", "let v: i64 = 42;")]
    [InlineData("-7", "let v: i64 = -7;")]
    [InlineData("2.5", "let v: f64 = 2.5;")]
    [InlineData("3.0", "let v: f64 = 3.0;")]
    [InlineData("1e3", "let v: f64 = 1000.0;")]
    [InlineData("18446744073709551615", "let v: u64 = 18446744073709551615;")]
    [InlineData("\"hi\"", "let v: String = String::from(\"hi\");")]
    [InlineData("null", "let v: Option<f64> = None;")]
    [InlineData("[]", "let v: Vec<i64> = Vec::new();")]
    [InlineData("[1,2,3]", "let v: Vec<i64> = vec![1, 2, 3];")]
    [InlineData("[1,2.5]", "let v: Vec<f64> = vec![1.0, 2.5];")]
    [InlineData("[\"a\",\"b\"]", "let v: Vec<String> = vec![String::from(\"a\"), String::from(\"b\")];")]
    [InlineData("[[1,2],[3]]", "let v: Vec<Vec<i64>> = vec![vec![1, 2], vec![3]];")]
    [InlineData("[[1],[2.5]]", "let v: Vec<Vec<f64>> = vec![vec![1.0], vec![2.5]];")]
    [InlineData("[true,false]", "let v: Vec<bool> = vec![true, false];")]
    public void AJsonValue_BecomesTheRustTypeThatFitsIt(string json, string expected)
    {
        Assert.Equal(expected, RustValueLiterals.Declare("v", json));
    }

    [Theory]
    [InlineData("{\"a\":1}")]
    [InlineData("[1,\"x\"]")]
    [InlineData("[1,null]")]
    public void AnObjectOrAMixedList_IsAJsonValue(string json)
    {
        var declaration = RustValueLiterals.Declare("v", json);

        Assert.StartsWith("let v: fry::Json = fry::Json::parse(r#\"", declaration);
        Assert.EndsWith("\"#);", declaration);
        Assert.Equal("fry::Json", RustValueLiterals.TypeOf(json));
    }

    [Fact]
    public void AListOfObjects_IsAListOfJsonValues()
    {
        Assert.Equal("let v: Vec<fry::Json> = vec![fry::Json::parse(r#\"{\"a\":1}\"#), fry::Json::parse(r#\"{\"a\":2}\"#)];",
            RustValueLiterals.Declare("v", "[{\"a\":1},{\"a\":2}]"));
    }

    [Fact]
    public void ARawString_HasAsManyHashesAsTheTextNeeds()
    {
        Assert.Equal("r#\"{}\"#", RustValueLiterals.RawString("{}"));
        Assert.Equal("r##\"a\"#b\"##", RustValueLiterals.RawString("a\"#b"));
    }

    [Fact]
    public void AString_IsEscapedForRust()
    {
        Assert.Equal("let s: String = String::from(\"a\\\"b\\\\c\\nd\\te\\u{7}\");", RustValueLiterals.Declare("s", "\"a\\\"b\\\\c\\nd\\te\\u0007\""));
        Assert.Equal("let s: String = String::from(\"héllo → ✓\");", RustValueLiterals.Declare("s", "\"héllo → ✓\""));
    }

    [Theory]
    [InlineData("type", "r#type")]
    [InlineData("match", "r#match")]
    [InlineData("nums", "nums")]
    [InlineData("_x1", "_x1")]
    [InlineData("héllo", "héllo")]
    public void AKeywordAsAName_IsARawIdentifier(string name, string expected)
    {
        Assert.Equal(expected, RustValueLiterals.VariableName(name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1abc")]
    [InlineData("my-var")]
    [InlineData("a b")]
    [InlineData("self")]
    [InlineData("_")]
    public void ANameRustCantUse_IsRefusedWithAMessage(string name)
    {
        var ex = Assert.Throws<KernelValueException>(() => RustValueLiterals.VariableName(name));

        Assert.Contains("--as", ex.Message);
    }

    [Fact]
    public void JsonThatIsNotJson_IsRefusedWithAMessage()
    {
        var ex = Assert.Throws<KernelValueException>(() => RustValueLiterals.Declare("v", "{oops"));

        Assert.Contains("isn't valid JSON", ex.Message);
    }
}

/// <summary>The variables a <c>let</c> makes, when they are plain names.</summary>
public class RustLetNamesTests
{
    [Theory]
    [InlineData("let x = 5;", "x")]
    [InlineData("let mut total: u64 = 0;", "total")]
    [InlineData("let v: Vec<(i32, i32)> = Vec::new();", "v")]
    [InlineData("let (a, mut b) = (1, 2);", "a,b")]
    [InlineData("let ((a, b), c): ((i32, i32), i32) = ((1, 2), 3);", "a,b,c")]
    [InlineData("let x;", "x")]
    [InlineData("let x = a == b;", "x")]
    [InlineData("let r#type = 1;", "r#type")]
    [InlineData("let ref name = value;", "name")]
    [InlineData("let _ = compute();", "")]
    [InlineData("let Point { x, y } = p;", "")]
    [InlineData("let Some(v) = o else { return Ok(()); };", "")]
    [InlineData("let [a, b] = arr;", "")]
    [InlineData("let a::b = c;", "")]
    [InlineData("let x = 5", "x")]
    [InlineData("println!(\"let x = 1\");", "")]
    [InlineData("letter = 5;", "")]
    public void TheNamesAreThePlainOnes_AndAnyOtherPatternIsLeftAlone(string statement, string expected)
    {
        Assert.Equal(expected, string.Join(',', RustLetNames.Of(statement)));
    }
}

/// <summary>What a cell hands on to the next: a line per variable at the end of <c>main</c>, and declarations at its start.</summary>
public class RustCellPersistenceProgramTests
{
    [Fact]
    public void EachVariableToKeep_HasALineAtTheEndOfMain_AndTheLineIsKnown()
    {
        var program = RustCellProgramBuilder.Build(RustCellSplitter.Split("let a = 1;\nlet mut b = vec![a];\nb.push(2);"), [], [], ["a", "b"]);
        var lines = program.Source.Split('\n');

        Assert.Equal(2, program.PersistLines.Count);
        foreach (var (line, name) in program.PersistLines) Assert.Equal($"    fry::__persist!({name});", lines[line - 1]);
        var last = lines.Select((l, i) => (l, i)).Where(t => t.l.Contains("__persist!")).Max(t => t.i);
        Assert.Contains("Ok(())", lines[last + 1]);
    }

    [Fact]
    public void ACellThatDefinesMain_KeepsNothing()
    {
        var program = RustCellProgramBuilder.Build(RustCellSplitter.Split("fn main() { let a = 1; }"), [], [], ["a"]);

        Assert.Empty(program.PersistLines);
        Assert.DoesNotContain("__persist", program.Source);
    }

    [Fact]
    public void WithNothingToKeep_TheProgramIsAsItWas()
    {
        var program = RustCellProgramBuilder.Build(RustCellSplitter.Split("println!(\"hi\");"), [], []);

        Assert.Empty(program.PersistLines);
        Assert.DoesNotContain("__persist", program.Source);
    }
}
