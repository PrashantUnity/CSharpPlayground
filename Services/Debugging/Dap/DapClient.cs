using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Debugging.Dap;

/// <summary>
/// Asynchronous client for the Debug Adapter Protocol (DAP).
/// Handles standard Content-Length HTTP-style message framing over input and output streams.
/// </summary>
public sealed class DapClient : IAsyncDisposable
{
    private static readonly byte[] HeaderTerminator = "\r\n\r\n"u8.ToArray();
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,

        // An optional field is left out of a request, never sent as null: the specification has no null, and some adapters refuse it.
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly Stream _inputStream;
    private readonly Stream _outputStream;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly ConcurrentDictionary<int, TaskCompletionSource<DapResponse>> _pendingRequests = new();
    private readonly CancellationTokenSource _cts = new();

    // Events are handled one at a time, in the order the adapter sent them: "exited" before "terminated", output lines in
    // order. Handling one may wait for a response (stopped asks for the stack), so it can't run on the loop that reads
    // responses; a queue with its own consumer keeps both true.
    private readonly Channel<DapEvent> _events = Channel.CreateUnbounded<DapEvent>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

    private Task? _readLoopTask;
    private Task? _eventLoopTask;
    private int _sequence;
    private int _isDisposed;

    /// <summary>
    /// Told every message that crosses a connection, as <c>-&gt; json</c> (sent) or <c>&lt;- json</c> (received): for finding out what an adapter
    /// really says when a session doesn't behave. Nothing is set in normal use.
    /// </summary>
    internal static volatile Action<string>? Trace;

    public event Func<DapEvent, Task>? EventReceived;
    public event Action<Exception>? ErrorOccurred;
    public event Action? Disconnected;

    public DapClient(Stream inputStream, Stream outputStream)
    {
        _inputStream = inputStream ?? throw new ArgumentNullException(nameof(inputStream));
        _outputStream = outputStream ?? throw new ArgumentNullException(nameof(outputStream));
    }

    public void Start()
    {
        if (_readLoopTask != null) return;
        _eventLoopTask = Task.Run(EventLoopAsync);
        _readLoopTask = Task.Run(ReadLoopAsync);
    }

    public async Task<DapResponse> SendRequestAsync(string command, object? arguments = null, CancellationToken ct = default)
    {
        ThrowIfDisposed();

        var seq = Interlocked.Increment(ref _sequence);
        var request = new DapRequest
        {
            Seq = seq,
            Command = command,
            Arguments = arguments
        };

        var tcs = new TaskCompletionSource<DapResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[seq] = tcs;

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, ct);
        using var reg = linkedCts.Token.Register(() =>
        {
            if (_pendingRequests.TryRemove(seq, out var pending))
            {
                pending.TrySetCanceled(linkedCts.Token);
            }
        });

