using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;
using Xunit;

namespace CSharpEditorPlugin.Tests;

// The samples are real rustc / cargo 1.94 output (paths shortened).
public class RustDiagnosticParserTests : IDisposable
{
    private readonly RustDiagnosticParser _parser = new();
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_RustParser_" + Guid.NewGuid().ToString("N"));

    public RustDiagnosticParserTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void ATypeError_IsFoundWithItsColumnsAndLabel()
    {
        var result = _parser.Parse("""
               Compiling case_e0308_ab12cd v0.1.0 (/stage/e0308)
            error[E0308]: mismatched types
             --> /work/main.rs:2:18
              |
            2 |     let x: i32 = "not a number";
              |            ---   ^^^^^^^^^^^^^^ expected `i32`, found `&str`
              |            |
              |            expected due to this

            For more information about this error, try `rustc --explain E0308`.
            error: could not compile `case_e0308_ab12cd` (bin "case_e0308_ab12cd") due to 1 previous error
            """, "/work/main.rs");

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("E0308", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("mismatched types: expected `i32`, found `&str`", diagnostic.Message);
        Assert.Equal(2, diagnostic.Line);
        Assert.Equal(18, diagnostic.Column);
        Assert.Equal(2, diagnostic.EndLine);
        Assert.Equal(32, diagnostic.EndColumn);
        Assert.Null(result.MissingDependency);
    }

    [Fact]
    public void AnUnknownName_IsFound()
    {
        var result = _parser.Parse("""
            error[E0425]: cannot find value `undefined_var` in this scope
             --> /work/main.rs:2:20
              |
            2 |     println!("{}", undefined_var);
              |                    ^^^^^^^^^^^^^ not found in this scope

            For more information about this error, try `rustc --explain E0425`.
            error: could not compile `x` (bin "x") due to 1 previous error
            """, "/work/main.rs");

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("E0425", diagnostic.Id);
        Assert.Equal(20, diagnostic.Column);
        Assert.Equal(33, diagnostic.EndColumn);
        Assert.StartsWith("cannot find value `undefined_var` in this scope", diagnostic.Message);
    }

    [Fact]
    public void SeveralErrors_AreEachReported_AndAPointedAtNoteIsNotAnotherProblem()
    {
        var result = _parser.Parse("""
            error[E0308]: mismatched types
             --> /work/main.rs:3:20
              |
            3 |     let a = add(1, "2");
              |             ---    ^^^ expected `i32`, found `&str`
              |             |
              |             arguments to this function are incorrect
              |
            note: function defined here
             --> /work/main.rs:1:4
              |
            1 | fn add(a: i32, b: i32) -> i32 { a + b }
              |    ^^^         ------

            error[E0308]: mismatched types
             --> /work/main.rs:4:21
              |
            4 |     let b: String = add(1, 2);
              |            ------   ^^^^^^^^^ expected `String`, found `i32`
              |            |
              |            expected due to this
              |
            help: try using a conversion method
              |
            4 |     let b: String = add(1, 2).to_string();
              |                              ++++++++++++

            For more information about this error, try `rustc --explain E0308`.
            error: could not compile `case_multi` (bin "case_multi") due to 2 previous errors
            """, "/work/main.rs");

        Assert.Equal(2, result.Diagnostics.Count);
        Assert.Equal((3, 20, 23), (result.Diagnostics[0].Line, result.Diagnostics[0].Column, result.Diagnostics[0].EndColumn));
        Assert.Equal((4, 21, 30), (result.Diagnostics[1].Line, result.Diagnostics[1].Column, result.Diagnostics[1].EndColumn));
        Assert.Equal("mismatched types: expected `String`, found `i32`", result.Diagnostics[1].Message);
    }

    [Fact]
    public void AWarning_KeepsItsLintNameAndDropsTheHelpText()
    {
        var result = _parser.Parse("""
            warning: unused variable: `unused`
             --> /work/main.rs:3:9
              |
            3 |     let unused = 5;
              |         ^^^^^^ help: if this is intentional, prefix it with an underscore: `_unused`
              |
              = note: `#[warn(unused_variables)]` (part of `#[warn(unused)]`) on by default
            """, "/work/main.rs");

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal("unused_variables", diagnostic.Id);
        Assert.Equal("unused variable: `unused`", diagnostic.Message);
        Assert.Equal((3, 9, 15), (diagnostic.Line, diagnostic.Column, diagnostic.EndColumn));
    }

    [Fact]
    public void ASpanWithNoCarets_GetsOneColumn()
    {
        var result = _parser.Parse("""
            error: expected one of `!` or `::`, found `}`
             --> /work/main.rs:7:1
            """, "/work/main.rs");

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("RUSTC", diagnostic.Id);
        Assert.Equal((7, 1, 2), (diagnostic.Line, diagnostic.Column, diagnostic.EndColumn));
    }

    [Fact]
    public void AMissingCrate_IsOfferedForInstall_FromCargosOwnHint()
    {
        var result = _parser.Parse("""
            error[E0432]: unresolved import `rand`
             --> /work/main.rs:1:5
              |
            1 | use rand::Rng;
              |     ^^^^ use of unresolved module or unlinked crate `rand`
              |
              = help: if you wanted to use a crate named `rand`, use `cargo add rand` to add it to your `Cargo.toml`

            error[E0433]: failed to resolve: use of unresolved module or unlinked crate `rand`
             --> /work/main.rs:3:18
              |
            3 |     let n: u32 = rand::thread_rng().gen();
              |                  ^^^^ use of unresolved module or unlinked crate `rand`
              |
              = help: if you wanted to use a crate named `rand`, use `cargo add rand` to add it to your `Cargo.toml`

            Some errors have detailed explanations: E0432, E0433.
            error: could not compile `x` (bin "x") due to 2 previous errors
            """, "/work/main.rs");

        Assert.Equal(2, result.Diagnostics.Count);
        Assert.Equal((1, 5, 9), (result.Diagnostics[0].Line, result.Diagnostics[0].Column, result.Diagnostics[0].EndColumn));
        Assert.StartsWith("unresolved import `rand`", result.Diagnostics[0].Message);
        Assert.Equal("rand", result.MissingDependency);
    }

    [Fact]
    public void AMissingCrate_IsOfferedForInstall_FromAnOlderCompilersWording()
    {
        var result = _parser.Parse("""
            error[E0432]: unresolved import `rand`
             --> /work/main.rs:1:5
              |
            1 | use rand::Rng;
              |     ^^^^ maybe a missing crate `rand`?
              |
              = help: consider adding `extern crate rand` to use the `rand` crate
            """, "/work/main.rs");

        Assert.Equal("rand", result.MissingDependency);
    }

    [Fact]
    public void ACrateTheScriptAlreadyDeclares_IsNotOfferedAgain()
    {
        var path = Path.Combine(_dir, "main.rs");
        File.WriteAllText(path, "// #crate: rand = \"0.8\"\nuse rand::Rng;\nfn main() {}\n");

        var result = _parser.Parse($"""
            error[E0432]: unresolved import `rand`
             --> {path}:2:5
              |
            2 | use rand::Rng;
              |     ^^^^ use of unresolved module or unlinked crate `rand`
              |
              = help: if you wanted to use a crate named `rand`, use `cargo add rand` to add it to your `Cargo.toml`
            """, path);

        Assert.Single(result.Diagnostics);
        Assert.Null(result.MissingDependency);
    }

    [Theory]
    [InlineData("std")]
    [InlineData("core")]
    [InlineData("crate")]
    [InlineData("self")]
    public void TheStandardLibraryAndKeywords_AreNeverOfferedAsCrates(string name)
    {
        var result = _parser.Parse($"""
            error[E0432]: unresolved import `{name}`
             --> /work/main.rs:1:5
              |
            1 | use {name}::foo;
              |     ^^^^ use of unresolved module or unlinked crate `{name}`
            """, "/work/main.rs");

        Assert.Null(result.MissingDependency);
    }

    [Fact]
    public void APanic_IsPlacedOnItsLine_WithTheThreadIdOfNewerRust()
    {
        var result = _parser.Parse("""
            before

            thread 'main' (41616597) panicked at /work/main.rs:5:21:
            index out of bounds: the len is 3 but the index is 7
            note: run with `RUST_BACKTRACE=1` environment variable to display a backtrace
            """, "/work/main.rs");

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("PANIC", diagnostic.Id);
        Assert.Equal("panic: index out of bounds: the len is 3 but the index is 7", diagnostic.Message);
        Assert.Equal((5, 21, 22), (diagnostic.Line, diagnostic.Column, diagnostic.EndColumn));
    }

    [Fact]
    public void APanic_WithoutAThreadId_IsPlacedOnItsLine()
    {
        var result = _parser.Parse("""
            thread 'main' panicked at /work/main.rs:4:7:
            value should be present
            note: run with `RUST_BACKTRACE=1` environment variable to display a backtrace
            """, "/work/main.rs");

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("panic: value should be present", diagnostic.Message);
        Assert.Equal((4, 7), (diagnostic.Line, diagnostic.Column));
    }

    [Fact]
    public void APanic_InTheOlderSingleLineForm_IsPlacedOnItsLine()
    {
        var result = _parser.Parse("""
            thread 'main' panicked at 'explicit panic', /work/main.rs:2:5
            note: run with `RUST_BACKTRACE=1` environment variable to display a backtrace
            """, "/work/main.rs");

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("panic: explicit panic", diagnostic.Message);
        Assert.Equal((2, 5), (diagnostic.Line, diagnostic.Column));
    }

    [Fact]
    public void APanicMessage_ThatSpansLines_IsJoined()
    {
        var result = _parser.Parse("""
            thread 'main' (1) panicked at /work/main.rs:4:5:
            assertion `left == right` failed
              left: 1
             right: 2
            note: run with `RUST_BACKTRACE=1` environment variable to display a backtrace
            """, "/work/main.rs");

        Assert.Equal("panic: assertion `left == right` failed left: 1 right: 2", Assert.Single(result.Diagnostics).Message);
    }

    [Fact]
    public void APanic_InsideTheStandardLibrary_LandsOnTheScriptsFirstLine_WithItsRealPlace()
    {
        var result = _parser.Parse("""
            thread 'main' (1) panicked at /rustc/4a4ef493e3a1488c6e321570238084b38948f6db/library/core/src/option.rs:2027:5:
            called `Option::unwrap()` on a `None` value
            """, "/work/main.rs");

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal((1, 1), (diagnostic.Line, diagnostic.Column));
        Assert.StartsWith("panic in option.rs:2027:5: called `Option::unwrap()`", diagnostic.Message);
    }

    [Fact]
    public void AStackOverflow_IsReported()
    {
        var result = _parser.Parse("""
            thread 'main' (1) has overflowed its stack
            fatal runtime error: stack overflow, aborting
            """, "/work/main.rs");

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("STACK_OVERFLOW", diagnostic.Id);
        Assert.Contains("recursion", diagnostic.Message);
    }

    [Fact]
    public void AMainThatReturnsAnError_IsReported()
    {
        var result = _parser.Parse("working…\nError: ParseIntError { kind: InvalidDigit }\n", "/work/main.rs");

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("RUST_ERROR", diagnostic.Id);
        Assert.Equal("main returned an error: ParseIntError { kind: InvalidDigit }", diagnostic.Message);
    }

    [Fact]
    public void AProgramsOwnErrorMessage_IsNotMistakenForABuildError()
    {
        var result = _parser.Parse("error: bad input, expected a number\n", "/work/main.rs");

        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void AMissingLinker_ExplainsWhatToInstall_OnEveryOs()
    {
        var result = _parser.Parse("""
               Compiling case v0.1.0 (/stage)
            error: linker `fry-nonexistent-cc` not found
              |
              = note: No such file or directory (os error 2)

            error: could not compile `case` (bin "case") due to 1 previous error
            """, "/work/main.rs");

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("RUSTC_LINKER", diagnostic.Id);
        Assert.Contains("fry-nonexistent-cc", diagnostic.Message);
        Assert.Contains("xcode-select --install", diagnostic.Message);
        Assert.Contains("build-essential", diagnostic.Message);
        Assert.Contains("Visual Studio Build Tools", diagnostic.Message);
    }

    [Fact]
    public void AFailedLink_KeepsRustcsMessage_AndAddsTheHint()
    {
        var result = _parser.Parse("""
               Compiling case v0.1.0 (/stage)
            error: linking with `cc` failed: exit status: 1
              |
              = note: ld: library 'foo' not found
            """, "/work/main.rs");

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("RUSTC_LINKER", diagnostic.Id);
        Assert.StartsWith("linking with `cc` failed: exit status: 1.", diagnostic.Message);
    }

    [Fact]
    public void AnUnknownCrate_LandsOnItsCrateComment()
    {
        var path = Path.Combine(_dir, "main.rs");
        File.WriteAllText(path, "fn main() {}\n// filler\n// #crate: fry_no_such_crate_zzz = \"1\"\n");

        var result = _parser.Parse("""
                Updating crates.io index
            error: no matching package named `fry_no_such_crate_zzz` found
            location searched: crates.io index
            required by package `case_dep_bad v0.1.0 (/stage/dep_bad)`
            """, path);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("CARGO", diagnostic.Id);
        Assert.Equal(3, diagnostic.Line);
        Assert.Contains("no matching package named `fry_no_such_crate_zzz` found", diagnostic.Message);
        Assert.Contains("// #crate:", diagnostic.Message);
    }

    [Fact]
    public void AnImpossibleVersion_LandsOnItsCrateComment()
    {
        var path = Path.Combine(_dir, "main.rs");
        File.WriteAllText(path, "// #crate: serde = \"99\"\nfn main() {}\n");

        var result = _parser.Parse("""
                Updating crates.io index
            error: failed to select a version for the requirement `serde = "^99"`
            candidate versions found which didn't match: 1.0.228, 1.0.227, 1.0.226
            location searched: crates.io index
            """, path);

        Assert.Equal(1, Assert.Single(result.Diagnostics).Line);
    }

    [Fact]
    public void AnErrorInASiblingModule_IsShownOnTheScriptsFirstLine()
    {
        var result = _parser.Parse("""
            error[E0425]: cannot find value `y` in this scope
             --> /work/helper.rs:3:5
              |
            3 |     y
              |     ^ not found in this scope
            """, "/work/main.rs");

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal((1, 1), (diagnostic.Line, diagnostic.Column));
        Assert.StartsWith("helper.rs:3:5: cannot find value `y` in this scope", diagnostic.Message);
    }

    [Fact]
    public void ARegistryCratesError_IsNotShownAsTheScriptsProblem()
    {
        var result = _parser.Parse("""
            error[E0425]: cannot find value `y` in this scope
             --> /Users/me/.cargo/registry/src/index.crates.io-1949cf8c6b5b557f/some-1.0.0/src/lib.rs:3:5
              |
            3 |     y
              |     ^ not found in this scope
            """, "/work/main.rs");

        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void TheSameFileNameInAnotherFolder_CountsAsTheScript()
    {
        var result = _parser.Parse("""
            error[E0425]: cannot find value `y` in this scope
             --> /private/var/work/main.rs:3:5
              |
            3 |     y
              |     ^ not found in this scope
            """, "/var/work/main.rs");

        Assert.Equal(3, Assert.Single(result.Diagnostics).Line);
    }

    [Fact]
    public void WindowsPathsAndLineEndings_AreUnderstood()
    {
        var result = _parser.Parse(
            "error[E0308]: mismatched types\r\n --> C:\\Users\\me\\main.rs:2:9\r\n  |\r\n2 |     let x: i32 = \"a\";\r\n  |         ^ expected `i32`, found `&str`\r\n",
            @"C:\Users\me\main.rs");

        Assert.Equal((2, 9), (Assert.Single(result.Diagnostics).Line, result.Diagnostics[0].Column));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n")]
    [InlineData("Hello from Rust!\n")]
    public void OutputWithNothingToReport_IsEmpty(string output)
    {
        var result = _parser.Parse(output, "/work/main.rs");

        Assert.Empty(result.Diagnostics);
        Assert.Null(result.MissingDependency);
    }
}
