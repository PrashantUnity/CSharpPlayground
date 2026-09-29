using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class GoDebuggerProviderTests : IDisposable
{
    private readonly string _tempDir;

    public GoDebuggerProviderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FryPDF_GoDebuggerTest_" + Guid.NewGuid().ToString("N"));
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

    [Fact]
    public async Task WhenDelveNotFound_ReturnsMissingGuidance()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        host.AddGo("/opt/homebrew/bin/go", "1.22.4");

        var toolchain = new GoToolchainProvider(host, new FakeProcessLauncher(), new ToolchainSettingsStore(Path.Combine(_tempDir, "tc.json")), _tempDir);
        var debugger = new GoDebuggerProvider(toolchain, new FakeProcessLauncher(), host);

        var resolution = await debugger.ResolveDebuggerAsync(null);

        Assert.False(resolution.IsAvailable);
        Assert.Null(resolution.ExecutablePath);
        Assert.NotNull(resolution.MissingGuidance);
        Assert.Contains("Delve", resolution.MissingGuidance.Title);
        Assert.Contains("go install github.com/go-delve/delve/cmd/dlv@latest", string.Join(" ", resolution.MissingGuidance.Steps));
    }

    [Fact]
    public async Task WhenDelveExists_ReturnsAvailable()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        host.AddGo("/opt/homebrew/bin/go", "1.22.4");
        host.AddFile("/opt/homebrew/bin/dlv");
        host.OnCommand = (file, args) =>
        {
            if (file == "/opt/homebrew/bin/dlv" && args.Count > 0 && args[0] == "version")
            {
                return new CommandResult(0, "Delve Debugger\nVersion: 1.22.1\n", string.Empty, false);
            }
            return null;
        };

        var toolchain = new GoToolchainProvider(host, new FakeProcessLauncher(), new ToolchainSettingsStore(Path.Combine(_tempDir, "tc.json")), _tempDir);
        var debugger = new GoDebuggerProvider(toolchain, new FakeProcessLauncher(), host);

        var resolution = await debugger.ResolveDebuggerAsync(null);

        Assert.True(resolution.IsAvailable);
        Assert.Equal("/opt/homebrew/bin/dlv", resolution.ExecutablePath);
        Assert.Equal("1.22.1", resolution.Version);
    }
}
