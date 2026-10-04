using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging.Cdp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.JavaScript;
using Xunit;

namespace CSharpEditorPlugin.Tests.Debugging;

/// <summary>An inspector standing in for Node's: <c>/json/list</c> names its WebSocket, and the WebSocket answers as a test says.</summary>
internal sealed class FakeInspector : IAsyncDisposable
{
    private readonly HttpListener _listener;
    private readonly TaskCompletionSource<WebSocket> _connected = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task _serving;

    public FakeInspector(int? port = null)
    {
        // A port that was free a moment ago can be taken by another test's process before this one binds it: take another.
        for (var attempt = 0; ; attempt++)
        {
            var candidate = port ?? FreePort();
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{candidate}/");
            try
            {
                listener.Start();
                _listener = listener;
                Port = candidate;
                break;
            }
            catch (HttpListenerException) when (port == null && attempt < 25)
            {
                try
                {
                    listener.Close();
                }
                catch (HttpListenerException)
                {
                    // Closing a listener that never started can fail the same way.
                }
            }
        }

        _serving = ServeAsync();
    }

    public int Port { get; }

    /// <summary>What the client sent, as JSON, in order.</summary>
    public List<JsonElement> Requests { get; } = [];

    /// <summary>How the inspector answers a request; by default with an empty result.</summary>
    public Func<FakeInspector, int, string, JsonElement, Task> OnRequest { get; set; } = (inspector, id, _, _) => inspector.ReplyAsync(id);

    public static int FreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public async Task<WebSocket> ConnectedAsync() => await _connected.Task.WaitAsync(TimeSpan.FromSeconds(10));

    public Task ReplyAsync(int id, object? result = null) => SendAsync(new { id, result = result ?? new { } });

    public Task FailAsync(int id, string message) => SendAsync(new { id, error = new { code = -32602, message } });

    public Task EventAsync(string method, object? parameters = null) => SendAsync(new { method, @params = parameters ?? new { } });

    public async Task SendAsync(object message)
    {
        var socket = await ConnectedAsync();
        await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(message), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public async Task CloseAsync()
    {
        var socket = await ConnectedAsync();
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
    }

    private async Task ServeAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    if (context.Request.IsWebSocketRequest)
                    {
                        var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
                        _connected.TrySetResult(socket);
                        _ = Task.Run(() => ReadAsync(socket));
                    }
                    else if (context.Request.Url?.AbsolutePath == "/json/list")
                    {
                        var body = Encoding.UTF8.GetBytes($"[{{\"id\":\"abc\",\"webSocketDebuggerUrl\":\"ws://127.0.0.1:{Port}/abc\"}}]");
                        context.Response.ContentType = "application/json";
                        context.Response.ContentLength64 = body.Length;
                        await context.Response.OutputStream.WriteAsync(body);
                        context.Response.Close();
                    }
                    else
                    {
                        context.Response.StatusCode = 404;
                        context.Response.Close();
                    }
                }
                catch
                {
                    try { context.Response.Abort(); } catch { }
                }
            });
        }
    }

    private async Task ReadAsync(WebSocket socket)
    {
        var buffer = new byte[16 * 1024];
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close) return;
                using var document = JsonDocument.Parse(buffer.AsMemory(0, result.Count));
                var request = document.RootElement.Clone();
                lock (Requests) Requests.Add(request);
                await OnRequest(this, request.GetProperty("id").GetInt32(), request.GetProperty("method").GetString()!, request.GetProperty("params"));
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Close stops the listener too. Stopping first makes Close look the port up again, and that fails when another
        // process (another test's node, dlv or java) has taken the port in the meantime.
        try
        {
            _listener.Close();
        }
        catch (Exception ex) when (ex is ObjectDisposedException or HttpListenerException)
        {
        }

        if (_connected.Task.IsCompletedSuccessfully)
        {
            try { _connected.Task.Result.Dispose(); } catch { }
        }

        await Task.WhenAny(_serving, Task.Delay(1000));
    }
}

