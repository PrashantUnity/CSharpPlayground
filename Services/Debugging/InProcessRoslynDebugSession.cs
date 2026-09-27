using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;

/// <summary>
/// Fast in-process C# debug session using Roslyn syntax instrumentation.
/// Starts instantaneously (&lt;1ms) with zero external dependencies.
/// </summary>
public sealed class InProcessRoslynDebugSession : IDebugSession
{
    private readonly ScriptDebugSession _session;
    private readonly ScriptDebuggerService _debuggerService;
    private readonly ScriptExecutionEngine _executionEngine;
    private readonly CancellationTokenSource _cts;
    private readonly string _fileName;
    private Task? _executionTask;
    private int _isDisposed;

    public string LanguageId => LanguageIds.CSharp;
    public DebugSessionState State => (DebugSessionState)(int)_session.State;
    public int PausedLine => _session.PausedLine;
    public string? PausedFilePath => _fileName;

    public event Action<DebugPausedEventArgs>? Paused;
    public event Action? Resumed;
    public event Action<string>? OutputReceived;
    public event Action<DebugTerminatedEventArgs>? Terminated;

    public InProcessRoslynDebugSession(
        ScriptDebugSession session,
        ScriptDebuggerService debuggerService,
        ScriptExecutionEngine executionEngine,
        CancellationTokenSource cts,
        string fileName)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _debuggerService = debuggerService ?? throw new ArgumentNullException(nameof(debuggerService));
        _executionEngine = executionEngine ?? throw new ArgumentNullException(nameof(executionEngine));
        _cts = cts ?? throw new ArgumentNullException(nameof(cts));
        _fileName = string.IsNullOrWhiteSpace(fileName) ? "script.cs" : fileName;

        _session.Paused += HandleSessionPaused;
        _session.Resumed += HandleSessionResumed;
        _session.Stopped += HandleSessionStopped;
    }

    public void StartExecution(byte[] assemblyBytes)
    {
        _executionTask = Task.Run(async () =>
        {
            try
            {
                var result = await _executionEngine.ExecuteAsync(
                    assemblyBytes,
                    output => OutputReceived?.Invoke(output),
                    _cts.Token).ConfigureAwait(false);

                Terminated?.Invoke(new DebugTerminatedEventArgs(
                    result.Success ? 0 : 1,
                    result.Error,
                    result.WasCancelled));
            }
            catch (Exception ex)
            {
                Terminated?.Invoke(new DebugTerminatedEventArgs(1, ex.Message, false));
            }
            finally
            {
                ScriptDebugSession.EndSession();
            }
        });
    }

    private void HandleSessionPaused(int line, IReadOnlyList<DebugVariableItem> locals)
    {
        var frames = _session.CallStack;
        foreach (var frame in frames)
        {
            if (string.IsNullOrEmpty(frame.FileName)) frame.FileName = _fileName;
        }

        Paused?.Invoke(new DebugPausedEventArgs(
            line,
            _fileName,
            "breakpoint",
            null,
            frames,
            locals));
    }

    private void HandleSessionResumed() => Resumed?.Invoke();

    private void HandleSessionStopped()
    {
        Terminated?.Invoke(new DebugTerminatedEventArgs(0, "Stopped by user", WasCancelled: true));
    }

    public Task SetBreakpointsAsync(string filePath, IReadOnlyList<BreakpointItem> breakpoints, CancellationToken ct = default)
    {
        _session.Breakpoints.Clear();
        _session.Breakpoints.AddRange(breakpoints);
        return Task.CompletedTask;
    }

    public Task ContinueAsync(CancellationToken ct = default)
    {
        _session.Continue();
        return Task.CompletedTask;
    }

    public Task StepOverAsync(CancellationToken ct = default)
    {
        _session.StepOver();
        return Task.CompletedTask;
    }

    public Task StepIntoAsync(CancellationToken ct = default)
    {
        _session.StepInto();
        return Task.CompletedTask;
    }

    public Task StepOutAsync(CancellationToken ct = default)
    {
        _session.Continue();
        return Task.CompletedTask;
    }

    public Task PauseAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        _cts.Cancel();
        _session.Stop();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CallStackFrameItem>> GetCallStackAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_session.CallStack);
    }

    public Task<IReadOnlyList<DebugVariableItem>> GetVariablesAsync(int frameIndex, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<DebugVariableItem>>(_session.CapturedLocals);
    }

    public Task<IReadOnlyList<DebugVariableItem>> GetVariableChildrenAsync(DebugVariableItem parent, CancellationToken ct = default)
    {
        if (parent == null) return Task.FromResult<IReadOnlyList<DebugVariableItem>>(Array.Empty<DebugVariableItem>());

        if (!parent.ChildrenLoaded && parent.RawValue != null)
        {
            var children = ScriptDebugSession.ExpandVariableChildren(parent);
            return Task.FromResult(children);
        }

        return Task.FromResult<IReadOnlyList<DebugVariableItem>>(parent.Children);
    }

    public async Task<EvaluationResult> EvaluateAsync(string expression, int? frameIndex, EvaluationContext context, CancellationToken ct = default)
    {
        var (success, result, typeName) = await _debuggerService.EvaluateExpressionAsync(
            expression,
            _session.CapturedLocals,
            ct).ConfigureAwait(false);

        return new EvaluationResult(success, result, typeName, success ? null : result);
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0) return ValueTask.CompletedTask;
        StopAsync();
        return ValueTask.CompletedTask;
    }
}
