using System;

namespace FrySharp.Sdk;

/// <summary>
/// Context passed before a script or notebook cell begins execution.
/// </summary>
public class ExecutionHookContext
{
    public string LanguageId { get; init; } = string.Empty;
    public string? DocumentPath { get; init; }
    public string SourceCode { get; set; } = string.Empty;
    public bool CancelExecution { get; set; }
    public string? CancellationReason { get; set; }

    public void Cancel(string reason)
    {
        CancelExecution = true;
        CancellationReason = reason;
    }
}

/// <summary>
/// Context passed after a script or notebook cell completes execution.
/// </summary>
public class ExecutionFinishedHookContext
{
    public string LanguageId { get; init; } = string.Empty;
    public string? DocumentPath { get; init; }
    public bool Success { get; init; }
    public TimeSpan Elapsed { get; init; }
    public string Output { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;
}