public class CdpClientTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    private static async Task<CdpClient> Connect(FakeInspector inspector)
    {
        var client = new CdpClient();
        await client.ConnectAsync(await CdpClient.DiscoverWebSocketUrlAsync(inspector.Port, TimeSpan.FromSeconds(10), CancellationToken.None), CancellationToken.None);
        return client;
    }

    [Fact]
    public async Task ARequest_CarriesNoNulls_AndItsResultComesBack()
    {
        await using var inspector = new FakeInspector();
        inspector.OnRequest = (i, id, _, _) => i.ReplyAsync(id, new { breakpointId = "1:2:0", locations = Array.Empty<object>() });
        await using var client = await Connect(inspector);

        var result = await client.SendAsync("Debugger.setBreakpointByUrl", new { lineNumber = 2, urlRegex = "^x$", condition = (string?)null }).WaitAsync(Patience);

        Assert.Equal("1:2:0", result.GetProperty("breakpointId").GetString());
        var sent = inspector.Requests.Single();
        Assert.Equal("Debugger.setBreakpointByUrl", sent.GetProperty("method").GetString());
        Assert.Equal(2, sent.GetProperty("params").GetProperty("lineNumber").GetInt32());
        Assert.False(sent.GetProperty("params").TryGetProperty("condition", out _), "a null condition must be left out");
    }

    [Fact]
    public async Task AnAnswer_IsMatchedToItsRequest_EvenWhenTheyComeInAnotherOrder()
    {
        await using var inspector = new FakeInspector();
        var held = new TaskCompletionSource<int>();
        inspector.OnRequest = async (i, id, method, _) =>
        {
            if (method == "First")
            {
                held.SetResult(id); // answered only after the second
                return;
            }

            await i.ReplyAsync(id, new { which = "second" });
            await i.ReplyAsync(await held.Task, new { which = "first" });
        };
        await using var client = await Connect(inspector);

        var first = client.SendAsync("First");
        await Task.Delay(100);
        var second = client.SendAsync("Second");

        Assert.Equal("second", (await second.WaitAsync(Patience)).GetProperty("which").GetString());
        Assert.Equal("first", (await first.WaitAsync(Patience)).GetProperty("which").GetString());
    }

    [Fact]
    public async Task AnErrorAnswer_IsACdpException_WithTheInspectorsMessage()
    {
        await using var inspector = new FakeInspector();
        inspector.OnRequest = (i, id, _, _) => i.FailAsync(id, "Invalid parameters");
        await using var client = await Connect(inspector);

        var ex = await Assert.ThrowsAsync<CdpException>(() => client.SendAsync("Debugger.setBreakpointByUrl").WaitAsync(Patience));

        Assert.Equal("Debugger.setBreakpointByUrl", ex.Method);
        Assert.Contains("Invalid parameters", ex.Message);
        Assert.Equal(-32602, ex.Code);
    }

    [Fact]
    public async Task Events_ComeInOrder_AndAHandlerMayWaitForAnAnswer()
    {
        await using var inspector = new FakeInspector();
        await using var client = new CdpClient();
        var seen = new List<string>();
        var done = new TaskCompletionSource();
        client.EventReceived += async (method, parameters) =>
        {
            // Waiting here needs the reader free to bring the answer in.
            var answer = await client.SendAsync("Runtime.getProperties");
            lock (seen) seen.Add($"{method}:{parameters.GetProperty("n").GetInt32()}:{answer.ValueKind}");
            if (seen.Count == 3) done.TrySetResult();
        };
        await client.ConnectAsync(await CdpClient.DiscoverWebSocketUrlAsync(inspector.Port, TimeSpan.FromSeconds(10), CancellationToken.None), CancellationToken.None);

        await inspector.EventAsync("Debugger.paused", new { n = 1 });
        await inspector.EventAsync("Debugger.resumed", new { n = 2 });
        await inspector.EventAsync("Debugger.paused", new { n = 3 });
        await done.Task.WaitAsync(Patience);

        Assert.Equal(["Debugger.paused:1:Object", "Debugger.resumed:2:Object", "Debugger.paused:3:Object"], seen);
    }

    [Fact]
    public async Task AConnectionThatEnds_FailsWhatIsWaitingForAnAnswer_AndSaysItClosed()
    {
        await using var inspector = new FakeInspector();
        inspector.OnRequest = (_, _, _, _) => Task.CompletedTask; // never answers
        await using var client = await Connect(inspector);
        var closed = new TaskCompletionSource();
        client.Closed += () => closed.TrySetResult();

        var waiting = client.SendAsync("Debugger.enable");
        await Task.Delay(100);

        // The inspector's close waits for the client's answer to the handshake: a client that never gave one would hang here.
        await inspector.CloseAsync().WaitAsync(Patience);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WaitAsync(Patience));
        await closed.Task.WaitAsync(Patience);
    }

    [Fact]
    public async Task TheAddress_IsFoundOnceTheInspectorIsListening()
    {
        var port = FakeInspector.FreePort();
        var discovering = CdpClient.DiscoverWebSocketUrlAsync(port, TimeSpan.FromSeconds(10), CancellationToken.None);
        await Task.Delay(400);
        await using var inspector = new FakeInspector(port);

        var url = await discovering.WaitAsync(Patience);

        Assert.Equal($"ws://127.0.0.1:{port}/abc", url);
    }

    [Fact]
    public async Task WithNothingListening_TheSearchTimesOut_SayingSo()
    {
        var ex = await Assert.ThrowsAsync<TimeoutException>(() =>
            CdpClient.DiscoverWebSocketUrlAsync(FakeInspector.FreePort(), TimeSpan.FromMilliseconds(500), CancellationToken.None));

        Assert.Contains("inspector", ex.Message);
    }

    [Fact]
    public async Task StoppingTheSearch_IsACancellation()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CdpClient.DiscoverWebSocketUrlAsync(FakeInspector.FreePort(), TimeSpan.FromSeconds(30), cts.Token));
    }
}

