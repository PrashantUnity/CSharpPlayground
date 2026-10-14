using System.Diagnostics;
using System.Text;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Processes;

public sealed record ScriptRunResult(
    int? ExitCode,
    bool WasCancelled,
    TimeSpan Elapsed,
    DiagnosticParseResult Diagnostics,
    string? FailedBuildStep = null,
    string? StartError = null)
{
    public bool Succeeded => !WasCancelled && StartError == null && FailedBuildStep == null && ExitCode == 0;
}

/// <summary>A run in progress: its result, its standard input, and Stop.</summary>
public sealed class ScriptRunSession
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _cts;
    private IManagedProcess? _runningProgram;
    private readonly List<string> _pendingInput = new();

    internal ScriptRunSession(CancellationTokenSource cts)
    {
        _cts = cts;
    }

    public Task<ScriptRunResult> Completion { get; internal set; } = Task.FromResult(
        new ScriptRunResult(null, true, TimeSpan.Zero, DiagnosticParseResult.Empty));

    /// <summary>True while the program itself (not a build step) is running and can be typed to.</summary>
    public bool AcceptsInput
    {
        get
        {
            lock (_gate) return _runningProgram is { HasExited: false };
        }
    }

    public Task SendInputAsync(string text)
    {
        IManagedProcess? program;
        lock (_gate)
        {
            program = _runningProgram;
            if (program == null || program.HasExited)
            {
                _pendingInput.Add(text);
                return Task.CompletedTask;
            }
        }
        return program.WriteInputAsync(text);
    }

    /// <summary>Kills the current step and everything it started; the run ends as cancelled.</summary>
    public void Stop()
    {
        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The run already ended.
        }
    }

    internal void SetRunningProgram(IManagedProcess? program)
    {
        List<string>? toFlush = null;
        lock (_gate)
        {
            _runningProgram = program;
            if (program != null && _pendingInput.Count > 0)
            {
                toFlush = new List<string>(_pendingInput);
                _pendingInput.Clear();
            }
        }

        if (toFlush != null && program != null)
        {
            foreach (var input in toFlush)
            {
                _ = program.WriteInputAsync(input);
            }
        }
    }
}

/// <summary>Runs a <see cref="ScriptRunPlan"/>: each step in turn, output streamed, stopping at a failed build step.</summary>
public sealed class ScriptRunExecutor
{
    /// <summary>How much of a step's output is kept for reading errors from (a traceback is at the end).</summary>
    private const int OutputTailLength = 64 * 1024;

    private readonly IProcessLauncher _launcher;

    public ScriptRunExecutor(IProcessLauncher launcher)
    {
        _launcher = launcher;
    }

    /// <param name="onOutput">Every chunk of output from every step, on background threads.</param>
    /// <param name="environment">Variables for the program itself (not its build steps), on top of each step's own.</param>
    public ScriptRunSession Start(
        ScriptRunPlan plan,
        string sourceFilePath,
        IDiagnosticParser? parser,
        Action<string> onOutput,
        CancellationToken ct = default,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var session = new ScriptRunSession(cts);
        session.Completion = Task.Run(() => RunStepsAsync(plan, sourceFilePath, parser, onOutput, environment, session, cts));
        return session;
    }

    private async Task<ScriptRunResult> RunStepsAsync(
        ScriptRunPlan plan,
        string sourceFilePath,
        IDiagnosticParser? parser,
        Action<string> onOutput,
        IReadOnlyDictionary<string, string?>? environment,
        ScriptRunSession session,
        CancellationTokenSource cts)
    {
        var clock = Stopwatch.StartNew();
        try
        {
            ScriptRunResult? last = null;
            for (var i = 0; i < plan.Steps.Count; i++)
            {
                if (cts.IsCancellationRequested) return new ScriptRunResult(null, true, clock.Elapsed, DiagnosticParseResult.Empty);

                var step = plan.Steps[i];
                var tail = new OutputTail(OutputTailLength);
                var errorTail = new OutputTail(OutputTailLength);
                void Receive(string text)
                {
                    tail.Append(text);
                    onOutput(text);
                }
                void ReceiveError(string text)
                {
                    tail.Append(text);
                    errorTail.Append(text);
                    onOutput(text);
                }

                IManagedProcess process;
                try
                {
                    process = _launcher.Start(step.IsBuildStep ? step.Spec : WithEnvironment(step.Spec, environment), Receive, ReceiveError);
                }
                catch (ProcessStartException ex)
                {
                    return new ScriptRunResult(null, false, clock.Elapsed, DiagnosticParseResult.Empty, StartError: ex.Message);
                }

                int exitCode;
                using (process)
                {
                    if (!step.IsBuildStep) session.SetRunningProgram(process);
                    using (cts.Token.Register(process.Kill))
                    {
                        exitCode = await process.Completion;
                    }
                    session.SetRunningProgram(null);
                }

                if (cts.IsCancellationRequested) return new ScriptRunResult(exitCode, true, clock.Elapsed, DiagnosticParseResult.Empty);

                // Only a failed step is read for errors: a successful run can print text that merely looks like one.
                // Standard error is prioritized because compilers and interpreters write tracebacks and errors there,
                // avoiding interleaving with standard output.
                var diagnostics = DiagnosticParseResult.Empty;
                if (exitCode != 0 && parser != null)
                {
                    var errorOutput = errorTail.ToString();
                    if (!string.IsNullOrWhiteSpace(errorOutput))
                    {
                        diagnostics = parser.Parse(errorOutput, sourceFilePath);
                    }

                    if (diagnostics.Diagnostics.Count == 0 && diagnostics.MissingDependency == null)
                    {
                        diagnostics = parser.Parse(tail.ToString(), sourceFilePath);
                    }
                }

                if (step.IsBuildStep && exitCode != 0)
                {
                    return new ScriptRunResult(exitCode, false, clock.Elapsed, diagnostics, FailedBuildStep: step.Label);
                }

                last = new ScriptRunResult(exitCode, false, clock.Elapsed, diagnostics);
            }

            return last ?? new ScriptRunResult(0, false, clock.Elapsed, DiagnosticParseResult.Empty);
        }
        finally
        {
            cts.Dispose();
        }
    }

    private static ProcessStartSpec WithEnvironment(ProcessStartSpec spec, IReadOnlyDictionary<string, string?>? environment)
    {
        if (environment is not { Count: > 0 }) return spec;
        var merged = new Dictionary<string, string?>(spec.Environment);
        foreach (var (name, value) in environment) merged[name] = value;
        return spec with { Environment = merged };
    }

    private sealed class OutputTail(int maxLength)
    {
        private readonly StringBuilder _text = new();

        public void Append(string chunk)
        {
            lock (_text)
            {
                _text.Append(chunk);
                if (_text.Length > maxLength * 2) _text.Remove(0, _text.Length - maxLength);
            }
        }

        public override string ToString()
        {
            lock (_text)
            {
                return _text.Length > maxLength ? _text.ToString(_text.Length - maxLength, maxLength) : _text.ToString();
            }
        }
    }
}
