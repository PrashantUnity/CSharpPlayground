using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Debugging.Dap;

/// <summary>
/// Adapts any Debug Adapter Protocol (DAP) server (netcoredbg, debugpy, lldb-dap, etc.)
/// into the IDE's unified IDebugSession contract.
/// </summary>
public sealed class DapDebugSession : IDebugSession
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly DapClient _client;
    private readonly IManagedProcess? _process;
    private readonly ConcurrentDictionary<DebugVariableItem, int> _variableReferences = new();
    private readonly object _stateLock = new();

    private int? _lastThreadId;
    private int? _topFrameId;
    private int _isDisposed;

    public string LanguageId { get; }
    public DebugSessionState State { get; private set; } = DebugSessionState.Running;
    public int PausedLine { get; private set; } = -1;
    public string? PausedFilePath { get; private set; }

    // The session starts listening before the program does, so it can pause, print or finish before whoever started it has
    // subscribed. A new subscriber is told what it missed: where the program is paused, that it ended, and what it printed.
    private Action<DebugPausedEventArgs>? _paused;
    private Action<string>? _outputReceived;
    private Action<DebugTerminatedEventArgs>? _terminated;
    private DebugPausedEventArgs? _lastPaused;
    private DebugTerminatedEventArgs? _terminatedArgs;
    private readonly System.Text.StringBuilder _earlyOutput = new();
    private const int MaxEarlyOutput = 64 * 1024;

    public event Action<DebugPausedEventArgs>? Paused
    {
        add
        {
            DebugPausedEventArgs? missed;
            lock (_stateLock)
            {
                _paused += value;
                missed = State == DebugSessionState.Paused ? _lastPaused : null;
            }

            if (missed != null) value?.Invoke(missed);
        }
        remove
        {
            lock (_stateLock) _paused -= value;
        }
    }

    public event Action? Resumed;

    public event Action<string>? OutputReceived
    {
        add
        {
            string? missed;
            lock (_stateLock)
            {
                var first = _outputReceived == null;
                _outputReceived += value;
                missed = first && _earlyOutput.Length > 0 ? _earlyOutput.ToString() : null;
                if (first) _earlyOutput.Clear();
            }

            if (missed != null) value?.Invoke(missed);
        }
        remove
        {
            lock (_stateLock) _outputReceived -= value;
        }
    }

    public event Action<DebugTerminatedEventArgs>? Terminated
    {
        add
        {
            DebugTerminatedEventArgs? missed;
            lock (_stateLock)
            {
                _terminated += value;
                missed = _terminatedArgs;
            }

            if (missed != null) value?.Invoke(missed);
        }
        remove
        {
            lock (_stateLock) _terminated -= value;
        }
    }

    public event Action<int, bool>? BreakpointVerifiedChanged;

    public DapDebugSession(string languageId, DapClient client, IManagedProcess? process = null)
    {
        LanguageId = languageId;
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _process = process;

        _client.EventReceived += HandleDapEventAsync;
        _client.Disconnected += HandleDapDisconnected;
    }

    /// <summary>
    /// The name the adapter knows the program's one source file by, when the editor calls it something else (a script's debug build
    /// names its source <c>script.cs</c> whatever the document is titled). Breakpoints set while the session runs are sent under it.
    /// </summary>
    public string? SourcePathOverride { get; set; }

    /// <summary>
    /// Says how the program really ended, when the adapter's <c>exited</c> event can't be trusted (netcoredbg reports 0 for every program).
    /// Returns null when it doesn't know, and then the adapter's own code is used.
    /// </summary>
    public Func<int?>? ExitCodeOverride { get; set; }

    public async Task SetBreakpointsAsync(string filePath, IReadOnlyList<BreakpointItem> breakpoints, CancellationToken ct = default)
    {
        var response = await _client.SendRequestAsync("setBreakpoints", BreakpointsRequest(SourcePathOverride ?? filePath, breakpoints), ct).ConfigureAwait(false);
        ApplyBreakpointResults(breakpoints, response, (line, verified) => BreakpointVerifiedChanged?.Invoke(line, verified));
        if (!response.Success) EmitOutput($"⚠️ The debugger could not set the breakpoints: {response.Message}\n");
    }

    /// <summary>
    /// The setBreakpoints request for one file, the same when a session starts and while it runs. A field without a value is left out
    /// rather than sent as null: netcoredbg refuses a null condition, and then sets no breakpoint at all.
    /// </summary>
    internal static object BreakpointsRequest(string filePath, IReadOnlyList<BreakpointItem> breakpoints)
    {
        var enabled = breakpoints
            .Where(b => b.IsEnabled)
            .Select(b => new DapSourceBreakpoint
            {
                Line = b.LineNumber,
                Condition = string.IsNullOrWhiteSpace(b.Condition) ? null : b.Condition
            })
            .ToList();

        return new
        {
            source = new DapSource
            {
                Path = filePath,
                Name = Path.GetFileName(filePath)
            },
            breakpoints = enabled,
            lines = enabled.Select(b => b.Line).ToList()
        };
    }

    /// <summary>
    /// Marks each enabled breakpoint verified or not from the adapter's answer, which lists them in the order they were asked for;
    /// an adapter that refused the request has bound none. An answer that says nothing about them leaves them as they are.
    /// </summary>
    internal static void ApplyBreakpointResults(IReadOnlyList<BreakpointItem> breakpoints, DapResponse response, Action<int, bool>? verifiedChanged = null)
    {
        List<DapBreakpoint>? results = null;
        if (response.Success)
        {
            if (!response.Body.HasValue ||
                !response.Body.Value.TryGetProperty("breakpoints", out var array) ||
                array.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            results = array.Deserialize<List<DapBreakpoint>>(JsonOptions);
        }

        var enabled = breakpoints.Where(b => b.IsEnabled).ToList();
        for (int i = 0; i < enabled.Count; i++)
        {
            var verified = results != null && i < results.Count && results[i].Verified;
            enabled[i].IsVerified = verified;
            verifiedChanged?.Invoke(enabled[i].LineNumber, verified);
        }
    }

    public async Task ContinueAsync(CancellationToken ct = default)
    {
        int threadId = _lastThreadId ?? 1;
        await _client.SendRequestAsync("continue", new { threadId }, ct).ConfigureAwait(false);
        await _client.WaitForPendingEventsAsync().ConfigureAwait(false);
        SetRunningState();
    }

    public async Task StepOverAsync(CancellationToken ct = default)
    {
        int threadId = _lastThreadId ?? 1;
        await _client.SendRequestAsync("next", new { threadId }, ct).ConfigureAwait(false);
        await _client.WaitForPendingEventsAsync().ConfigureAwait(false);
        SetRunningState();
    }

    public async Task StepIntoAsync(CancellationToken ct = default)
    {
        int threadId = _lastThreadId ?? 1;
        await _client.SendRequestAsync("stepIn", new { threadId }, ct).ConfigureAwait(false);
        await _client.WaitForPendingEventsAsync().ConfigureAwait(false);
        SetRunningState();
    }

    public async Task StepOutAsync(CancellationToken ct = default)
    {
        int threadId = _lastThreadId ?? 1;
        await _client.SendRequestAsync("stepOut", new { threadId }, ct).ConfigureAwait(false);
        await _client.WaitForPendingEventsAsync().ConfigureAwait(false);
        SetRunningState();
    }

    public async Task PauseAsync(CancellationToken ct = default)
    {
        int threadId = _lastThreadId ?? 1;
        await _client.SendRequestAsync("pause", new { threadId }, ct).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            if (State == DebugSessionState.Terminated) return;
        }

        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            await _client.SendRequestAsync("disconnect", new { terminateDebuggee = true }, linked.Token).ConfigureAwait(false);
        }
        catch
        {
            // Ignore disconnect transport errors if process already exited
        }
        finally
        {
            try { _process?.Kill(); } catch { }
            SetTerminatedState(0, "Debug session stopped by user.", wasCancelled: true);
        }
    }

    public async Task<IReadOnlyList<CallStackFrameItem>> GetCallStackAsync(CancellationToken ct = default)
    {
        int threadId = _lastThreadId ?? 1;
        var response = await _client.SendRequestAsync("stackTrace", new { threadId, startFrame = 0, levels = 20 }, ct).ConfigureAwait(false);

        if (response.Body == null) return Array.Empty<CallStackFrameItem>();

        var frames = new List<CallStackFrameItem>();
        if (response.Body.Value.TryGetProperty("stackFrames", out var framesArray) && framesArray.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (var frameElem in framesArray.EnumerateArray())
            {
                var frame = frameElem.Deserialize<DapStackFrame>(JsonOptions);
                if (frame == null) continue;

                if (index == 0) _topFrameId = frame.Id;

                frames.Add(new CallStackFrameItem
                {
                    FrameIndex = index,
                    MethodName = string.IsNullOrWhiteSpace(frame.Name) ? "<unknown>" : frame.Name,
                    LineNumber = frame.Line,
                    ColumnNumber = frame.Column > 0 ? frame.Column : 1,
                    FileName = frame.Source?.Name ?? (string.IsNullOrEmpty(frame.Source?.Path) ? "script" : Path.GetFileName(frame.Source.Path)),
                    IsCurrentFrame = index == 0
                });
                index++;
            }
        }

        return frames;
    }

    public async Task<IReadOnlyList<DebugVariableItem>> GetVariablesAsync(int frameIndex, CancellationToken ct = default)
    {
        _variableReferences.Clear();
        int frameId = _topFrameId ?? frameIndex;

        var scopesResp = await _client.SendRequestAsync("scopes", new { frameId }, ct).ConfigureAwait(false);
        if (scopesResp.Body == null) return Array.Empty<DebugVariableItem>();

        var locals = new List<DebugVariableItem>();
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        if (scopesResp.Body.Value.TryGetProperty("scopes", out var scopesArray) && scopesArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var scopeElem in scopesArray.EnumerateArray())
            {
                var scope = scopeElem.Deserialize<DapScope>(JsonOptions);
                if (scope == null || scope.VariablesReference <= 0 || IsRegisterScope(scope)) continue;

                var varsResp = await _client.SendRequestAsync("variables", new { variablesReference = scope.VariablesReference }, ct).ConfigureAwait(false);
                if (varsResp.Body == null) continue;

                if (varsResp.Body.Value.TryGetProperty("variables", out var varsArray) && varsArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var varElem in varsArray.EnumerateArray())
                    {
                        var dapVar = varElem.Deserialize<DapVariable>(JsonOptions);
                        if (dapVar == null || string.IsNullOrWhiteSpace(dapVar.Name)) continue;
                        if (!seenNames.Add(dapVar.Name)) continue;

                        var item = CreateDebugVariable(dapVar, scope.Name, null);
                        locals.Add(item);
                    }
                }
            }
        }

        return locals;
    }

    // A native adapter (lldb-dap) offers the CPU's register groups as a scope beside the locals. They are for reading machine code;
    // listed with the program's variables they bury them.
    private static bool IsRegisterScope(DapScope scope) =>
        string.Equals(scope.PresentationHint, "registers", StringComparison.OrdinalIgnoreCase) ||
        scope.Name.Contains("Register", StringComparison.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<DebugVariableItem>> GetVariableChildrenAsync(DebugVariableItem parent, CancellationToken ct = default)
    {
        if (!_variableReferences.TryGetValue(parent, out int varRef) || varRef <= 0)
        {
            parent.HasChildren = false;
            parent.ChildrenLoaded = true;
            return Array.Empty<DebugVariableItem>();
        }

        parent.IsLoadingChildren = true;
        try
        {
            var varsResp = await _client.SendRequestAsync("variables", new { variablesReference = varRef }, ct).ConfigureAwait(false);
            if (varsResp.Body == null) return Array.Empty<DebugVariableItem>();

            var children = new List<DebugVariableItem>();
            if (varsResp.Body.Value.TryGetProperty("variables", out var varsArray) && varsArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var varElem in varsArray.EnumerateArray())
                {
                    var dapVar = varElem.Deserialize<DapVariable>(JsonOptions);
                    if (dapVar == null) continue;

                    var item = CreateDebugVariable(dapVar, "Member", parent.PathExpression);
                    children.Add(item);
                }
            }

            parent.Children.Clear();
            foreach (var child in children)
            {
                parent.Children.Add(child);
            }
            parent.HasChildren = parent.Children.Count > 0;
            parent.ChildrenLoaded = true;

            return children;
        }
        finally
        {
            parent.IsLoadingChildren = false;
        }
    }

    private DebugVariableItem CreateDebugVariable(DapVariable dapVar, string kind, string? parentPath = null)
    {
        string name = dapVar.Name;
        string typeName = dapVar.Type ?? "object";
        string valueDisplay = dapVar.Value;

        string path = parentPath != null
            ? (name.StartsWith('[') ? $"{parentPath}{name}" : $"{parentPath}.{name}")
            : name;

        string nodeKind = kind switch
        {
            "Locals" => "Local",
            "Arguments" => "Parameter",
            "Registers" => "Local",
            _ when name.StartsWith('[') => "CollectionItem",
            _ when name.StartsWith("special variables", StringComparison.OrdinalIgnoreCase) || name.StartsWith("function variables", StringComparison.OrdinalIgnoreCase) => "StaticMember",
            _ when name.Equals("this", StringComparison.OrdinalIgnoreCase) || name.Equals("self", StringComparison.OrdinalIgnoreCase) => "Local",
            _ => "Property"
        };

        bool isCollection = (dapVar.Type != null && (
            dapVar.Type.Contains("list", StringComparison.OrdinalIgnoreCase) ||
            dapVar.Type.Contains("dict", StringComparison.OrdinalIgnoreCase) ||
            dapVar.Type.Contains("array", StringComparison.OrdinalIgnoreCase) ||
            dapVar.Type.Contains("tuple", StringComparison.OrdinalIgnoreCase) ||
            dapVar.Type.Contains("DataFrame", StringComparison.OrdinalIgnoreCase) ||
            dapVar.Type.Contains("ndarray", StringComparison.OrdinalIgnoreCase) ||
            dapVar.Type.Contains("Set", StringComparison.OrdinalIgnoreCase) ||
            dapVar.Type.Contains("Map", StringComparison.OrdinalIgnoreCase) ||
            dapVar.Type.EndsWith("[]")))
            || (dapVar.Value != null && (
                dapVar.Value.StartsWith('[') ||
                dapVar.Value.StartsWith('{') ||
                dapVar.Value.Contains("Count =") ||
                dapVar.Value.Contains("len =") ||
                dapVar.Value.Contains("len:")));

        bool isText = (dapVar.Type != null && (
            dapVar.Type.Contains("str", StringComparison.OrdinalIgnoreCase) ||
            dapVar.Type.Contains("string", StringComparison.OrdinalIgnoreCase) ||
            dapVar.Type.Contains("text", StringComparison.OrdinalIgnoreCase)))
            || (dapVar.Value != null && (dapVar.Value.Length > 20 || dapVar.Value.Contains('\n')));

        var item = new DebugVariableItem
        {
            Name = name,
            TypeName = typeName,
            ValueDisplay = valueDisplay,
            Kind = kind,
            NodeKind = nodeKind,
            PathExpression = path,
            VariablesReference = dapVar.VariablesReference,
            HasChildren = dapVar.VariablesReference > 0,
            IsCollection = isCollection,
            IsTextOrStructured = isText
        };

        if (dapVar.VariablesReference > 0)
        {
            _variableReferences[item] = dapVar.VariablesReference;
        }

        return item;
    }

    public async Task<EvaluationResult> EvaluateAsync(string expression, int? frameIndex, EvaluationContext context, CancellationToken ct = default)
    {
        var contextStr = context switch
        {
            EvaluationContext.Hover => "hover",
            EvaluationContext.Watch => "watch",
            _ => "repl"
        };

        var args = new
        {
            expression,
            frameId = _topFrameId ?? frameIndex,
            context = contextStr
        };

        try
        {
            var body = await _client.SendRequestAsync<DapEvaluateResponseBody>("evaluate", args, ct).ConfigureAwait(false);
            if (body == null)
            {
                return new EvaluationResult(false, string.Empty, ErrorMessage: "Empty evaluation response.");
            }

            return new EvaluationResult(true, body.Result, body.Type, VariablesReference: body.VariablesReference);
        }
        catch (Exception ex)
        {
            return new EvaluationResult(false, string.Empty, ErrorMessage: ex.Message);
        }
    }

    private async Task HandleDapEventAsync(DapEvent evt)
    {
        switch (evt.Event.ToLowerInvariant())
        {
            case "stopped":
                await HandleStoppedEventAsync(evt.Body).ConfigureAwait(false);
                break;
            case "continued":
                SetRunningState();
                break;
            case "output":
                HandleOutputEvent(evt.Body);
                break;
            case "breakpoint":
                HandleBreakpointEvent(evt.Body);
                break;
            case "exited":
                SetTerminatedState(ExitCodeOverride?.Invoke() ?? ReadExitCode(evt.Body) ?? 0, "Process exited.", wasCancelled: false);
                break;
            case "terminated":
                SetTerminatedState(0, "Process terminated.", wasCancelled: false);
                break;
        }
    }

    // The "exited" event says how the program ended; "terminated" only says debugging is over, and usually follows it.
    private static int? ReadExitCode(JsonElement? body) =>
        body is { ValueKind: JsonValueKind.Object } element && element.TryGetProperty("exitCode", out var code) && code.TryGetInt32(out var value)
            ? value
            : null;

    private void HandleBreakpointEvent(JsonElement? body)
    {
        if (!body.HasValue) return;
        if (body.Value.TryGetProperty("breakpoint", out var bpElem))
        {
            var dapBp = bpElem.Deserialize<DapBreakpoint>(JsonOptions);
            if (dapBp != null && dapBp.Line.HasValue)
            {
                BreakpointVerifiedChanged?.Invoke(dapBp.Line.Value, dapBp.Verified);
            }
        }
    }

    private async Task HandleStoppedEventAsync(JsonElement? body)
    {
        string reason = "paused";
        int? threadId = null;

        if (body.HasValue)
        {
            var stopped = body.Value.Deserialize<DapStoppedEventBody>(JsonOptions);
            if (stopped != null)
            {
                reason = stopped.Reason;
                threadId = stopped.ThreadId;
                _lastThreadId = threadId;

                if (!string.IsNullOrEmpty(stopped.Text))
                {
                    EmitOutput($"\n⚠️ Paused on {stopped.Reason}: {stopped.Text}\n");
                }
                else if (!string.IsNullOrEmpty(stopped.Description))
                {
                    EmitOutput($"\n⚠️ Paused on {stopped.Reason}: {stopped.Description}\n");
                }
            }
        }

        lock (_stateLock)
        {
            State = DebugSessionState.Paused;
            _lastPaused = null;
        }

        var callStack = await GetCallStackAsync().ConfigureAwait(false);
        var topFrame = callStack.FirstOrDefault();
        var pausedLine = topFrame?.LineNumber ?? 0;
        var pausedFile = topFrame?.FileName;

        var locals = await GetVariablesAsync(topFrame?.FrameIndex ?? 0).ConfigureAwait(false);

        var args = new DebugPausedEventArgs(
            pausedLine,
            pausedFile,
            reason,
            threadId,
            callStack,
            locals);

        Action<DebugPausedEventArgs>? handlers;
        lock (_stateLock)
        {
            // The program may have been resumed or ended while the stack and variables were being read.
            if (State != DebugSessionState.Paused) return;
            PausedLine = pausedLine;
            PausedFilePath = pausedFile;
            _lastPaused = args;
            handlers = _paused;
        }

        handlers?.Invoke(args);
    }

    private void HandleOutputEvent(JsonElement? body)
    {
        if (body.HasValue)
        {
            var output = body.Value.Deserialize<DapOutputEventBody>(JsonOptions);
            if (output != null && !string.IsNullOrEmpty(output.Output)) EmitOutput(output.Output);
        }
    }

    private void EmitOutput(string text)
    {
        Action<string>? handlers;
        lock (_stateLock)
        {
            handlers = _outputReceived;
            if (handlers == null)
            {
                // Nobody is listening yet (a program that prints as it starts): keep it for the first listener.
                if (_earlyOutput.Length < MaxEarlyOutput) _earlyOutput.Append(text);
                return;
            }
        }

        handlers(text);
    }

    private void SetRunningState()
    {
        lock (_stateLock)
        {
            // "continue" is answered after the program may already have ended: an ended session doesn't run again.
            if (State == DebugSessionState.Terminated) return;
            State = DebugSessionState.Running;
            PausedLine = -1;
            PausedFilePath = null;
            _lastPaused = null;
        }
        Resumed?.Invoke();
    }

    private void SetTerminatedState(int? exitCode, string? message, bool wasCancelled)
    {
        DebugTerminatedEventArgs args;
        Action<DebugTerminatedEventArgs>? handlers;
        lock (_stateLock)
        {
            if (State == DebugSessionState.Terminated) return;
            State = DebugSessionState.Terminated;
            PausedLine = -1;
            PausedFilePath = null;
            _lastPaused = null;
            args = new DebugTerminatedEventArgs(exitCode, message, wasCancelled);
            _terminatedArgs = args;
            handlers = _terminated;
        }

        handlers?.Invoke(args);
    }

    private void HandleDapDisconnected()
    {
        SetTerminatedState(-1, "Debug adapter disconnected.", wasCancelled: false);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0) return;
        await StopAsync().ConfigureAwait(false);
        await _client.DisposeAsync().ConfigureAwait(false);
        _process?.Dispose();
    }
}
