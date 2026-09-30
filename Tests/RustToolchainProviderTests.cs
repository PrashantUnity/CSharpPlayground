using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class RustToolchainProviderTests : IDisposable
{
    private readonly string _tempDir;

    public RustToolchainProviderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FryPDF_RustToolchainTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }
        catch (IOException)
        {
        }
    }

    private RustToolchainProvider CreateProvider(FakeHostEnvironment host, IProcessLauncher? launcher = null)
    {
        var settings = new ToolchainSettingsStore(Path.Combine(_tempDir, "toolchains.json"));
        return new RustToolchainProvider(host, launcher ?? new FakeProcessLauncher(), settings, Path.Combine(_tempDir, "rust"));
    }

    [Fact]
    public async Task OnAMac_RustupsCargo_IsFound_EvenThoughTheAppsOwnPathIsMinimal()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        host.Variables["PATH"] = "/usr/bin:/bin";
        host.LoginShellPath = "/Users/test/.cargo/bin:/usr/bin:/bin";
        host.AddRust("/Users/test/.cargo/bin/cargo", "1.94.0");

        var resolution = await CreateProvider(host).ResolveAsync(new ToolchainQuery());

        var rust = resolution.Toolchain;
        Assert.NotNull(rust);
        Assert.Equal("/Users/test/.cargo/bin/cargo", rust.ExecutablePath);
        Assert.Equal(new Version(1, 94, 0), rust.Version);
        Assert.Equal("Rust 1.94.0", rust.DisplayName);
        Assert.Equal("rustup", rust.Source);
        Assert.Equal("Rust 1.94.0 (rustup)", rust.Label);
        Assert.Equal("aarch64-apple-darwin", rust.Get("hostTriple"));
        Assert.Equal("stable", rust.Get("channel"));
        Assert.Equal("21.1.8", rust.Get("llvmVersion"));
        Assert.Equal("/Users/test/.cargo/bin/rustc", rust.Get("rustc"));
    }

    [Fact]
    public async Task WithNoPathAtAll_TheDefaultCargoHomeIsStillSearched()
    {
        var host = new FakeHostEnvironment(FakeOs.Linux);
        host.AddRust("/home/test/.cargo/bin/cargo", "1.85.1", host: "x86_64-unknown-linux-gnu");

        var resolution = await CreateProvider(host).ResolveAsync(new ToolchainQuery());

        Assert.True(resolution.IsFound);
        Assert.Equal("/home/test/.cargo/bin/cargo", resolution.Toolchain!.ExecutablePath);
        Assert.Equal("x86_64-unknown-linux-gnu", resolution.Toolchain.Get("hostTriple"));
    }

    [Fact]
    public async Task CargoHome_IsSearchedBeforePath()
    {
        var host = new FakeHostEnvironment(FakeOs.Linux);
        host.Variables["CARGO_HOME"] = "/opt/cargo";
        host.Variables["PATH"] = "/usr/bin";
        host.AddRust("/usr/bin/cargo", "1.75.0");
        host.AddRust("/opt/cargo/bin/cargo", "1.94.0");

        var resolution = await CreateProvider(host).ResolveAsync(new ToolchainQuery());

        Assert.Equal("/opt/cargo/bin/cargo", resolution.Toolchain!.ExecutablePath);
        Assert.Equal("CARGO_HOME", resolution.Toolchain.Source);
    }

    [Fact]
    public async Task OnAMac_HomebrewsCargo_IsLabelledHomebrew()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        host.LoginShellPath = "/opt/homebrew/bin:/usr/bin";
        host.AddRust("/opt/homebrew/bin/cargo", "1.88.0");

        var resolution = await CreateProvider(host).ResolveAsync(new ToolchainQuery());

        Assert.Equal("Homebrew", resolution.Toolchain!.Source);
    }

    [Fact]
    public async Task OnLinux_TheDistributionsCargo_IsFoundInUsrBin()
    {
        var host = new FakeHostEnvironment(FakeOs.Linux);
        host.Variables["PATH"] = "/usr/bin:/bin";
        host.AddRust("/usr/bin/cargo", "1.75.0");

        var resolution = await CreateProvider(host).ResolveAsync(new ToolchainQuery());

        Assert.Equal("/usr/bin/cargo", resolution.Toolchain!.ExecutablePath);
        Assert.Equal("System", resolution.Toolchain.Source);
    }

    [Fact]
    public async Task OnWindows_CargoIsFoundInTheUsersCargoFolder()
    {
        var host = new FakeHostEnvironment(FakeOs.Windows);
        host.Variables["PATH"] = @"C:\Windows\System32";
        host.AddRust(@"C:\Users\test\.cargo\bin\cargo.exe", "1.94.0", host: "x86_64-pc-windows-msvc");

        var resolution = await CreateProvider(host).ResolveAsync(new ToolchainQuery());

        Assert.True(resolution.IsFound);
        Assert.Equal(FakeHostEnvironment.Normalize(@"C:\Users\test\.cargo\bin\cargo.exe"), FakeHostEnvironment.Normalize(resolution.Toolchain!.ExecutablePath));
        Assert.Equal("x86_64-pc-windows-msvc", resolution.Toolchain.Get("hostTriple"));
        Assert.Equal("rustup", resolution.Toolchain.Source);
    }

    [Fact]
    public async Task ANightlyToolchain_SaysSoInItsName()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        host.LoginShellPath = "/Users/test/.cargo/bin";
        host.AddRust("/Users/test/.cargo/bin/cargo", "1.97.0", channel: "nightly");

        var resolution = await CreateProvider(host).ResolveAsync(new ToolchainQuery());

        Assert.Equal(new Version(1, 97, 0), resolution.Toolchain!.Version);
        Assert.Equal("Rust 1.97.0 (nightly)", resolution.Toolchain.DisplayName);
        Assert.Equal("nightly", resolution.Toolchain.Get("channel"));
    }

    [Fact]
    public async Task CargoWithoutAnySiblingRustc_IsStillUsable()
    {
        var host = new FakeHostEnvironment(FakeOs.Linux);
        host.Variables["PATH"] = "/usr/bin";
        host.AddRust("/usr/bin/cargo", "1.80.0", hasRustc: false);

        var resolution = await CreateProvider(host).ResolveAsync(new ToolchainQuery());

        Assert.True(resolution.IsFound);
        Assert.Null(resolution.Toolchain!.Get("rustc"));
        Assert.Null(resolution.Toolchain.Get("hostTriple"));
    }

    [Fact]
    public async Task AnOlderRust_IsIgnored_AndTheGuidanceNamesIt()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        host.LoginShellPath = "/usr/local/bin";
        host.AddRust("/usr/local/bin/cargo", "1.60.0");

        var resolution = await CreateProvider(host).ResolveAsync(new ToolchainQuery());

        Assert.False(resolution.IsFound);
        Assert.Equal("Rust isn't installed", resolution.Missing!.Title);
        Assert.Contains("1.70", resolution.Missing.Summary);
        Assert.Contains("1.60.0", resolution.Missing.Summary);
    }

    [Fact]
    public async Task WhenNoRustIsInstalled_TheGuidanceIsActionable_ForEveryOs()
    {
        var mac = (await CreateProvider(new FakeHostEnvironment(FakeOs.MacOS)).ResolveAsync(new ToolchainQuery())).Missing!;
        var linux = (await CreateProvider(new FakeHostEnvironment(FakeOs.Linux)).ResolveAsync(new ToolchainQuery())).Missing!;
        var windows = (await CreateProvider(new FakeHostEnvironment(FakeOs.Windows)).ResolveAsync(new ToolchainQuery())).Missing!;

        Assert.Equal("https://rustup.rs", mac.DownloadUrl);
        Assert.Contains("sh.rustup.rs", string.Join(" ", mac.Steps));
        Assert.Contains("xcode-select --install", string.Join(" ", mac.Steps));
        Assert.Contains("build-essential", string.Join(" ", linux.Steps));
        Assert.Contains("winget install Rustlang.Rustup", string.Join(" ", windows.Steps));
        Assert.Contains("Visual Studio Build Tools", string.Join(" ", windows.Steps));
    }

    [Fact]
    public async Task ARustupWithNoToolchainInstalled_IsToldToInstallOne()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        host.LoginShellPath = "/Users/test/.cargo/bin";
        host.AddRust("/Users/test/.cargo/bin/cargo", "1.94.0", cargoError:
            "error: rustup could not choose a version of cargo to run, because one wasn't specified explicitly, and no default is configured.");

        var resolution = await CreateProvider(host).ResolveAsync(new ToolchainQuery());

        Assert.False(resolution.IsFound);
        Assert.Contains("rustup is installed", resolution.Missing!.Summary);
        Assert.Contains("rustup default stable", string.Join(" ", resolution.Missing.Steps));
    }

    [Fact]
    public async Task ManualSelection_PersistsAndOverridesAutoDiscovery()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        host.LoginShellPath = "/Users/test/.cargo/bin";
        host.AddRust("/Users/test/.cargo/bin/cargo", "1.94.0");
        host.AddRust("/custom/rust/bin/cargo", "1.90.0");

        var provider = CreateProvider(host);
        provider.Select("/custom/rust/bin/cargo");

        var resolution = await provider.ResolveAsync(new ToolchainQuery());
        Assert.Equal("/custom/rust/bin/cargo", resolution.Toolchain!.ExecutablePath);
        Assert.Equal(new Version(1, 90, 0), resolution.Toolchain.Version);
        Assert.Equal("Selected", resolution.Toolchain.Source);
        Assert.Equal("/custom/rust/bin/cargo", provider.SelectedPath);
    }

    [Fact]
    public async Task TheList_OffersEachRustupToolchain_ButAProxyAndItsToolchainAreOne()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        host.LoginShellPath = "/Users/test/.cargo/bin";
        host.AddRust("/Users/test/.cargo/bin/cargo", "1.94.0");
        host.AddRust("/Users/test/.rustup/toolchains/stable-aarch64-apple-darwin/bin/cargo", "1.94.0");
        host.AddRust("/Users/test/.rustup/toolchains/nightly-aarch64-apple-darwin/bin/cargo", "1.97.0", channel: "nightly");

        var list = await CreateProvider(host).ListAsync(new ToolchainQuery());

        Assert.Equal(2, list.Count);
        Assert.Contains(list, t => t.Version == new Version(1, 94, 0) && t.Source == "rustup");
        var nightly = Assert.Single(list, t => t.Get("channel") == "nightly");
        Assert.Equal("rustup toolchain nightly-aarch64-apple-darwin", nightly.Source);
    }

    [Fact]
    public async Task TheProbeIsRunOncePerCandidate_UntilRefreshed()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        host.LoginShellPath = "/Users/test/.cargo/bin";
        host.AddRust("/Users/test/.cargo/bin/cargo", "1.94.0");
        var provider = CreateProvider(host);

        await provider.ResolveAsync(new ToolchainQuery());
        await provider.ResolveAsync(new ToolchainQuery());
        var probesBefore = host.Commands.Count(c => c.Arguments.FirstOrDefault() == "--version");
        provider.Refresh();
        await provider.ResolveAsync(new ToolchainQuery());
        var probesAfter = host.Commands.Count(c => c.Arguments.FirstOrDefault() == "--version");

        Assert.Equal(1, probesBefore);
        Assert.Equal(2, probesAfter);
    }

    [Fact]
    public void TheActions_MatchTheOs()
    {
        var mac = CreateProvider(new FakeHostEnvironment(FakeOs.MacOS)).Actions.Select(a => a.Id).ToList();
        var linux = CreateProvider(new FakeHostEnvironment(FakeOs.Linux)).Actions.Select(a => a.Id).ToList();
        var windows = CreateProvider(new FakeHostEnvironment(FakeOs.Windows)).Actions.Select(a => a.Id).ToList();

        Assert.Equal(["rustup-init", "install-xcode-clt", "open-download"], mac);
        Assert.Equal(["rustup-init", "open-download"], linux);
        Assert.Equal(["winget-install-rustup", "winget-install-vs", "open-download"], windows);
    }

    [Fact]
    public void RustupUpdate_IsOfferedOnlyWhenRustupIsInstalled()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        var provider = CreateProvider(host);
        Assert.DoesNotContain(provider.Actions, a => a.Id == "rustup-update");

        host.AddFile("/Users/test/.cargo/bin/rustup");

        Assert.Contains(provider.Actions, a => a.Id == "rustup-update");
    }

    [Fact]
    public async Task ClearCache_DeletesTheSharedBuildFolder()
    {
        var provider = CreateProvider(new FakeHostEnvironment(FakeOs.MacOS));
        var cache = provider.BuildCacheFolder;
        Directory.CreateDirectory(Path.Combine(cache, "debug"));
        File.WriteAllText(Path.Combine(cache, "debug", "x"), "x");
        Assert.Contains(provider.Actions, a => a.Id == "clear-cache");

        var output = new List<string>();
        var result = await provider.RunActionAsync("clear-cache", new ToolchainQuery(), output.Add);

        Assert.True(result.Success);
        Assert.False(Directory.Exists(cache));
        Assert.DoesNotContain(provider.Actions, a => a.Id == "clear-cache");
    }

    [Fact]
    public async Task AnUnknownAction_IsRefused()
    {
        var result = await CreateProvider(new FakeHostEnvironment(FakeOs.MacOS)).RunActionAsync("nope", new ToolchainQuery(), _ => { });

        Assert.False(result.Success);
    }
}
