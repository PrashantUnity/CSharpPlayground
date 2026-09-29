using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class RustPackageManagerTests : IDisposable
{
    private const string Cargo = "/Users/test/.cargo/bin/cargo";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "FryPDF_RustPackages_" + Guid.NewGuid().ToString("N"));
    private readonly FakeProcessLauncher _launcher = new();
    private readonly FakeHostEnvironment _host = new(FakeOs.MacOS);
    private readonly RustPackageManager _manager;
    private readonly ToolchainInfo _toolchain = new()
    {
        LanguageId = "rust",
        ExecutablePath = Cargo,
        Version = new Version(1, 94, 0),
        DisplayName = "Rust 1.94.0",
        Source = "rustup"
    };

    public RustPackageManagerTests()
    {
        Directory.CreateDirectory(_root);
        _host.AddRust(Cargo, "1.94.0");
        _manager = new RustPackageManager(_root, toolchains: null, _launcher, _host);
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

    private string Scratch => Path.Combine(_root, "notebook", "deps");

    // Pretends to be cargo fetch: locks each crate in the scratch package at a version and exits.
    private void CargoFetchLocks(params (string Name, string Version)[] versions) =>
        _launcher.Behavior = (spec, process) =>
        {
            var lockText = string.Concat(versions.Select(v => $"[[package]]\nname = \"{v.Name}\"\nversion = \"{v.Version}\"\nsource = \"registry+https://github.com/rust-lang/crates.io-index\"\n\n"));
            File.WriteAllText(Path.Combine(spec.WorkingDirectory!, "Cargo.lock"), lockText);
            process.Write("    Updating crates.io index\n  Downloaded crates\n");
            process.Exit(0);
            return Task.CompletedTask;
        };

    [Theory]
    [InlineData("%cargo add rand", "rand = \"*\"")]
    [InlineData("%cargo add rand@0.8", "rand = \"0.8\"")]
    [InlineData("!cargo add regex", "regex = \"*\"")]
    [InlineData("#cargo add regex", "regex = \"*\"")]
    [InlineData("%cargo add serde --features derive", "serde = { version = \"*\", features = [\"derive\"] }")]
    [InlineData("%cargo add serde@1 --features=derive,std", "serde = { version = \"1\", features = [\"derive\", \"std\"] }")]
    [InlineData("%cargo add tokio -F full,macros --no-default-features", "tokio = { version = \"*\", features = [\"full\", \"macros\"], default-features = false }")]
    [InlineData("%crate itertools = \"0.14\"", "itertools = \"0.14\"")]
    [InlineData("// #crate: rand = \"0.8\"", "rand = \"0.8\"")]
    [InlineData("  // #crate: rand  ", "rand = \"*\"")]
    [InlineData("#r \"crate: serde_json\"", "serde_json = \"*\"")]
    public void ADirective_IsReadInEveryForm(string line, string expectedSpec)
    {
        Assert.True(_manager.TryParseDirective(line, out var command));

        Assert.Equal(["add", expectedSpec], command.Arguments);
        Assert.Equal(line.Trim(), command.Text);
    }

    [Theory]
    [InlineData("fn main() {}")]
    [InlineData("// just a comment")]
    [InlineData("%pip install numpy")]
    [InlineData("%cargo add")]
    [InlineData("%cargo add --features derive")]
    [InlineData("%cargo add rand --bogus")]
    [InlineData("%cargo add serde --features")]
    [InlineData("%cargo add 9lives")]
    [InlineData("// #crate:")]
    public void ANonDirective_IsNotOne(string line) => Assert.False(_manager.TryParseDirective(line, out _));

    [Fact]
    public void TheInstallCommand_AddsTheFeatureACrateNeeds()
    {
        var serde = _manager.InstallCommand("serde");
        var rand = _manager.InstallCommand("rand");

        Assert.Equal("// #crate: serde = { version = \"1\", features = [\"derive\"] }", serde.Text);
        Assert.Equal(["add", "serde = { version = \"1\", features = [\"derive\"] }"], serde.Arguments);
        Assert.Equal(["add", "rand = \"*\""], rand.Arguments);
    }

    [Theory]
    [InlineData("tokio_stream", "tokio-stream")]
    [InlineData("async_trait", "async-trait")]
    [InlineData("rand", "rand")]
    [InlineData("  serde_json ", "serde_json")]
    public void AMissingCrate_MapsToItsPackage(string codeName, string package) =>
        Assert.Equal(package, _manager.PackageForMissingDependency(codeName));

    [Fact]
    public void TheToolIsCalledCargo() => Assert.Equal("cargo", _manager.ToolName);

    [Fact]
    public async Task AddingACrate_FetchesItThroughAScratchPackage_AndHandsBackTheLineForTheFile()
    {
        CargoFetchLocks(("rand", "0.9.2"), ("rand_core", "0.9.3"));
        var output = new List<string>();
        _manager.TryParseDirective("%cargo add rand", out var command);

        var result = await _manager.RunAsync(command, _toolchain, output.Add);

        Assert.True(result.Success);
        Assert.Equal("// #crate: rand = \"0.9.2\"", result.DirectiveToInsert);

        var started = Assert.Single(_launcher.Started);
        Assert.Equal(Cargo, started.FileName);
        Assert.Equal(["fetch", "--manifest-path", Path.Combine(Scratch, "Cargo.toml"), "--color", "never"], started.Arguments);
        Assert.Equal(Scratch, started.WorkingDirectory);
        Assert.Equal("1", started.Environment!["NO_COLOR"]);

        var manifest = File.ReadAllText(Path.Combine(Scratch, "Cargo.toml"));
        Assert.Contains("[lib]\npath = \"lib.rs\"", manifest);
        Assert.Contains("rand = \"*\"", manifest);
        Assert.EndsWith("[workspace]\n", manifest);
        Assert.True(File.Exists(Path.Combine(Scratch, "lib.rs")));

        Assert.Contains(output, o => o.Contains("▶ cargo fetch"));
        Assert.Contains(output, o => o.Contains("✓ Added rand 0.9.2"));
    }

    [Fact]
    public async Task ACrateWithAVersionAsked_KeepsThatVersionRatherThanTheLockedOne()
    {
        CargoFetchLocks(("rand", "0.8.5"));
        _manager.TryParseDirective("// #crate: rand = \"0.8\"", out var command);

        var result = await _manager.RunAsync(command, _toolchain, _ => { });

        Assert.Equal("// #crate: rand = \"0.8\"", result.DirectiveToInsert);
    }

    [Fact]
    public async Task ACrateAddedByANotebook_IsRememberedForEveryNotebookBuild()
    {
        CargoFetchLocks(("rand", "0.9.2"), ("serde", "1.0.228"));
        _manager.TryParseDirective("%cargo add rand", out var rand);
        _manager.TryParseDirective("%cargo add serde --features derive", out var serde);

        await _manager.RunAsync(rand, _toolchain, _ => { });
        await _manager.RunAsync(serde, _toolchain, _ => { });

        var remembered = _manager.NotebookDependencies.Load();
        Assert.Equal(["rand = \"0.9.2\"", "serde = { version = \"*\", features = [\"derive\"] }"], remembered.Select(c => c.ToManifestLine()));

        // The second fetch checked both crates together, so they can't conflict.
        var manifest = File.ReadAllText(Path.Combine(Scratch, "Cargo.toml"));
        Assert.Contains("rand = \"0.9.2\"", manifest);
        Assert.Contains("serde = { version = \"*\", features = [\"derive\"] }", manifest);
    }

    [Fact]
    public async Task ACrateAddedTwice_ReplacesItsEarlierLine()
    {
        CargoFetchLocks(("rand", "0.9.2"));
        _manager.TryParseDirective("%cargo add rand@0.8", out var old);
        _manager.TryParseDirective("%cargo add rand@0.9", out var newer);

        await _manager.RunAsync(old, _toolchain, _ => { });
        await _manager.RunAsync(newer, _toolchain, _ => { });

        Assert.Equal(["rand = \"0.9\""], _manager.NotebookDependencies.Load().Select(c => c.ToManifestLine()));
    }

    [Fact]
    public async Task ACrateCargoCannotFetch_IsNotRemembered_AndTheOutputSaysWhy()
    {
        _launcher.Behavior = (_, process) =>
        {
            process.WriteError("error: no matching package named `fry_no_such_crate` found\n");
            process.Exit(101);
            return Task.CompletedTask;
        };
        var output = new List<string>();
        _manager.TryParseDirective("%cargo add fry_no_such_crate", out var command);

        var result = await _manager.RunAsync(command, _toolchain, output.Add);

        Assert.False(result.Success);
        Assert.Null(result.DirectiveToInsert);
        Assert.Contains("fry_no_such_crate", result.Message);
        Assert.Empty(_manager.NotebookDependencies.Load());
        Assert.Contains(output, o => o.Contains("no matching package named"));
        Assert.Contains(output, o => o.Contains("exit code 101"));
    }

    [Fact]
    public async Task WithoutCargo_TheGuidanceIsShown()
    {
        var missing = new FakeHostEnvironment(FakeOs.MacOS);
        var manager = new RustPackageManager(_root, toolchains: null, _launcher, missing);
        var output = new List<string>();
        manager.TryParseDirective("%cargo add rand", out var command);

        var result = await manager.RunAsync(command, _toolchain, output.Add);

        Assert.False(result.Success);
        Assert.Empty(_launcher.Started);
        Assert.Contains(output, o => o.Contains("Rust isn't installed"));
    }

    [Fact]
    public async Task ABadCommand_IsRefusedBeforeAnythingRuns()
    {
        var result = await _manager.RunAsync(new PdfEditorApp.Plugins.CSharpEditor.Services.Packages.PackageCommand("%cargo add", ["add"]), _toolchain, _ => { });

        Assert.False(result.Success);
        Assert.Contains("Name a crate", result.Message);
        Assert.Empty(_launcher.Started);
    }

    [Fact]
    public async Task Stopping_KillsCargo_AndCancels()
    {
        _launcher.Behavior = async (_, process) =>
        {
            try { await Task.Delay(Timeout.Infinite, process.KilledToken); }
            catch (OperationCanceledException) { }
        };
        using var cts = new CancellationTokenSource();
        _manager.TryParseDirective("%cargo add rand", out var command);

        var run = _manager.RunAsync(command, _toolchain, _ => { }, cts.Token);
        await Task.Delay(100);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    [Fact]
    public void TheStore_IgnoresGarbageAndTheDisplayCrate()
    {
        var store = new RustNotebookDependencies(_root);
        Directory.CreateDirectory(Path.GetDirectoryName(store.FilePath)!);
        File.WriteAllText(store.FilePath, "rand = \"0.9\"\nnot a crate line\nfry = \"1\"\n\nserde = \"1\"\n");

        Assert.Equal(["rand", "serde"], store.Load().Select(c => c.Name));
    }

    [Fact]
    public void ThePackageMap_PinsTheFeaturesTheCommonCratesNeed()
    {
        Assert.Equal("{ version = \"1\", features = [\"derive\"] }", RustPackageMap.DefaultValue("serde"));
        Assert.Equal("{ version = \"1\", features = [\"full\"] }", RustPackageMap.DefaultValue("tokio"));
        Assert.Equal("\"*\"", RustPackageMap.DefaultValue("rand"));
        Assert.Equal("// #crate: rand = \"0.9\"", RustPackageMap.CrateLine("rand", "0.9"));
        Assert.Equal("// #crate: serde = { version = \"1\", features = [\"derive\"] }", RustPackageMap.CrateLine("serde", "1"));
    }
}