/// <summary>How a value is written for the Variables panel.</summary>
public class NodeValueDescriptionTests
{
    private static string Describe(string remoteObject)
    {
        using var document = JsonDocument.Parse(remoteObject);
        return NodeInspectorDapAdapter.Describe(document.RootElement);
    }

    [Theory]
    [InlineData("""{"type":"undefined"}""", "undefined")]
    [InlineData("""{"type":"object","subtype":"null","value":null}""", "null")]
    [InlineData("""{"type":"boolean","value":true}""", "true")]
    [InlineData("""{"type":"boolean","value":false}""", "false")]
    [InlineData("""{"type":"number","value":41,"description":"41"}""", "41")]
    [InlineData("""{"type":"number","unserializableValue":"NaN","description":"NaN"}""", "NaN")]
    [InlineData("""{"type":"bigint","unserializableValue":"12n","description":"12n"}""", "12n")]
    [InlineData("""{"type":"string","value":"Ada"}""", "\"Ada\"")]
    [InlineData("""{"type":"string","value":"a \"quote\" and a\nnewline"}""", "\"a \\\"quote\\\" and a\\nnewline\"")]
    [InlineData("""{"type":"string","value":"héllo → ✓"}""", "\"héllo → ✓\"")]
    [InlineData("""{"type":"function","className":"Function","description":"function double(n) {\n  return n * 2;\n}"}""", "function double(n) {")]
    [InlineData("""{"type":"symbol","description":"Symbol(id)"}""", "Symbol(id)")]
    public void APrimitive_IsWrittenTheWayItReadsInCode(string remoteObject, string expected)
    {
        Assert.Equal(expected, Describe(remoteObject));
    }

    [Fact]
    public void AnArray_ShowsItsFirstItems()
    {
        var text = Describe("""{"type":"object","subtype":"array","className":"Array","description":"Array(3)","objectId":"1","preview":{"type":"object","subtype":"array","description":"Array(3)","overflow":false,"properties":[{"name":"0","type":"number","value":"10"},{"name":"1","type":"number","value":"20"},{"name":"2","type":"number","value":"30"}]}}""");

        Assert.Equal("Array(3) [10, 20, 30]", text);
    }

    [Fact]
    public void ALongArray_IsCutAndSaysMoreFollows()
    {
        var properties = string.Join(",", Enumerable.Range(0, 8).Select(i => "{\"name\":\"" + i + "\",\"type\":\"number\",\"value\":\"" + i + "\"}"));
        var text = Describe("{\"type\":\"object\",\"subtype\":\"array\",\"description\":\"Array(100)\",\"objectId\":\"1\",\"preview\":{\"overflow\":true,\"properties\":[" + properties + "]}}");

        Assert.Equal("Array(100) [0, 1, 2, 3, 4, …]", text);
    }

    [Fact]
    public void AnObject_ShowsItsFieldsByName()
    {
        var text = Describe("""{"type":"object","className":"Object","description":"Object","objectId":"1","preview":{"overflow":false,"properties":[{"name":"x","type":"number","value":"3"},{"name":"label","type":"string","value":"hi"},{"name":"inner","type":"object","value":"Object"},{"name":"gone","type":"object","subtype":"null","value":"null"}]}}""");

        Assert.Equal("Object {x: 3, label: \"hi\", inner: Object, gone: null}", text);
    }

    [Fact]
    public void AnObjectWithoutAPreview_IsItsDescription()
    {
        Assert.Equal("Map(2)", Describe("""{"type":"object","subtype":"map","className":"Map","description":"Map(2)","objectId":"1"}"""));
    }
}
