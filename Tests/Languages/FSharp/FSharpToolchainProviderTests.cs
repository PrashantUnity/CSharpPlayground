using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.FSharp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class FSharpToolchainProviderTests : IDisposable
{
    private readonly string _tempDir;

    public FSharpToolchainProviderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FryPDF_FSToolchainTest_" + Guid.NewGuid().ToString("N"));
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

    private FSharpToolchainProvider CreateProvider(FakeHostEnvironment host, IProcessLauncher? launcher = null)
    {
        var settingsStore = new ToolchainSettingsStore(Path.Combine(_tempDir, "toolchains.json"));
        return new FSharpToolchainProvider(host, launcher ?? new FakeProcessLauncher(), settingsStore);
    }

    [Fact]
    public async Task OnMacOS_DiscoversDotnetFromStandardPaths()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        host.LoginShellPath = "/usr/local/share/dotnet:/opt/homebrew/bin:/usr/bin:/bin";
        host.AddFSharp("/usr/local/share/dotnet/dotnet", "10.0.100");

        var provider = CreateProvider(host);
        var resolution = await provider.ResolveAsync(new ToolchainQuery());

        Assert.True(resolution.IsFound);
        Assert.NotNull(resolution.Toolchain);
        Assert.Equal("/usr/local/share/dotnet/dotnet", resolution.Toolchain.ExecutablePath);
        Assert.Equal(new Version(10, 0, 100), resolution.Toolchain.Version);
        Assert.Contains("F#", resolution.Toolchain.DisplayName);
    }

    [Fact]
    public async Task OnLinux_DiscoversDotnetFromPath()
    {
        var host = new FakeHostEnvironment(FakeOs.Linux);
        host.Variables["PATH"] = "/usr/bin:/bin";
        host.AddFSharp("/usr/bin/dotnet", "9.0.200");

        var provider = CreateProvider(host);
        var resolution = await provider.ResolveAsync(new ToolchainQuery());

        Assert.True(resolution.IsFound);
        Assert.NotNull(resolution.Toolchain);
        Assert.Equal("/usr/bin/dotnet", resolution.Toolchain.ExecutablePath);
        Assert.Equal(new Version(9, 0, 200), resolution.Toolchain.Version);
    }

    [Fact]
    public async Task OnWindows_DiscoversDotnetFromProgramFiles()
    {
        var host = new FakeHostEnvironment(FakeOs.Windows);
        host.Variables["ProgramFiles"] = @"C:\Program Files";
        host.AddFSharp(@"C:\Program Files\dotnet\dotnet.exe", "10.0.100");

        var provider = CreateProvider(host);
        var resolution = await provider.ResolveAsync(new ToolchainQuery());

        Assert.True(resolution.IsFound);
        Assert.NotNull(resolution.Toolchain);
        Assert.Equal(@"C:\Program Files\dotnet\dotnet.exe", resolution.Toolchain.ExecutablePath.Replace('/', '\\'));
    }

    [Fact]
    public async Task MissingToolchain_ReturnsGuidanceWithInstallActions()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        var provider = CreateProvider(host);
        var resolution = await provider.ResolveAsync(new ToolchainQuery());

        Assert.False(resolution.IsFound);
        Assert.NotNull(resolution.Missing);
        Assert.Contains("F#", resolution.Missing.Summary);
        Assert.NotEmpty(resolution.Missing.Steps);
    }

    [Fact]
    public async Task ExplicitlySelectedPath_TakesPrecedence()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        host.AddFSharp("/opt/custom/dotnet", "10.0.0");
        host.AddFSharp("/usr/local/share/dotnet/dotnet", "9.0.0");
        host.LoginShellPath = "/usr/local/share/dotnet";

        var provider = CreateProvider(host);
        provider.Select("/opt/custom/dotnet");

        var resolution = await provider.ResolveAsync(new ToolchainQuery());

        Assert.True(resolution.IsFound);
        Assert.NotNull(resolution.Toolchain);
        Assert.Equal("/opt/custom/dotnet", resolution.Toolchain.ExecutablePath);
    }
}
