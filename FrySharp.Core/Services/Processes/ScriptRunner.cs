using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Processes;

/// <summary>What to run: a saved source file, the folder to run it in, and the toolchain found for it.</summary>
public sealed record ScriptRunContext(string SourceFilePath, string WorkingDirectory, ToolchainInfo Toolchain);

/// <summary>One command of a run. Build steps (a compiler) come first; the last step runs the program.</summary>
public sealed record ProcessStep(string Label, ProcessStartSpec Spec, bool IsBuildStep = false);

public sealed record ScriptRunPlan(IReadOnlyList<ProcessStep> Steps);

/// <summary>
/// How a language runs a source file, as commands: <c>python -u file.py</c>, <c>java File.java</c>, or a compile step
/// followed by running what it built. The studio runs the steps, streams their output and stops them.
/// </summary>
public interface IScriptRunner
{
    Task<ScriptRunPlan> PlanAsync(ScriptRunContext context, CancellationToken ct = default);
}

/// <summary>Errors found in a run's output, and a missing dependency to offer installing (e.g. a Python module).</summary>
public sealed record DiagnosticParseResult(IReadOnlyList<DiagnosticItem> Diagnostics, string? MissingDependency = null)
{
    public static readonly DiagnosticParseResult Empty = new(Array.Empty<DiagnosticItem>());
}

/// <summary>Reads Problems out of the output of a failed build or run: tracebacks, compiler errors.</summary>
public interface IDiagnosticParser
{
    /// <param name="output">What the step printed (both streams, in the order they arrived; only the end of a long run).</param>
    /// <param name="sourceFilePath">The file that was run, so only locations inside it are reported.</param>
    DiagnosticParseResult Parse(string output, string sourceFilePath);
}
