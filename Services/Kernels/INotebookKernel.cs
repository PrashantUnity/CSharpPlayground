using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;

/// <summary>One cell's code for a kernel, and where its output goes.</summary>
public sealed class KernelExecutionRequest
{
    public required string Code { get; init; }

    /// <summary>The cell's id, used to name the code in tracebacks and caller-info attributes.</summary>
    public string? SourceId { get; init; }

    /// <summary>How the notebook shows the cell, e.g. "[3]" (its execution count): what a kernel names the code in tracebacks.</summary>
    public string? Label { get; init; }

    public Action<string>? OnConsole { get; init; }

    public Action<RichCellOutput>? OnRichOutput { get; init; }

    /// <summary>
    /// Asks the user for a line of input (Python's <c>input()</c>): the prompt, whether it's a password, and the answer,
    /// or null for end of input. Without it the code sees end of input.
    /// </summary>
    public Func<string, bool, CancellationToken, Task<string?>>? OnInputRequest { get; init; }
}

/// <summary>
/// Runs the code cells of one language in one notebook and keeps their state between cells, like a Jupyter or
/// Polyglot Notebooks kernel. See docs/kernel-protocol.md for kernels that run as a separate program.
/// </summary>
public interface INotebookKernel : IDisposable
{
    string LanguageId { get; }

    /// <summary>E.g. ".NET (C#)" or "Python 3.14.6".</summary>
    string DisplayName { get; }

    /// <summary>True once code has run and there is state to lose on a restart.</summary>
    bool IsSessionActive { get; }

    /// <summary>
    /// True when Stop always ends the execution (a kernel in its own process can be killed), so the notebook waits for it
    /// instead of giving up after a grace period.
    /// </summary>
    bool CanForceStop { get; }

    Task<KernelExecutionResult> ExecuteAsync(KernelExecutionRequest request, CancellationToken ct);

    Task<IReadOnlyList<NotebookVariableInfo>> GetVariablesAsync(CancellationToken ct);

    /// <summary>A variable's value as JSON, for <c>#!share</c>. Throws <see cref="KernelValueException"/> when there's no such variable or it can't be sent.</summary>
    Task<string> GetValueJsonAsync(string name, CancellationToken ct);

    /// <summary>Sets (or declares) a variable from JSON, for <c>#!share</c>. Throws <see cref="KernelValueException"/> with a message for the user.</summary>
    Task SetValueFromJsonAsync(string name, string json, CancellationToken ct);

    /// <summary>Forgets all state and recovers from an execution that never finished.</summary>
    void HardReset();
}

/// <summary>Where a kernel runs: the notebook's folder (its working directory and first import path) and the workspace root.</summary>
public sealed record KernelCreationContext(Func<string?> WorkingDirectory, Func<string?>? WorkspaceRoot = null);

public interface INotebookKernelFactory
{
    INotebookKernel Create(KernelCreationContext context);
}

public sealed class DelegateKernelFactory(Func<KernelCreationContext, INotebookKernel> create) : INotebookKernelFactory
{
    public INotebookKernel Create(KernelCreationContext context) => create(context);
}

/// <summary>A <c>#!share</c> that can't be done, with a message for the user.</summary>
public sealed class KernelValueException(string message) : Exception(message);