        try
        {
            var json = JsonSerializer.Serialize(request, JsonOptions);
            Trace?.Invoke("-> " + json);
            var bodyBytes = Utf8WithoutBom.GetBytes(json);
            var header = $"Content-Length: {bodyBytes.Length}\r\n\r\n";
            var headerBytes = Encoding.ASCII.GetBytes(header);

            await _sendLock.WaitAsync(linkedCts.Token).ConfigureAwait(false);
            try
            {
                await _outputStream.WriteAsync(headerBytes, linkedCts.Token).ConfigureAwait(false);
                await _outputStream.WriteAsync(bodyBytes, linkedCts.Token).ConfigureAwait(false);
                await _outputStream.FlushAsync(linkedCts.Token).ConfigureAwait(false);
            }
            finally
            {
                _sendLock.Release();
            }

            return await tcs.Task.ConfigureAwait(false);
        }
        catch
        {
            _pendingRequests.TryRemove(seq, out _);
            throw;
        }
    }

    public async Task<TBody?> SendRequestAsync<TBody>(string command, object? arguments = null, CancellationToken ct = default)
    {
        var response = await SendRequestAsync(command, arguments, ct).ConfigureAwait(false);
        if (!response.Success)
        {
            throw new DapException(response.Message ?? $"DAP command '{command}' failed.", command, response);
        }

        if (response.Body == null) return default;
        return response.Body.Value.Deserialize<TBody>(JsonOptions);
    }

    private async Task ReadLoopAsync()
    {
        var buffer = new byte[8192];
        var memoryStream = new MemoryStream();

        try
        {
            while (!_cts.IsCancellationRequested)
            {
                // Read until we find \r\n\r\n
                int contentLength = -1;
                while (!_cts.IsCancellationRequested)
                {
                    var headerBytes = memoryStream.ToArray();
                    var termIndex = FindSubsequence(headerBytes, HeaderTerminator);
                    if (termIndex >= 0)
                    {
                        var headerText = Encoding.ASCII.GetString(headerBytes, 0, termIndex);
                        contentLength = ParseContentLength(headerText);

                        // Advance memory stream past \r\n\r\n
                        var remainingStart = termIndex + HeaderTerminator.Length;
                        var remainingCount = headerBytes.Length - remainingStart;
                        memoryStream.SetLength(0);
                        if (remainingCount > 0)
                        {
                            memoryStream.Write(headerBytes, remainingStart, remainingCount);
                        }
                        break;
                    }

                    var bytesRead = await _inputStream.ReadAsync(buffer, 0, buffer.Length, _cts.Token).ConfigureAwait(false);
                    if (bytesRead == 0)
                    {
                        // End of stream
                        return;
                    }
                    memoryStream.Write(buffer, 0, bytesRead);
                }

                if (contentLength < 0) break;

                // Read exact body payload of contentLength bytes
                while (memoryStream.Length < contentLength && !_cts.IsCancellationRequested)
                {
                    var bytesRead = await _inputStream.ReadAsync(buffer, 0, buffer.Length, _cts.Token).ConfigureAwait(false);
                    if (bytesRead == 0) return;
                    memoryStream.Write(buffer, 0, bytesRead);
                }

                var fullBody = memoryStream.ToArray();
                var jsonBytes = new byte[contentLength];
                Array.Copy(fullBody, 0, jsonBytes, 0, contentLength);

                // Preserve any trailing bytes for the next message
                var extraBytesCount = fullBody.Length - contentLength;
                memoryStream.SetLength(0);
                if (extraBytesCount > 0)
                {
                    memoryStream.Write(fullBody, contentLength, extraBytesCount);
                }

                ProcessMessage(jsonBytes);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex);
        }
        finally
        {
            CancelAllPending();

            // The event loop reports the disconnect once it has handled every event that arrived before it.
            _events.Writer.TryComplete();
        }
    }

    private async Task EventLoopAsync()
    {
        try
        {
            await foreach (var dapEvent in _events.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                var handler = EventReceived;
                if (handler == null) continue;

                try
                {
                    await handler.Invoke(dapEvent).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(ex);
                }
            }
        }
        finally
        {
            Disconnected?.Invoke();
        }
    }

    private void ProcessMessage(byte[] jsonBytes)
    {
        try
        {
            Trace?.Invoke("<- " + Utf8WithoutBom.GetString(jsonBytes));
            using var doc = JsonDocument.Parse(jsonBytes);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeProp)) return;

            var type = typeProp.GetString();
            if (string.Equals(type, "response", StringComparison.OrdinalIgnoreCase))
            {
                var response = JsonSerializer.Deserialize<DapResponse>(jsonBytes, JsonOptions);
                if (response != null && _pendingRequests.TryRemove(response.RequestSeq, out var tcs))
                {
                    tcs.TrySetResult(response);
                }
            }
            else if (string.Equals(type, "event", StringComparison.OrdinalIgnoreCase))
            {
                var dapEvent = JsonSerializer.Deserialize<DapEvent>(jsonBytes, JsonOptions);
                if (dapEvent != null) _events.Writer.TryWrite(dapEvent);
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex);
        }
    }

    private static int ParseContentLength(string headerText)
    {
        var lines = headerText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var parts = line.Split(':', 2);
            if (parts.Length == 2 && string.Equals(parts[0].Trim(), "Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(parts[1].Trim(), out var len)) return len;
            }
        }
        return -1;
    }

    private static int FindSubsequence(byte[] buffer, byte[] pattern)
    {
        if (buffer.Length < pattern.Length) return -1;
        for (int i = 0; i <= buffer.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (buffer[i + j] != pattern[j])
                {
                    match = false;
                    break;
                }
            }
            if (match) return i;
        }
        return -1;
    }

    private void CancelAllPending()
    {
        foreach (var kvp in _pendingRequests)
        {
            if (_pendingRequests.TryRemove(kvp.Key, out var tcs))
            {
                tcs.TrySetCanceled();
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _isDisposed) == 1)
        {
            throw new ObjectDisposedException(nameof(DapClient));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0) return;

        _cts.Cancel();
        CancelAllPending();

        foreach (var loop in new[] { _readLoopTask, _eventLoopTask })
        {
            if (loop == null) continue;
            try
            {
                await loop.ConfigureAwait(false);
            }
            catch
            {
                // Ignore task cancellation
            }
        }

        _sendLock.Dispose();
        _cts.Dispose();
        _inputStream.Dispose();
        _outputStream.Dispose();
    }
}

public sealed class DapException : Exception
{
    public string Command { get; }
    public DapResponse? Response { get; }

    public DapException(string message, string command, DapResponse? response = null)
        : base(message)
    {
        Command = command;
        Response = response;
    }
}
