using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;

public enum DebugSessionState
{
    Idle,
    Running,
    Paused,
    Terminated
}

public enum EvaluationContext
{
    Watch,
    Hover,
    Repl
}

public sealed record EvaluationResult(
    bool Success,
    string Value,
    string? TypeName = null,
    string? ErrorMessage = null,
    int VariablesReference = 0);

public sealed record DebugPausedEventArgs(
    int LineNumber,
    string? SourceFilePath,
    string Reason,
    int? ThreadId,
    IReadOnlyList<CallStackFrameItem> CallStack,
    IReadOnlyList<DebugVariableItem> Locals);

public sealed record DebugTerminatedEventArgs(
    int? ExitCode,
    string? Message,
    bool WasCancelled);

public sealed record DebugLaunchContext(
    string ScriptId,
    string SourceFilePath,
    string SourceCode,
    IReadOnlyList<BreakpointItem> Breakpoints,
    ToolchainResolution? Toolchain,
    Action<string>? OnLiveOutput,
    CancellationToken CancellationToken);

public sealed record DebuggerResolution(
    bool IsAvailable,
    string DebuggerName,
    string? ExecutablePath,
    string? Version,
    MissingToolchainGuidance? MissingGuidance);

public interface IDebuggerProvider
{
    ValueTask<DebuggerResolution> ResolveDebuggerAsync(ToolchainResolution? toolchain, CancellationToken ct = default);

    Task<IDebugSession> LaunchAsync(DebugLaunchContext context, CancellationToken ct = default);
}

public interface IDebugSession : IAsyncDisposable
{
    string LanguageId { get; }
    DebugSessionState State { get; }
    int PausedLine { get; }
    string? PausedFilePath { get; }

    event Action<DebugPausedEventArgs>? Paused;
    event Action? Resumed;
    event Action<string>? OutputReceived;
    event Action<DebugTerminatedEventArgs>? Terminated;

    Task SetBreakpointsAsync(string filePath, IReadOnlyList<BreakpointItem> breakpoints, CancellationToken ct = default);
    Task ContinueAsync(CancellationToken ct = default);
    Task StepOverAsync(CancellationToken ct = default);
    Task StepIntoAsync(CancellationToken ct = default);
    Task StepOutAsync(CancellationToken ct = default);
    Task PauseAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);

    Task<IReadOnlyList<CallStackFrameItem>> GetCallStackAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DebugVariableItem>> GetVariablesAsync(int frameIndex, CancellationToken ct = default);
    Task<IReadOnlyList<DebugVariableItem>> GetVariableChildrenAsync(DebugVariableItem parent, CancellationToken ct = default);
    Task<EvaluationResult> EvaluateAsync(string expression, int? frameIndex, EvaluationContext context, CancellationToken ct = default);
}
