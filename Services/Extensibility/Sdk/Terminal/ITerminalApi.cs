using System;
using System.Threading;
using System.Threading.Tasks;

namespace FrySharp.Sdk;

/// <summary>
/// Result of an external command or process execution.
/// </summary>
public class CommandRunResult
{
    public int ExitCode { get; init; }
    public string Output { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
    public bool Success => ExitCode == 0;
}

/// <summary>
/// Public API for writing to the Studio Terminal/Console and executing CLI subprocesses.
/// </summary>
public interface ITerminalApi
{
    /// <summary>Writes text to the active studio Terminal/Console output.</summary>
    void Write(string text);

    /// <summary>Writes a line of text to the active studio Terminal/Console output.</summary>
    void WriteLine(string line = "");

    /// <summary>Clears the studio Terminal/Console output buffer.</summary>
    void Clear();

    /// <summary>
    /// Executes an external CLI tool or process, streaming its output in real time to the Terminal deck.
    /// </summary>
    Task<CommandRunResult> RunCommandAsync(string command, string arguments = "", string? workingDirectory = null, CancellationToken ct = default);
}
