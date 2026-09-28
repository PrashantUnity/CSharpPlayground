using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class CppDebuggerProviderTests : IDisposable
{
    private readonly string _baseDir = Path.Combine(Path.GetTempPath(), "FryPDF_CppDbg_" + Guid.NewGuid().ToString("N"));

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

    [Fact]
    public async Task ResolveDebuggerAsync_WhenLldbDapAvailable_ReturnsAvailable()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        host.AddFile("/opt/homebrew/opt/llvm/bin/lldb-dap");
        host.OnCommand = (file, args) =>
        {
            if (file == "/opt/homebrew/opt/llvm/bin/lldb-dap" && args.Contains("--version"))
            {
                return new CommandResult(0, "lldb-dap: Homebrew LLVM version 22.1.7\nliblldb: lldb version 22.1.7", "", false);
            }
            return new CommandResult(-1, "", "not found", false);
        };

        var processes = new FakeProcessLauncher();
        var settings = new ToolchainSettingsStore(Path.Combine(_baseDir, "toolchains.json"));
        var toolchain = new CppToolchainProvider(host, processes, settings, Path.Combine(_baseDir, "cpp"));
        var provider = new CppDebuggerProvider(toolchain, processes, host);

        var resolution = await provider.ResolveDebuggerAsync(null);

        Assert.True(resolution.IsAvailable);
        Assert.Equal("lldb-dap", resolution.DebuggerName);
        Assert.Contains("22.1.7", resolution.Version);
        Assert.Null(resolution.MissingGuidance);
    }

    [Fact]
    public async Task ResolveDebuggerAsync_WhenDebuggerMissing_ReturnsGuidance()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        var processes = new FakeProcessLauncher();
        var settings = new ToolchainSettingsStore(Path.Combine(_baseDir, "toolchains.json"));
        var toolchain = new CppToolchainProvider(host, processes, settings, Path.Combine(_baseDir, "cpp"));
        var provider = new CppDebuggerProvider(toolchain, processes, host);

        var resolution = await provider.ResolveDebuggerAsync(null);

        Assert.False(resolution.IsAvailable);
        Assert.NotNull(resolution.MissingGuidance);
        Assert.Contains("brew install llvm", resolution.MissingGuidance.Steps);
    }

    [Fact]
    public void CppLanguage_ExposesFullDebuggingAndNotebookCapabilities()
    {
        var host = new FakeHostEnvironment(FakeOs.MacOS);
        var services = new StudioLanguageServices(
            Path.Combine(_baseDir, "studio"),
            host,
            new FakeProcessLauncher());

        var language = services.Registry.Get(LanguageIds.Cpp);
        Assert.NotNull(language);
        Assert.True(language.IsCompiled);
        Assert.True(language.Has(LanguageCapabilities.StandardInput));
        Assert.True(language.Has(LanguageCapabilities.Debugging));
        Assert.True(language.Has(LanguageCapabilities.Breakpoints));
        Assert.True(language.Has(LanguageCapabilities.NotebookCells));
        Assert.True(language.Has(LanguageCapabilities.ValueSharing));
        Assert.NotNull(language.Debugger);
        Assert.NotNull(language.NotebookKernels);
    }
}
