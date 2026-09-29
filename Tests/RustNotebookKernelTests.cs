using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>
/// The Rust notebook kernel with pretend Cargo and pretend programs: what it builds, in what order, what it keeps from one cell to
/// the next, and how it reports what went wrong. (RealRust/RustNotebookTests runs the same against the real toolchain.)
/// </summary>
public class RustNotebookKernelTests : IDisposable
{
    private const string Cargo = "/Users/test/.cargo/bin/cargo";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "FryPDF_RustKernel_" + Guid.NewGuid().ToString("N"));
    private readonly string _rustRoot;
    private readonly string _notebookFolder;
    private readonly FakeHostEnvironment _host = new(FakeOs.MacOS);
    private readonly FakeProcessLauncher _launcher = new();
    private readonly List<RustNotebookKernel> _kernels = [];

    public RustNotebookKernelTests()
    {
        _rustRoot = Path.Combine(_root, "rust");
        _notebookFolder = Path.Combine(_root, "notebooks");
        Directory.CreateDirectory(_notebookFolder);
        _host.AddRust(Cargo, "1.94.0");
        Cells();
    }

    public void Dispose()
    {
        foreach (var kernel in _kernels) kernel.Dispose();
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
        catch (IOException)
        {
        }
    }

    private RustNotebookKernel Kernel()
    {
        var toolchain = new RustToolchainProvider(_host, _launcher, new ToolchainSettingsStore(Path.Combine(_root, "toolchains.json")), _rustRoot);
        var kernel = new RustNotebookKernel(toolchain, _launcher, _host, _rustRoot, new RustNotebookDependencies(_rustRoot), new KernelCreationContext(() => _notebookFolder));
        _kernels.Add(kernel);
        return kernel;
    }

    // What every started program does unless a test says otherwise: the build succeeds, and the program prints and ends.
    private void Cells(int buildExit = 0, string buildOutput = "", string programOutput = "", int programExit = 0, string programErrors = "")
    {
        _launcher.Behavior = (spec, process) =>
        {
            if (spec.FileName == Cargo)
            {
                if (buildOutput.Length > 0) process.WriteError(buildOutput);
                process.Exit(buildExit);
            }
            else
            {
                if (programOutput.Length > 0) process.Write(programOutput);
                if (programErrors.Length > 0) process.WriteError(programErrors);
                process.Exit(programExit);
            }

            return Task.CompletedTask;
        };
    }

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    private async Task<(KernelExecutionResult Result, string Console, List<RichCellOutput> Rich)> Run(RustNotebookKernel kernel, string code, CancellationToken ct = default)
    {
        var console = new System.Text.StringBuilder();
        var rich = new List<RichCellOutput>();
        var result = await kernel.ExecuteAsync(new KernelExecutionRequest
        {
            Code = code,
            OnConsole = text => { lock (console) console.Append(text); },
            OnRichOutput = rich.Add
        }, ct).WaitAsync(Patience);
        return (result, console.ToString(), rich);
    }

    private string CellSourcePath() => Directory.GetFiles(Path.Combine(_rustRoot, "notebook", "cells"), "cell.rs", SearchOption.AllDirectories).Single();

    private string Generated() => File.ReadAllText(CellSourcePath());

    private string Manifest() => File.ReadAllText(Directory.GetFiles(Path.Combine(_rustRoot, "projects"), "Cargo.toml", SearchOption.AllDirectories).Single());

    private static int CountOf(string text, string part) => Regex.Matches(text, Regex.Escape(part)).Count;

    [Fact]
    public async Task ACell_IsBuiltWithCargoAndThenItsProgramIsRun()
    {
        Cells(programOutput: "hello from rust\n");
        var kernel = Kernel();

        var (result, console, _) = await Run(kernel, "println!(\"hello from rust\");");

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Contains("hello from rust", console);
        Assert.Contains("hello from rust", result.ConsoleOutput);

        var started = _launcher.Started.ToList();
        Assert.Equal(2, started.Count);
        Assert.Equal(Cargo, started[0].FileName);
        Assert.Equal(["build", "--manifest-path", Directory.GetFiles(Path.Combine(_rustRoot, "projects"), "Cargo.toml", SearchOption.AllDirectories).Single(), "--color", "never"], started[0].Arguments);
        Assert.Equal(_notebookFolder, started[0].WorkingDirectory);
        Assert.Contains(Path.Combine(_rustRoot, "target", "debug"), started[1].FileName);
        Assert.Equal(_notebookFolder, started[1].WorkingDirectory);
        Assert.Equal(Path.Combine(_rustRoot, "target"), started[1].Environment["CARGO_TARGET_DIR"]);
    }

    [Fact]
    public async Task TheCellsStatements_AreTheBodyOfMain()
    {
        var kernel = Kernel();

        await Run(kernel, "let x = 6;\nprintln!(\"{}\", x * 7);");

        var source = Generated();
        Assert.StartsWith(RustCellProgramBuilder.Header, source);
        Assert.Contains("fn main() -> ::std::result::Result", source);
        Assert.Contains("let x = 6;", source);
        Assert.Contains("use fry::prelude::*;", source);
    }

    [Fact]
    public async Task TheDisplayCrate_IsAddedToThePackage_AndTheFilesAreWrittenOnce()
    {
        var kernel = Kernel();

        await Run(kernel, "1 + 1");

        Assert.Contains("fry = { path =", Manifest());
        Assert.True(File.Exists(Path.Combine(_rustRoot, "fry", "src", "lib.rs")));
        Assert.Contains("fry::__auto!(__fry_last);", Generated());
    }

    [Fact]
    public async Task ARichOutput_TheProgramPrints_ReachesTheCell_AndIsNotShownAsText()
    {
        Cells(programOutput: "before\n__FRY_DISPLAY__ {\"type\":\"display\",\"data\":{\"text/html\":\"<b>hi</b>\"},\"metadata\":{}}\nafter\n");
        var kernel = Kernel();

        var (result, console, rich) = await Run(kernel, "fry::html(\"<b>hi</b>\");");

        Assert.True(result.Success);
        Assert.Single(rich);
        Assert.DoesNotContain("__FRY_DISPLAY__", console);
        Assert.Contains("before", console);
        Assert.Contains("after", console);
    }

    [Fact]
    public async Task TheFunctionsAndTypesOfEarlierCells_AreThereForLaterOnes()
    {
        var kernel = Kernel();

        await Run(kernel, "use std::collections::HashMap;\nfn double(x: i32) -> i32 { x * 2 }\nstruct Point { x: i32 }");
        await Run(kernel, "let p = Point { x: double(4) };");

        var source = Generated();
        Assert.Equal(1, CountOf(source, "fn double(x: i32) -> i32 { x * 2 }"));
        Assert.Equal(1, CountOf(source, "struct Point { x: i32 }"));
        Assert.Equal(1, CountOf(source, "use std::collections::HashMap;"));
    }

    [Fact]
    public async Task ACellRunAgain_DoesNotRepeatItsDefinitions()
    {
        var kernel = Kernel();

        await Run(kernel, "fn twice() {}\ntwice();");
        await Run(kernel, "fn twice() {}\ntwice();");

        Assert.Equal(1, CountOf(Generated(), "fn twice() {}"));
    }

    [Fact]
    public async Task ARedefinedFunction_ReplacesTheOldOne()
    {
        var kernel = Kernel();

        await Run(kernel, "fn value() -> i32 { 1 }");
        await Run(kernel, "fn value() -> i32 { 2 }");
        await Run(kernel, "value()");

        var source = Generated();
        Assert.Contains("fn value() -> i32 { 2 }", source);
        Assert.DoesNotContain("fn value() -> i32 { 1 }", source);
    }

    [Fact]
    public async Task ACellThatDoesntBuild_LeavesNothingBehind()
    {
        var kernel = Kernel();
        await Run(kernel, "fn kept() {}");

        Cells(buildExit: 101, buildOutput: "error: expected one of `!` or `::`, found `oops`\n");
        var (failed, _, _) = await Run(kernel, "fn broken() { oops }");
        Cells();
        await Run(kernel, "kept();");

        Assert.False(failed.Success);
        var source = Generated();
        Assert.Contains("fn kept() {}", source);
        Assert.DoesNotContain("fn broken", source);
    }

    [Fact]
    public async Task ACompileError_IsPutOnTheLineOfTheCell()
    {
        var kernel = Kernel();
        const string code = "fn helper() -> i32 {\n    1\n}\n\nlet count: i32 = \"three\";\nprintln!(\"{}\", count);";

        // The kernel writes the program before it builds it, so the error can be made to name the line it really has.
        _launcher.Behavior = (spec, process) =>
        {
            if (spec.FileName == Cargo)
            {
                var source = File.ReadAllLines(Directory.GetFiles(Path.Combine(_rustRoot, "notebook", "cells"), "cell.rs", SearchOption.AllDirectories).Single());
                var line = Array.FindIndex(source, l => l.Contains("let count: i32", StringComparison.Ordinal)) + 1;
                var path = Directory.GetFiles(Path.Combine(_rustRoot, "notebook", "cells"), "cell.rs", SearchOption.AllDirectories).Single();
                process.WriteError($"   Compiling cell v0.1.0\nerror[E0308]: mismatched types\n --> {path}:{line}:23\n  |\n{line} | let count: i32 = \"three\";\n  |         ---   ^^^^^^^ expected `i32`, found `&str`\n  |         |\n  |         expected due to this\n\nerror: could not compile `cell` (bin \"cell\") due to 1 previous error\n");
                process.Exit(101);
            }
            else
            {
                process.Exit(0);
            }

            return Task.CompletedTask;
        };

        var (result, console, _) = await Run(kernel, code);

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("E0308", diagnostic.Id);
        Assert.Equal(5, diagnostic.Line);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.StartsWith("mismatched types", result.ErrorMessage);
        Assert.Contains("mismatched types", console);
        Assert.DoesNotContain("Compiling", console);
    }

    [Fact]
    public async Task AnErrorInAnEarlierCellsCode_SaysSo_InsteadOfPointingAtAnyLine()
    {
        var kernel = Kernel();
        await Run(kernel, "fn old() { 1 }\nlet a = 1;");
        _launcher.Behavior = (spec, process) =>
        {
            if (spec.FileName == Cargo)
            {
                var path = Directory.GetFiles(Path.Combine(_rustRoot, "notebook", "cells"), "cell.rs", SearchOption.AllDirectories).Single();
                var line = Array.FindIndex(File.ReadAllLines(path), l => l.StartsWith("fn old", StringComparison.Ordinal)) + 1;
                process.WriteError($"error[E0308]: mismatched types\n --> {path}:{line}:11\n  |\n{line} | fn old() {{ 1 }}\n  |           ^ expected `()`, found integer\n");
                process.Exit(101);
            }
            else
            {
                process.Exit(0);
            }

            return Task.CompletedTask;
        };

        var (result, _, _) = await Run(kernel, "let b = 2;");

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.StartsWith("In code from an earlier cell:", diagnostic.Message);
        Assert.Equal(1, diagnostic.Line);
    }

    [Fact]
    public async Task ACrateNamedInAComment_IsInThePackage_AndStaysForLaterCells()
    {
        var kernel = Kernel();

        await Run(kernel, "// #crate: rand = \"0.8\"\nuse rand::Rng;");
        Assert.Contains("rand = \"0.8\"", Manifest());

        await Run(kernel, "let mut rng = rand::thread_rng();");
        Assert.Contains("rand = \"0.8\"", Manifest());
    }

    [Fact]
    public async Task ACrateAddedWithCargoAdd_IsInThePackage()
    {
        var kernel = Kernel();
        new RustNotebookDependencies(_rustRoot).Add(new RustCrate("serde_json", "\"1\""));

        await Run(kernel, "let x = 1;");

        Assert.Contains("serde_json = \"1\"", Manifest());
    }

    [Fact]
    public async Task ACellThatDoesntBuild_DoesntKeepItsCrates()
    {
        var kernel = Kernel();
        Cells(buildExit: 101, buildOutput: "error: failed\n");
        await Run(kernel, "// #crate: oops = \"1\"\nlet x = ;");
        Cells();

        await Run(kernel, "let y = 1;");

        Assert.DoesNotContain("oops", Manifest());
    }

    [Fact]
    public async Task TheReleaseProfile_CanBeAskedForByAComment()
    {
        var kernel = Kernel();

        await Run(kernel, "// #profile: release\nlet x = 1;");

        var started = _launcher.Started.ToList();
        Assert.Contains("--release", started[0].Arguments);
        Assert.Contains(Path.Combine(_rustRoot, "target", "release"), started[1].FileName);
    }

    [Fact]
    public async Task AnUnresolvedImport_OffersToInstallTheCrate()
    {
        var kernel = Kernel();
        Cells(buildExit: 101, buildOutput: "error[E0432]: unresolved import `rand`\n --> /x/cell.rs:5:5\n  |\n5 | use rand::Rng;\n  |     ^^^^ use of undeclared crate or module `rand`\n");

        var (result, _, _) = await Run(kernel, "use rand::Rng;");

        Assert.False(result.Success);
        Assert.Equal("rand", result.MissingDependency);
    }

    [Fact]
    public async Task ThePanicOfAProgram_IsAFailure_WithItsExitCode()
    {
        var kernel = Kernel();
        _launcher.Behavior = (spec, process) =>
        {
            if (spec.FileName == Cargo)
            {
                process.Exit(0);
            }
            else
            {
                var path = Directory.GetFiles(Path.Combine(_rustRoot, "notebook", "cells"), "cell.rs", SearchOption.AllDirectories).Single();
                var line = Array.FindIndex(File.ReadAllLines(path), l => l.Contains("let v: Vec<i32>", StringComparison.Ordinal)) + 1;
                process.WriteError($"\nthread 'main' panicked at {path}:{line + 1}:20:\nindex out of bounds: the len is 0 but the index is 3\nnote: run with `RUST_BACKTRACE=1` environment variable to display a backtrace\n");
                process.Exit(101);
            }

            return Task.CompletedTask;
        };

        var (result, console, _) = await Run(kernel, "let v: Vec<i32> = Vec::new();\nprintln!(\"{}\", v[3]);");

        Assert.False(result.Success);
        Assert.Equal("The program exited with code 101", result.ErrorMessage);
        Assert.Contains("index out of bounds", console);
        var panic = Assert.Single(result.Diagnostics);
        Assert.Equal(2, panic.Line);
    }

    [Fact]
    public async Task AMacroThatMakesItems_IsKeptForLaterCells_LikeAnyItem()
    {
        var kernel = Kernel();

        await Run(kernel, "impl_answer!(i32, u8);");
        await Run(kernel, "println!(\"{}\", 5i32.answer());");

        var source = Generated();
        var main = source.IndexOf("fn main()", StringComparison.Ordinal);
        Assert.Equal(1, CountOf(source, "impl_answer!(i32, u8);"));
        Assert.True(source.IndexOf("impl_answer!(i32, u8);", StringComparison.Ordinal) < main, "the call is among the items, above main");
    }

    [Fact]
    public async Task AMacroThatIsCodeToRun_IsRetriedAsAStatement_WhenItWontBuildAmongTheItems()
    {
        // At the top of a module `noisy!(1);` is an error; inside main it is fine, as an expression macro is.
        _launcher.Behavior = (spec, process) =>
        {
            if (spec.FileName == Cargo)
            {
                var source = File.ReadAllText(Directory.GetFiles(Path.Combine(_rustRoot, "notebook", "cells"), "cell.rs", SearchOption.AllDirectories).Single());
                var main = source.IndexOf("fn main()", StringComparison.Ordinal);
                if (source.IndexOf("noisy!(1);", StringComparison.Ordinal) is var at && at >= 0 && at < main)
                {
                    process.WriteError("error: macro expansion ignores token `1` and any following\n");
                    process.Exit(101);
                }
                else
                {
                    process.Exit(0);
                }
            }
            else
            {
                process.Write("ran\n");
                process.Exit(0);
            }

            return Task.CompletedTask;
        };
        var kernel = Kernel();

        var (result, console, _) = await Run(kernel, "noisy!(1);");

        Assert.True(result.Success, result.ErrorMessage + console);
        Assert.Equal("ran\n", console);
        var source = Generated();
        Assert.True(source.IndexOf("noisy!(1);", StringComparison.Ordinal) > source.IndexOf("fn main()", StringComparison.Ordinal), "the retry put it in main");
        Assert.Equal(3, _launcher.Started.Count); // two builds and one run

        // and it isn't kept as an item for the next cell
        await Run(kernel, "let x = 1;");
        Assert.DoesNotContain("noisy!(1);", Generated());
    }

    [Fact]
    public async Task WhenABuildFailsBothWays_TheErrorIsReportedOnce_AsTheStatementPlacementSawIt()
    {
        Cells(buildExit: 101, buildOutput: "error[E0425]: cannot find value `nope` in this scope\n --> /x/cell.rs:9:5\n");
        var kernel = Kernel();

        var (result, console, _) = await Run(kernel, "noisy!(nope);");

        Assert.False(result.Success);
        Assert.Equal(1, CountOf(console, "cannot find value `nope`"));
        Assert.Equal(2, _launcher.Started.Count); // both placements were built
    }

    [Fact]
    public async Task ACellWithoutUncertainMacros_IsBuiltOnce()
    {
        Cells(buildExit: 101, buildOutput: "error: broken\n");
        var kernel = Kernel();

        await Run(kernel, "println!(\"x\");\nlet a = broken;");

        Assert.Single(_launcher.Started);
    }

    [Fact]
    public async Task ACellThatDefinesMainAndAlsoHasStatements_IsRefusedWithAMessage()
    {
        var kernel = Kernel();

        var (result, console, _) = await Run(kernel, "let x = 1;\nfn main() {}");

        Assert.False(result.Success);
        Assert.Contains("fn main", result.ErrorMessage);
        Assert.Contains("fn main", console);
        Assert.Empty(_launcher.Started);
    }

    [Fact]
    public async Task ACellThatIsAWholeProgram_IsRunAsItIs()
    {
        var kernel = Kernel();

        var (result, _, _) = await Run(kernel, "fn main() { println!(\"whole\"); }");

        Assert.True(result.Success);
        Assert.DoesNotContain("fn main() ->", Generated());
    }

    [Fact]
    public async Task AValueSharedFromAnotherKernel_IsDeclaredInMain_AndListedAsAVariable()
    {
        var kernel = Kernel();
        await kernel.SetValueFromJsonAsync("nums", "[1,2,3]", CancellationToken.None);
        await kernel.SetValueFromJsonAsync("label", "\"scores\"", CancellationToken.None);

        await Run(kernel, "nums.len()");

        var source = Generated();
        Assert.Contains("let nums: Vec<i64> = vec![1, 2, 3];", source);
        Assert.Contains("let label: String = String::from(\"scores\");", source);

        var variables = await kernel.GetVariablesAsync(CancellationToken.None);
        var nums = Assert.Single(variables, v => v.Name == "nums");
        Assert.Equal("Vec<i64>", nums.TypeName);
        Assert.Equal("[1,2,3]", nums.ValueDisplay);
        Assert.Equal("Rust", nums.Kernel);
        Assert.Equal("[1,2,3]", await kernel.GetValueJsonAsync("nums", CancellationToken.None));
    }

    [Fact]
    public async Task AValueThatCantBeARustVariable_IsRefusedWhenItIsShared()
    {
        var kernel = Kernel();

        var name = await Assert.ThrowsAsync<KernelValueException>(() => kernel.SetValueFromJsonAsync("my-var", "1", CancellationToken.None));
        var json = await Assert.ThrowsAsync<KernelValueException>(() => kernel.SetValueFromJsonAsync("ok", "{oops", CancellationToken.None));

        Assert.Contains("--as", name.Message);
        Assert.Contains("JSON", json.Message);
        Assert.Empty(await kernel.GetVariablesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task AValueTheProgramShares_IsKept_AndIsNotShownAsOutput()
    {
        Cells(programOutput: "computing\n__FRY_SHARE__ {\"name\":\"total\",\"json\":\"6\"}\ndone\n");
        var kernel = Kernel();

        var (result, console, _) = await Run(kernel, "let total = 1 + 2 + 3;\nfry::share!(total);");
        await Run(kernel, "total + 1");

        Assert.True(result.Success);
        Assert.DoesNotContain("__FRY_SHARE__", console);
        Assert.Contains("computing", console);
        Assert.Equal("6", await kernel.GetValueJsonAsync("total", CancellationToken.None));
        Assert.Contains("let total: i64 = 6;", Generated());
    }

    [Fact]
    public async Task AValueTheProgramSharesUnderAnUnusableName_IsRefused_WithAWarning()
    {
        Cells(programOutput: "__FRY_SHARE__ {\"name\":\"my-var\",\"json\":\"1\"}\n");
        var kernel = Kernel();

        var (result, console, _) = await Run(kernel, "fry::share(\"my-var\", &1);");

        Assert.True(result.Success);
        Assert.Contains("my-var", console);
        Assert.Contains("--as", console);
        Assert.Empty(await kernel.GetVariablesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task AskingForAValueNoCellShared_SaysHowToShareIt()
    {
        var kernel = Kernel();

        var ex = await Assert.ThrowsAsync<KernelValueException>(() => kernel.GetValueJsonAsync("missing", CancellationToken.None));

        Assert.Contains("fry::share!(missing)", ex.Message);
    }

    [Fact]
    public async Task TheVariablesList_AlsoShowsTheFunctionsAndTypesDefined()
    {
        var kernel = Kernel();
        await Run(kernel, "/// Adds.\npub fn add(a: i32, b: i32) -> i32 { a + b }\nstruct Point { x: i32 }\nconst MAX: usize = 3;");

        var variables = await kernel.GetVariablesAsync(CancellationToken.None);

        var add = Assert.Single(variables, v => v.Name == "add");
        Assert.Equal("Function", add.Kind);
        Assert.Equal("pub fn add(a: i32, b: i32) -> i32", add.ValueDisplay);
        Assert.Equal("Type", Assert.Single(variables, v => v.Name == "Point").Kind);
        Assert.Equal("Constant", Assert.Single(variables, v => v.Name == "MAX").Kind);
    }

    [Fact]
    public async Task StoppingACellWhileItBuilds_KillsCargo_AndReportsACancellation()
    {
        FakeProcess? cargo = null;
        _launcher.Behavior = async (spec, process) =>
        {
            cargo = process;
            await Task.Delay(Timeout.Infinite, process.KilledToken);
        };
        var kernel = Kernel();
        using var cts = new CancellationTokenSource();

        var running = Run(kernel, "let x = 1;", cts.Token);
        await WaitUntil(() => cargo != null);
        cts.Cancel();
        var (result, _, _) = await running;

        Assert.True(result.WasCancelled);
        Assert.True(cargo!.WasKilled);
    }

    [Fact]
    public async Task StoppingACellWhileItRuns_KillsTheProgram_AndKeepsWhatItDefined()
    {
        FakeProcess? program = null;
        _launcher.Behavior = async (spec, process) =>
        {
            if (spec.FileName == Cargo)
            {
                process.Exit(0);
                return;
            }

            program = process;
            await Task.Delay(Timeout.Infinite, process.KilledToken);
        };
        var kernel = Kernel();
        using var cts = new CancellationTokenSource();

        var running = Run(kernel, "fn spin() {}\nloop {}", cts.Token);
        await WaitUntil(() => program != null);
        cts.Cancel();
        var (result, _, _) = await running;

        Assert.True(result.WasCancelled);
        Assert.True(program!.WasKilled);
        Assert.Contains(await kernel.GetVariablesAsync(CancellationToken.None), v => v.Name == "spin");
    }

    [Fact]
    public async Task CellsRunOneAtATime()
    {
        var release = new TaskCompletionSource();
        var buildsStarted = 0;
        _launcher.Behavior = async (spec, process) =>
        {
            if (spec.FileName == Cargo)
            {
                Interlocked.Increment(ref buildsStarted);
                await release.Task;
            }

            process.Exit(0);
        };
        var kernel = Kernel();

        var first = Run(kernel, "let a = 1;");
        await WaitUntil(() => Volatile.Read(ref buildsStarted) == 1);
        var second = Run(kernel, "let b = 2;");
        await Task.Delay(300);

        Assert.Equal(1, Volatile.Read(ref buildsStarted));
        release.SetResult();
        Assert.True((await first).Result.Success);
        Assert.True((await second).Result.Success);
        Assert.Equal(2, buildsStarted);
    }

    [Fact]
    public async Task WithoutRust_TheCellExplainsHowToInstallIt()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        var toolchain = new RustToolchainProvider(host, _launcher, new ToolchainSettingsStore(Path.Combine(_root, "none.json")), _rustRoot);
        using var kernel = new RustNotebookKernel(toolchain, _launcher, host, _rustRoot, new RustNotebookDependencies(_rustRoot), new KernelCreationContext(() => _notebookFolder));
        var console = new System.Text.StringBuilder();

        var result = await kernel.ExecuteAsync(new KernelExecutionRequest { Code = "let x = 1;", OnConsole = text => console.Append(text) }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("rustup", console.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_launcher.Started);
    }

    [Fact]
    public async Task ResettingTheKernel_ForgetsEverything()
    {
        var kernel = Kernel();
        await Run(kernel, "// #crate: rand = \"0.8\"\nfn kept() {}\nlet x = 1;");
        await kernel.SetValueFromJsonAsync("nums", "[1]", CancellationToken.None);
        Assert.True(kernel.IsSessionActive);

        kernel.HardReset();

        Assert.False(kernel.IsSessionActive);
        Assert.Empty(await kernel.GetVariablesAsync(CancellationToken.None));
        await Run(kernel, "let y = 1;");
        var source = Generated();
        Assert.DoesNotContain("fn kept", source);
        Assert.DoesNotContain("nums", source);
        Assert.DoesNotContain("rand", Manifest());
    }

    [Fact]
    public async Task ADisposedKernel_RemovesItsFilesButNotTheSharedBuildFolder()
    {
        var kernel = Kernel();
        await Run(kernel, "let x = 1;");
        var cellFolder = Path.GetDirectoryName(CellSourcePath())!;
        Directory.CreateDirectory(Path.Combine(_rustRoot, "target"));

        kernel.Dispose();

        Assert.False(Directory.Exists(cellFolder));
        Assert.Empty(Directory.GetFiles(Path.Combine(_rustRoot, "projects"), "Cargo.toml", SearchOption.AllDirectories));
        Assert.True(Directory.Exists(Path.Combine(_rustRoot, "target")));
    }

    [Fact]
    public void TheRustLanguage_HasANotebookKernel_AndTheCapabilitiesThatGoWithIt()
    {
        var language = new StudioLanguageServices(Path.Combine(_root, "services")).Registry.Get(LanguageIds.Rust)!;

        Assert.NotNull(language.NotebookKernels);
        Assert.True(language.Capabilities.HasFlag(LanguageCapabilities.NotebookCells));
        Assert.True(language.Capabilities.HasFlag(LanguageCapabilities.ValueSharing));
        using var kernel = language.NotebookKernels!.Create(new KernelCreationContext(() => _notebookFolder));
        Assert.Equal(LanguageIds.Rust, kernel.LanguageId);
        Assert.True(kernel.CanForceStop);
    }

    [Fact]
    public void ACellStartingWithAnInnerAttribute_OrPickingRust_IsRoutedToTheRustKernel()
    {
        var registry = new StudioLanguageServices(Path.Combine(_root, "routing")).Registry;

        var directives = NotebookCellDirectives.Parse("#!rust\n#![allow(unused)]\nlet x = 1;", registry, LanguageIds.CSharp);

        Assert.Equal(LanguageIds.Rust, directives.LanguageId);
        Assert.Empty(directives.Errors);
        Assert.Equal(LanguageIds.Rust, NotebookCellDirectives.LanguageOf("#!rs\nlet x = 1;", registry, LanguageIds.CSharp));
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Patience;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition never became true.");
            await Task.Delay(10);
        }
    }
}
