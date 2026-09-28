using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.RealJava;

[Collection(RealJavaCollection.Name)]
public class JavaDebuggerTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_JavaDbg_" + Guid.NewGuid().ToString("N"));

    public JavaDebuggerTests()
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

    [JavaFact]
    public async Task JavaDebuggerProvider_ResolvesJdb_WhenJdkInstalled()
    {
        var java = TestJava.Require();
        var services = TestJava.Services(Path.Combine(_dir, ".studio"));
        var lang = (JavaLanguage)services.Registry.Get(LanguageIds.Java)!;

        Assert.NotNull(lang.Debugger);
        var resolution = await lang.Debugger.ResolveDebuggerAsync(new ToolchainResolution(java));

        Assert.True(resolution.IsAvailable);
        Assert.NotNull(resolution.ExecutablePath);
        Assert.Contains("jdb", resolution.DebuggerName, StringComparison.OrdinalIgnoreCase);
    }

    [JavaFact]
    public async Task JavaDebugger_HitsBreakpoint_PausesAndPopulatesLocals()
    {
        var java = TestJava.Require();
        var services = TestJava.Services(Path.Combine(_dir, ".studio"));
        var lang = (JavaLanguage)services.Registry.Get(LanguageIds.Java)!;

        var sourcePath = Write("Calc.java", """
            public class Calc {
                public static void main(String[] args) {
                    int x = 42;
                    int y = 58;
                    int sum = x + y;
                    System.out.println("Sum = " + sum);
                }
            }
            """);

        using var cts = new CancellationTokenSource(Patience);
        var pausedTcs = new TaskCompletionSource<DebugPausedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

        var breakpoints = new List<BreakpointItem>
        {
            new() { LineNumber = 6, IsEnabled = true } // int sum = x + y;
        };

        var launchContext = new DebugLaunchContext(
            ScriptId: "calc-test",
            SourceFilePath: sourcePath,
            SourceCode: File.ReadAllText(sourcePath),
            Breakpoints: breakpoints,
            Toolchain: new ToolchainResolution(java),
            OnLiveOutput: null,
            CancellationToken: cts.Token);

        await using var session = await lang.Debugger!.LaunchAsync(launchContext, cts.Token);
        session.Paused += args => pausedTcs.TrySetResult(args);

        var pausedArgs = await pausedTcs.Task.WaitAsync(Patience);

        Assert.Equal(6, pausedArgs.LineNumber);
        Assert.NotEmpty(pausedArgs.CallStack);
        Assert.Contains(pausedArgs.CallStack, f => f.MethodName.Contains("main"));

        Assert.NotEmpty(pausedArgs.Locals);
        Assert.Contains(pausedArgs.Locals, l => l.Name == "x" && l.ValueDisplay.Contains("42"));
        Assert.Contains(pausedArgs.Locals, l => l.Name == "y" && l.ValueDisplay.Contains("58"));

        // Test evaluation while paused
        var evalResult = await session.EvaluateAsync("x", frameIndex: null, EvaluationContext.Hover, cts.Token);
        Assert.True(evalResult.Success);
        Assert.Contains("42", evalResult.Value);

        // Resume and verify termination
        var termTcs = new TaskCompletionSource<DebugTerminatedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Terminated += args => termTcs.TrySetResult(args);

        await session.ContinueAsync(cts.Token);
        var termArgs = await termTcs.Task.WaitAsync(Patience);

        Assert.Equal(0, termArgs.ExitCode);
    }

    [JavaFact]
    public async Task JavaDebugger_CompilationError_ThrowsDebugCompilationException()
    {
        var java = TestJava.Require();
        var services = TestJava.Services(Path.Combine(_dir, ".studio"));
        var lang = (JavaLanguage)services.Registry.Get(LanguageIds.Java)!;

        var sourcePath = Write("SyntaxError.java", """
            public class SyntaxError {
                public static void main(String[] args) {
                    int a = ;
                }
            }
            """);

        using var cts = new CancellationTokenSource(Patience);

        var launchContext = new DebugLaunchContext(
            ScriptId: "syntax-error-test",
            SourceFilePath: sourcePath,
            SourceCode: File.ReadAllText(sourcePath),
            Breakpoints: [new() { LineNumber = 3, IsEnabled = true }],
            Toolchain: new ToolchainResolution(java),
            OnLiveOutput: null,
            CancellationToken: cts.Token);

        var ex = await Assert.ThrowsAsync<DebugCompilationException>(() =>
            lang.Debugger!.LaunchAsync(launchContext, cts.Token));

        Assert.NotEmpty(ex.Diagnostics);
        Assert.Contains(ex.Diagnostics, d => d.Id == "JAVAC" || d.Message.Length > 0);
    }

    [JavaFact]
    public async Task JavaDebugger_StepOver_AdvancesToNextLine()
    {
        var java = TestJava.Require();
        var services = TestJava.Services(Path.Combine(_dir, ".studio"));
        var lang = (JavaLanguage)services.Registry.Get(LanguageIds.Java)!;

        var sourcePath = Write("Stepping.java", """
            public class Stepping {
                public static void main(String[] args) {
                    int a = 10;
                    int b = 20;
                    int c = a + b;
                    System.out.println("Result: " + c);
                }
            }
            """);

        using var cts = new CancellationTokenSource(Patience);
        var pauseTcs1 = new TaskCompletionSource<DebugPausedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pauseTcs2 = new TaskCompletionSource<DebugPausedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

        int pauseCount = 0;

        var launchContext = new DebugLaunchContext(
            ScriptId: "stepping-test",
            SourceFilePath: sourcePath,
            SourceCode: File.ReadAllText(sourcePath),
            Breakpoints: [new() { LineNumber = 3, IsEnabled = true }],
            Toolchain: new ToolchainResolution(java),
            OnLiveOutput: null,
            CancellationToken: cts.Token);

        await using var session = await lang.Debugger!.LaunchAsync(launchContext, cts.Token);
        session.Paused += args =>
        {
            var count = Interlocked.Increment(ref pauseCount);
            if (count == 1) pauseTcs1.TrySetResult(args);
            else if (count == 2) pauseTcs2.TrySetResult(args);
        };

        var firstPause = await pauseTcs1.Task.WaitAsync(Patience);
        Assert.Equal(3, firstPause.LineNumber);

        // Step over to line 4
        await session.StepOverAsync(cts.Token);
        var secondPause = await pauseTcs2.Task.WaitAsync(Patience);
        Assert.Equal(4, secondPause.LineNumber);

        // Stop session cleanly
        await session.StopAsync(cts.Token);
    }

    [JavaFact]
    public async Task JavaDebugger_WithMismatchedFileName_DebugsSeamlessly()
    {
        var java = TestJava.Require();
        var services = TestJava.Services(Path.Combine(_dir, ".studio"));
        var lang = (JavaLanguage)services.Registry.Get(LanguageIds.Java)!;

        var sourcePath = Write("script_202945.java", """
            public class Quicksort {
                public static void main(String[] args) {
                    int[] data = { 64, 34, 25 };
                    int first = data[0];
                    System.out.println("First = " + first);
                }
            }
            """);

        using var cts = new CancellationTokenSource(Patience);
        var pausedTcs = new TaskCompletionSource<DebugPausedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

        var launchContext = new DebugLaunchContext(
            ScriptId: "scratch-test",
            SourceFilePath: sourcePath,
            SourceCode: File.ReadAllText(sourcePath),
            Breakpoints: [new() { LineNumber = 4, IsEnabled = true }],
            Toolchain: new ToolchainResolution(java),
            OnLiveOutput: null,
            CancellationToken: cts.Token);

        await using var session = await lang.Debugger!.LaunchAsync(launchContext, cts.Token);
        session.Paused += args => pausedTcs.TrySetResult(args);

        var pausedArgs = await pausedTcs.Task.WaitAsync(Patience);
        Assert.Equal(4, pausedArgs.LineNumber);
        Assert.Contains(pausedArgs.CallStack, f => f.MethodName.Contains("main"));
        Assert.Contains(pausedArgs.Locals, l => l.Name == "data");

        await session.StopAsync(cts.Token);
    }
}
