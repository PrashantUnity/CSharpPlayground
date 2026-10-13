using System.Collections.Concurrent;
using System.Text;
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
/// The sidecar's active session with VS Code over the stdio pipe.
/// </summary>
public sealed class SidecarSession : IAsyncDisposable
{
    private readonly JsonRpc _connection;
    private readonly ILogger _logger;

    public SidecarSession(string clientId, JsonRpc connection, ILogger logger)
    {
        ClientId = clientId;
        _connection = connection;
        _logger = logger;
    }

    public string ClientId { get; }

    public ConcurrentDictionary<string, OpenDocument> Documents { get; } = new(StringComparer.Ordinal);

    public bool IsConnected => !_connection.IsDisposed;

    public void Notify(string method, object payload)
    {
        if (_connection.IsDisposed) return;
        try
        {
            _ = _connection.NotifyWithParameterObjectAsync(method, payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send notification {Method}", method);
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

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
