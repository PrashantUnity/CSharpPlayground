using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;

using DebugSessionState = PdfEditorApp.Plugins.CSharpEditor.Services.Debugging.DebugSessionState;

/// <summary>
/// Headless interactive Java debugger session driving JDK's native <c>jdb</c> debugger over standard I/O.
/// </summary>
public sealed partial class JavaDebugSession : IDebugSession
{
    [GeneratedRegex(@"""thread=(?<thread>[^""]+)""[,\s]+(?<method>[^\(\)\r\n]+)\(\)[,\s]+line=(?<line>\d+)", RegexOptions.Compiled)]
    private static partial Regex StopRegex();

    [GeneratedRegex(@"\[(?<idx>\d+)\]\s+(?<method>[\w\.\$<>\s]+?)\s+\((?<file>[^:]+):(?<line>\d+)\)", RegexOptions.Compiled)]
    private static partial Regex FrameRegex();

    [GeneratedRegex(@"(?:^|[\r\n]|\]\s+)(?<name>[\w\$]+)\s*=\s*(?<val>[^\r\n]*)", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex VarRegex();

    [GeneratedRegex(@"(?:\w+\[\d+\]\s*$|>\s*$)", RegexOptions.Compiled)]
    private static partial Regex PromptRegex();

    private readonly IManagedProcess _process;
    private readonly string _sourceFilePath;
    private readonly string _fqn;
    private readonly object _lock = new();
    private readonly StringBuilder _outputBuffer = new();
    private readonly SemaphoreSlim _commandLock = new(1, 1);
    private TaskCompletionSource<string>? _activeCommandTcs;
    private int _activeCommandStartOffset;

    private int _pausedLine = -1;
    private string? _pausedFilePath;
    private int _lastResumeIndex;
    private bool _isCurrentlyPaused;

    public JavaDebugSession(IManagedProcess process, string sourceFilePath, string fqn)
    {
        _process = process;
        _sourceFilePath = sourceFilePath;
        _fqn = fqn;
        _pausedFilePath = sourceFilePath;
        State = DebugSessionState.Running;

        _ = MonitorProcessCompletionAsync();
    }

    public string LanguageId => LanguageIds.Java;
    public DebugSessionState State { get; private set; }
    public int PausedLine => _pausedLine;
    public string? PausedFilePath => _pausedFilePath;

    public event Action<DebugPausedEventArgs>? Paused;
    public event Action? Resumed;
    public event Action<string>? OutputReceived;
    public event Action<DebugTerminatedEventArgs>? Terminated;

    public void OnProcessOutput(string chunk)
    {
        OutputReceived?.Invoke(chunk);

        string fullText;
        int checkOffset;
        bool shouldCheckStop;
        TaskCompletionSource<string>? cmdTcs = null;
        string? cmdOutput = null;

        lock (_lock)
        {
            _outputBuffer.Append(chunk);
            fullText = _outputBuffer.ToString();
            checkOffset = _lastResumeIndex;
            shouldCheckStop = !_isCurrentlyPaused;

            if (_activeCommandTcs != null)
            {
                var slice = fullText.Length > _activeCommandStartOffset
                    ? fullText.Substring(_activeCommandStartOffset)
                    : string.Empty;
                var match = PromptRegex().Match(slice);
                if (match.Success)
                {
                    cmdOutput = slice.Substring(0, match.Index).Trim();
                    cmdTcs = _activeCommandTcs;
                    _activeCommandTcs = null;
                }
            }
        }

        cmdTcs?.TrySetResult(cmdOutput ?? string.Empty);

        // Check for process termination
        if (chunk.Contains("The application exited", StringComparison.OrdinalIgnoreCase) ||
            chunk.Contains("The application has finished", StringComparison.OrdinalIgnoreCase))
        {
            SetTerminated(0, "Java application execution finished.");
        }

        // Check for breakpoint hit or step completed
        if (shouldCheckStop)
        {
            var unhandledText = fullText.Length > checkOffset ? fullText.Substring(checkOffset) : chunk;
            var match = StopRegex().Match(unhandledText);
            if (match.Success && int.TryParse(match.Groups["line"].Value, out int line))
            {
                var afterStop = unhandledText.Substring(match.Index + match.Length);
                if (PromptRegex().IsMatch(afterStop))
                {
                    lock (_lock)
                    {
                        _isCurrentlyPaused = true;
                        _pausedLine = line;
                        _pausedFilePath = _sourceFilePath;
                        State = DebugSessionState.Paused;
                    }

                    var stopReason = unhandledText.Contains("Step completed", StringComparison.OrdinalIgnoreCase) ? "step" : "breakpoint";

                    _ = Task.Run(async () =>
                    {
                        var (callStack, locals) = await FetchCallStackAndLocalsAsync().ConfigureAwait(false);
                        Paused?.Invoke(new DebugPausedEventArgs(
                            _pausedLine,
                            _pausedFilePath,
                            stopReason,
                            1,
                            callStack,
                            locals));
                    });
                }
            }
        }
    }

    private async Task<(IReadOnlyList<CallStackFrameItem> CallStack, IReadOnlyList<DebugVariableItem> Locals)> FetchCallStackAndLocalsAsync()
    {
        try
        {
            var stackText = await SendCommandWaitPromptAsync("where").ConfigureAwait(false);
            var localsText = await SendCommandWaitPromptAsync("locals").ConfigureAwait(false);

            var callStack = ParseCallStack(stackText);
            var locals = ParseLocals(localsText);

            _lastCallStack = callStack;
            _lastLocals = locals;

            return (callStack, locals);
        }
        catch
        {
            var fallbackStack = new List<CallStackFrameItem>
            {
                new()
                {
                    FrameIndex = 0,
                    MethodName = _fqn,
                    FileName = Path.GetFileName(_sourceFilePath),
                    LineNumber = _pausedLine > 0 ? _pausedLine : 1,
                    ColumnNumber = 1,
                    IsCurrentFrame = true
                }
            };
            return (fallbackStack, Array.Empty<DebugVariableItem>());
        }
    }

    private IReadOnlyList<CallStackFrameItem> ParseCallStack(string text)
    {
        var frames = new List<CallStackFrameItem>();
        var matches = FrameRegex().Matches(text);
        int index = 0;
        foreach (Match m in matches)
        {
            int line = int.TryParse(m.Groups["line"].Value, out var l) ? l : 1;
            frames.Add(new CallStackFrameItem
            {
                FrameIndex = index,
                MethodName = m.Groups["method"].Value.Trim(),
                FileName = m.Groups["file"].Value.Trim(),
                LineNumber = line,
                ColumnNumber = 1,
                IsCurrentFrame = index == 0
            });
            index++;
        }

        if (frames.Count == 0 && _pausedLine > 0)
        {
            frames.Add(new CallStackFrameItem
            {
                FrameIndex = 0,
                MethodName = _fqn,
                FileName = Path.GetFileName(_sourceFilePath),
                LineNumber = _pausedLine,
                ColumnNumber = 1,
                IsCurrentFrame = true
            });
        }

        return frames;
    }

    private async Task<string> SendCommandWaitPromptAsync(string command)
    {
        await _commandLock.WaitAsync().ConfigureAwait(false);
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_lock)
        {
            _activeCommandStartOffset = _outputBuffer.Length;
            _activeCommandTcs = tcs;
        }

        try
        {
            await _process.WriteInputAsync($"{command}\n").ConfigureAwait(false);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using (cts.Token.Register(() => tcs.TrySetCanceled()))
            {
                return await tcs.Task.ConfigureAwait(false);
            }
        }
        catch
        {
            return string.Empty;
        }
        finally
        {
            lock (_lock)
            {
                _activeCommandTcs = null;
            }
            _commandLock.Release();
        }
    }

    public async Task ContinueAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            _isCurrentlyPaused = false;
            _lastResumeIndex = _outputBuffer.Length;
            State = DebugSessionState.Running;
        }
        Resumed?.Invoke();
        await _process.WriteInputAsync("cont\n", ct).ConfigureAwait(false);
    }

    public async Task StepOverAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            _isCurrentlyPaused = false;
            _lastResumeIndex = _outputBuffer.Length;
            State = DebugSessionState.Running;
        }
        Resumed?.Invoke();
        await _process.WriteInputAsync("next\n", ct).ConfigureAwait(false);
    }

    public async Task StepIntoAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            _isCurrentlyPaused = false;
            _lastResumeIndex = _outputBuffer.Length;
            State = DebugSessionState.Running;
        }
        Resumed?.Invoke();
        await _process.WriteInputAsync("step\n", ct).ConfigureAwait(false);
    }

    public async Task StepOutAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            _isCurrentlyPaused = false;
            _lastResumeIndex = _outputBuffer.Length;
            State = DebugSessionState.Running;
        }
        Resumed?.Invoke();
        await _process.WriteInputAsync("step up\n", ct).ConfigureAwait(false);
    }

    public Task PauseAsync(CancellationToken ct = default) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (State == DebugSessionState.Terminated) return;
        }

        try
        {
            await _process.WriteInputAsync("quit\n", ct).ConfigureAwait(false);
        }
        catch { }
        finally
        {
            try { _process.Kill(); } catch { }
            SetTerminated(0, "Java debug session stopped by user.", wasCancelled: true);
        }
    }

    private IReadOnlyList<CallStackFrameItem> _lastCallStack = Array.Empty<CallStackFrameItem>();
    private IReadOnlyList<DebugVariableItem> _lastLocals = Array.Empty<DebugVariableItem>();

    public async Task StartRunAsync(CancellationToken ct = default)
    {
        await _process.WriteInputAsync("run\n", ct).ConfigureAwait(false);
    }

    public async Task SetBreakpointsAsync(string filePath, IReadOnlyList<BreakpointItem> breakpoints, CancellationToken ct = default)
    {
        foreach (var bp in breakpoints.Where(b => b.IsEnabled))
        {
            await _process.WriteInputAsync($"stop at {_fqn}:{bp.LineNumber}\n", ct).ConfigureAwait(false);
        }
    }

    public Task<IReadOnlyList<CallStackFrameItem>> GetCallStackAsync(CancellationToken ct = default) =>
        Task.FromResult(_lastCallStack);

    public Task<IReadOnlyList<DebugVariableItem>> GetVariablesAsync(int frameIndex, CancellationToken ct = default) =>
        Task.FromResult(_lastLocals);


    public async Task<EvaluationResult> EvaluateAsync(string expression, int? frameIndex, EvaluationContext context, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return new EvaluationResult(false, string.Empty, ErrorMessage: "Empty expression.");
        }

        try
        {
            var output = await SendCommandWaitPromptAsync($"print {expression}").ConfigureAwait(false);
            var match = Regex.Match(output, $@"(?:^|[\r\n]|\]\s+){Regex.Escape(expression)}\s*=\s*(?<val>[^\r\n]+)");
            if (match.Success)
            {
                var val = match.Groups["val"].Value.Trim();
                var typeName = val.StartsWith("instance of ", StringComparison.OrdinalIgnoreCase)
                    ? val.Substring("instance of ".Length).Split(' ')[0]
                    : "result";
                return new EvaluationResult(true, val, typeName);
            }
            return new EvaluationResult(false, string.Empty, ErrorMessage: $"Evaluation returned no value. Raw output: [{output}]");
        }
        catch (Exception ex)
        {
            return new EvaluationResult(false, string.Empty, ErrorMessage: ex.Message);
        }
    }

    private void SetTerminated(int exitCode, string message, bool wasCancelled = false)
    {
        lock (_lock)
        {
            if (State == DebugSessionState.Terminated) return;
            State = DebugSessionState.Terminated;
        }

        Terminated?.Invoke(new DebugTerminatedEventArgs(exitCode, message, wasCancelled));
    }

    private async Task MonitorProcessCompletionAsync()
    {
        try
        {
            int code = await _process.Completion.ConfigureAwait(false);
            SetTerminated(code, $"Process exited with code {code}.");
        }
        catch
        {
            SetTerminated(0, "Process completed.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _process.Dispose();
    }
}
