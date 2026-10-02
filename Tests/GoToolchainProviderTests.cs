using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class GoToolchainProviderTests : IDisposable
{
    private readonly string _tempDir;

    public GoToolchainProviderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FryPDF_GoToolchainTest_" + Guid.NewGuid().ToString("N"));
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

    private GoToolchainProvider CreateProvider(FakeHostEnvironment host, IProcessLauncher? launcher = null)
    {
        var settingsStore = new ToolchainSettingsStore(Path.Combine(_tempDir, "toolchains.json"));
        return new GoToolchainProvider(host, launcher ?? new FakeProcessLauncher(), settingsStore, Path.Combine(_tempDir, "go_env"));
    }

    [Fact]
    public async Task OnMacOS_DiscoversGoFromStandardPaths()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        host.LoginShellPath = "/opt/homebrew/bin:/usr/bin:/bin";
        host.AddGo("/opt/homebrew/bin/go", "1.22.4");

        var provider = CreateProvider(host);
        var resolution = await provider.ResolveAsync(new ToolchainQuery());

        Assert.True(resolution.IsFound);
        Assert.NotNull(resolution.Toolchain);
        Assert.Equal("/opt/homebrew/bin/go", resolution.Toolchain.ExecutablePath);
        Assert.Equal(new Version(1, 22, 4), resolution.Toolchain.Version);
        Assert.Equal("Go 1.22.4", resolution.Toolchain.DisplayName);
    }

    [Fact]
    public async Task OnLinux_DiscoversGoFromUsrBin()
    {
        var host = new FakeHostEnvironment(FakeOs.Linux);
        host.Variables["PATH"] = "/usr/bin:/bin";
        host.AddGo("/usr/bin/go", "1.21.3");

        var provider = CreateProvider(host);
        var resolution = await provider.ResolveAsync(new ToolchainQuery());

        Assert.True(resolution.IsFound);
        Assert.NotNull(resolution.Toolchain);
        Assert.Equal("/usr/bin/go", resolution.Toolchain.ExecutablePath);
        Assert.Equal(new Version(1, 21, 3), resolution.Toolchain.Version);
    }

    [Fact]
    public async Task OnWindows_DiscoversGoFromProgramFiles()
    {
        var host = new FakeHostEnvironment(FakeOs.Windows);
        host.Variables["PATH"] = @"C:\Program Files\Go\bin;C:\Windows\System32";
        host.AddGo(@"C:\Program Files\Go\bin\go.exe", "1.23.0");

        var provider = CreateProvider(host);
        var resolution = await provider.ResolveAsync(new ToolchainQuery());

        Assert.True(resolution.IsFound);
        Assert.NotNull(resolution.Toolchain);
        Assert.Equal(FakeHostEnvironment.Normalize(@"C:\Program Files\Go\bin\go.exe"), FakeHostEnvironment.Normalize(resolution.Toolchain.ExecutablePath));
        Assert.Equal(new Version(1, 23, 0), resolution.Toolchain.Version);
    }

    [Fact]
    public async Task OlderGo_IsIgnored_WhenBelowMinimumVersion()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        host.LoginShellPath = "/usr/local/bin:/usr/bin";
        host.AddGo("/usr/local/bin/go", "1.16.5"); // Below 1.18.0

        var provider = CreateProvider(host);
        var resolution = await provider.ResolveAsync(new ToolchainQuery());

        Assert.False(resolution.IsFound);
        Assert.NotNull(resolution.Missing);
        Assert.Contains("Go toolchain isn't installed", resolution.Missing.Title);
    }

    [Fact]
    public async Task MissingGo_ReturnsActionableGuidance()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        var provider = CreateProvider(host);
        var resolution = await provider.ResolveAsync(new ToolchainQuery());

        Assert.False(resolution.IsFound);
        Assert.NotNull(resolution.Missing);
        Assert.Equal("Go toolchain isn't installed", resolution.Missing.Title);
        Assert.Contains("brew install go", string.Join(" ", resolution.Missing.Steps));
        Assert.Equal("https://go.dev/dl/", resolution.Missing.DownloadUrl);
    }

    [Fact]
    public async Task ManualSelection_PersistsAndOverridesAutoDiscovery()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        host.AddGo("/opt/homebrew/bin/go", "1.22.4");
        host.AddGo("/custom/go/bin/go", "1.23.1");

        var provider = CreateProvider(host);
        provider.Select("/custom/go/bin/go");

        var resolution = await provider.ResolveAsync(new ToolchainQuery());
        Assert.True(resolution.IsFound);
        Assert.Equal("/custom/go/bin/go", resolution.Toolchain!.ExecutablePath);
        Assert.Equal(new Version(1, 23, 1), resolution.Toolchain.Version);
        Assert.Equal("Selected", resolution.Toolchain.Source);
    }
}
