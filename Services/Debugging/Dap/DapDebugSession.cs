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

    public event Action<DebugPausedEventArgs>? Paused;
    public event Action? Resumed;
    public event Action<string>? OutputReceived;
    public event Action<DebugTerminatedEventArgs>? Terminated;

    public DapDebugSession(string languageId, DapClient client, IManagedProcess? process = null)
    {
        LanguageId = languageId;
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _process = process;

        _client.EventReceived += HandleDapEventAsync;
        _client.Disconnected += HandleDapDisconnected;
    }

    public async Task SetBreakpointsAsync(string filePath, IReadOnlyList<BreakpointItem> breakpoints, CancellationToken ct = default)
    {
        var enabledBreakpoints = breakpoints
            .Where(b => b.IsEnabled)
            .Select(b => new DapSourceBreakpoint
            {
                Line = b.LineNumber,
                Condition = string.IsNullOrWhiteSpace(b.Condition) ? null : b.Condition
            })
            .ToList();

        var args = new
        {
            source = new DapSource
            {
                Path = filePath,
                Name = Path.GetFileName(filePath)
            },
            breakpoints = enabledBreakpoints,
            lines = enabledBreakpoints.Select(b => b.Line).ToList()
        };

        await _client.SendRequestAsync("setBreakpoints", args, ct).ConfigureAwait(false);
    }

    public async Task ContinueAsync(CancellationToken ct = default)
    {
        int threadId = _lastThreadId ?? 1;
        await _client.SendRequestAsync("continue", new { threadId }, ct).ConfigureAwait(false);
        SetRunningState();
    }

    public async Task StepOverAsync(CancellationToken ct = default)
    {
        int threadId = _lastThreadId ?? 1;
        await _client.SendRequestAsync("next", new { threadId }, ct).ConfigureAwait(false);
        SetRunningState();
    }

    public async Task StepIntoAsync(CancellationToken ct = default)
    {
        int threadId = _lastThreadId ?? 1;
        await _client.SendRequestAsync("stepIn", new { threadId }, ct).ConfigureAwait(false);
        SetRunningState();
    }

    public async Task StepOutAsync(CancellationToken ct = default)
    {
        int threadId = _lastThreadId ?? 1;
        await _client.SendRequestAsync("stepOut", new { threadId }, ct).ConfigureAwait(false);
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
                if (scope == null || scope.VariablesReference <= 0) continue;

                var varsResp = await _client.SendRequestAsync("variables", new { variablesReference = scope.VariablesReference }, ct).ConfigureAwait(false);
                if (varsResp.Body == null) continue;

                if (varsResp.Body.Value.TryGetProperty("variables", out var varsArray) && varsArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var varElem in varsArray.EnumerateArray())
                    {
                        var dapVar = varElem.Deserialize<DapVariable>(JsonOptions);
                        if (dapVar == null || string.IsNullOrWhiteSpace(dapVar.Name)) continue;
                        if (!seenNames.Add(dapVar.Name)) continue;

                        var item = new DebugVariableItem
                        {
                            Name = dapVar.Name,
                            TypeName = dapVar.Type ?? "object",
                            ValueDisplay = dapVar.Value,
                            Kind = scope.Name
                        };

                        if (dapVar.VariablesReference > 0)
                        {
                            _variableReferences[item] = dapVar.VariablesReference;
                        }

                        locals.Add(item);
                    }
                }
            }
        }

        return locals;
    }

    public async Task<IReadOnlyList<DebugVariableItem>> GetVariableChildrenAsync(DebugVariableItem parent, CancellationToken ct = default)
    {
        if (!_variableReferences.TryGetValue(parent, out int varRef) || varRef <= 0)
        {
            return Array.Empty<DebugVariableItem>();
        }

        var varsResp = await _client.SendRequestAsync("variables", new { variablesReference = varRef }, ct).ConfigureAwait(false);
        if (varsResp.Body == null) return Array.Empty<DebugVariableItem>();

        var children = new List<DebugVariableItem>();
        if (varsResp.Body.Value.TryGetProperty("variables", out var varsArray) && varsArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var varElem in varsArray.EnumerateArray())
            {
                var dapVar = varElem.Deserialize<DapVariable>(JsonOptions);
                if (dapVar == null) continue;

                var item = new DebugVariableItem
                {
                    Name = dapVar.Name,
                    TypeName = dapVar.Type ?? "object",
                    ValueDisplay = dapVar.Value,
                    Kind = "Member"
                };

                if (dapVar.VariablesReference > 0)
                {
                    _variableReferences[item] = dapVar.VariablesReference;
                }

                children.Add(item);
            }
        }

        return children;
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
            case "terminated":
            case "exited":
                SetTerminatedState(0, "Process terminated.", wasCancelled: false);
                break;
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
            }
        }

        lock (_stateLock)
        {
            State = DebugSessionState.Paused;
        }

        var callStack = await GetCallStackAsync().ConfigureAwait(false);
        var topFrame = callStack.FirstOrDefault();
        if (topFrame != null)
        {
            PausedLine = topFrame.LineNumber;
            PausedFilePath = topFrame.FileName;
        }

        var locals = await GetVariablesAsync(topFrame?.FrameIndex ?? 0).ConfigureAwait(false);

        Paused?.Invoke(new DebugPausedEventArgs(
            PausedLine,
            PausedFilePath,
            reason,
            threadId,
            callStack,
            locals));
    }

    private void HandleOutputEvent(JsonElement? body)
    {
        if (body.HasValue)
        {
            var output = body.Value.Deserialize<DapOutputEventBody>(JsonOptions);
            if (output != null && !string.IsNullOrEmpty(output.Output))
            {
                OutputReceived?.Invoke(output.Output);
            }
        }
    }

    private void SetRunningState()
    {
        lock (_stateLock)
        {
            State = DebugSessionState.Running;
            PausedLine = -1;
            PausedFilePath = null;
        }
        Resumed?.Invoke();
    }

    private void SetTerminatedState(int? exitCode, string? message, bool wasCancelled)
    {
        lock (_stateLock)
        {
            if (State == DebugSessionState.Terminated) return;
            State = DebugSessionState.Terminated;
            PausedLine = -1;
            PausedFilePath = null;
        }
        Terminated?.Invoke(new DebugTerminatedEventArgs(exitCode, message, wasCancelled));
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
