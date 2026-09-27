using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Python;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.Debugging;

public class DebuggerProviderTests
{
    [Fact]
    public void CSharpCoreClrCompiler_EmitsDllPdbAndRuntimeConfig()
    {
        var compilerService = new RoslynCompilerService();
        var coreClrCompiler = new CSharpCoreClrCompiler(compilerService);

        var tempDir = Path.Combine(Path.GetTempPath(), $"coreclr_test_{Guid.NewGuid():N}");
        try
        {
            var code = """
            var x = 10;
            var y = 20;
            Console.WriteLine(x + y);
            """;

            var (success, dllPath, diagnostics) = coreClrCompiler.CompileToStandaloneBinary(
                code,
                tempDir,
                assemblyName: "test_script");

            Assert.True(success, string.Join("; ", diagnostics.Select(d => d.Message)));
            Assert.NotNull(dllPath);
            Assert.True(File.Exists(dllPath));

            var pdbPath = Path.Combine(tempDir, "test_script.pdb");
            Assert.True(File.Exists(pdbPath));
            Assert.True(new FileInfo(pdbPath).Length > 0);

            var runtimeConfigPath = Path.Combine(tempDir, "test_script.runtimeconfig.json");
            Assert.True(File.Exists(runtimeConfigPath));
            var configContent = File.ReadAllText(runtimeConfigPath);
            Assert.Contains("Microsoft.NETCore.App", configContent);
            Assert.Contains("10.0.0", configContent);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task CSharpDebuggerProvider_FallsBackToInProcess_WhenNetCoreDbgMissing()
    {
        var compilerService = new RoslynCompilerService();
        var execEngine = new ScriptExecutionEngine();
        var scriptDebugger = new ScriptDebuggerService(compilerService, execEngine);
        var host = new FakeHostEnvironment(); // has no netcoredbg on PATH
        var launcher = new FakeProcessLauncher();

        var provider = new CSharpDebuggerProvider(
            compilerService,
            scriptDebugger,
            execEngine,
            launcher,
            host);

        var resolution = await provider.ResolveDebuggerAsync(null);
        Assert.True(resolution.IsAvailable);
        Assert.Contains("In-Process", resolution.DebuggerName);

        var context = new DebugLaunchContext(
            ScriptId: "test_script",
            SourceFilePath: "script.cs",
            SourceCode: "var x = 10; Console.WriteLine(x);",
            Breakpoints: Array.Empty<BreakpointItem>(),
            Toolchain: null,
            OnLiveOutput: null,
            CancellationToken: CancellationToken.None);

        await using var session = await provider.LaunchAsync(context);
        Assert.NotNull(session);
        Assert.IsType<InProcessRoslynDebugSession>(session);
    }

    [Fact]
    public async Task PythonDebuggerProvider_ResolvesMissingGuidance_WhenPythonMissing()
    {
        var host = new FakeHostEnvironment();
        var launcher = new FakeProcessLauncher();
        var settings = new ToolchainSettingsStore(Path.Combine(Path.GetTempPath(), $"toolchains_{Guid.NewGuid():N}.json"));
        var toolchain = new PythonToolchainProvider(host, launcher, settings, Path.GetTempPath());

        var provider = new PythonDebuggerProvider(toolchain, launcher, host);

        var resolution = await provider.ResolveDebuggerAsync(null);
        Assert.False(resolution.IsAvailable);
        Assert.Equal("debugpy (Python)", resolution.DebuggerName);
        Assert.NotNull(resolution.MissingGuidance);
    }

    [Fact]
    public async Task PythonDebuggerProvider_DetectsDebugpy_WhenProbeSucceeds()
    {
        var host = new FakeHostEnvironment();
        host.AddFile("/usr/local/bin/python3");
        var launcher = new FakeProcessLauncher
        {
            Behavior = (spec, p) =>
            {
                if (spec.Arguments.Contains("--version"))
                {
                    p.Write("1.8.0\n");
                    p.Exit(0);
                }
                return Task.CompletedTask;
            }
        };

        var settings = new ToolchainSettingsStore(Path.Combine(Path.GetTempPath(), $"toolchains_{Guid.NewGuid():N}.json"));
        var toolchain = new PythonToolchainProvider(host, launcher, settings, Path.GetTempPath());

        var toolchainInfo = new ToolchainInfo
        {
            LanguageId = "python",
            ExecutablePath = "/usr/local/bin/python3",
            Version = new Version(3, 12, 0),
            DisplayName = "Python 3.12",
            Source = "Test"
        };
        var toolchainResolution = ToolchainResolution.Found(toolchainInfo);

        var provider = new PythonDebuggerProvider(toolchain, launcher, host);

        var resolution = await provider.ResolveDebuggerAsync(toolchainResolution);
        Assert.True(resolution.IsAvailable);
        Assert.Equal("debugpy", resolution.DebuggerName);
        Assert.Equal("1.8.0", resolution.Version);
    }
}
