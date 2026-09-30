using System.Collections.Concurrent;
using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Interaction;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>
/// The host side of the kernel protocol, against a pretend kernel program that answers as the test says: output, rich
/// output, errors, input, values, a crash, an interrupt it ignores. No real program runs.
/// </summary>
public class ProtocolKernelTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private sealed class Launcher(bool available = true) : IKernelLauncher
    {
        public int Prepared;

        public Task<KernelLaunchSpec> PrepareAsync(KernelCreationContext context, CancellationToken ct)
        {
            Interlocked.Increment(ref Prepared);
            return available
                ? Task.FromResult(new KernelLaunchSpec(new ProcessStartSpec { FileName = "fake-kernel" }, "Fake 1.0"))
                : throw new KernelUnavailableException("Fake isn't installed.\n  • fakepkg install fake");
        }
    }

    private static string Json(object message) => JsonSerializer.Serialize(message) + "\n";

    // A pretend kernel: "print X" streams X, "show" displays HTML, "fail" errors, "ask" asks for input, "crash" exits,
    // "wait" waits for an interrupt, "stuck" ignores one, "late" prints for its cell once the next cell has started.
    // Values set with set_value can be read back.
    private static FakeProcessLauncher KernelProgram(ConcurrentQueue<string>? received = null)
    {
        return new FakeProcessLauncher
        {
            Behavior = async (_, p) =>
            {
                var values = new Dictionary<string, string>();
                string? lateId = null;
                p.Write(Json(new { type = "ready", language = "fake", version = "1.0" }));
                while (await p.ReadLineAsync() is { } line)
                {
                    received?.Enqueue(line);
                    using var document = JsonDocument.Parse(line);
                    var message = document.RootElement;
                    var type = message.GetProperty("type").GetString();
                    var id = message.TryGetProperty("id", out var i) ? i.GetString() : null;
                    switch (type)
                    {
                        case "execute":
                            var code = message.GetProperty("code").GetString()!;
                            if (lateId != null)
                            {
                                p.Write(Json(new { type = "stream", id = lateId, name = "stdout", text = "late output\n" }));
                                lateId = null;
                            }

                            if (code == "late")
                            {
                                lateId = id;
                                p.Write(Json(new { type = "reply", id, status = "ok" }));
                            }
                            else if (code.StartsWith("print ", StringComparison.Ordinal))
                            {
                                p.Write(Json(new { type = "stream", id, name = "stdout", text = code[6..] + "\n" }));
                                p.WriteError("from C code\n");
                                p.Write(Json(new { type = "reply", id, status = "ok" }));
                            }
                            else if (code == "show")
                            {
                                p.Write(Json(new { type = "display", id, data = new Dictionary<string, object> { ["text/html"] = "<b>hi</b>", ["text/plain"] = "hi" } }));
                                p.Write(Json(new { type = "reply", id, status = "ok" }));
                            }
                            else if (code == "fail")
                            {
                                p.Write(Json(new { type = "error", id, ename = "NameError", evalue = "name 'x' is not defined", traceback = "Traceback…\nNameError: name 'x' is not defined\n", line = 1, missingName = "x" }));
                                p.Write(Json(new { type = "reply", id, status = "error" }));
                            }
                            else if (code == "ask")
                            {
                                p.Write(Json(new { type = "input_request", id, prompt = "Name? ", password = false }));
                                var answer = await p.ReadLineAsync();
                                using var reply = JsonDocument.Parse(answer!);
                                p.Write(Json(new { type = "stream", id, name = "stdout", text = "Hi " + reply.RootElement.GetProperty("value").GetString() + "\n" }));
                                p.Write(Json(new { type = "reply", id, status = "ok" }));
                            }
                            else if (code == "crash")
                            {
                                p.WriteError("Segmentation fault\n");
                                p.Exit(139);
                                return;
                            }
                            else if (code == "wait")
                            {
                                var next = await p.ReadLineAsync();
                                if (next != null && next.Contains("\"interrupt\"")) p.Write(Json(new { type = "reply", id, status = "interrupted" }));
                            }
                            else if (code == "stuck")
                            {
                                await Task.Delay(Timeout.Infinite, p.KilledToken);
                            }
                            else if (code == "figure")
                            {
                                var chart = new Dictionary<string, object> { ["application/vnd.fry.chart.v1+json"] = new { title = "Figure", series = new[] { new { y = new[] { 1, 2, 3 } } } } };
                                p.Write(Json(new { type = "display", id, data = chart, transient = new { display_id = "fig" } }));
                                p.Write(Json(new { type = "subscribe", id, display_id = "fig", events = new[] { "click" } }));
                                p.Write(Json(new { type = "reply", id, status = "ok" }));
                            }
                            else if (code == "grow")
                            {
                                var chart = new Dictionary<string, object> { ["application/vnd.fry.chart.v1+json"] = new { title = "Grown", series = new[] { new { y = new[] { 1, 2, 3, 4 } } } } };
                                p.Write(Json(new { type = "update_display", id, data = chart, transient = new { display_id = "fig" } }));
                                p.Write(Json(new { type = "reply", id, status = "ok" }));
                            }
                            else if (code == "garbage")
                            {
                                p.Write("this line isn't JSON\n");
                                p.Write(Json(new { type = "reply", id, status = "ok" }));
                            }
                            else
                            {
                                p.Write(Json(new { type = "reply", id, status = "ok" }));
                            }
                            break;
                        case "event":
                            // The program's callback: it prints which value was clicked, as output of the event.
                            var target = message.GetProperty("event").GetProperty("target");
                            p.Write(Json(new { type = "stream", id, name = "stdout", text = $"clicked {target.GetProperty("index").GetInt32()}\n" }));
                            p.Write(Json(new { type = "reply", id, status = "ok" }));
                            break;
                        case "variables":
                            p.Write(Json(new { type = "reply", id, status = "ok", variables = values.Select(kv => new { name = kv.Key, type = "json", value = kv.Value, kind = "Primitive" }) }));
                            break;
                        case "set_value":
                            values[message.GetProperty("name").GetString()!] = message.GetProperty("json").GetString()!;
                            p.Write(Json(new { type = "reply", id, status = "ok" }));
                            break;
                        case "get_value":
                            var name = message.GetProperty("name").GetString()!;
                            p.Write(values.TryGetValue(name, out var json)
                                ? Json(new { type = "reply", id, status = "ok", json })
                                : Json(new { type = "reply", id, status = "error", message = $"no variable {name}" }));
                            break;
                    }
                }

                p.Exit(0);
            }
        };
    }

    private static ProtocolKernel Kernel(FakeProcessLauncher processes, Launcher? launcher = null, TimeSpan? interruptGrace = null) =>
        new("fake", "Fake", launcher ?? new Launcher(), processes, new KernelCreationContext(() => "/work"), TimeSpan.FromSeconds(5), interruptGrace);

    private static async Task<(KernelExecutionResult Result, string Console, List<RichCellOutput> Rich)> Run(
        ProtocolKernel kernel, string code, CancellationToken ct = default, Func<string, bool, CancellationToken, Task<string?>>? ask = null)
    {
        var console = new ConcurrentQueue<string>();
        var rich = new ConcurrentQueue<RichCellOutput>();
        var result = await kernel.ExecuteAsync(new KernelExecutionRequest
        {
            Code = code,
            SourceId = "cell-1",
            OnConsole = console.Enqueue,
            OnRichOutput = rich.Enqueue,
            OnInputRequest = ask
        }, ct).WaitAsync(Patience);
        return (result, string.Concat(console), rich.ToList());
    }

    [Fact]
    public async Task ACell_RunsInTheProgram_AndItsOutputArrives()
    {
        var sent = new ConcurrentQueue<string>();
        using var kernel = Kernel(KernelProgram(sent));

        var (result, console, _) = await Run(kernel, "print hello");

        Assert.True(result.Success);
        Assert.Contains("hello\n", console);
        Assert.Contains("from C code", console); // the program's other output (fd 2) shows too
        Assert.Contains(sent, line => line.Contains("\"execute\"") && line.Contains("print hello") && line.Contains("cell-1"));
        Assert.Equal("Fake 1.0", kernel.DisplayName);
        Assert.True(kernel.IsSessionActive);
    }

    // A program can print after its cell's reply (a background thread, a buffer flushed late): the text belongs to the
    // cell that started it, whichever cell runs by the time it arrives.
    [Fact]
    public async Task OutputThatArrivesAfterItsCellEnded_StaysWithThatCell()
    {
        using var kernel = Kernel(KernelProgram());
        var firstConsole = new ConcurrentQueue<string>();

        var first = await kernel.ExecuteAsync(new KernelExecutionRequest
        {
            Code = "late",
            SourceId = "cell-1",
            OnConsole = firstConsole.Enqueue,
            OnRichOutput = _ => { }
        }, CancellationToken.None).WaitAsync(Patience);
        var (second, secondConsole, _) = await Run(kernel, "print next");

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.DoesNotContain("late output", secondConsole);
        Assert.Contains("late output", string.Concat(firstConsole));
    }

    // update_display redraws the visual the program showed with that display_id, where it is.
    [Fact]
    public async Task AnUpdate_RedrawsTheVisualWhereItIs()
    {
        using var kernel = Kernel(KernelProgram());

        var (_, _, shown) = await Run(kernel, "figure");
        var (grown, _, again) = await Run(kernel, "grow");

        var figure = Assert.Single(shown).Visual!;
        Assert.True(grown.Success);
        Assert.Empty(again);
        Assert.Equal("Grown", figure.Spec.Title);
        Assert.Equal("fig", figure.DisplayId);
    }

    // A click on a visual the program listens to reaches its callback; what the callback prints goes to the cell that
    // showed the visual, whichever cell ran since.
    [Fact]
    public async Task AClick_ReachesTheProgram_AndItsOutputGoesToTheCellThatShowedTheVisual()
    {
        using var kernel = Kernel(KernelProgram());
        var figureConsole = new ConcurrentQueue<string>();
        var shown = new ConcurrentQueue<RichCellOutput>();
        await kernel.ExecuteAsync(new KernelExecutionRequest
        {
            Code = "figure",
            SourceId = "cell-1",
            OnConsole = figureConsole.Enqueue,
            OnRichOutput = shown.Enqueue
        }, CancellationToken.None).WaitAsync(Patience);
        var (_, laterConsole, _) = await Run(kernel, "print later");
        var figure = Assert.Single(shown).Visual!;

        Assert.True(figure.IsInteractive);
        figure.Raise(VisualEvent.Click(new VisualEventTarget { Series = 0, Index = 2, Y = 3 }));

        await WaitUntil(() => string.Concat(figureConsole).Contains("clicked 2"));
        Assert.DoesNotContain("clicked", laterConsole);
    }

    [Fact]
    public async Task AVisual_StopsListening_WhenItsProgramEnds()
    {
        using var kernel = Kernel(KernelProgram());
        var (_, _, shown) = await Run(kernel, "figure");
        var figure = Assert.Single(shown).Visual!;

        await Run(kernel, "crash");

        await WaitUntil(() => !figure.IsInteractive);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (!condition())
        {
            if (clock.Elapsed > Patience) throw new TimeoutException("It never happened.");
            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task RichOutput_BecomesTheStudiosOutput()
    {
        using var kernel = Kernel(KernelProgram());

        var (_, _, rich) = await Run(kernel, "show");

        var html = Assert.Single(rich);
        Assert.Equal(CellOutputKind.Html, html.Kind);
        Assert.Equal("<b>hi</b>", html.HtmlContent);
    }

    [Fact]
    public async Task AnError_FailsTheCell_WithTheTracebackAndTheMissingName()
    {
        using var kernel = Kernel(KernelProgram());

        var (result, console, _) = await Run(kernel, "fail");

        Assert.False(result.Success);
        Assert.Equal("NameError: name 'x' is not defined", result.ErrorMessage);
        Assert.Equal("x", result.MissingName);
        Assert.Contains("Traceback", console);
    }

    [Fact]
    public async Task InputRequests_AreAnswered_AndShownAsATerminalWould()
    {
        using var kernel = Kernel(KernelProgram());

        var (result, console, _) = await Run(kernel, "ask", ask: (prompt, _, _) => Task.FromResult<string?>("Ada"));

        Assert.True(result.Success);
        Assert.Contains("Name? Ada\n", console);
        Assert.Contains("Hi Ada", console);
    }

    [Fact]
    public async Task ACrash_FailsTheCell_WithWhatItSaid_AndTheNextCellStartsAFreshKernelWithANote()
    {
        var processes = KernelProgram();
        using var kernel = Kernel(processes);

        var (crashed, _, _) = await Run(kernel, "crash");
        var (next, console, _) = await Run(kernel, "print again");

        Assert.False(crashed.Success);
        Assert.Contains("stopped (exit code 139)", crashed.ErrorMessage);
        Assert.Contains("Segmentation fault", crashed.ErrorMessage);
        Assert.True(next.Success);
        Assert.Contains("variables from before are gone", console);
        Assert.Equal(2, processes.Started.Count);
    }

    [Fact]
    public async Task Stop_InterruptsTheCell_AndTheKernelKeepsRunning()
    {
        var processes = KernelProgram();
        using var kernel = Kernel(processes);
        await Run(kernel, "print warm-up");
        // 500 ms gives the interrupt + protocol round-trip enough headroom under full-suite
        // parallel load (200 ms was too tight on slower CI runners).
        using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        var (result, _, _) = await Run(kernel, "wait", stop.Token);
        var (after, _, _) = await Run(kernel, "print still here");

        Assert.True(result.WasCancelled);
        Assert.True(after.Success);
        Assert.Single(processes.Started);
    }

    [Fact]
    public async Task Stop_EndsAKernelThatIgnoresTheInterrupt()
    {
        var processes = KernelProgram();
        using var kernel = Kernel(processes, interruptGrace: TimeSpan.FromMilliseconds(300));
        using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var (result, console, _) = await Run(kernel, "stuck", stop.Token);

        Assert.True(result.WasCancelled);
        Assert.Contains("didn't stop when asked", console);
        Assert.False(kernel.IsSessionActive);
    }

    [Fact]
    public async Task AKernelThatCantStart_SaysWhy_InTheCell()
    {
        using var kernel = Kernel(KernelProgram(), new Launcher(available: false));

        var (result, console, _) = await Run(kernel, "print hi");

        Assert.False(result.Success);
        Assert.Contains("Fake isn't installed", console);
        Assert.Contains("fakepkg install fake", result.ErrorMessage);
    }

    [Fact]
    public async Task ALineThatIsntProtocol_IsShownRatherThanLost()
    {
        using var kernel = Kernel(KernelProgram());

        var (result, console, _) = await Run(kernel, "garbage");

        Assert.True(result.Success);
        Assert.Contains("this line isn't JSON", console);
    }

    [Fact]
    public async Task Values_GoInAndOut_AsJson_AndShowAsVariables()
    {
        using var kernel = Kernel(KernelProgram());

        await kernel.SetValueFromJsonAsync("nums", "[1,2,3]", CancellationToken.None);
        var json = await kernel.GetValueJsonAsync("nums", CancellationToken.None);
        var variables = await kernel.GetVariablesAsync(CancellationToken.None);

        Assert.Equal("[1,2,3]", json);
        Assert.Equal("nums", Assert.Single(variables).Name);
        var missing = await Assert.ThrowsAsync<KernelValueException>(() => kernel.GetValueJsonAsync("other", CancellationToken.None));
        Assert.Contains("no variable other", missing.Message);
    }

    [Fact]
    public async Task ReadingAValue_BeforeTheKernelHasRun_SaysSo()
    {
        using var kernel = Kernel(KernelProgram());

        var error = await Assert.ThrowsAsync<KernelValueException>(() => kernel.GetValueJsonAsync("x", CancellationToken.None));

        Assert.Contains("hasn't run any cell yet", error.Message);
    }

    [Fact]
    public async Task HardReset_EndsTheProgram_AndTheNextCellStartsANewOneQuietly()
    {
        var processes = KernelProgram();
        using var kernel = Kernel(processes);
        await Run(kernel, "print one");

        kernel.HardReset();
        var (result, console, _) = await Run(kernel, "print two");

        Assert.True(result.Success);
        Assert.DoesNotContain("variables from before", console); // a restart the user asked for needs no note
        Assert.Equal(2, processes.Started.Count);
    }
}
