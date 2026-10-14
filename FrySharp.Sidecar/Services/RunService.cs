using System.Collections.Concurrent;
using System.Diagnostics;
using FrySharp.Sidecar.Protocol;
using FrySharp.Sidecar.Sessions;
using Microsoft.Extensions.Logging;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Execution;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using StreamJsonRpc;

namespace FrySharp.Sidecar.Services;

/// <summary>
/// Executes scripts through <see cref="FileRunner"/> and streams results back over JSON-RPC.
/// </summary>
public sealed class RunService
{
    private readonly ConcurrentDictionary<string, Run> _runs = new(StringComparer.Ordinal);
    private readonly LanguageRegistry<ILanguageEngine> _languages;
    private readonly WorkspaceService _workspace;
    private readonly FileRunner _runner;
    private readonly ILogger<RunService> _logger;

    public RunService(
        LanguageRegistry<ILanguageEngine> languages,
        WorkspaceService workspace,
        FileRunner runner,
        ILogger<RunService> logger)
    {
        _languages = languages;
        _workspace = workspace;
        _runner = runner;
        _logger = logger;
    }

    public RunStartResult Start(SidecarSession session, RunStartParams request)
    {
        if (!IsValidRunId(request.RunId))
        {
            throw Error(ErrorCodes.Invalid, "A run id is 1 to 64 letters, digits, '-' or '_'.");
        }

        string? fullPath = null;
        if (request.Path is { Length: > 0 } path)
        {
            fullPath = _workspace.Resolve(path);
            if (!File.Exists(fullPath)) throw Error(ErrorCodes.NotFound, $"'{path}' doesn't exist.");
        }
        else if (string.IsNullOrEmpty(request.Code))
        {
            throw Error(ErrorCodes.Invalid, "Name a file to run, or give C# code.");
        }

        var language = request.LanguageId is { Length: > 0 } id ? _languages.Get(id)
            : fullPath != null ? _languages.LanguageOf(fullPath)
            : _languages.Get(LanguageIds.CSharp);
        if (language == null || !FileRunner.CanRun(language))
        {
            throw Error(ErrorCodes.NotSupported, fullPath != null
                ? $"'{_workspace.Paths.ToRelative(fullPath)}' can't be run here."
                : $"{request.LanguageId} code can't be run here.");
        }
        if (fullPath == null && language.Id != LanguageIds.CSharp)
        {
            throw Error(ErrorCodes.NotSupported, $"{language.DisplayName} code can only be run from a file.");
        }

        var run = new Run(request.RunId, fullPath == null ? "C# code" : Path.GetFileName(fullPath),
            fullPath == null ? null : _workspace.Paths.ToRelative(fullPath), session);
        if (!_runs.TryAdd(run.Id, run))
        {
            throw Error(ErrorCodes.AlreadyExists, $"Already has an active run '{run.Id}'.");
        }

        _ = Task.Run(async () =>
        {
            var clock = Stopwatch.StartNew();
            var exitCode = 1;
            IReadOnlyList<RunProblem> problems = [];
            try
            {
                using var stdin = new InteractiveStdinReader(run.Token);
                stdin.WaitingStarted += () => session.Notify(ClientMethods.RunInputRequested, new RunIdParams(run.Id));
                run.Stdin = stdin;
                var options = new FileRunOptions
                {
                    OnOutput = text => run.Output.Append(ToTerminal(text)),
                    OnDisplay = output => Display(run, output),
                    CSharpInput = stdin,
                    OnProcessStarted = process => run.Process = process,
                    Arguments = request.Arguments ?? [],
                    WorkspaceRoot = _workspace.Paths.Root,
                };

                var result = fullPath != null
                    ? await _runner.RunFileAsync(fullPath, language, options, run.Token).ConfigureAwait(false)
                    : await _runner.RunCSharpAsync(request.Code!, options, run.Token).ConfigureAwait(false);
                exitCode = result.ExitCode;
                problems = result.Problems.Select(d => Problem(run.Path, d)).ToList();
            }
            catch (OperationCanceledException)
            {
                exitCode = 130;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Run {RunId} failed.", run.Id);
                run.Output.Append($"\r\n❌ {ex.Message}\r\n");
            }
            finally
            {
                run.Finish(exitCode);
                run.Output.Flush();
                session.Notify(ClientMethods.RunExited, new RunExitedNotification(run.Id, exitCode, clock.ElapsedMilliseconds, problems));
            }
        });

        return new RunStartResult(run.Id, run.Title);
    }

