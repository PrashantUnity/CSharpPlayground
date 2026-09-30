using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging.Cdp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.JavaScript;

/// <summary>
/// A Debug Adapter Protocol server for Node.js. <c>node --inspect-brk</c> opens V8's inspector, which speaks the Chrome DevTools
/// Protocol over a WebSocket, not the Debug Adapter Protocol; this class is the bridge. It reads DAP requests from
/// <see cref="ClientOutputStream"/>, does them through a <see cref="CdpClient"/>, and answers on <see cref="ClientInputStream"/>, which is
/// what <c>DapAdapterManager.LaunchBridgeAdapterAsync</c> connects a <c>DapClient</c> to (the same way the Java debugger's bridge over
/// <c>jdb</c> works). Breakpoints, stepping, the call stack, variables and evaluation are supported.
/// </summary>
public sealed class NodeInspectorDapAdapter : IAsyncDisposable
{
    // What Node prints about its inspector; it says nothing about the user's program.
    private static readonly Regex InspectorBanner = new(@"^(Debugger listening on ws://.*|For help, see: https://nodejs\.org/.*|Debugger attached\.|Waiting for the debugger to disconnect\.\.\.)\r?\n?", RegexOptions.Multiline | RegexOptions.Compiled);

    // The names a CommonJS module's wrapper function takes: the top level of a script is that function's body, and these are not the script's.
    private static readonly HashSet<string> ModuleWrapperNames = new(StringComparer.Ordinal) { "exports", "require", "module", "__filename", "__dirname" };

    private const int MaxVariables = 300;
    private const int ThreadId = 1;

    private static readonly JsonSerializerOptions JsonOptions = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private readonly IManagedProcess _process;
    private readonly int _port;
    private readonly Action<string>? _liveOutput;

    private readonly Pipe _clientToServer = new();
    private readonly Pipe _serverToClient = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly object _state = new();
    private readonly CancellationTokenSource _cts = new();

    private CdpClient? _cdp;
    private List<Frame> _frames = [];
    private readonly Dictionary<string, string> _scriptUrls = new(StringComparer.Ordinal);
    private readonly Dictionary<int, (string ObjectId, bool HideModuleWrapper)> _references = new();
    private readonly Dictionary<string, List<string>> _breakpointsByFile = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _requestedLines = new(StringComparer.Ordinal);
    private readonly TaskCompletionSource _initialPause = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly string _scriptUrl;
    private int? _mainContextId;
    private string? _nextStopReason;
    private bool _holdingInitialPause;
    private int _nextReference = 1;
    private int _sequence;
    private int _isDisposed;

    private sealed record Frame(string CallFrameId, string FunctionName, string ScriptId, string Url, int Line, int Column, IReadOnlyList<ScopeInfo> Scopes);

    private sealed record ScopeInfo(string Type, string ObjectId);

    public NodeInspectorDapAdapter(IManagedProcess process, int inspectorPort, string scriptPath, Action<string>? liveOutput = null)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
        _port = inspectorPort;
        _scriptUrl = new Uri(Path.GetFullPath(scriptPath)).AbsoluteUri;
        _liveOutput = liveOutput;

        ClientInputStream = _serverToClient.Reader.AsStream();
        ClientOutputStream = _clientToServer.Writer.AsStream();

