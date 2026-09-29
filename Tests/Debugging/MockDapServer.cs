using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging.Dap;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.Debugging;

/// <summary>A pretend debug adapter on the other end of a <see cref="DapClient"/>: it records the requests it gets and answers as each test says.</summary>
internal sealed class MockDapServer : IAsyncDisposable
{
    private readonly Stream _serverIn;
    private readonly Stream _serverOut;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly Task _loop;
    private int _seq;

    /// <summary>An adapter reached through in-memory pipes: <see cref="CreateClient"/> makes the client on the other end.</summary>
    public MockDapServer()
    {
        var toClient = new Pipe();
        var fromClient = new Pipe();
        ClientIn = toClient.Reader.AsStream();
        _serverOut = toClient.Writer.AsStream();
        ClientOut = fromClient.Writer.AsStream();
        _serverIn = fromClient.Reader.AsStream();
        _loop = Task.Run(LoopAsync);
    }

    /// <summary>An adapter listening on streams that something else connects to a real client: a socket, or a started program's standard streams.</summary>
    public MockDapServer(Stream serverIn, Stream serverOut, Func<MockDapServer, string, int, JsonElement, Task>? onRequest = null)
    {
        _serverIn = serverIn;
        _serverOut = serverOut;
        ClientIn = Stream.Null;
        ClientOut = Stream.Null;
        if (onRequest != null) OnRequest = onRequest;
        _loop = Task.Run(LoopAsync);
    }

    public Stream ClientIn { get; }
    public Stream ClientOut { get; }

    /// <summary>The whole request (its arguments too) for each command received, in the order they arrived.</summary>
    public ConcurrentQueue<JsonElement> RequestBodies { get; } = new();

    /// <summary>
    /// Serves one client on a TCP port, the way a debug adapter that has no standard-stream mode does (Delve, debugpy): the
    /// first connection is answered by the returned server once <paramref name="accepted"/> is awaited.
    /// </summary>
    public static (Task<MockDapServer> Accepted, IDisposable Listener) ListenOnce(int port, Func<MockDapServer, string, int, JsonElement, Task>? onRequest = null)
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
        listener.Start();
        var accepted = Task.Run(async () =>
        {
            var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
            var stream = client.GetStream();
            return new MockDapServer(stream, stream, onRequest);
        });
        return (accepted, new ListenerHandle(listener));
    }

    private sealed class ListenerHandle(System.Net.Sockets.TcpListener listener) : IDisposable
    {
        public void Dispose() => listener.Stop();
    }

    /// <summary>The commands received, in the order they arrived.</summary>
    public ConcurrentQueue<string> Requests { get; } = new();

    /// <summary>How the adapter reacts to a request; by default it just says success.</summary>
    public Func<MockDapServer, string, int, JsonElement, Task> OnRequest { get; set; } = (server, command, seq, _) => server.RespondAsync(seq, command);

    public DapClient CreateClient()
    {
        return new DapClient(ClientIn, ClientOut);
    }

    public Task RespondAsync(int requestSeq, string command, bool success = true, object? body = null, string? message = null) =>
        WriteAsync(new { seq = Interlocked.Increment(ref _seq), type = "response", request_seq = requestSeq, success, command, message, body });

    public Task EventAsync(string name, object? body = null) =>
        WriteAsync(new { seq = Interlocked.Increment(ref _seq), type = "event", @event = name, body = body ?? new { } });

    private async Task WriteAsync(object message)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await _serverOut.WriteAsync(Encoding.ASCII.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n")).ConfigureAwait(false);
            await _serverOut.WriteAsync(bytes).ConfigureAwait(false);
            await _serverOut.FlushAsync().ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task LoopAsync()
    {
        while (true)
        {
            string raw;
            try
            {
                raw = await ReadAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                return;
            }

            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement.Clone();
            var command = root.GetProperty("command").GetString()!;
            var seq = root.GetProperty("seq").GetInt32();
            Requests.Enqueue(command);
            RequestBodies.Enqueue(root);
            await OnRequest(this, command, seq, root).ConfigureAwait(false);
        }
    }

    private async Task<string> ReadAsync()
    {
        var header = new StringBuilder();
        var one = new byte[1];
        while (!header.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
        {
            if (await _serverIn.ReadAsync(one).ConfigureAwait(false) == 0) throw new EndOfStreamException();
            header.Append((char)one[0]);
        }

        var length = int.Parse(header.ToString().Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
            .First(l => l.StartsWith("Content-Length", StringComparison.OrdinalIgnoreCase)).Split(':')[1].Trim());
        var body = new byte[length];
        var read = 0;
        while (read < length)
        {
            var n = await _serverIn.ReadAsync(body.AsMemory(read, length - read)).ConfigureAwait(false);
            if (n == 0) throw new EndOfStreamException();
            read += n;
        }

        return Encoding.UTF8.GetString(body);
    }

    public async ValueTask DisposeAsync()
    {
        try { _serverOut.Dispose(); } catch { }
        try { _serverIn.Dispose(); } catch { }
        try { await _loop.WaitAsync(TimeSpan.FromSeconds(2)); } catch { }
    }
}
