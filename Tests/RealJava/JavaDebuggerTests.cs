using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using Xunit;

namespace CSharpEditorPlugin.Tests.RealJava;

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

    [JavaFact]
    public async Task JavaDebugger_Quicksort_RecursiveStepping()
    {
        var java = TestJava.Require();
        var services = TestJava.Services(Path.Combine(_dir, ".studio"));
        var lang = (JavaLanguage)services.Registry.Get(LanguageIds.Java)!;

        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var sourcePath = Path.Combine(repoRoot, "tools", "UiSnapshots", "Samples", "Quicksort.java");
        var sourceCode = File.ReadAllText(sourcePath);
        var copiedSourcePath = Write("Quicksort.java", sourceCode);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var pauses = new List<DebugPausedEventArgs>();
        var pauseTcs = new TaskCompletionSource<DebugPausedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

        var launchContext = new DebugLaunchContext(
            ScriptId: "quicksort-test",
            SourceFilePath: copiedSourcePath,
            SourceCode: sourceCode,
            Breakpoints: [new() { LineNumber = 11, IsEnabled = true }],
            Toolchain: new ToolchainResolution(java),
            OnLiveOutput: s => Console.WriteLine("[LIVE] " + s),
            CancellationToken: cts.Token);

        await using var session = await lang.Debugger!.LaunchAsync(launchContext, cts.Token);
        session.Paused += args =>
        {
            lock (pauses)
            {
                pauses.Add(args);
                pauseTcs.TrySetResult(args);
            }
        };

        async Task<DebugPausedEventArgs> StepAndAwaitPauseAsync(Func<Task> stepAction)
        {
            lock (pauses)
            {
                pauseTcs = new TaskCompletionSource<DebugPausedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            await stepAction();
            return await pauseTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        var initialPause = await pauseTcs.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(11, initialPause.LineNumber);
        Assert.Contains("main", initialPause.CallStack.FirstOrDefault()?.MethodName);

        // Step 1: Step into quicksort(data, 0, data.length - 1)
        var p1 = await StepAndAwaitPauseAsync(() => session.StepIntoAsync(cts.Token));
        Assert.Equal(19, p1.LineNumber);
        Assert.Equal(2, p1.CallStack.Count);
        Assert.Contains("quicksort", p1.CallStack[0].MethodName);

        // Step 2: Step over line 19 (if (low < high))
        var p2 = await StepAndAwaitPauseAsync(() => session.StepOverAsync(cts.Token));
        Assert.Equal(20, p2.LineNumber);
        Assert.Equal(2, p2.CallStack.Count);

        // Step 3: Step over line 20 (int pi = partition(...))
        var p3 = await StepAndAwaitPauseAsync(() => session.StepOverAsync(cts.Token));
        Assert.Equal(21, p3.LineNumber);
        Assert.Equal(2, p3.CallStack.Count);

        // Step 4: Step into recursive quicksort at line 21 (low=0, high=-1)
        var p4 = await StepAndAwaitPauseAsync(() => session.StepIntoAsync(cts.Token));
        Assert.Equal(19, p4.LineNumber);
        Assert.Equal(3, p4.CallStack.Count);

        // Step 5: Step over inside recursive quicksort (at line 19 where 0 < -1 is false)
        var p5 = await StepAndAwaitPauseAsync(() => session.StepOverAsync(cts.Token));
        Assert.Equal(24, p5.LineNumber);
        Assert.Equal(3, p5.CallStack.Count);

        // Step 6: Step over line 24 (return from first recursive quicksort)
        var p6 = await StepAndAwaitPauseAsync(() => session.StepOverAsync(cts.Token));
        Assert.Equal(22, p6.LineNumber);
        Assert.Equal(2, p6.CallStack.Count);

        // Step 7: Step into second recursive quicksort at line 22 (quicksort(arr, pi + 1, high))
        var p7 = await StepAndAwaitPauseAsync(() => session.StepIntoAsync(cts.Token));
        Assert.Equal(19, p7.LineNumber);
        Assert.Equal(3, p7.CallStack.Count);

        // Step 8: Step over line 19 (if (low < high) where low=1, high=10)
        var p8 = await StepAndAwaitPauseAsync(() => session.StepOverAsync(cts.Token));
        Assert.Equal(20, p8.LineNumber);
        Assert.Equal(3, p8.CallStack.Count);

        // Step 9: Step over line 20 (partition) -> advances to line 21 (quicksort(arr, low, pi - 1))
        var p9 = await StepAndAwaitPauseAsync(() => session.StepOverAsync(cts.Token));
        Assert.Equal(21, p9.LineNumber);
        Assert.Equal(3, p9.CallStack.Count);

        // Step 10: Step over line 21 (quicksort(arr, low, pi - 1))
        var p10 = await StepAndAwaitPauseAsync(() => session.StepOverAsync(cts.Token));
        Assert.Equal(22, p10.LineNumber);
        Assert.Equal(3, p10.CallStack.Count);

        // Step 11: Step over line 22 (quicksort(arr, pi + 1, high)) -> finishes sort, returns to main
        var p11 = await StepAndAwaitPauseAsync(() => session.StepOverAsync(cts.Token));
        Assert.Equal(13, p11.LineNumber);
        Assert.Single(p11.CallStack);
        Assert.Contains("main", p11.CallStack[0].MethodName);

        await session.StopAsync(cts.Token);
    }

    [JavaFact]
    public async Task JavaDebugger_Quicksort_StepInFirst_ThenStepOverSecond()
    {
        var java = TestJava.Require();
        var services = TestJava.Services(Path.Combine(_dir, ".studio"));
        var lang = (JavaLanguage)services.Registry.Get(LanguageIds.Java)!;

        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var sourcePath = Path.Combine(repoRoot, "tools", "UiSnapshots", "Samples", "Quicksort.java");
        var sourceCode = File.ReadAllText(sourcePath);
        var copiedSourcePath = Write("Quicksort2.java", sourceCode);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var pauseTcs = new TaskCompletionSource<DebugPausedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

        var launchContext = new DebugLaunchContext(
            ScriptId: "quicksort2-test",
            SourceFilePath: copiedSourcePath,
            SourceCode: sourceCode,
            Breakpoints: [new() { LineNumber = 11, IsEnabled = true }],
            Toolchain: new ToolchainResolution(java),
            OnLiveOutput: null,
            CancellationToken: cts.Token);

        await using var session = await lang.Debugger!.LaunchAsync(launchContext, cts.Token);
        session.Paused += args => pauseTcs.TrySetResult(args);

        async Task<DebugPausedEventArgs> StepAndAwaitPauseAsync(Func<Task> stepAction)
        {
            pauseTcs = new TaskCompletionSource<DebugPausedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            await stepAction();
            return await pauseTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        var initialPause = await pauseTcs.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(11, initialPause.LineNumber);

        // Step in to quicksort at line 19
        var p1 = await StepAndAwaitPauseAsync(() => session.StepIntoAsync(cts.Token));
        Assert.Equal(19, p1.LineNumber);

        // Step over line 19 -> 20
        var p2 = await StepAndAwaitPauseAsync(() => session.StepOverAsync(cts.Token));
        Assert.Equal(20, p2.LineNumber);

        // Step over line 20 -> 21
        var p3 = await StepAndAwaitPauseAsync(() => session.StepOverAsync(cts.Token));
        Assert.Equal(21, p3.LineNumber);

        // Step in on first quicksort (line 21) -> enters recursive call at line 19
        var p4 = await StepAndAwaitPauseAsync(() => session.StepIntoAsync(cts.Token));
        Assert.Equal(19, p4.LineNumber);
        Assert.Equal(3, p4.CallStack.Count);

        // Step over line 19 -> reaches line 24 (empty branch)
        var p5 = await StepAndAwaitPauseAsync(() => session.StepOverAsync(cts.Token));
        Assert.Equal(24, p5.LineNumber);

        // Step over line 24 -> returns to caller at line 22 (the second quicksort call)
        var p6 = await StepAndAwaitPauseAsync(() => session.StepOverAsync(cts.Token));
        Assert.Equal(22, p6.LineNumber);
        Assert.Equal(2, p6.CallStack.Count);

        // Step over second quicksort (line 22) -> runs remaining recursive sort, returns cleanly to main at line 13
        var p7 = await StepAndAwaitPauseAsync(() => session.StepOverAsync(cts.Token));
        Assert.Equal(13, p7.LineNumber);
        Assert.Single(p7.CallStack);
        Assert.Contains("main", p7.CallStack[0].MethodName);

        await session.StopAsync(cts.Token);
    }
}
