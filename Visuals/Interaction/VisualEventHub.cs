using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Interaction;

/// <summary>
/// How a program whose input belongs to its user (every Run, and a Go, Rust, C++ or F# notebook cell) hears about events
/// on the visuals it shows: one socket on the loopback address for the whole app. Each program is told the address and a
/// token of its own (<see cref="AddressVariable"/>, <see cref="TokenVariable"/>); when its code first listens to a visual it
/// connects, sends <c>{"type":"hello","token":…}</c> as its first line, and then reads <c>event</c> messages, one per line,
/// the same ones a notebook kernel reads on its input.
/// </summary>
public sealed class VisualEventHub : IDisposable
{
    /// <summary>The environment variable that holds the address to connect to, as <c>127.0.0.1:port</c>.</summary>
    public const string AddressVariable = "FRY_EVENTS";

    /// <summary>The environment variable that holds the program's token, which its hello must carry.</summary>
    public const string TokenVariable = "FRY_EVENTS_TOKEN";

    // A hello is short and comes at once; anything longer, or later, isn't one of our programs.
    private const int MaxHelloBytes = 1024;
    private static readonly TimeSpan HelloTimeout = TimeSpan.FromSeconds(10);

    private static readonly Lazy<VisualEventHub> SharedHub = new(() => new VisualEventHub());

    private readonly Lock _gate = new();
    private readonly ConcurrentDictionary<string, ExternalVisualSession> _sessions = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _stopping = new();
    private TcpListener? _listener;
    private string? _address;

    /// <summary>The app's hub. It starts listening the first time a program is given its address.</summary>
    public static VisualEventHub Shared => SharedHub.Value;

    /// <summary>Where programs connect: <c>127.0.0.1:port</c>.</summary>
    public string Address
    {
        get
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_stopping.IsCancellationRequested, this);
                if (_address != null) return _address;

                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                _listener = listener;
                _address = $"127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}";
                _ = AcceptAsync(listener, _stopping.Token);
                return _address;
            }
        }
    }

    /// <summary>How many programs can connect now (ended ones can't).</summary>
    internal int SessionCount => _sessions.Count;

    /// <summary>A new token for <paramref name="session"/>; its program is told it, and must say it to connect.</summary>
    internal string Register(ExternalVisualSession session)
    {
        var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        _sessions[token] = session;
        return token;
    }

    internal void Unregister(string token) => _sessions.TryRemove(token, out _);

    public void Dispose()
    {
        lock (_gate)
        {
            if (_stopping.IsCancellationRequested) return;
            _stopping.Cancel();
            _listener?.Stop();
        }
    }

    private async Task AcceptAsync(TcpListener listener, CancellationToken stopping)
    {
        while (!stopping.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(stopping).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                continue; // one connection that failed on the way in; the next may be fine
            }

            _ = GreetAsync(client, stopping);
        }
    }

    // The first line says whose connection it is; without a token we gave out, it's let go.
    private async Task GreetAsync(TcpClient client, CancellationToken stopping)
    {
        try
        {
            client.NoDelay = true;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stopping);
            timeout.CancelAfter(HelloTimeout);
            var hello = await ReadLineAsync(client.GetStream(), timeout.Token).ConfigureAwait(false);
            if (TokenOf(hello) is { } token && _sessions.TryGetValue(token, out var session) && session.Attach(client)) return;
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
        {
            // It went away, or never said hello.
        }

        client.Dispose();
    }

    private static async Task<byte[]?> ReadLineAsync(NetworkStream stream, CancellationToken ct)
    {
        var line = new byte[MaxHelloBytes];
        var length = 0;
        while (length < line.Length)
        {
            var read = await stream.ReadAsync(line.AsMemory(length, 1), ct).ConfigureAwait(false);
            if (read == 0) return null;
            if (line[length] == (byte)'\n') return line[..length];
            length++;
        }

        return null;
    }

    private static string? TokenOf(byte[]? hello)
    {
        if (hello == null) return null;
        try
        {
            using var document = JsonDocument.Parse(hello);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object &&
                   root.TryGetProperty("type", out var type) && type.ValueEquals("hello") &&
                   root.TryGetProperty("token", out var token) && token.ValueKind == JsonValueKind.String
                ? token.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