        _ = Task.Run(MessageLoopAsync);
        _ = Task.Run(MonitorProcessExitAsync);
    }

    /// <summary>What the <c>DapClient</c> reads: the adapter's answers and events.</summary>
    public Stream ClientInputStream { get; }

    /// <summary>What the <c>DapClient</c> writes: its requests.</summary>
    public Stream ClientOutputStream { get; }

    /// <summary>The program's output as it arrives, without Node's own notices about the inspector.</summary>
    public void OnProcessOutput(string chunk)
    {
        var text = InspectorBanner.Replace(chunk, string.Empty);
        if (text.Length > 0) _liveOutput?.Invoke(text);
    }

    // ── DAP transport ─────────────────────────────────────────────────────

    private async Task MessageLoopAsync()
    {
        var reader = _clientToServer.Reader;
        var pending = new List<byte>();
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var read = await reader.ReadAsync(_cts.Token).ConfigureAwait(false);
                foreach (var segment in read.Buffer) pending.AddRange(segment.Span.ToArray());
                reader.AdvanceTo(read.Buffer.End);

                while (TryTakeMessage(pending, out var json)) await HandleAsync(json).ConfigureAwait(false);
                if (read.IsCompleted) break;
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    // Content-Length: N\r\n\r\n{json}
    private static bool TryTakeMessage(List<byte> buffer, out string json)
    {
        json = string.Empty;
        var text = buffer.ToArray();
        var terminator = IndexOf(text, "\r\n\r\n"u8);
        if (terminator < 0) return false;

        var header = Encoding.ASCII.GetString(text, 0, terminator);
        var match = Regex.Match(header, @"Content-Length:\s*(\d+)", RegexOptions.IgnoreCase);
        if (!match.Success) return false;
        var length = int.Parse(match.Groups[1].Value);
        var start = terminator + 4;
        if (text.Length < start + length) return false;

        json = Encoding.UTF8.GetString(text, start, length);
        buffer.RemoveRange(0, start + length);
        return true;
    }

    private static int IndexOf(byte[] haystack, ReadOnlySpan<byte> needle)
    {
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle)) return i;
        }

        return -1;
    }

    private async Task WriteAsync(object message)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await _serverToClient.Writer.WriteAsync(Encoding.ASCII.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n")).ConfigureAwait(false);
            await _serverToClient.Writer.WriteAsync(bytes).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            // The client is gone.
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private Task RespondAsync(int requestSeq, string command, object? body = null) =>
        WriteAsync(new { seq = Interlocked.Increment(ref _sequence), type = "response", request_seq = requestSeq, success = true, command, body });

    private Task RefuseAsync(int requestSeq, string command, string message) =>
        WriteAsync(new { seq = Interlocked.Increment(ref _sequence), type = "response", request_seq = requestSeq, success = false, command, message });

    private Task EventAsync(string name, object? body = null) =>
        WriteAsync(new { seq = Interlocked.Increment(ref _sequence), type = "event", @event = name, body = body ?? new { } });

    // ── DAP requests ──────────────────────────────────────────────────────

    private async Task HandleAsync(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var command = root.GetProperty("command").GetString() ?? string.Empty;
        var seq = root.GetProperty("seq").GetInt32();
        var arguments = root.TryGetProperty("arguments", out var a) ? a.Clone() : default;

        try
        {
            switch (command)
            {
                case "initialize":
                    await InitializeAsync(seq).ConfigureAwait(false);
                    break;
                case "setBreakpoints":
                    await SetBreakpointsAsync(seq, arguments).ConfigureAwait(false);
                    break;
                case "configurationDone":
                    await ConfigurationDoneAsync(seq).ConfigureAwait(false);
                    break;
                case "continue":
                    await ResumeAsync(seq, command, "Debugger.resume", null).ConfigureAwait(false);
                    break;
                case "next":
                    await ResumeAsync(seq, command, "Debugger.stepOver", "step").ConfigureAwait(false);
                    break;
                case "stepIn":
                    await ResumeAsync(seq, command, "Debugger.stepInto", "step").ConfigureAwait(false);
                    break;
                case "stepOut":
                    await ResumeAsync(seq, command, "Debugger.stepOut", "step").ConfigureAwait(false);
                    break;
                case "pause":
                    lock (_state) _nextStopReason = "pause";
                    await RespondAsync(seq, command).ConfigureAwait(false);
                    await Cdp().SendAsync("Debugger.pause").ConfigureAwait(false);
                    break;
                case "threads":
                    await RespondAsync(seq, command, new { threads = new[] { new { id = ThreadId, name = "main" } } }).ConfigureAwait(false);
                    break;
                case "stackTrace":
                    await StackTraceAsync(seq, arguments).ConfigureAwait(false);
                    break;
                case "scopes":
                    await ScopesAsync(seq, arguments).ConfigureAwait(false);
                    break;
                case "variables":
                    await VariablesAsync(seq, arguments).ConfigureAwait(false);
                    break;
                case "evaluate":
                    await EvaluateAsync(seq, arguments).ConfigureAwait(false);
                    break;
                case "disconnect" or "terminate":
                    try { _process.Kill(); } catch (Exception) { }
                    await RespondAsync(seq, command).ConfigureAwait(false);
                    break;
                default:
                    // Nothing to do for the rest (setExceptionBreakpoints, source, ...), and saying so is what a client needs.
                    await RespondAsync(seq, command).ConfigureAwait(false);
                    break;
            }
        }
        catch (Exception ex) when (_isDisposed == 0)
        {
            // Whatever went wrong is the answer to this request; the next one is still read.
            await RefuseAsync(seq, command, ex.Message).ConfigureAwait(false);
        }
    }

    private CdpClient Cdp() => _cdp ?? throw new InvalidOperationException("The debugger isn't connected to Node's inspector.");

    private async Task InitializeAsync(int seq)
    {
        // Node opens its inspector port a moment after it starts.
        var url = await CdpClient.DiscoverWebSocketUrlAsync(_port, TimeSpan.FromSeconds(15), _cts.Token).ConfigureAwait(false);
        var cdp = new CdpClient();
        cdp.EventReceived += OnCdpEventAsync;
        await cdp.ConnectAsync(url, _cts.Token).ConfigureAwait(false);
        _cdp = cdp;

        await cdp.SendAsync("Debugger.enable").ConfigureAwait(false);
        await cdp.SendAsync("Runtime.enable").ConfigureAwait(false);

        // Stepping and pausing stay in the user's code: Node's own scripts (node:internal/...) are skipped.
        await cdp.SendAsync("Debugger.setBlackboxPatterns", new { patterns = new[] { "^node:.*", "^internal/.*" } }).ConfigureAwait(false);
        await cdp.SendAsync("Debugger.setPauseOnExceptions", new { state = "uncaught" }).ConfigureAwait(false);

        // --inspect-brk waits for this, and then stops on the script's first line; that stop is held until configurationDone.
        await cdp.SendAsync("Runtime.runIfWaitingForDebugger").ConfigureAwait(false);
        await Task.WhenAny(_initialPause.Task, Task.Delay(TimeSpan.FromSeconds(10), _cts.Token)).ConfigureAwait(false);

        await RespondAsync(seq, "initialize", new
        {
            supportsConfigurationDoneRequest = true,
            supportsConditionalBreakpoints = true,
            supportsEvaluateForHovers = true,
            supportTerminateDebuggee = true
        }).ConfigureAwait(false);
        await EventAsync("initialized").ConfigureAwait(false);
    }

    private async Task SetBreakpointsAsync(int seq, JsonElement arguments)
    {
        var path = arguments.GetProperty("source").TryGetProperty("path", out var p) ? p.GetString() ?? string.Empty : string.Empty;
        var url = UrlOf(path);
        var urlRegex = UrlRegexOf(path);

        // The list a file's breakpoints are set to replaces the one before it.
        if (_breakpointsByFile.Remove(url, out var previous))
        {
            foreach (var id in previous)
            {
                try { await Cdp().SendAsync("Debugger.removeBreakpoint", new { breakpointId = id }).ConfigureAwait(false); }
                catch (CdpException) { }
                _requestedLines.Remove(id);
            }
        }

        var ids = new List<string>();
        var results = new List<object>();
        if (arguments.TryGetProperty("breakpoints", out var requested) && requested.ValueKind == JsonValueKind.Array)
        {
            foreach (var breakpoint in requested.EnumerateArray())
            {
                var line = breakpoint.GetProperty("line").GetInt32();
                var condition = breakpoint.TryGetProperty("condition", out var c) ? c.GetString() : null;
                var response = await Cdp().SendAsync("Debugger.setBreakpointByUrl", new
                {
                    lineNumber = line - 1,
                    urlRegex,
                    condition = string.IsNullOrWhiteSpace(condition) ? null : condition
                }).ConfigureAwait(false);

                var id = response.GetProperty("breakpointId").GetString()!;
                ids.Add(id);
                _requestedLines[id] = line;

                // A script that isn't parsed yet has no location to give: the breakpoint binds, and is announced, when it is.
                var bound = response.TryGetProperty("locations", out var locations) && locations.ValueKind == JsonValueKind.Array && locations.GetArrayLength() > 0;
                results.Add(new { id = ids.Count, verified = bound, line });
            }
        }

        _breakpointsByFile[url] = ids;
        await RespondAsync(seq, "setBreakpoints", new { breakpoints = results }).ConfigureAwait(false);
    }

    private async Task ConfigurationDoneAsync(int seq)
    {
        await RespondAsync(seq, "configurationDone").ConfigureAwait(false);

        // The stop on the first line was Node waiting for the debugger: the program starts now, and stops only where the user asked.
        bool holding;
        Frame? top;
        int[] breakpointLines;
        lock (_state)
        {
            holding = _holdingInitialPause;
            _holdingInitialPause = false;
            top = _frames.FirstOrDefault();
            breakpointLines = _requestedLines.Values.ToArray();
        }

        if (!holding) return;

        // A breakpoint on that very first line was set after Node stopped there, so V8 won't report it: it is a stop like any other.
        if (top != null && SameScript(top.Url, _scriptUrl) && breakpointLines.Contains(top.Line + 1))
        {
            await EventAsync("stopped", new { reason = "breakpoint", threadId = ThreadId, allThreadsStopped = true }).ConfigureAwait(false);
            return;
        }

        await Cdp().SendAsync("Debugger.resume").ConfigureAwait(false);
    }

    // The answer goes first and the program moves after it: the next stop may come at once, and the client must not see it before it has
    // the answer to the request that ended the last one.
    private async Task ResumeAsync(int seq, string command, string method, string? stopReason)
    {
        lock (_state) _nextStopReason = stopReason;
        await RespondAsync(seq, command, command == "continue" ? new { allThreadsContinued = true } : null).ConfigureAwait(false);
        await Cdp().SendAsync(method).ConfigureAwait(false);
    }

    private async Task StackTraceAsync(int seq, JsonElement arguments)
    {
        List<Frame> frames;
        lock (_state) frames = _frames;

        var start = arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty("startFrame", out var s) ? s.GetInt32() : 0;
        var levels = arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty("levels", out var l) && l.GetInt32() > 0 ? l.GetInt32() : int.MaxValue;

        var stack = frames.Select((frame, index) => new { frame, index }).Skip(start).Take(levels).Select(x => new
        {
            id = x.index,
            name = x.frame.FunctionName.Length > 0 ? x.frame.FunctionName : "(anonymous)",
            source = SourceOf(x.frame.Url),
            line = x.frame.Line + 1,
            column = x.frame.Column + 1
        }).ToList();

        await RespondAsync(seq, "stackTrace", new { stackFrames = stack, totalFrames = frames.Count }).ConfigureAwait(false);
    }

    private async Task ScopesAsync(int seq, JsonElement arguments)
    {
        var frameId = arguments.GetProperty("frameId").GetInt32();
        Frame frame;
        bool isModuleTop;
        lock (_state)
        {
            frame = _frames[frameId];
            isModuleTop = frame.FunctionName.Length == 0 && SameScript(frame.Url, _scriptUrl) && !_frames.Skip(frameId + 1).Any(f => SameScript(f.Url, _scriptUrl));
            _references.Clear();
        }

        var scopes = new List<object>();
        foreach (var scope in frame.Scopes)
        {
            // The global object has hundreds of members that aren't the program's variables; it is still there to evaluate in.
            var name = scope.Type switch
            {
                "local" => "Local",
                "block" => "Block",
                "closure" => "Closure",
                "catch" => "Catch",
                "with" => "With",
                "module" => "Module",
                "script" => "Script",
                _ => null
            };
            if (name == null) continue;

            int reference;
            lock (_state)
            {
                reference = _nextReference++;
                _references[reference] = (scope.ObjectId, HideModuleWrapper: isModuleTop && scope.Type == "local");
            }

            scopes.Add(new { name, variablesReference = reference, expensive = false });
        }

        await RespondAsync(seq, "scopes", new { scopes }).ConfigureAwait(false);
    }

    private async Task VariablesAsync(int seq, JsonElement arguments)
    {
        var reference = arguments.GetProperty("variablesReference").GetInt32();
        (string ObjectId, bool HideModuleWrapper) target;
        lock (_state)
        {
            if (!_references.TryGetValue(reference, out target)) throw new KeyNotFoundException("That variable is no longer available: the program has moved on.");
        }

        var response = await Cdp().SendAsync("Runtime.getProperties", new { objectId = target.ObjectId, ownProperties = true, generatePreview = true }).ConfigureAwait(false);
        var variables = new List<object>();
        if (response.TryGetProperty("result", out var properties))
        {
            foreach (var property in properties.EnumerateArray())
            {
                var name = property.GetProperty("name").GetString() ?? string.Empty;
                if (name == "__proto__" || (target.HideModuleWrapper && ModuleWrapperNames.Contains(name))) continue;
                if (variables.Count >= MaxVariables) break;

                if (!property.TryGetProperty("value", out var value))
                {
                    // A getter or setter: reading it could run code, so it is named and left alone. A let or const the program hasn't
                    // reached yet has neither.
                    var accessor = property.TryGetProperty("get", out _) || property.TryGetProperty("set", out _);
                    variables.Add(new { name, value = accessor ? "(accessor)" : "<not initialized>", type = accessor ? "accessor" : "undefined", variablesReference = 0 });
                    continue;
                }

                variables.Add(new { name, value = Describe(value), type = TypeOf(value), variablesReference = ReferenceFor(value) });
            }
        }

        await RespondAsync(seq, "variables", new { variables }).ConfigureAwait(false);
    }

    private async Task EvaluateAsync(int seq, JsonElement arguments)
    {
        var expression = arguments.GetProperty("expression").GetString() ?? string.Empty;
        Frame? frame = null;
        if (arguments.TryGetProperty("frameId", out var f) && f.ValueKind == JsonValueKind.Number)
        {
            lock (_state)
            {
                var id = f.GetInt32();
                if (id >= 0 && id < _frames.Count) frame = _frames[id];
            }
        }

        JsonElement response = frame != null
            ? await Cdp().SendAsync("Debugger.evaluateOnCallFrame", new { callFrameId = frame.CallFrameId, expression, generatePreview = true, silent = true }).ConfigureAwait(false)
            : await Cdp().SendAsync("Runtime.evaluate", new { expression, generatePreview = true, silent = true }).ConfigureAwait(false);

        if (response.TryGetProperty("exceptionDetails", out var failure))
        {
            var text = failure.TryGetProperty("exception", out var exception) && exception.TryGetProperty("description", out var description)
                ? description.GetString()
                : failure.TryGetProperty("text", out var t) ? t.GetString() : "The expression threw an error.";
            await RefuseAsync(seq, "evaluate", FirstLine(text ?? "The expression threw an error.")).ConfigureAwait(false);
            return;
        }

        var result = response.GetProperty("result");
        await RespondAsync(seq, "evaluate", new { result = Describe(result), type = TypeOf(result), variablesReference = ReferenceFor(result) }).ConfigureAwait(false);
    }

    // ── What Node says ────────────────────────────────────────────────────

    private async Task OnCdpEventAsync(string method, JsonElement parameters)
    {
        switch (method)
        {
            case "Debugger.scriptParsed":
                lock (_state)
                {
                    if (parameters.TryGetProperty("scriptId", out var id) && parameters.TryGetProperty("url", out var url)) _scriptUrls[id.GetString() ?? string.Empty] = url.GetString() ?? string.Empty;
                }

                break;
            case "Debugger.paused":
                await OnPausedAsync(parameters).ConfigureAwait(false);
                break;
            case "Runtime.executionContextCreated":
                if (parameters.TryGetProperty("context", out var context) && context.TryGetProperty("id", out var contextId) &&
                    context.TryGetProperty("auxData", out var aux) && aux.TryGetProperty("isDefault", out var isDefault) && isDefault.ValueKind == JsonValueKind.True)
                {
                    lock (_state) _mainContextId ??= contextId.GetInt32();
                }

                break;
            case "Runtime.executionContextDestroyed":
                // The program has finished. Node keeps its inspector open ("Waiting for the debugger to disconnect...") and won't exit
                // until this side lets go, so the connection is closed here: the process then ends and the session with it.
                bool mainEnded;
                lock (_state) mainEnded = parameters.TryGetProperty("executionContextId", out var destroyed) && destroyed.TryGetInt32(out var destroyedId) && destroyedId == _mainContextId;
                if (mainEnded && _cdp is { } connection) _ = Task.Run(async () => { try { await connection.DisposeAsync().ConfigureAwait(false); } catch (Exception) { } });
                break;
            case "Debugger.resumed":
                lock (_state)
                {
                    _frames = [];
                    _references.Clear();
                }

                await EventAsync("continued", new { threadId = ThreadId, allThreadsContinued = true }).ConfigureAwait(false);
                break;
            case "Debugger.breakpointResolved":
                if (parameters.TryGetProperty("breakpointId", out var breakpointId) && _requestedLines.TryGetValue(breakpointId.GetString() ?? string.Empty, out var line))
                {
                    await EventAsync("breakpoint", new { reason = "changed", breakpoint = new { verified = true, line } }).ConfigureAwait(false);
                }

                break;
        }
    }

    private async Task OnPausedAsync(JsonElement parameters)
    {
        var reason = parameters.TryGetProperty("reason", out var r) ? r.GetString() ?? string.Empty : string.Empty;
        var hitBreakpoint = parameters.TryGetProperty("hitBreakpoints", out var hits) && hits.ValueKind == JsonValueKind.Array && hits.GetArrayLength() > 0;

        var frames = new List<Frame>();
        if (parameters.TryGetProperty("callFrames", out var callFrames))
        {
            foreach (var frame in callFrames.EnumerateArray())
            {
                var location = frame.GetProperty("location");
                var scriptId = location.GetProperty("scriptId").GetString() ?? string.Empty;
                string url;
                lock (_state) url = frame.TryGetProperty("url", out var u) && u.GetString() is { Length: > 0 } direct ? direct : _scriptUrls.GetValueOrDefault(scriptId, string.Empty);

                var scopes = new List<ScopeInfo>();
                if (frame.TryGetProperty("scopeChain", out var chain))
                {
                    foreach (var scope in chain.EnumerateArray())
                    {
                        var objectId = scope.GetProperty("object").TryGetProperty("objectId", out var o) ? o.GetString() : null;
                        if (objectId != null) scopes.Add(new ScopeInfo(scope.GetProperty("type").GetString() ?? string.Empty, objectId));
                    }
                }

                frames.Add(new Frame(
                    frame.GetProperty("callFrameId").GetString() ?? string.Empty,
                    frame.TryGetProperty("functionName", out var fn) ? fn.GetString() ?? string.Empty : string.Empty,
                    scriptId,
                    url,
                    location.GetProperty("lineNumber").GetInt32(),
                    location.TryGetProperty("columnNumber", out var col) ? col.GetInt32() : 0,
                    scopes));
            }
        }

        string stopReason;
        lock (_state)
        {
            _frames = frames;
            _references.Clear();

            // Node's stop on the first line is waiting for the debugger: kept until configurationDone, and not the user's business.
            if (reason.StartsWith("Break on start", StringComparison.Ordinal) && _nextStopReason == null)
            {
                _holdingInitialPause = true;
                _initialPause.TrySetResult();
                return;
            }

            stopReason = reason switch
            {
                "exception" or "promiseRejection" => "exception",
                _ when hitBreakpoint => "breakpoint",
                _ => _nextStopReason ?? "pause"
            };
            _nextStopReason = null;
        }

        await EventAsync("stopped", new { reason = stopReason, threadId = ThreadId, allThreadsStopped = true }).ConfigureAwait(false);
    }

    private async Task MonitorProcessExitAsync()
    {
        int exitCode;
        try
        {
            exitCode = await _process.Completion.ConfigureAwait(false);
        }
        catch (Exception)
        {
            exitCode = -1;
        }

        if (_isDisposed == 1) return;
        await EventAsync("exited", new { exitCode }).ConfigureAwait(false);
        await EventAsync("terminated").ConfigureAwait(false);

        // The client sees the connection end, as it does when any adapter goes away.
        try { await _serverToClient.Writer.CompleteAsync().ConfigureAwait(false); } catch (InvalidOperationException) { }
    }

    // ── Values ────────────────────────────────────────────────────────────

    private string UrlOf(string path) =>
        string.IsNullOrEmpty(path) ? _scriptUrl : new Uri(Path.GetFullPath(path)).AbsoluteUri;

    // Node reports a script by its real path, so a file under /var on macOS is /private/var to it: the breakpoint is matched by the
    // path, with or without that prefix, rather than by the exact address the editor knows.
    // On Windows, Node V8 normalizes script URLs with lowercase drive letters (file:///c:/...) while .NET Uri produces uppercase (/C:/...).
    // Matching by trailing separator and file name ensures breakpoints bind reliably across platforms and path variations.
    private string UrlRegexOf(string path)
    {
        var fullPath = string.IsNullOrEmpty(path) ? new Uri(_scriptUrl).LocalPath : Path.GetFullPath(path);
        var fileName = Path.GetFileName(fullPath);
        return ".*[\\/]" + Regex.Escape(fileName) + "$";
    }

    private static bool SameScript(string a, string b)
    {
        var normA = Normalize(a);
        var normB = Normalize(b);
        if (string.Equals(normA, normB, StringComparison.OrdinalIgnoreCase)) return true;
        var fileA = Path.GetFileName(normA);
        var fileB = Path.GetFileName(normB);
        return !string.IsNullOrEmpty(fileA) && string.Equals(fileA, fileB, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string url)
    {
        var path = url.StartsWith("file://", StringComparison.Ordinal) ? url["file://".Length..] : url;
        path = path.StartsWith("/private/", StringComparison.Ordinal) ? path["/private".Length..] : path;
        return path.TrimStart('/');
    }

    private static object SourceOf(string url)
    {
        if (url.StartsWith("file://", StringComparison.Ordinal))
        {
            var path = new Uri(url).LocalPath;
            return new { name = Path.GetFileName(path), path };
        }

        // node:internal/... and eval code have no file to open.
        return new { name = url.Length > 0 ? url : "(unknown)" };
    }

    private static string TypeOf(JsonElement value) =>
        value.TryGetProperty("className", out var c) && c.GetString() is { Length: > 0 } className ? className
        : value.TryGetProperty("subtype", out var s) && s.GetString() is { Length: > 0 } subtype ? subtype
        : value.TryGetProperty("type", out var t) ? t.GetString() ?? "object" : "object";

    // An object or function can be opened; a number or string can't.
    private int ReferenceFor(JsonElement value)
    {
        if (!value.TryGetProperty("objectId", out var id) || id.GetString() is not { Length: > 0 } objectId) return 0;
        lock (_state)
        {
            var reference = _nextReference++;
            _references[reference] = (objectId, HideModuleWrapper: false);
            return reference;
        }
    }

    /// <summary>A value as a person reads it in a debugger: <c>41</c>, <c>"text"</c>, <c>Array(3) [1, 2, 3]</c>, <c>Point {x: 1, y: 2}</c>.</summary>
    internal static string Describe(JsonElement value)
    {
        var type = value.TryGetProperty("type", out var t) ? t.GetString() : null;
        var subtype = value.TryGetProperty("subtype", out var s) ? s.GetString() : null;
        var description = value.TryGetProperty("description", out var d) ? d.GetString() : null;

        switch (type)
        {
            case "undefined":
                return "undefined";
            case "string":
                return JsonSerializer.Serialize(value.TryGetProperty("value", out var text) ? text.GetString() : string.Empty, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            case "boolean":
                return value.TryGetProperty("value", out var flag) && flag.GetBoolean() ? "true" : "false";
            case "number" or "bigint" or "symbol":
                return description ?? (value.TryGetProperty("unserializableValue", out var u) ? u.GetString() ?? string.Empty : value.TryGetProperty("value", out var n) ? n.GetRawText() : string.Empty);
            case "function":
                return FirstLine(description ?? "function");
        }

        if (subtype == "null") return "null";

        // An object: its class and, when the inspector sent one, a look at what it holds.
        if (value.TryGetProperty("preview", out var preview) && preview.TryGetProperty("properties", out var properties))
        {
            var parts = new List<string>();
            var total = 0;
            foreach (var property in properties.EnumerateArray())
            {
                total++;
                if (parts.Count >= 5) continue;
                var shown = PreviewValue(property);
                parts.Add(subtype == "array" ? shown : $"{property.GetProperty("name").GetString()}: {shown}");
            }

            var more = preview.TryGetProperty("overflow", out var overflow) && overflow.GetBoolean() || total > parts.Count ? ", …" : string.Empty;
            var body = string.Join(", ", parts) + more;
            return subtype == "array" ? $"{description} [{body}]" : $"{description} {{{body}}}";
        }

        return description ?? "Object";
    }

    private static string PreviewValue(JsonElement property)
    {
        var type = property.TryGetProperty("type", out var t) ? t.GetString() : null;
        var text = property.TryGetProperty("value", out var v) ? v.GetString() ?? string.Empty : string.Empty;
        return type switch
        {
            "string" => JsonSerializer.Serialize(text, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }),
            "object" when property.TryGetProperty("subtype", out var s) && s.GetString() == "null" => "null",
            "object" => text.Length > 0 ? text : "Object",
            "function" => "ƒ",
            _ => text
        };
    }

    private static string FirstLine(string text)
    {
        var line = text.Split('\n')[0].Trim();
        return line.Length > 80 ? line[..80] + "…" : line;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0) return;
        _cts.Cancel();
        try { _process.Kill(); } catch (Exception) { }
        if (_cdp != null) await _cdp.DisposeAsync().ConfigureAwait(false);
        try { await _serverToClient.Writer.CompleteAsync().ConfigureAwait(false); } catch (InvalidOperationException) { }
        try { await _clientToServer.Reader.CompleteAsync().ConfigureAwait(false); } catch (InvalidOperationException) { }
        _writeLock.Dispose();
    }
}
