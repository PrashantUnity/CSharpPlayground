using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Interaction;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Spec;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>
/// A program that is run (and a Go, Rust, C++ or F# notebook cell) shows visuals on <c>__FRY_DISPLAY__</c> lines, updates
/// them in place, and hears about clicks on them over the loopback event socket, with the token it was given.
/// </summary>
public class ExternalVisualSessionTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private static string Chart(string title, int[] y, string? displayId = null, string type = "display") => JsonSerializer.Serialize(new Dictionary<string, object?>
    {
        ["type"] = type,
        ["data"] = new Dictionary<string, object> { ["application/vnd.fry.chart.v1+json"] = new { title, series = new[] { new { y } } } },
        ["transient"] = displayId == null ? null : new { display_id = displayId }
    });

    private static string Line(object message) => ExternalOutputProcessor.DisplayMarker + " " + (message as string ?? JsonSerializer.Serialize(message)) + "\n";

    private static (ExternalOutputProcessor Processor, List<string> Console, List<RichCellOutput> Outputs) Reader(ExternalVisualSession? session)
    {
        var console = new List<string>();
        var outputs = new List<RichCellOutput>();
        var processor = new ExternalOutputProcessor(console.Add, outputs.Add, visuals: session?.Visuals);
        return (processor, console, outputs);
    }

    // The program's side: connect where the environment says, say hello with the token, read event lines.
    private static async Task<(TcpClient Client, StreamReader Events)> ConnectAsync(IReadOnlyDictionary<string, string?> environment, string? token = null)
    {
        var address = environment[VisualEventHub.AddressVariable]!.Split(':');
        var client = new TcpClient();
        await client.ConnectAsync(address[0], int.Parse(address[1]));
        var stream = client.GetStream();
        var hello = JsonSerializer.Serialize(new { type = "hello", token = token ?? environment[VisualEventHub.TokenVariable] }) + "\n";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(hello));
        return (client, new StreamReader(stream, Encoding.UTF8));
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
    public void AnUpdateLine_RedrawsTheVisualWhereItIs()
    {
        using var session = new ExternalVisualSession();
        var (processor, console, outputs) = Reader(session);

        processor.ProcessChunk(Line(Chart("Before", [1, 2], "fig")));
        processor.ProcessChunk(Line(Chart("After", [1, 2, 3], "fig", "update_display")));

        var visual = Assert.Single(outputs).Visual!;
        var spec = Assert.IsType<ChartSpec>(visual.Spec);
        Assert.Equal(("After", 3), (spec.Title, spec.Series[0].Y.Count));
        Assert.Empty(console);
    }

    [Fact]
    public void AMidLineDisplayMarker_EmitsPrecedingConsoleTextAndHandlesVisual()
    {
        var (processor, console, outputs) = Reader(null);

        processor.ProcessChunk("hello" + Line(Chart("Inline", [1, 2], "fig")));

        Assert.Single(outputs);
        Assert.Equal("hello", Assert.Single(console));
    }

    // A program printing its own JSON must not have it taken for a message.
    [Fact]
    public void AMessageLineWithoutTheMarker_IsReadOnlyForAVisualType()
    {
        var (processor, console, outputs) = Reader(null);

        processor.ProcessChunk(Chart("Direct", [1], "d") + "\n");
        processor.ProcessChunk("""{"type":"order","data":{"text/plain":"not for us"}}""" + "\n");

        Assert.Single(outputs);
        Assert.Equal("""{"type":"order","data":{"text/plain":"not for us"}}""" + "\n", Assert.Single(console));
    }

    [Fact]
    public void ListeningToADisplayThatWasNeverShown_SaysSo()
    {
        using var session = new ExternalVisualSession();
        var (processor, console, _) = Reader(session);

        processor.ProcessChunk(Line(new { type = "subscribe", display_id = "nope", events = new[] { "click" } }));

        Assert.Contains("There is no display \"nope\"", Assert.Single(console));
    }

    [Fact]
    public async Task AClick_ReachesTheProgramThatListens_OverTheEventSocket()
    {
        using var session = new ExternalVisualSession();
        var (processor, _, outputs) = Reader(session);
        processor.ProcessChunk(Line(Chart("Click me", [1, 2, 3], "fig")));
        processor.ProcessChunk(Line(new { type = "subscribe", display_id = "fig", events = new[] { "click" } }));
        var (client, events) = await ConnectAsync(session.Environment);
        using var _ = client;
        await WaitUntil(() => session.IsConnected);

        outputs[0].Visual!.Raise(VisualEvent.Click(new VisualEventTarget { Series = 0, Index = 2 }));

        var line = await events.ReadLineAsync().WaitAsync(Patience);
        using var message = JsonDocument.Parse(line!);
        Assert.Equal("event", message.RootElement.GetProperty("type").GetString());
        Assert.Equal("fig", message.RootElement.GetProperty("display_id").GetString());
        Assert.Equal("""{"event":"click","target":{"series":0,"index":2}}""", message.RootElement.GetProperty("event").GetRawText());
    }

    // The program connects when its code first listens; a click that comes before then waits for it.
    [Fact]
    public async Task EventsBeforeTheProgramConnects_WaitForIt_InOrder()
    {
        using var session = new ExternalVisualSession();
        var (processor, _, outputs) = Reader(session);
        processor.ProcessChunk(Line(Chart("Early", [1, 2, 3], "fig")));
        processor.ProcessChunk(Line(new { type = "subscribe", display_id = "fig" }));
        outputs[0].Visual!.Raise(VisualEvent.Step(1));
        outputs[0].Visual!.Raise(VisualEvent.Step(2));

        var (client, events) = await ConnectAsync(session.Environment);
        using var _ = client;

        Assert.Contains("\"index\":1", await events.ReadLineAsync().WaitAsync(Patience));
        Assert.Contains("\"index\":2", await events.ReadLineAsync().WaitAsync(Patience));
    }

    [Fact]
    public async Task AConnectionWithAnotherToken_IsLetGo()
    {
        using var session = new ExternalVisualSession();
        var (client, events) = await ConnectAsync(session.Environment, token: "0123456789abcdef0123456789abcdef");
        using var _ = client;

        Assert.Null(await events.ReadLineAsync().WaitAsync(Patience)); // closed
        Assert.False(session.IsConnected);
    }

    [Fact]
    public void WhenTheProgramEnds_ItsVisualsStay_Disconnected_AndItsTokenIsGone()
    {
        var hub = new VisualEventHub();
        var session = new ExternalVisualSession(hub);
        var (processor, _, outputs) = Reader(session);
        processor.ProcessChunk(Line(Chart("Live", [1, 2], "fig")));
        processor.ProcessChunk(Line(new { type = "subscribe", display_id = "fig" }));
        var visual = outputs[0].Visual!;
        Assert.True(visual.IsInteractive);

        session.Dispose();

        Assert.Equal((false, true, 0), (visual.IsInteractive, visual.IsDisconnected, hub.SessionCount));
        hub.Dispose();
    }

    // Only the program gets the variables: a build step (a compiler) has no events to hear.
    [Fact]
    public async Task ARun_GivesTheEventSocketToTheProgram_NotToItsBuildSteps()
    {
        using var session = new ExternalVisualSession();
        var launcher = new RecordingLauncher();
        var plan = new ScriptRunPlan([
            new ProcessStep("Build", new ProcessStartSpec { FileName = "cc" }, IsBuildStep: true),
            new ProcessStep("Run", new ProcessStartSpec { FileName = "./app", Environment = new Dictionary<string, string?> { ["KEEP"] = "1" } })
        ]);

        await new ScriptRunExecutor(launcher).Start(plan, "app.c", null, _ => { }, environment: session.Environment).Completion;

        Assert.False(launcher.Started[0].Environment.ContainsKey(VisualEventHub.TokenVariable));
        Assert.Equal(session.Environment[VisualEventHub.TokenVariable], launcher.Started[1].Environment[VisualEventHub.TokenVariable]);
        Assert.Equal("1", launcher.Started[1].Environment["KEEP"]);
    }

    private sealed class RecordingLauncher : IProcessLauncher
    {
        public readonly List<ProcessStartSpec> Started = [];

        public IManagedProcess Start(ProcessStartSpec spec, Action<string> onStandardOutput, Action<string> onStandardError)
        {
            Started.Add(spec);
            return new EndedProcess();
        }
    }

    private sealed class EndedProcess : IManagedProcess
    {
        public int Id => 1;
        public bool HasExited => true;
        public Task<int> Completion => Task.FromResult(0);
        public Task WriteInputAsync(string text, CancellationToken ct = default) => Task.CompletedTask;
        public void CloseInput() { }
        public void Kill() { }
        public void Dispose() { }
    }
}
