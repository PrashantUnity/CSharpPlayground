using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Debugging.Cdp;

/// <summary>The inspector answered a request with an error.</summary>
public sealed class CdpException(string method, string message, int code) : Exception($"{method}: {message}")
{
    public string Method { get; } = method;
    public int Code { get; } = code;
}

/// <summary>
/// A client of the Chrome DevTools Protocol over a WebSocket, which is how V8's inspector is spoken to (<c>node --inspect</c>,
/// Chrome). Requests are <c>{"id":1,"method":"Debugger.enable","params":{}}</c> answered by <c>{"id":1,"result":{}}</c>; the
/// inspector's own news arrives as <c>{"method":"Debugger.paused","params":{}}</c>. Events are handed on one at a time, in the order they
/// arrived, on their own task: a handler may wait for a request's answer, which comes in through the reader.
/// </summary>
public sealed class CdpClient : IAsyncDisposable
{
    // An optional parameter is left out, never sent as null: the inspector refuses a null where it expects a string.
    private static readonly JsonSerializerOptions RequestOptions = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    private readonly ClientWebSocket _socket = new();
    private readonly ConcurrentDictionary<int, (string Method, TaskCompletionSource<JsonElement> Reply)> _pending = new();
    private readonly Channel<(string Method, JsonElement Parameters)> _events = Channel.CreateUnbounded<(string, JsonElement)>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private Task? _readLoop;
    private Task? _eventLoop;
    private int _nextId;
    private int _disposed;

    /// <summary>Told every message that crosses a connection, as <c>-&gt; json</c> (sent) or <c>&lt;- json</c> (received), for finding out what the inspector really says. Nothing is set in normal use.</summary>
    internal static volatile Action<string>? Trace;

    /// <summary>The inspector's news: the event's name (<c>Debugger.paused</c>) and its parameters.</summary>
    public event Func<string, JsonElement, Task>? EventReceived;

    /// <summary>The connection ended: the program exited, or the inspector closed it.</summary>
    public event Action? Closed;

    /// <summary>
    /// Asks the inspector where to connect: <c>GET /json/list</c> on its port names the page (or the Node process) and its WebSocket
    /// address. Node opens the port a moment after it starts, so this tries again until it has an answer or the time is up.
    /// </summary>
    public static async Task<string> DiscoverWebSocketUrlAsync(int port, TimeSpan timeout, CancellationToken ct)
    {
        var requestTimeout = timeout < TimeSpan.FromSeconds(2) ? timeout : TimeSpan.FromSeconds(5);
        using var http = new HttpClient { Timeout = requestTimeout };
        var deadline = DateTime.UtcNow + timeout;
        Exception? last = null;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var json = await http.GetStringAsync($"http://127.0.0.1:{port}/json/list", ct).ConfigureAwait(false);
                using var document = JsonDocument.Parse(json);
                foreach (var target in document.RootElement.EnumerateArray())
                {
                    if (target.TryGetProperty("webSocketDebuggerUrl", out var url) && url.GetString() is { Length: > 0 } address) return address;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or IOException)
            {
                if (ct.IsCancellationRequested) throw;
                last = ex;
            }

            await Task.Delay(100, ct).ConfigureAwait(false);
        }

