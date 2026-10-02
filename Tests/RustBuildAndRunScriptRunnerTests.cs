using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class RustBuildAndRunScriptRunnerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "FryPDF_RustRunner_" + Guid.NewGuid().ToString("N"));

    public RustBuildAndRunScriptRunnerTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
        catch (IOException)
        {
        }
    }

    private static ToolchainInfo Cargo(string path = "/Users/test/.cargo/bin/cargo") => new()
    {
        LanguageId = "rust",
        ExecutablePath = path,
        Version = new Version(1, 94, 0),
        DisplayName = "Rust 1.94.0",
        Source = "rustup"
    };

    private string ManifestFor(string sourcePath) =>
        Path.Combine(_root, "rust", "projects", RustProjectStager.StableId(sourcePath), "Cargo.toml");

    private RustBuildAndRunScriptRunner Runner(FakeHostEnvironment host) => new(host, Path.Combine(_root, "rust"));

    [Fact]
    public async Task GeneratesATwoPhasePlan_ACargoBuildThenTheProgram()
    {
        var source = "/Users/test/workspace/main.rs";
        var plan = await Runner(new FakeHostEnvironment(FakeOs.MacOS))
            .PlanAsync(new ScriptRunContext(source, "/Users/test/workspace", Cargo()));

        Assert.Equal(2, plan.Steps.Count);

        var build = plan.Steps[0];
        Assert.True(build.IsBuildStep);
        Assert.Equal("cargo build", build.Label);
        Assert.Equal("/Users/test/.cargo/bin/cargo", build.Spec.FileName);
        Assert.Equal(["build", "--manifest-path", ManifestFor(source), "--color", "never"], build.Spec.Arguments);
        Assert.Equal("/Users/test/workspace", build.Spec.WorkingDirectory);

        var run = plan.Steps[1];
        Assert.False(run.IsBuildStep);
        Assert.Equal("main", run.Label);
        Assert.StartsWith(Path.Combine(_root, "rust", "target", "debug") + Path.DirectorySeparatorChar + "main_", run.Spec.FileName);
        Assert.Empty(run.Spec.Arguments);
        Assert.Equal("/Users/test/workspace", run.Spec.WorkingDirectory);
    }

    [Fact]
    public async Task TheBuildsEnvironment_SharesOneBuildFolderAndTurnsColourOff()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS) { LoginShellPath = "/opt/homebrew/bin:/usr/bin" };
        var plan = await Runner(host).PlanAsync(new ScriptRunContext("/w/main.rs", "/w", Cargo()));

        foreach (var step in plan.Steps)
        {
            var environment = step.Spec.Environment;
            Assert.Equal(Path.Combine(_root, "rust", "target"), environment["CARGO_TARGET_DIR"]);
            Assert.Equal("1", environment["NO_COLOR"]);
            Assert.Equal("never", environment["CARGO_TERM_COLOR"]);
            Assert.Equal("/Users/test/.cargo/bin:/opt/homebrew/bin:/usr/bin", environment["PATH"]);
        }
    }

    [Fact]
    public async Task TheManifest_PointsItsBinaryAtTheRealFile_AndIsItsOwnWorkspace()
    {
        var source = "/Users/test/workspace/main.rs";
        await Runner(new FakeHostEnvironment(FakeOs.MacOS)).PlanAsync(new ScriptRunContext(source, "/Users/test/workspace", Cargo()));

        var manifest = File.ReadAllText(ManifestFor(source));

        Assert.Contains("[[bin]]", manifest);
        Assert.Contains("path = '/Users/test/workspace/main.rs'", manifest);
        Assert.Contains("edition = '2021'", manifest);
        Assert.Contains("autobins = false", manifest);
        Assert.Contains($"fry = {{ path = '{Path.Combine(_root, "rust", "fry")}' }}", manifest);
        Assert.EndsWith("[workspace]\n", manifest);
    }

    [Fact]
    public async Task TheDisplayCrate_IsWrittenOnceForAllScripts()
    {
        var runner = Runner(new FakeHostEnvironment(FakeOs.MacOS));
        await runner.PlanAsync(new ScriptRunContext("/w/a.rs", "/w", Cargo()));
        var lib = Path.Combine(_root, "rust", "fry", "src", "lib.rs");
        Assert.Contains("pub fn table", File.ReadAllText(lib));
        Assert.Contains("name = \"fry\"", File.ReadAllText(Path.Combine(_root, "rust", "fry", "Cargo.toml")));

        File.SetLastWriteTimeUtc(lib, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await runner.PlanAsync(new ScriptRunContext("/w/b.rs", "/w", Cargo()));

        Assert.Equal(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), File.GetLastWriteTimeUtc(lib));
    }

    [Fact]
    public async Task DirectivesInTheFile_BecomeCratesEditionAndProfile()
    {
        var source = Path.Combine(_root, "script.rs");
        File.WriteAllText(source, """
            // #crate: rand = "0.8"
            // #crate: serde = { version = "1", features = ["derive"] }
            // #edition: 2024
            // #profile: release
            fn main() {}
            """.Replace("\r\n", "\n"));

        var plan = await Runner(new FakeHostEnvironment(FakeOs.MacOS)).PlanAsync(new ScriptRunContext(source, _root, Cargo()));

        var manifest = File.ReadAllText(ManifestFor(source));
        Assert.Contains("rand = \"0.8\"", manifest);
        Assert.Contains("serde = { version = \"1\", features = [\"derive\"] }", manifest);
        Assert.Contains("edition = '2024'", manifest);
        Assert.Contains("--release", plan.Steps[0].Spec.Arguments);
        Assert.Contains(Path.Combine("target", "release") + Path.DirectorySeparatorChar, plan.Steps[1].Spec.FileName);
    }

    [Fact]
    public async Task OnWindows_TheProgramIsAnExe_AndTheBuildRunsInTheFilesFolder()
    {
        var host = new FakeHostEnvironment(FakeOs.Windows);
        var plan = await Runner(host).PlanAsync(new ScriptRunContext(
            @"C:\Users\test\workspace\server.rs", @"C:\Users\test\workspace",
            Cargo(@"C:\Users\test\.cargo\bin\cargo.exe")));

        Assert.EndsWith(".exe", plan.Steps[1].Spec.FileName, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(@"C:\Users\test\workspace", plan.Steps[0].Spec.WorkingDirectory);
        Assert.StartsWith(@"C:\Users\test\.cargo\bin;", plan.Steps[0].Spec.Environment["PATH"]);
        Assert.Contains(@"path = 'C:\Users\test\workspace\server.rs'", File.ReadAllText(ManifestFor(@"C:\Users\test\workspace\server.rs")));
    }

    [Fact]
    public async Task ThePackageFolder_IsTheSameOnEveryRun_AndDifferentPerFile()
    {
        var runner = Runner(new FakeHostEnvironment(FakeOs.MacOS));

        var first = await runner.PlanAsync(new ScriptRunContext("/w/main.rs", "/w", Cargo()));
        var second = await runner.PlanAsync(new ScriptRunContext("/w/main.rs", "/w", Cargo()));
        var other = await runner.PlanAsync(new ScriptRunContext("/elsewhere/main.rs", "/elsewhere", Cargo()));

        Assert.Equal(first.Steps[0].Spec.Arguments[2], second.Steps[0].Spec.Arguments[2]);
        Assert.Equal(first.Steps[1].Spec.FileName, second.Steps[1].Spec.FileName);
        Assert.NotEqual(first.Steps[0].Spec.Arguments[2], other.Steps[0].Spec.Arguments[2]);
        Assert.NotEqual(first.Steps[1].Spec.FileName, other.Steps[1].Spec.FileName);
    }

    [Fact]
    public async Task AnUnchangedManifest_IsNotRewritten()
    {
        var runner = Runner(new FakeHostEnvironment(FakeOs.MacOS));
        var context = new ScriptRunContext("/w/main.rs", "/w", Cargo());
        await runner.PlanAsync(context);
        var manifest = ManifestFor("/w/main.rs");
        File.SetLastWriteTimeUtc(manifest, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        await runner.PlanAsync(context);

        Assert.Equal(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), File.GetLastWriteTimeUtc(manifest));
    }

    [Theory]
    [InlineData("01. Two Sum.rs", "two_sum_")]
    [InlineData("Hello World!.rs", "hello_world_")]
    [InlineData("123.rs", "main_")]
    [InlineData("___.rs", "main_")]
    [InlineData("script_143025.rs", "script_143025_")]
    [InlineData("größe.rs", "gr_e_")]
    [InlineData("fry.rs", "fry_")]
    public void AFileName_BecomesAValidCrateName_WithItsOwnIdOnTheEnd(string file, string expectedStart)
    {
        var path = "/w/" + file;
        var id = RustProjectStager.StableId(path);

        var name = RustProjectStager.PackageNameFor(path, id);

        Assert.Equal(expectedStart + id[..6], name);
        Assert.Matches("^[a-z][a-z0-9_]*$", name);
    }

    [Fact]
    public void TheId_IgnoresWhichSlashAPathUses()
    {
        Assert.Equal(RustProjectStager.StableId(@"C:\a\b.rs"), RustProjectStager.StableId("C:/a/b.rs"));
        Assert.Equal(12, RustProjectStager.StableId("/a/b.rs").Length);
        Assert.NotEqual(RustProjectStager.StableId("/a/b.rs"), RustProjectStager.StableId("/a/c.rs"));
    }

    [Theory]
    [InlineData(@"C:\Users\me\main.rs", @"'C:\Users\me\main.rs'")]
    [InlineData("/plain/path.rs", "'/plain/path.rs'")]
    [InlineData("/it's/here.rs", "\"/it's/here.rs\"")]
    [InlineData("/back\\slash'x", "\"/back\\\\slash'x\"")]
    public void APathInTheManifest_IsQuotedSoTomlReadsItBack(string path, string expected)
    {
        Assert.Equal(expected, RustProjectStager.TomlString(path));
    }

    [Fact]
    public void ThePackageFolder_CannotBePreparedWhereTheRootIsAFile()
    {
        var blocker = Path.Combine(_root, "blocker");
        File.WriteAllText(blocker, "not a folder");

        var ex = Assert.Throws<IOException>(() =>
            RustProjectStager.Prepare(blocker, "/w/main.rs", RustDirectives.None, null, false));

        Assert.Contains("Couldn't prepare the Rust build folder", ex.Message);
    }
}
