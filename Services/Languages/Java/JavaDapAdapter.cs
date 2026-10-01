using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging.Dap;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;

/// <summary>
/// A Debug Adapter Protocol (DAP) server for Java.
/// Exposes standard DAP JSON-RPC streams to <see cref="DapClient"/> and translates DAP commands
/// into the JVM debug runtime, delivering universal protocol-driven debugging.
/// </summary>
public sealed partial class JavaDapAdapter : IAsyncDisposable
{
    [GeneratedRegex(@"""thread=(?<thread>[^""]+)""[,\s]+(?<method>[^\(\)\r\n]+)\(\)[,\s]+line=(?<line>\d+)", RegexOptions.Compiled)]
    private static partial Regex StopRegex();

    [GeneratedRegex(@"\[(?<idx>\d+)\]\s+(?<method>[\w\.\$<>\s]+?)\s+\((?<file>[^:]+):(?<line>\d+)\)", RegexOptions.Compiled)]
    private static partial Regex FrameRegex();

    [GeneratedRegex(@"(?:^|[\r\n]|\]\s+)(?<name>[\w\$]+)\s*=\s*(?<val>[^\r\n]*)", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex VarRegex();

    [GeneratedRegex(@"(?:\w+\[\d+\]\s*$|>\s*$)", RegexOptions.Compiled)]
    private static partial Regex PromptRegex();

    [GeneratedRegex(@"\w+\[\d+\]\s*$", RegexOptions.Compiled)]
    private static partial Regex SuspendedPromptRegex();

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IManagedProcess _process;
    private readonly string _sourceFilePath;
    private readonly string _fqn;
    private readonly Action<string>? _liveOutput;

    private readonly Pipe _clientToServerPipe = new();
    private readonly Pipe _serverToClientPipe = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly SemaphoreSlim _jdbLock = new(1, 1);
    private readonly StringBuilder _jdbOutputBuffer = new();
    private readonly object _stateLock = new();

    private readonly TaskCompletionSource<bool> _initialPromptTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource<string>? _activeJdbCommandTcs;
    private int _jdbCommandStartOffset;
    private int _seq;
    private int _pausedLine = -1;
    private string _pausedMethod = "main";
    private int _lastResumeIndex;
    private bool _isCurrentlyPaused;
    private int _isDisposed;

    public JavaDapAdapter(
        IManagedProcess process,
        string sourceFilePath,
        string fqn,
        Action<string>? liveOutput = null)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
        _sourceFilePath = sourceFilePath;
        _fqn = fqn;
        _liveOutput = liveOutput;

        ClientInputStream = _serverToClientPipe.Reader.AsStream();
        ClientOutputStream = _clientToServerPipe.Writer.AsStream();

        _ = Task.Run(DapMessageLoopAsync);
        _ = Task.Run(MonitorProcessExitAsync);
    }

    public Stream ClientInputStream { get; }
    public Stream ClientOutputStream { get; }

    public void OnProcessOutput(string chunk)
    {
        _liveOutput?.Invoke(chunk);

        string fullText;
        int checkOffset;
        bool shouldCheckStop;
        TaskCompletionSource<string>? cmdTcs = null;
        string? cmdOutput = null;

        lock (_stateLock)
        {
            _jdbOutputBuffer.Append(chunk);
            fullText = _jdbOutputBuffer.ToString();
            checkOffset = _lastResumeIndex;
            shouldCheckStop = !_isCurrentlyPaused;

            if (!_initialPromptTcs.Task.IsCompleted && PromptRegex().IsMatch(fullText))
            {
                _initialPromptTcs.TrySetResult(true);
            }

            var textSinceCommand = fullText.Length > _jdbCommandStartOffset ? fullText[_jdbCommandStartOffset..] : string.Empty;
            if (_activeJdbCommandTcs != null && PromptRegex().IsMatch(textSinceCommand))
            {
                cmdTcs = _activeJdbCommandTcs;
                _activeJdbCommandTcs = null;
                int start = _jdbCommandStartOffset;
                cmdOutput = fullText.Length > start ? fullText[start..] : string.Empty;
            }
        }

        cmdTcs?.TrySetResult(cmdOutput ?? string.Empty);

        if (shouldCheckStop)
        {
            var textToCheck = checkOffset < fullText.Length ? fullText[checkOffset..] : string.Empty;
            var matches = StopRegex().Matches(textToCheck);
            if (matches.Count > 0 && SuspendedPromptRegex().IsMatch(textToCheck))
            {
                var stopMatch = matches[^1];
                lock (_stateLock)
                {
                    if (_isCurrentlyPaused) return;
                    _isCurrentlyPaused = true;
                    _lastResumeIndex = fullText.Length;
                    _pausedLine = int.Parse(stopMatch.Groups["line"].Value);
                    _pausedMethod = stopMatch.Groups["method"].Value.Trim();
                }

                _ = EmitDapEventAsync("stopped", new
                {
                    reason = "breakpoint",
                    threadId = 1,
                    allThreadsStopped = true
                });
            }
        }
    }

    private async Task DapMessageLoopAsync()
    {
        var stream = _clientToServerPipe.Reader.AsStream();
        var buffer = new byte[8192];
        var memory = new MemoryStream();

        try
        {
            while (Interlocked.CompareExchange(ref _isDisposed, 0, 0) == 0)
            {
                int read = await stream.ReadAsync(buffer).ConfigureAwait(false);
                if (read == 0) break;

                memory.Write(buffer, 0, read);

                while (true)
                {
                    var bytes = memory.ToArray();
                    int headerEnd = FindHeaderTerminator(bytes);
                    if (headerEnd < 0) break;

                    var headerText = Encoding.ASCII.GetString(bytes, 0, headerEnd);
                    int contentLength = ParseContentLength(headerText);
                    if (contentLength < 0) break;

                    int bodyStart = headerEnd + 4;
                    if (bytes.Length < bodyStart + contentLength) break;

                    var json = Encoding.UTF8.GetString(bytes, bodyStart, contentLength);
                    memory.SetLength(0);
                    int remaining = bytes.Length - (bodyStart + contentLength);
                    if (remaining > 0)
                    {
                        memory.Write(bytes, bodyStart + contentLength, remaining);
                    }

                    _ = Task.Run(() => HandleDapRequestJsonAsync(json));
                }
            }
        }
        catch
        {
        }
    }

    private async Task HandleDapRequestJsonAsync(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            int seq = root.GetProperty("seq").GetInt32();
            string command = root.GetProperty("command").GetString() ?? string.Empty;
            JsonElement? args = root.TryGetProperty("arguments", out var a) ? a : null;

            await DispatchDapCommandAsync(seq, command, args).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private async Task<string> SendCommandAsync(string cmd)
    {
        await _jdbLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_initialPromptTcs.Task.IsCompleted)
            {
                using var readyCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                using var regReady = readyCts.Token.Register(() => _initialPromptTcs.TrySetResult(false));
                await _initialPromptTcs.Task.ConfigureAwait(false);
            }

            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_stateLock)
            {
                _jdbCommandStartOffset = _jdbOutputBuffer.Length;
                _activeJdbCommandTcs = tcs;
            }

            await _process.WriteInputAsync(cmd + "\n").ConfigureAwait(false);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            using var reg = timeoutCts.Token.Register(() =>
            {
                lock (_stateLock)
                {
                    if (_activeJdbCommandTcs == tcs)
                    {
                        _activeJdbCommandTcs = null;
                    }
                }
                tcs.TrySetResult(string.Empty);
            });
            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            _jdbLock.Release();
        }
    }

    private async Task SendResponseAsync(int reqSeq, string command, bool success, object? body)
    {
        int seq = Interlocked.Increment(ref _seq);
        var res = new
        {
            seq,
            type = "response",
            request_seq = reqSeq,
            command,
            success,
            body
        };

        var json = JsonSerializer.Serialize(res, JsonOptions);
        await WriteDapMessageAsync(json).ConfigureAwait(false);
    }

    private async Task EmitDapEventAsync(string eventName, object? body)
    {
        int seq = Interlocked.Increment(ref _seq);
        var evt = new
        {
            seq,
            type = "event",
            @event = eventName,
            body
        };

        var json = JsonSerializer.Serialize(evt, JsonOptions);
        await WriteDapMessageAsync(json).ConfigureAwait(false);
    }

    private async Task WriteDapMessageAsync(string json)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(json);
        var header = $"Content-Length: {bodyBytes.Length}\r\n\r\n";
        var headerBytes = Encoding.ASCII.GetBytes(header);

        await _sendLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var outStream = _serverToClientPipe.Writer.AsStream();
            await outStream.WriteAsync(headerBytes).ConfigureAwait(false);
            await outStream.WriteAsync(bodyBytes).ConfigureAwait(false);
            await outStream.FlushAsync().ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task MonitorProcessExitAsync()
    {
        try
        {
            int exitCode = await _process.Completion.ConfigureAwait(false);
            lock (_stateLock)
            {
                _initialPromptTcs.TrySetResult(false);
                _activeJdbCommandTcs?.TrySetResult(string.Empty);
                _activeJdbCommandTcs = null;
            }
            await EmitDapEventAsync("terminated", new { exitCode }).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private static int FindHeaderTerminator(byte[] bytes)
    {
        for (int i = 0; i <= bytes.Length - 4; i++)
        {
            if (bytes[i] == '\r' && bytes[i + 1] == '\n' && bytes[i + 2] == '\r' && bytes[i + 3] == '\n')
            {
                return i;
            }
        }
        return -1;
    }

    private static int ParseContentLength(string header)
    {
        foreach (var line in header.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                var val = line["Content-Length:".Length..].Trim();
                if (int.TryParse(val, out int len)) return len;
            }
        }
        return -1;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0) return;

        lock (_stateLock)
        {
            _initialPromptTcs.TrySetResult(false);
            _activeJdbCommandTcs?.TrySetResult(string.Empty);
            _activeJdbCommandTcs = null;
        }

        try { _clientToServerPipe.Writer.Complete(); } catch { }
        try { _clientToServerPipe.Reader.Complete(); } catch { }
        try { _serverToClientPipe.Writer.Complete(); } catch { }
        try { _serverToClientPipe.Reader.Complete(); } catch { }
        try { _process.Kill(); } catch { }
        await Task.CompletedTask;
    }
}