        throw new TimeoutException($"The inspector on port {port} didn't answer within {timeout.TotalSeconds:0} seconds{(last == null ? string.Empty : ": " + last.Message)}.");
    }

    public async Task ConnectAsync(string webSocketUrl, CancellationToken ct)
    {
        await _socket.ConnectAsync(new Uri(webSocketUrl), ct).ConfigureAwait(false);
        _eventLoop = Task.Run(EventLoopAsync);
        _readLoop = Task.Run(ReadLoopAsync);
    }

    /// <summary>Sends a request and returns its <c>result</c>; an error answer is a <see cref="CdpException"/>.</summary>
    public async Task<JsonElement> SendAsync(string method, object? parameters = null, CancellationToken ct = default)
    {
        var id = Interlocked.Increment(ref _nextId);
        var reply = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = (method, reply);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, ct);
        using var registration = linked.Token.Register(() =>
        {
            if (_pending.TryRemove(id, out var pending)) pending.Reply.TrySetCanceled(linked.Token);
        });

        var message = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object?> { ["id"] = id, ["method"] = method, ["params"] = parameters ?? new { } }, RequestOptions);
        Trace?.Invoke("-> " + Encoding.UTF8.GetString(message));
        await _sendLock.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            await _socket.SendAsync(message, WebSocketMessageType.Text, endOfMessage: true, linked.Token).ConfigureAwait(false);
        }
        catch
        {
            _pending.TryRemove(id, out _);
            throw;
        }
        finally
        {
            _sendLock.Release();
        }

        return await reply.Task.ConfigureAwait(false);
    }

    private async Task ReadLoopAsync()
    {
        var buffer = new byte[16 * 1024];
        using var message = new MemoryStream();
        try
        {
            while (!_cts.IsCancellationRequested && _socket.State == WebSocketState.Open)
            {
                message.SetLength(0);
                WebSocketReceiveResult received;
                do
                {
                    received = await _socket.ReceiveAsync(buffer, _cts.Token).ConfigureAwait(false);
                    if (received.MessageType == WebSocketMessageType.Close)
                    {
                        // The inspector is closing the connection: answering finishes the handshake on its side too.
                        try
                        {
                            using var closing = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                            await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, closing.Token).ConfigureAwait(false);
                        }
                        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException or ObjectDisposedException)
                        {
                        }

                        return;
                    }
                    message.Write(buffer, 0, received.Count);
                }
                while (!received.EndOfMessage);

                Handle(message.ToArray());
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException or IOException or ObjectDisposedException)
        {
            // The connection ended.
        }
        finally
        {
            foreach (var id in _pending.Keys)
            {
                if (_pending.TryRemove(id, out var pending)) pending.Reply.TrySetCanceled();
            }

            // The event loop reports the close once it has handled every event that arrived before it.
            _events.Writer.TryComplete();
        }
    }

    private void Handle(byte[] json)
    {
        try
        {
            Trace?.Invoke("<- " + Encoding.UTF8.GetString(json));
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.TryGetProperty("id", out var idElement) && idElement.TryGetInt32(out var id))
            {
                if (!_pending.TryRemove(id, out var pending)) return;
                if (root.TryGetProperty("error", out var error))
                {
                    var text = error.TryGetProperty("message", out var m) ? m.GetString() ?? "error" : "error";
                    var code = error.TryGetProperty("code", out var c) && c.TryGetInt32(out var value) ? value : 0;
                    pending.Reply.TrySetException(new CdpException(pending.Method, text, code));
                }
                else
                {
                    pending.Reply.TrySetResult(root.TryGetProperty("result", out var result) ? result.Clone() : default);
                }
            }
            else if (root.TryGetProperty("method", out var method))
            {
                _events.Writer.TryWrite((method.GetString() ?? string.Empty, root.TryGetProperty("params", out var parameters) ? parameters.Clone() : default));
            }
        }
        catch (JsonException)
        {
            // Not a message this client understands.
        }
    }

    private async Task EventLoopAsync()
    {
        try
        {
            await foreach (var (method, parameters) in _events.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                var handler = EventReceived;
                if (handler == null) continue;
                try
                {
                    await handler(method, parameters).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // A handler's failure is its own; the next event is still delivered.
                }
            }
        }
        finally
        {
            Closed?.Invoke();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _cts.Cancel();
        try
        {
            if (_socket.State == WebSocketState.Open)
            {
                using var closing = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, closing.Token).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException or ObjectDisposedException)
        {
        }

        foreach (var loop in new[] { _readLoop, _eventLoop })
        {
            if (loop == null) continue;
            try
            {
                await loop.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is TimeoutException or OperationCanceledException)
            {
            }
        }

        _socket.Dispose();
        _sendLock.Dispose();
    }
}
