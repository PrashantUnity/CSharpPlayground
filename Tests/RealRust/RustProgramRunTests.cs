using System.Collections.Concurrent;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.RealRust;

/// <summary>One studio folder for the whole class, so the display crate and cargo's build folder are made once.</summary>
public sealed class RustStudioFixture : IDisposable
{
    public string Folder { get; } = Path.Combine(Path.GetTempPath(), "FryPDF_RealRust_" + Guid.NewGuid().ToString("N"));

    public StudioLanguageServices Services { get; }

    public RustStudioFixture()
    {
        Directory.CreateDirectory(Folder);
        Services = TestRust.Services(Folder);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Folder)) Directory.Delete(Folder, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

/// <summary>.rs files built and run with the real Rust toolchain: cargo build, the program, diagnostics and the display crate.</summary>
[Collection(RealRustCollection.Name)]
public class RustProgramRunTests : IClassFixture<RustStudioFixture>, IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromMinutes(3);
    private readonly RustStudioFixture _studio;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_RustRun_" + Guid.NewGuid().ToString("N"));

    public RustProgramRunTests(RustStudioFixture studio)
    {
        _studio = studio;
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private string Write(string name, string code)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, code.Replace("\r\n", "\n"));
        return path;
    }

    private async Task<(ScriptRunSession Session, ConcurrentQueue<string> Output)> Start(string path)
    {
        var rust = TestRust.Require();
        var language = (RustLanguage)_studio.Services.Registry.Get(LanguageIds.Rust)!;
        var plan = await language.ScriptRunner.PlanAsync(new ScriptRunContext(path, Path.GetDirectoryName(path)!, rust));
        var output = new ConcurrentQueue<string>();
        var session = new ScriptRunExecutor(_studio.Services.Processes).Start(plan, path, language.RunDiagnostics, output.Enqueue);
        return (session, output);
    }

    // Crates come from the network; a machine without it can't run those tests.
    private static bool LooksOffline(string text) =>
        new[] { "Could not resolve host", "failed to download", "spurious network error", "Unable to update registry", "failed to get", "timed out", "Network is unreachable" }
            .Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));

    [RustFact]
    public void TheInstalledRust_IsFound()
    {
        var rust = TestRust.Require();

        Assert.True(rust.Version >= RustToolchainProvider.MinimumVersion, rust.Label);
        Assert.True(File.Exists(rust.ExecutablePath), rust.ExecutablePath);
        Assert.NotNull(rust.Get("hostTriple"));
    }

    [RustFact]
    public async Task AFile_BuildsAndRunsAndPrints()
    {
        var path = Write("hello.rs", """
            fn main() {
                println!("Hello from the Rust run test!");
            }
            """);
        var (session, output) = await Start(path);

        var result = await session.Completion.WaitAsync(Patience);

        Assert.Equal(0, result.ExitCode);
        Assert.True(result.Succeeded);
        Assert.Contains("Hello from the Rust run test!", string.Concat(output));
    }

    [RustFact]
    public async Task ARunAgain_ReusesTheBuild()
    {
        var path = Write("again.rs", "fn main() { println!(\"again\"); }");
        var (first, _) = await Start(path);
        await first.Completion.WaitAsync(Patience);

        var (second, output) = await Start(path);
        var result = await second.Completion.WaitAsync(Patience);

        Assert.True(result.Succeeded);
        Assert.Contains("Finished", string.Concat(output));
        Assert.DoesNotContain("Compiling again_", string.Concat(output));
    }

    [RustFact]
    public async Task ACompileError_StopsTheRun_AndIsParsedIntoADiagnostic()
    {
        var path = Write("broken.rs", """
            fn main() {
                let x: i32 = "not a number";
                println!("{}", x);
            }
            """);
        var (session, output) = await Start(path);

        var result = await session.Completion.WaitAsync(Patience);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal("cargo build", result.FailedBuildStep);
        var diagnostic = Assert.Single(result.Diagnostics.Diagnostics);
        Assert.Equal("E0308", diagnostic.Id);
        Assert.Equal(2, diagnostic.Line);
        Assert.Equal(18, diagnostic.Column);
        Assert.Equal(32, diagnostic.EndColumn);
        Assert.DoesNotContain("not a number\n", string.Concat(output).Replace("let x: i32 = \"not a number\";", string.Empty));
    }

    [RustFact]
    public async Task APanic_ExitsWith101_AndIsParsedIntoADiagnostic()
    {
        var path = Write("panics.rs", """
            fn main() {
                let v = vec![1, 2, 3];
                let i = 7;
                println!("before");
                println!("{}", v[i]);
            }
            """);
        var (session, output) = await Start(path);

        var result = await session.Completion.WaitAsync(Patience);

        Assert.Equal(101, result.ExitCode);
        Assert.Null(result.FailedBuildStep);
        Assert.Contains("before", string.Concat(output));
        var diagnostic = Assert.Single(result.Diagnostics.Diagnostics);
        Assert.Equal("PANIC", diagnostic.Id);
        Assert.Equal(5, diagnostic.Line);
        Assert.Contains("index out of bounds", diagnostic.Message);
    }

    [RustFact]
    public async Task ArithmeticOverflow_PanicsInTheDefaultDevProfile()
    {
        var path = Write("overflow.rs", """
            fn main() {
                let mut x: u8 = 250;
                for _ in 0..10 { x += 1; }
                println!("{}", x);
            }
            """);
        var (session, _) = await Start(path);

        var result = await session.Completion.WaitAsync(Patience);

        Assert.Equal(101, result.ExitCode);
        Assert.Contains("overflow", Assert.Single(result.Diagnostics.Diagnostics).Message);
    }

    [RustFact]
    public async Task AProgramReadingStdin_CanReceiveInput()
    {
        var path = Write("interactive.rs", """
            use std::io::{self, BufRead};

            fn main() {
                let mut line = String::new();
                io::stdin().lock().read_line(&mut line).unwrap();
                println!("Welcome, {}!", line.trim());
            }
            """);
        var (session, output) = await Start(path);

        await session.SendInputAsync("Ferris\n");

        var result = await session.Completion.WaitAsync(Patience);

        Assert.True(result.Succeeded);
        Assert.Contains("Welcome, Ferris!", string.Concat(output));
    }

    [RustFact]
    public async Task ASiblingModuleFile_IsFoundNextToTheScript()
    {
        Write("helper.rs", "pub fn twice(x: i32) -> i32 { x * 2 }\n");
        var path = Write("uses_module.rs", """
            mod helper;

            fn main() {
                println!("twice: {}", helper::twice(21));
            }
            """);
        var (session, output) = await Start(path);

        var result = await session.Completion.WaitAsync(Patience);

        Assert.True(result.Succeeded, string.Concat(output));
        Assert.Contains("twice: 42", string.Concat(output));
    }

    [RustFact]
    public async Task AnErrorInASiblingModule_IsShownOnTheScript()
    {
        Write("bad_helper.rs", "pub fn broken() -> i32 { missing_name }\n");
        var path = Write("uses_bad_module.rs", "mod bad_helper;\nfn main() { println!(\"{}\", bad_helper::broken()); }\n");
        var (session, _) = await Start(path);

        var result = await session.Completion.WaitAsync(Patience);

        Assert.Equal("cargo build", result.FailedBuildStep);
        var diagnostic = Assert.Single(result.Diagnostics.Diagnostics);
        Assert.Equal(1, diagnostic.Line);
        Assert.StartsWith("bad_helper.rs:1:", diagnostic.Message);
    }

    [RustFact]
    public async Task TheDisplayCrate_PrintsATableTheStudioCanRead()
    {
        var path = Write("table.rs", """
            #[derive(Debug)]
            struct Person { name: String, age: u32 }

            fn main() {
                let people = vec![
                    Person { name: "Ada".into(), age: 36 },
                    Person { name: "Linus".into(), age: 54 },
                ];
                fry::table(&people, "People");
                fry::dump!(people.len());
            }
            """);
        var (session, output) = await Start(path);

        var result = await session.Completion.WaitAsync(Patience);

        var text = string.Concat(output);
        Assert.True(result.Succeeded, text);
        Assert.Contains("__FRY_DISPLAY__ {\"type\":\"display\",\"data\":{\"application/vnd.fry.table+json\":{\"title\":\"People\",\"columns\":[\"name\",\"age\"],\"numeric\":[false,true],\"rows\":[[\"Ada\",36],[\"Linus\",54]]", text);
        Assert.Contains("people.len(): 2", text);

        // …and ExternalOutputProcessor turns that line into a table.
        var rich = new List<RichCellOutput>();
        var processor = new ExternalOutputProcessor(_ => { }, rich.Add);
        processor.ProcessChunk(text);
        processor.Flush();
        Assert.Contains(rich, o => o.Kind == CellOutputKind.Table && o.TableResult?.Title == "People");
    }

    [RustFact]
    public async Task ACrateNamedInAComment_IsFetchedAndUsed()
    {
        var path = Write("crate.rs", """
            // #crate: fastrand = "2"
            fn main() {
                let n = fastrand::u32(7..=7);
                println!("fastrand says {}", n);
            }
            """);
        var (session, output) = await Start(path);

        var result = await session.Completion.WaitAsync(Patience);

        var text = string.Concat(output);
        if (!result.Succeeded && LooksOffline(text)) return; // no network: nothing to prove
        Assert.True(result.Succeeded, text);
        Assert.Contains("fastrand says 7", text);
    }

    [RustFact]
    public async Task AMissingCrate_IsOfferedForInstall()
    {
        var path = Write("missing.rs", """
            use rand::Rng;

            fn main() {
                println!("{}", rand::thread_rng().gen::<u8>());
            }
            """);
        var (session, _) = await Start(path);

        var result = await session.Completion.WaitAsync(Patience);

        Assert.Equal("cargo build", result.FailedBuildStep);
        Assert.Equal("rand", result.Diagnostics.MissingDependency);
        Assert.Equal(2, result.Diagnostics.Diagnostics.Count);
    }

    [RustFact]
    public async Task AReleaseDirective_BuildsTheOptimizedProfile()
    {
        var path = Write("fast.rs", """
            // #profile: release
            fn main() { println!("optimized"); }
            """);
        var (session, output) = await Start(path);

        var result = await session.Completion.WaitAsync(Patience);

        var text = string.Concat(output);
        Assert.True(result.Succeeded, text);
        Assert.Contains("optimized", text);
        Assert.Contains("release", text);
    }

    [RustFact]
    public async Task ARunOfTheSameNamedFileInAnotherFolder_DoesNotRunTheOthersProgram()
    {
        var one = Write("main.rs", "fn main() { println!(\"first\"); }");
        Directory.CreateDirectory(Path.Combine(_dir, "other"));
        var two = Path.Combine(_dir, "other", "main.rs");
        File.WriteAllText(two, "fn main() { println!(\"second\"); }");

        var (sessionOne, outputOne) = await Start(one);
        await sessionOne.Completion.WaitAsync(Patience);
        var (sessionTwo, outputTwo) = await Start(two);
        await sessionTwo.Completion.WaitAsync(Patience);
        var (again, outputAgain) = await Start(one);
        await again.Completion.WaitAsync(Patience);

        Assert.Contains("first", string.Concat(outputOne));
        Assert.Contains("second", string.Concat(outputTwo));
        Assert.Contains("first", string.Concat(outputAgain));
        Assert.DoesNotContain("second", string.Concat(outputAgain));
    }

    [RustFact]
    public async Task TheInstallFix_EndToEnd_MakesAMissingCrateWork()
    {
        var path = Write("needs_crate.rs", "fn main() {\n    println!(\"picked {}\", fastrand::u8(5..=5));\n}\n");
        var language = (RustLanguage)_studio.Services.Registry.Get(LanguageIds.Rust)!;

        // 1. It doesn't build, and the parser names the crate to offer.
        var (failing, _) = await Start(path);
        var failed = await failing.Completion.WaitAsync(Patience);
        Assert.Equal("cargo build", failed.FailedBuildStep);
        var package = Assert.IsType<string>(failed.Diagnostics.MissingDependency);
        Assert.Equal("fastrand", package);

        // 2. Installing it downloads the crate and hands back the line for the file.
        var packages = language.Packages!;
        var install = new List<string>();
        var result = await packages.RunAsync(packages.InstallCommand(packages.PackageForMissingDependency(package)), TestRust.Require(), install.Add);
        if (!result.Success && LooksOffline(string.Concat(install))) return; // no network: nothing to prove
        Assert.True(result.Success, string.Concat(install));
        Assert.Matches("^// #crate: fastrand = \"2\\.\\d+\\.\\d+\"$", result.DirectiveToInsert);

        // 3. With that line in the file it builds and runs.
        File.WriteAllText(path, result.DirectiveToInsert + "\n" + File.ReadAllText(path));
        var (fixedRun, output) = await Start(path);
        var ran = await fixedRun.Completion.WaitAsync(Patience);
        Assert.True(ran.Succeeded, string.Concat(output));
        Assert.Contains("picked 5", string.Concat(output));
    }

    [RustFact]
    public async Task AnUnknownCrate_IsRefusedWhenAdded_AndReportedOnItsCommentWhenBuilt()
    {
        var language = (RustLanguage)_studio.Services.Registry.Get(LanguageIds.Rust)!;
        var packages = language.Packages!;
        packages.TryParseDirective("%cargo add fry_no_such_crate_zzz", out var command);
        var log = new List<string>();

        var result = await packages.RunAsync(command, TestRust.Require(), log.Add);

        if (LooksOffline(string.Concat(log))) return; // no network: cargo can't say the crate doesn't exist
        Assert.False(result.Success, string.Concat(log));
        Assert.Null(result.DirectiveToInsert);

        var path = Write("bad_crate.rs", "fn main() {}\n// filler\n// #crate: fry_no_such_crate_zzz = \"1\"\n");
        var (session, output) = await Start(path);
        var built = await session.Completion.WaitAsync(Patience);
        if (LooksOffline(string.Concat(output))) return;
        Assert.Equal("cargo build", built.FailedBuildStep);
        var diagnostic = Assert.Single(built.Diagnostics.Diagnostics);
        Assert.Equal(3, diagnostic.Line);
        Assert.Equal("CARGO", diagnostic.Id);
    }
}
