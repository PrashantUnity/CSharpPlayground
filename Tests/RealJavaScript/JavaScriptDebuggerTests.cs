using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.RealJavaScript;

/// <summary>JavaScript debugged with real Node.js: a breakpoint pauses the program and shows its variables.</summary>
[Collection(RealJavaScriptCollection.Name)]
public class JavaScriptDebuggerTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(90);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_JSDbg_" + Guid.NewGuid().ToString("N"));

    public JavaScriptDebuggerTests()
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

    [JavaScriptFact]
    public async Task ABreakpoint_PausesTheScript_AndItsLocalsAreShown()
    {
        var node = TestJavaScript.Require();
        var services = TestJavaScript.Services(Path.Combine(_dir, ".studio"));
        var language = services.Registry.Get(LanguageIds.JavaScript)!;

        var path = Path.Combine(_dir, "calc.js");
        File.WriteAllText(path, "const a = 41;\nconst b = 1;\nconst c = a + b;\nconsole.log(c);\n");
        using var cts = new CancellationTokenSource(Patience);
        var paused = new TaskCompletionSource<DebugPausedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var terminated = new TaskCompletionSource<DebugTerminatedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var output = new System.Text.StringBuilder();

        await using var session = await language.Debugger!.LaunchAsync(new DebugLaunchContext(
            "js-debug-test", path, File.ReadAllText(path),
            [new BreakpointItem { LineNumber = 3, IsEnabled = true }],
            new ToolchainResolution(node), text => output.Append(text), cts.Token), cts.Token);
        session.Paused += args => paused.TrySetResult(args);
        session.Terminated += args => terminated.TrySetResult(args);
        session.OutputReceived += text => output.Append(text);

        var pausedArgs = await paused.Task.WaitAsync(Patience);
        Assert.Equal(3, pausedArgs.LineNumber);
        var locals = pausedArgs.Locals.ToDictionary(v => v.Name);
        Assert.Equal("41", locals["a"].ValueDisplay);
        Assert.Equal("1", locals["b"].ValueDisplay);

        await session.ContinueAsync(cts.Token);
        var end = await terminated.Task.WaitAsync(Patience);
        Assert.Equal(0, end.ExitCode);
        Assert.Contains("42", output.ToString());
    }

    private DebugLaunchContext Context(string path, Action<string>? live, params int[] lines) => new(
        "js-debug-test", path, File.ReadAllText(path),
        lines.Select(l => new BreakpointItem { LineNumber = l, IsEnabled = true }).ToList(),
        new ToolchainResolution(TestJavaScript.Require()), live, CancellationToken.None);

    private string Write(string name, string code)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, code.Replace("\r\n", "\n"));
        return path;
    }

    private static async Task<DebugPausedEventArgs> NextPause(System.Collections.Concurrent.BlockingCollection<DebugPausedEventArgs> pauses, CancellationToken ct) =>
        await Task.Run(() => pauses.Take(ct), ct);

    [JavaScriptFact]
    public async Task ABreakpointOnTheFirstLine_IsHit()
    {
        var language = TestJavaScript.Services(Path.Combine(_dir, ".studio")).Registry.Get(LanguageIds.JavaScript)!;
        var path = Write("first.js", "const a = 1;\nconsole.log(a);\n");
        using var cts = new CancellationTokenSource(Patience);
        var pauses = new System.Collections.Concurrent.BlockingCollection<DebugPausedEventArgs>();

        await using var session = await language.Debugger!.LaunchAsync(Context(path, null, 1), cts.Token);
        session.Paused += args => pauses.Add(args);

        Assert.Equal(1, (await NextPause(pauses, cts.Token)).LineNumber);
    }

    [JavaScriptFact]
    public async Task Stepping_GoesOver_Into_AndOutOfCalls_AndShowsTheCallStack()
    {
        var language = TestJavaScript.Services(Path.Combine(_dir, ".studio")).Registry.Get(LanguageIds.JavaScript)!;
        var path = Write("steps.js", "function double(n) {\n  const result = n * 2;\n  return result;\n}\n\nconst x = 21;\nconst y = double(x);\nconsole.log(y);\n");
        using var cts = new CancellationTokenSource(Patience);
        var pauses = new System.Collections.Concurrent.BlockingCollection<DebugPausedEventArgs>();
        var terminated = new TaskCompletionSource<DebugTerminatedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var session = await language.Debugger!.LaunchAsync(Context(path, null, 7), cts.Token);
        session.Paused += args => pauses.Add(args);
        session.Terminated += args => terminated.TrySetResult(args);

        Assert.Equal(7, (await NextPause(pauses, cts.Token)).LineNumber);

        await session.StepIntoAsync(cts.Token);
        var inside = await NextPause(pauses, cts.Token);
        Assert.Equal(2, inside.LineNumber);
        Assert.Equal("double", inside.CallStack[0].MethodName);
        Assert.Equal("21", inside.Locals.Single(v => v.Name == "n").ValueDisplay);

        await session.StepOverAsync(cts.Token);
        var next = await NextPause(pauses, cts.Token);
        Assert.Equal(3, next.LineNumber);
        Assert.Equal("42", next.Locals.Single(v => v.Name == "result").ValueDisplay);

        await session.StepOutAsync(cts.Token);
        var back = await NextPause(pauses, cts.Token);

        // Back in the caller: V8 stops once the call statement has finished (line 8), not necessarily on the call's own line.
        Assert.NotEqual("double", back.CallStack[0].MethodName);
        Assert.InRange(back.LineNumber, 7, 8);

        await session.ContinueAsync(cts.Token);
        Assert.Equal(0, (await terminated.Task.WaitAsync(Patience)).ExitCode);
    }

    [JavaScriptFact]
    public async Task ABreakpointInALoop_IsHitEachTime_AndAConditionalOneOnlyWhenTrue()
    {
        var language = TestJavaScript.Services(Path.Combine(_dir, ".studio")).Registry.Get(LanguageIds.JavaScript)!;
        var path = Write("loop.js", "let total = 0;\nfor (let i = 1; i <= 4; i++) {\n  total += i;\n}\nconsole.log(total);\n");
        using var cts = new CancellationTokenSource(Patience);
        var pauses = new System.Collections.Concurrent.BlockingCollection<DebugPausedEventArgs>();
        var terminated = new TaskCompletionSource<DebugTerminatedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = Context(path, null) with { Breakpoints = [new BreakpointItem { LineNumber = 3, IsEnabled = true, Condition = "i === 3" }] };

        await using var session = await language.Debugger!.LaunchAsync(context, cts.Token);
        session.Paused += args => pauses.Add(args);
        session.Terminated += args => terminated.TrySetResult(args);

        var stop = await NextPause(pauses, cts.Token);
        Assert.Equal(3, stop.LineNumber);
        Assert.Equal("3", stop.Locals.Single(v => v.Name == "i").ValueDisplay);
        Assert.Equal("3", stop.Locals.Single(v => v.Name == "total").ValueDisplay);

        await session.ContinueAsync(cts.Token);
        Assert.Equal(0, (await terminated.Task.WaitAsync(Patience)).ExitCode);
        Assert.Empty(pauses);
    }

    [JavaScriptFact]
    public async Task AnUncaughtError_StopsWhereItWasThrown_ThenEndsWithExitCode1()
    {
        var language = TestJavaScript.Services(Path.Combine(_dir, ".studio")).Registry.Get(LanguageIds.JavaScript)!;
        var path = Write("throws.js", "const items = [1, 2];\nfunction fail() {\n  throw new Error('boom');\n}\nfail();\n");
        using var cts = new CancellationTokenSource(Patience);
        var pauses = new System.Collections.Concurrent.BlockingCollection<DebugPausedEventArgs>();
        var terminated = new TaskCompletionSource<DebugTerminatedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var output = new System.Text.StringBuilder();

        await using var session = await language.Debugger!.LaunchAsync(Context(path, text => { lock (output) output.Append(text); }), cts.Token);
        session.Paused += args => pauses.Add(args);
        session.Terminated += args => terminated.TrySetResult(args);

        var stop = await NextPause(pauses, cts.Token);
        Assert.Equal("exception", stop.Reason);
        Assert.Equal(3, stop.LineNumber);
        Assert.Equal("fail", stop.CallStack[0].MethodName);

        await session.ContinueAsync(cts.Token);
        Assert.Equal(1, (await terminated.Task.WaitAsync(Patience)).ExitCode);
        Assert.Contains("boom", output.ToString());
    }

    [JavaScriptFact]
    public async Task AScriptThatExitsWithACode_EndsTheSessionWithThatCode()
    {
        var language = TestJavaScript.Services(Path.Combine(_dir, ".studio")).Registry.Get(LanguageIds.JavaScript)!;
        var path = Write("exits.js", "console.log('leaving');\nprocess.exit(3);\n");
        using var cts = new CancellationTokenSource(Patience);
        var terminated = new TaskCompletionSource<DebugTerminatedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var session = await language.Debugger!.LaunchAsync(Context(path, null), cts.Token);
        session.Terminated += args => terminated.TrySetResult(args);

        Assert.Equal(3, (await terminated.Task.WaitAsync(Patience)).ExitCode);
    }

    [JavaScriptFact]
    public async Task AnObject_CanBeOpened_AndAnExpressionEvaluated()
    {
        var language = TestJavaScript.Services(Path.Combine(_dir, ".studio")).Registry.Get(LanguageIds.JavaScript)!;
        var path = Write("objects.js", "const point = { x: 3, y: 4 };\nconst list = [10, 20, 30];\nconst name = 'Ada';\nconsole.log(point, list, name);\n");
        using var cts = new CancellationTokenSource(Patience);
        var pauses = new System.Collections.Concurrent.BlockingCollection<DebugPausedEventArgs>();

        await using var session = await language.Debugger!.LaunchAsync(Context(path, null, 4), cts.Token);
        session.Paused += args => pauses.Add(args);

        var stop = await NextPause(pauses, cts.Token);
        var locals = stop.Locals.ToDictionary(v => v.Name);
        Assert.Equal("\"Ada\"", locals["name"].ValueDisplay);
        Assert.Contains("x: 3", locals["point"].ValueDisplay);
        Assert.Contains("10", locals["list"].ValueDisplay);
        Assert.DoesNotContain(stop.Locals, v => v.Name is "require" or "module" or "exports" or "__filename" or "__dirname");

        var children = await session.GetVariableChildrenAsync(locals["point"], cts.Token);
        Assert.Equal("3", children.Single(c => c.Name == "x").ValueDisplay);
        Assert.Equal("4", children.Single(c => c.Name == "y").ValueDisplay);

        var sum = await session.EvaluateAsync("point.x + point.y", null, EvaluationContext.Watch, cts.Token);
        Assert.True(sum.Success, sum.ErrorMessage);
        Assert.Equal("7", sum.Value);
        var broken = await session.EvaluateAsync("nothingLikeThis + 1", null, EvaluationContext.Watch, cts.Token);
        Assert.False(broken.Success);
        Assert.Contains("nothingLikeThis", broken.ErrorMessage);
    }

    [JavaScriptFact]
    public async Task ABreakpointAddedWhileTheProgramIsPaused_IsHit()
    {
        var language = TestJavaScript.Services(Path.Combine(_dir, ".studio")).Registry.Get(LanguageIds.JavaScript)!;
        var path = Write("live.js", "const a = 1;\nconst b = 2;\nconst c = a + b;\nconsole.log(c);\n");
        using var cts = new CancellationTokenSource(Patience);
        var pauses = new System.Collections.Concurrent.BlockingCollection<DebugPausedEventArgs>();
        var terminated = new TaskCompletionSource<DebugTerminatedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var session = await language.Debugger!.LaunchAsync(Context(path, null, 2), cts.Token);
        session.Paused += args => pauses.Add(args);
        session.Terminated += args => terminated.TrySetResult(args);
        Assert.Equal(2, (await NextPause(pauses, cts.Token)).LineNumber);

        await session.SetBreakpointsAsync(path, [new BreakpointItem { LineNumber = 2, IsEnabled = true }, new BreakpointItem { LineNumber = 4, IsEnabled = true }], cts.Token);
        await session.ContinueAsync(cts.Token);

        Assert.Equal(4, (await NextPause(pauses, cts.Token)).LineNumber);
        await session.ContinueAsync(cts.Token);
        Assert.Equal(0, (await terminated.Task.WaitAsync(Patience)).ExitCode);
    }

    [JavaScriptFact]
    public async Task AScriptWithNoBreakpoints_RunsToTheEnd_AndItsOutputIsShown()
    {
        var language = TestJavaScript.Services(Path.Combine(_dir, ".studio")).Registry.Get(LanguageIds.JavaScript)!;
        var path = Write("plain.js", "console.log('no stops here');\n");
        using var cts = new CancellationTokenSource(Patience);
        var terminated = new TaskCompletionSource<DebugTerminatedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var output = new System.Text.StringBuilder();

        await using var session = await language.Debugger!.LaunchAsync(Context(path, text => { lock (output) output.Append(text); }), cts.Token);
        session.Terminated += args => terminated.TrySetResult(args);

        Assert.Equal(0, (await terminated.Task.WaitAsync(Patience)).ExitCode);
        Assert.Contains("no stops here", output.ToString());
        Assert.DoesNotContain("Debugger listening", output.ToString());
        Assert.DoesNotContain("For help, see", output.ToString());
    }
}
