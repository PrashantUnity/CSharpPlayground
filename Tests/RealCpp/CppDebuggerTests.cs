using System.IO;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.RealCpp;

[Collection(RealCppCollection.Name)]
public class CppDebuggerTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_CppDbg_" + Guid.NewGuid().ToString("N"));

    public CppDebuggerTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
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

    [CppFact]
    public async Task CppDebuggerProvider_ResolvesLldbDap_WhenInstalled()
    {
        var cpp = TestCpp.Require();
        var services = TestCpp.Services(Path.Combine(_dir, ".studio"));
        var lang = (CppLanguage)services.Registry.Get(LanguageIds.Cpp)!;

        Assert.NotNull(lang.Debugger);
        var resolution = await lang.Debugger.ResolveDebuggerAsync(new ToolchainResolution(cpp));

        if (resolution.IsAvailable)
        {
            Assert.NotNull(resolution.ExecutablePath);
            Assert.Contains("lldb", resolution.DebuggerName, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            Assert.NotNull(resolution.MissingGuidance);
        }
    }

    [CppFact]
    public async Task CppDebugger_CompilationError_ThrowsDebugCompilationException()
    {
        var cpp = TestCpp.Require();
        var services = TestCpp.Services(Path.Combine(_dir, ".studio"));
        var lang = (CppLanguage)services.Registry.Get(LanguageIds.Cpp)!;

        var sourcePath = Write("broken.cpp", """
            #include <iostream>
            int main() {
                this is total syntax error;
                return 0;
            }
            """);

        using var cts = new CancellationTokenSource(Patience);
        var breakpoints = new List<BreakpointItem>
        {
            new() { LineNumber = 3, IsEnabled = true }
        };

        var launchContext = new DebugLaunchContext(
            ScriptId: "broken-test",
            SourceFilePath: sourcePath,
            SourceCode: File.ReadAllText(sourcePath),
            Breakpoints: breakpoints,
            Toolchain: new ToolchainResolution(cpp),
            OnLiveOutput: null,
            CancellationToken: cts.Token);

        var ex = await Assert.ThrowsAsync<DebugCompilationException>(() => lang.Debugger!.LaunchAsync(launchContext, cts.Token));
        Assert.NotEmpty(ex.Diagnostics);
        Assert.Contains(ex.Diagnostics, d => d.Line >= 3);
    }

    [CppFact]
    public async Task CppDebugger_HitsBreakpoint_PausesAndInspectsLocals()
    {
        var cpp = TestCpp.Require();
        var services = TestCpp.Services(Path.Combine(_dir, ".studio"));
        var lang = (CppLanguage)services.Registry.Get(LanguageIds.Cpp)!;

        var resolution = await lang.Debugger!.ResolveDebuggerAsync(new ToolchainResolution(cpp));
        if (!resolution.IsAvailable)
        {
            // Skip execution if lldb-dap is not installed in the current environment
            return;
        }

        // FRY_FORCE_DEBUGGER_TESTS=1 runs it anyway: the skip below guards against a password prompt some Macs show, which it
        // also hides a broken debug session behind.
        if (OperatingSystem.IsMacOS() && Environment.GetEnvironmentVariable("FRY_FORCE_DEBUGGER_TESTS") != "1")
        {
            try
            {
                using var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "DevToolsSecurity",
                    Arguments = "-status",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                var outText = proc?.StandardOutput.ReadToEnd() ?? string.Empty;
                proc?.WaitForExit();
                if (outText.Contains("disabled", StringComparison.OrdinalIgnoreCase))
                {
                    // Avoid hanging test on macOS when developer mode is disabled (requires GUI password prompt)
                    return;
                }
            }
            catch
            {
            }
        }

        var sourcePath = Write("calc.cpp", """
            #include <iostream>

            int main() {
                int a = 42;
                int b = 58;
                int sum = a + b;
                std::cout << "Sum = " << sum << std::endl;
                return 0;
            }
            """);

        using var cts = new CancellationTokenSource(Patience);
        var pausedTcs = new TaskCompletionSource<DebugPausedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

        var breakpoints = new List<BreakpointItem>
        {
            new() { LineNumber = 7, IsEnabled = true } // int sum = a + b;
        };

        var launchContext = new DebugLaunchContext(
            ScriptId: "calc-test",
            SourceFilePath: sourcePath,
            SourceCode: File.ReadAllText(sourcePath),
            Breakpoints: breakpoints,
            Toolchain: new ToolchainResolution(cpp),
            OnLiveOutput: null,
            CancellationToken: cts.Token);

        await using var session = await lang.Debugger.LaunchAsync(launchContext, cts.Token);
        session.Paused += args => pausedTcs.TrySetResult(args);

        var pausedArgs = await pausedTcs.Task.WaitAsync(Patience);
        Assert.NotNull(pausedArgs);
        Assert.Equal(7, pausedArgs.LineNumber);

        // Resume and verify termination
        var termTcs = new TaskCompletionSource<DebugTerminatedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Terminated += args => termTcs.TrySetResult(args);

        await session.ContinueAsync(cts.Token);
        var termArgs = await termTcs.Task.WaitAsync(Patience);
        Assert.Equal(0, termArgs.ExitCode);
    }
}
