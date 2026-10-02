using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.JavaScript;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>Which Node.js a script or cell runs with, on each OS, described with a pretend computer.</summary>
public class JavaScriptToolchainProviderTests : IDisposable
{
    private const string HomebrewNode = "/opt/homebrew/bin/node";
    private readonly string _baseDir = Path.Combine(Path.GetTempPath(), "FryPDF_JSToolchain_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_baseDir)) Directory.Delete(_baseDir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private JavaScriptToolchainProvider Provider(FakeHostEnvironment host, IProcessLauncher? launcher = null) =>
        new(host, launcher ?? new FakeProcessLauncher(), new ToolchainSettingsStore(Path.Combine(_baseDir, "toolchains.json")), Path.Combine(_baseDir, "javascript"));

    private static FakeHostEnvironment Mac()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        host.Variables["PATH"] = "/usr/bin:/bin:/usr/sbin:/sbin";
        return host;
    }

    [Fact]
    public async Task OnAMac_TheTerminalsNode_IsFound_EvenThoughTheAppsOwnPathIsMinimal()
    {
        var host = Mac();
        host.LoginShellPath = "/opt/homebrew/bin:/usr/bin:/bin";
        host.AddNode(HomebrewNode, "20.10.0");

        var node = (await Provider(host).ResolveAsync(new ToolchainQuery())).Toolchain;

        Assert.NotNull(node);
        Assert.Equal(HomebrewNode, node.ExecutablePath);
        Assert.Equal(new Version(20, 10, 0), node.Version);
        Assert.Equal("Node.js 20.10.0 (Homebrew)", node.Label);
    }

    [Fact]
    public async Task OlderNode_IsIgnored_WhenBelowMinimumVersion()
    {
        var host = Mac();
        host.LoginShellPath = "/usr/local/bin:/usr/bin";
        host.AddNode("/usr/local/bin/node", "16.14.0");

        var resolution = await Provider(host).ResolveAsync(new ToolchainQuery());

        Assert.False(resolution.IsFound);
        Assert.NotNull(resolution.Missing);
        Assert.Contains("16.14.0", resolution.Missing.Summary);
    }

    [Fact]
    public async Task UserChoice_WinsOverInstalledNode()
    {
        var host = Mac();
        host.LoginShellPath = "/opt/homebrew/bin";
        host.AddNode(HomebrewNode, "20.10.0");
        host.AddNode("/custom/bin/node", "22.1.0");

        var provider = Provider(host);
        provider.Select("/custom/bin/node");

        var resolution = await provider.ResolveAsync(new ToolchainQuery());

        Assert.True(resolution.IsFound);
        Assert.Equal("/custom/bin/node", resolution.Toolchain!.ExecutablePath);
        Assert.Equal("Node.js 22.1.0 (Selected)", resolution.Toolchain.Label);
    }

    [Fact]
    public async Task WhenNodeIsNotInstalled_GuidanceIsReturned()
    {
        var host = Mac();
        host.LoginShellPath = "/usr/bin";

        var resolution = await Provider(host).ResolveAsync(new ToolchainQuery());

        Assert.False(resolution.IsFound);
        Assert.NotNull(resolution.Missing);
        Assert.Equal("Node.js isn't installed", resolution.Missing.Title);
        Assert.Contains(resolution.Missing.Steps, s => s.Contains("brew install node"));
    }

    [Fact]
    public async Task OnWindows_NodeExeInProgramFiles_IsFound()
    {
        var host = new FakeHostEnvironment(FakeOs.Windows);
        host.Variables["ProgramFiles"] = @"C:\Program Files";
        host.Variables["PATH"] = @"C:\Windows\system32";
        host.AddNode(@"C:\Program Files\nodejs\node.exe", "20.9.0");

        var resolution = await Provider(host).ResolveAsync(new ToolchainQuery());

        Assert.True(resolution.IsFound);
        Assert.Equal(FakeHostEnvironment.Normalize(@"C:\Program Files\nodejs\node.exe"), FakeHostEnvironment.Normalize(resolution.Toolchain!.ExecutablePath));
        Assert.Equal("Node.js 20.9.0 (nodejs.org)", resolution.Toolchain.Label);
    }

    [Fact]
    public async Task ListAsync_ListsAvailableNodeRuntimes()
    {
        var host = Mac();
        host.LoginShellPath = "/opt/homebrew/bin:/usr/local/bin";
        host.AddNode(HomebrewNode, "20.10.0");
        host.AddNode("/usr/local/bin/node", "22.2.0");

        var list = await Provider(host).ListAsync(new ToolchainQuery());

        Assert.Equal(2, list.Count);
        Assert.Contains(list, n => n.Version == new Version(20, 10, 0));
        Assert.Contains(list, n => n.Version == new Version(22, 2, 0));
    }
}