    public bool Stop(SidecarSession session, string runId)
    {
        if (!TryGet(runId, out var run) || !run.Running) return false;
        run.Stop();
        return true;
    }

    public async Task InputAsync(SidecarSession session, RunInputParams request)
    {
        if (!TryGet(request.RunId, out var run) || !run.Running) return;
        run.Output.Append(request.Text + "\r\n");
        if (run.Process is { AcceptsInput: true } process)
        {
            await process.SendInputAsync(request.Text + "\n").ConfigureAwait(false);
        }
        else
        {
            run.Stdin?.PostInput(request.Text);
        }
    }

    public IReadOnlyList<RunInfo> List(SidecarSession session) =>
        _runs.Values
            .OrderBy(r => r.Started)
            .Select(r => new RunInfo(r.Id, r.Title, r.Path, r.Output.Transcript, r.Running, r.ExitCode))
            .ToList();

    private static void Display(Run run, RichCellOutput output)
    {
        if (RichOutputMime.ToBundle(output, out var metadata) is not { } bundle) return;
        run.Output.Flush();
        var data = Newtonsoft.Json.Linq.JObject.Parse(bundle.ToJsonString());
        var meta = metadata.Count > 0 ? Newtonsoft.Json.Linq.JObject.Parse(metadata.ToJsonString()) : null;
        run.Owner.Notify(ClientMethods.RunDisplay, new RunDisplayNotification(run.Id, data, meta));
    }

    private static string ToTerminal(string text) => text.ReplaceLineEndings("\r\n");

    private static RunProblem Problem(string? path, DiagnosticItem d) =>
        new(path, LanguageFeatures.ToDto(d).Severity, d.Id, d.Message, Math.Max(1, d.Line), Math.Max(1, d.Column));

    private bool TryGet(string runId, out Run run) => _runs.TryGetValue(runId, out run!);

    private static bool IsValidRunId(string? id) =>
        id is { Length: > 0 and <= 64 } && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    private static LocalRpcException Error(int code, string message) => new(message) { ErrorCode = code };

    private sealed class Run(string id, string title, string? path, SidecarSession owner)
    {
        private readonly CancellationTokenSource _cancel = new();
        private readonly Lock _gate = new();

        public string Id { get; } = id;
        public string Title { get; } = title;
        public string? Path { get; } = path;
        public SidecarSession Owner { get; } = owner;
        public DateTime Started { get; } = DateTime.UtcNow;
        public DateTime Ended { get; private set; } = DateTime.MaxValue;
        public bool Running { get; private set; } = true;
        public int? ExitCode { get; private set; }
        public CancellationToken Token => _cancel.Token;
        public RunOutput Output { get; } = new(text => owner.Notify(ClientMethods.RunOutput, new RunOutputNotification(id, text)));
        public InteractiveStdinReader? Stdin { get; set; }
        public ScriptRunSession? Process { get; set; }

        public void Stop()
        {
            ScriptRunSession? process;
            lock (_gate)
            {
                if (!Running) return;
                _cancel.Cancel();
                process = Process;
            }
            process?.Stop();
        }

        public void Finish(int exitCode)
        {
            lock (_gate)
            {
                Running = false;
                ExitCode = exitCode;
                Ended = DateTime.UtcNow;
                Stdin = null;
                Process = null;
                _cancel.Dispose();
            }
        }
    }
}
