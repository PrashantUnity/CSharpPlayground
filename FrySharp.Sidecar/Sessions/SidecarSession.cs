using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;
using FrySharp.Sidecar.Protocol;
using Microsoft.Extensions.Logging;
using StreamJsonRpc;

namespace FrySharp.Sidecar.Sessions;

public sealed class OpenDocument(string path, string fullPath, string text, int version, string diskVersion, Encoding encoding)
{
    public Lock Gate { get; } = new();
    public string Path { get; } = path;
    public string FullPath { get; } = fullPath;
    public string Text { get; set; } = text;
    public int Version { get; set; } = version;
    public string DiskVersion { get; set; } = diskVersion;
    public Encoding Encoding { get; set; } = encoding;
    public bool ChangeOnDiskReported { get; set; }
    public bool OutOfSync { get; set; }
}

/// <summary>
/// The sidecar's active session with VS Code over the stdio pipe. Notifications leave through one queue and are sent one
/// at a time, so the client receives them in the order they were made; <see cref="DrainAsync"/> waits until those made so
/// far have been sent (a reply that must not overtake them waits for it).
/// </summary>
public sealed class SidecarSession : IAsyncDisposable
{
    private readonly JsonRpc _connection;
    private readonly ILogger _logger;
    private readonly Channel<Func<Task>> _outbox = Channel.CreateUnbounded<Func<Task>>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Task _pump;

    public SidecarSession(string clientId, JsonRpc connection, ILogger logger)
    {
        ClientId = clientId;
        _connection = connection;
        _logger = logger;
        _pump = Task.Run(PumpAsync);
    }

    public string ClientId { get; }

    public ConcurrentDictionary<string, OpenDocument> Documents { get; } = new(StringComparer.Ordinal);

    public bool IsConnected => !_connection.IsDisposed;

    public void Notify(string method, object payload)
    {
        _outbox.Writer.TryWrite(async () =>
        {
            if (_connection.IsDisposed) return;
            try
            {
                await _connection.NotifyWithParameterObjectAsync(method, payload).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send notification {Method}", method);
            }
        });
    }

    /// <summary>Completes once every notification made before this call has been sent.</summary>
    public Task DrainAsync()
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_outbox.Writer.TryWrite(() =>
            {
                done.TrySetResult();
                return Task.CompletedTask;
            }))
        {
            done.TrySetResult();
        }
        return done.Task;
    }

    private async Task PumpAsync()
    {
        await foreach (var send in _outbox.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                await send().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "A queued notification failed.");
            }
        }
    }

    public async Task<string?> RequestInputAsync(InputRequest request, CancellationToken ct)
    {
        if (_connection.IsDisposed) return null;
        try
        {
            return await _connection.InvokeWithParameterObjectAsync<string?>(ClientMethods.InputRequest, request, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _outbox.Writer.TryComplete();
        await _pump.ConfigureAwait(false);
    }
}
