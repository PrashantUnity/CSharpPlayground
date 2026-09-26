using System.Collections.Concurrent;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>A run plan's steps run in order with their output streamed, input typed in, and Stop ending them.</summary>
public class ScriptRunExecutorTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private static ProcessStep Step(string label, string program, bool build = false) =>
        new(label, new ProcessStartSpec { FileName = program }, build);

    private static async Task<ScriptRunResult> Finish(ScriptRunSession session) => await session.Completion.WaitAsync(Patience);

    [Fact]
    public async Task OutputStreams_AndASuccessfulRunIsNotReadForErrors()
    {
        var launcher = new FakeProcessLauncher
        {
            Behavior = (_, p) =>
            {
                p.Write("hello ");
                p.WriteError("Line 3: looks like an error but the run worked\n");
                p.Write("world\n");
                p.Exit(0);
                return Task.CompletedTask;
            }
        };
        var output = new ConcurrentQueue<string>();

        var result = await Finish(new ScriptRunExecutor(launcher)
            .Start(new ScriptRunPlan([Step("Run", "prog")]), "/work/main.fake", new FakeDiagnosticParser(), output.Enqueue));

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("hello ", string.Concat(output));
        Assert.Contains("world", string.Concat(output));
        Assert.Empty(result.Diagnostics.Diagnostics);
    }

    [Fact]
    public async Task FailedBuildStep_StopsTheRun_AndItsErrorsAreReported()
    {
        var launcher = new FakeProcessLauncher
        {
            Behavior = (spec, p) =>
            {
                if (spec.FileName == "compiler") p.WriteError("Line 7: missing semicolon\n");
                p.Exit(spec.FileName == "compiler" ? 1 : 0);
                return Task.CompletedTask;
            }
        };

        var result = await Finish(new ScriptRunExecutor(launcher).Start(
            new ScriptRunPlan([Step("Compile", "compiler", build: true), Step("Run", "program")]),
            "/work/main.fake", new FakeDiagnosticParser(), _ => { }));

        Assert.False(result.Succeeded);
        Assert.Equal("Compile", result.FailedBuildStep);
        var error = Assert.Single(result.Diagnostics.Diagnostics);
        Assert.Equal(7, error.Line);
        Assert.Equal("missing semicolon", error.Message);
        Assert.DoesNotContain(launcher.Started, s => s.FileName == "program");
    }

    [Fact]
    public async Task FailedRun_IsReadForErrors_AndAMissingDependency()
    {
        var launcher = new FakeProcessLauncher
        {
            Behavior = (_, p) =>
            {
                p.WriteError("Line 2: boom\nmissing module numpy\n");
                p.Exit(1);
                return Task.CompletedTask;
            }
        };

        var result = await Finish(new ScriptRunExecutor(launcher)
            .Start(new ScriptRunPlan([Step("Run", "prog")]), "/work/main.fake", new FakeDiagnosticParser(), _ => { }));

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(2, Assert.Single(result.Diagnostics.Diagnostics).Line);
        Assert.Equal("numpy", result.Diagnostics.MissingDependency);
    }

    [Fact]
    public async Task InputTypedDuringTheRun_ReachesTheProgram()
    {
        var launcher = new FakeProcessLauncher
        {
            Behavior = async (_, p) =>
            {
                p.Write("Name? ");
                var name = await p.ReadLineAsync();
                p.Write($"Hi {name}\n");
                p.Exit(0);
            }
        };
        var output = new ConcurrentQueue<string>();
        var session = new ScriptRunExecutor(launcher)
            .Start(new ScriptRunPlan([Step("Run", "prog")]), "/work/main.fake", null, output.Enqueue);

        await WaitUntil(() => session.AcceptsInput && string.Concat(output).Contains("Name? "));
        await session.SendInputAsync("Ada\n");

        var result = await Finish(session);
        Assert.True(result.Succeeded);
        Assert.Contains("Hi Ada", string.Concat(output));
        Assert.False(session.AcceptsInput);
    }

    [Fact]
    public async Task Stop_KillsTheProgram_AndTheRunEndsCancelled()
    {
        FakeProcess? running = null;
        var launcher = new FakeProcessLauncher
        {
            Behavior = async (_, p) =>
            {
                running = p;
                await Task.Delay(Timeout.Infinite, p.KilledToken);
            }
        };
        var session = new ScriptRunExecutor(launcher)
            .Start(new ScriptRunPlan([Step("Run", "prog")]), "/work/main.fake", null, _ => { });
        await WaitUntil(() => running != null);

        session.Stop();

        var result = await Finish(session);
        Assert.True(result.WasCancelled);
        Assert.True(running!.WasKilled);
    }

    [Fact]
    public async Task CancellingTheCallersToken_StopsTheRunToo()
    {
        FakeProcess? running = null;
        var launcher = new FakeProcessLauncher
        {
            Behavior = async (_, p) =>
            {
                running = p;
                await Task.Delay(Timeout.Infinite, p.KilledToken);
            }
        };
        using var cts = new CancellationTokenSource();
        var session = new ScriptRunExecutor(launcher)
            .Start(new ScriptRunPlan([Step("Run", "prog")]), "/work/main.fake", null, _ => { }, cts.Token);
        await WaitUntil(() => running != null);

        cts.Cancel();

        Assert.True((await Finish(session)).WasCancelled);
        Assert.True(running!.WasKilled);
    }

    [Fact]
    public async Task AProgramThatCantStart_EndsTheRunWithWhy()
    {
        var launcher = new FakeProcessLauncher { FailToStart = _ => true };

        var result = await Finish(new ScriptRunExecutor(launcher)
            .Start(new ScriptRunPlan([Step("Run", "missing-program")]), "/work/main.fake", null, _ => { }));

        Assert.False(result.Succeeded);
        Assert.Contains("missing-program", result.StartError);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Patience;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition never became true.");
            await Task.Delay(10);
        }
    }
}
