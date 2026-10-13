using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;

/// <summary>
/// A kernel that runs as its own program and speaks the Fry kernel protocol over its stdin/stdout pipes (one JSON
/// object per line; docs/kernel-protocol.md). Any language gets notebook cells by shipping such a program and an
/// <see cref="IKernelLauncher"/>. The program is started with the first cell, and restarted with a note if it dies.
/// Stop asks it to interrupt the cell and ends it if it doesn't, so a cell never runs on unseen.
/// </summary>
public sealed partial class ProtocolKernel : INotebookKernel, ISearchPathKernel
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    private readonly IKernelLauncher _launcher;
    private readonly IProcessLauncher _processes;
    private readonly KernelCreationContext _context;
    private readonly TimeSpan _startupTimeout;
    private readonly TimeSpan _interruptGrace;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _sessionLock = new();
    private Session? _session;
    private bool _stateLost;
    private volatile bool _executed;
    private int _disposed;

    public ProtocolKernel(
        string languageId,
        string displayName,
        IKernelLauncher launcher,
        IProcessLauncher processes,
        KernelCreationContext context,
        TimeSpan? startupTimeout = null,
        TimeSpan? interruptGrace = null)
    {
        LanguageId = languageId;
        DisplayName = displayName;
        _launcher = launcher;
        _processes = processes;
        _context = context;
        _startupTimeout = startupTimeout ?? TimeSpan.FromSeconds(60);
        _interruptGrace = interruptGrace ?? TimeSpan.FromSeconds(2);
    }

    public string LanguageId { get; }

    public string DisplayName { get; private set; }

    public bool IsSessionActive => _executed && CurrentSession is { IsAlive: true };

    // A separate program can always be ended: Stop never has to give up on a cell.
    public bool CanForceStop => true;

    /// <summary>The running program's process id, for tests; null when it isn't running.</summary>
    public int? ProcessId => CurrentSession is { IsAlive: true } session ? session.ProcessId : null;

    private Session? CurrentSession
    {
        get
        {
            lock (_sessionLock) return _session;
        }
    }

    public async Task<KernelExecutionResult> ExecuteAsync(KernelExecutionRequest request, CancellationToken ct)
    {
        var clock = Stopwatch.StartNew();
        try
        {
            await _gate.WaitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return new KernelExecutionResult { WasCancelled = true, ErrorMessage = "Execution was cancelled.", Elapsed = clock.Elapsed };
        }

        try
        {
            Session session;
            try
            {
                session = await StartAsync(request.OnConsole, ct);
            }
            catch (KernelUnavailableException ex)
            {
                request.OnConsole?.Invoke(ex.Message.EndsWith('\n') ? ex.Message : ex.Message + "\n");
                return new KernelExecutionResult { Success = false, ErrorMessage = ex.Message, ConsoleOutput = ex.Message, Elapsed = clock.Elapsed };
            }
            catch (OperationCanceledException)
            {
                return new KernelExecutionResult { WasCancelled = true, ErrorMessage = "Execution was cancelled.", Elapsed = clock.Elapsed };
            }

            var execution = new Execution(NewId(), request, ct);
            session.Begin(execution);
            try
            {
                var reply = session.Expect(execution.Id);
                await session.SendAsync(new { type = "execute", id = execution.Id, code = request.Code, cell = request.Label ?? request.SourceId });
                _executed = true;

                JsonElement response;
                using (ct.Register(() => _ = InterruptAsync(session, execution)))
                {
                    try
                    {
                        response = await reply;
                    }
                    catch (KernelStoppedException stopped)
                    {
                        var interrupted = execution.InterruptRequested;
                        var message = interrupted
                            ? $"The {DisplayName} kernel didn't stop when asked, so it was ended. Its variables are gone."
                            : stopped.Message;
                        execution.Write("\n" + message + "\n");
                        return new KernelExecutionResult
                        {
                            Success = false,
                            WasCancelled = interrupted,
                            ErrorMessage = message,
                            ConsoleOutput = execution.Output,
                            Elapsed = clock.Elapsed
                        };
                    }
                }

                var status = response.TryGetProperty("status", out var s) ? s.GetString() : "error";
                return new KernelExecutionResult
                {
                    Success = status == "ok",
                    WasCancelled = status == "interrupted",
                    ErrorMessage = status == "interrupted" ? "Execution was interrupted." : execution.ErrorSummary ?? (status == "ok" ? string.Empty : "The cell failed."),
                    ConsoleOutput = execution.Output,
                    MissingDependency = execution.MissingDependency,
                    MissingName = execution.MissingName,
                    Elapsed = clock.Elapsed
                };
            }
            finally
            {
                session.End(execution);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<NotebookVariableInfo>> GetVariablesAsync(CancellationToken ct)
    {
        if (CurrentSession is not { IsAlive: true } session || !session.Ready.IsCompletedSuccessfully) return Array.Empty<NotebookVariableInfo>();

        await _gate.WaitAsync(ct);
        try
        {
            var reply = await RequestAsync(session, id => new { type = "variables", id }, ct);
            if (!reply.TryGetProperty("variables", out var variables) || variables.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<NotebookVariableInfo>();
            }

            return variables.EnumerateArray().Select(v => new NotebookVariableInfo
            {
                Name = Text(v, "name"),
                TypeName = Text(v, "type"),
                ValueDisplay = Text(v, "value"),
                Kind = Text(v, "kind") is { Length: > 0 } kind ? kind : "Variable"
            }).ToList();
        }
        catch (Exception ex) when (ex is KernelStoppedException or TimeoutException)
        {
            return Array.Empty<NotebookVariableInfo>();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> GetValueJsonAsync(string name, CancellationToken ct)
    {
        if (CurrentSession is not { IsAlive: true } session || !session.Ready.IsCompletedSuccessfully)
        {
            throw new KernelValueException($"{DisplayName} hasn't run any cell yet, so it has no '{name}'.");
        }

        await _gate.WaitAsync(ct);
        try
        {
            var reply = await RequestAsync(session, id => new { type = "get_value", id, name }, ct);
            return Text(reply, "status") == "ok"
                ? Text(reply, "json")
                : throw new KernelValueException(Text(reply, "message") is { Length: > 0 } message ? message : $"Couldn't read '{name}' from {DisplayName}.");
        }
        catch (Exception ex) when (ex is KernelStoppedException or TimeoutException)
        {
            throw new KernelValueException($"Couldn't read '{name}' from {DisplayName}: {ex.Message}");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetValueFromJsonAsync(string name, string json, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            Session session;
            try
            {
                session = await StartAsync(null, ct);
            }
            catch (KernelUnavailableException ex)
            {
                throw new KernelValueException(ex.Message);
            }

            var reply = await RequestAsync(session, id => new { type = "set_value", id, name, json }, ct);
            if (Text(reply, "status") != "ok")
            {
                throw new KernelValueException(Text(reply, "message") is { Length: > 0 } message ? message : $"Couldn't set '{name}' in {DisplayName}.");
            }

            _executed = true;
        }
        catch (Exception ex) when (ex is KernelStoppedException or TimeoutException)
        {
            throw new KernelValueException($"Couldn't set '{name}' in {DisplayName}: {ex.Message}");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AddSearchPathAsync(string path, CancellationToken ct = default)
    {
        if (CurrentSession is not { IsAlive: true } session || !session.Ready.IsCompletedSuccessfully) return;
        await _gate.WaitAsync(ct);
        try
        {
            await RequestAsync(session, id => new { type = "add_search_path", id, path }, ct);
        }
        catch (Exception ex) when (ex is KernelStoppedException or TimeoutException)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Couldn't add '{path}' to {DisplayName}: {ex.Message}");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Ends the program; the next cell starts a fresh one (Restart, Run All).</summary>
    public void HardReset()
    {
        Session? session;
        lock (_sessionLock)
        {
            session = _session;
            _session = null;
        }

        session?.Shutdown();
        _executed = false;
        _stateLost = false;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        HardReset();
    }

    private async Task<Session> StartAsync(Action<string>? onConsole, CancellationToken ct)
    {
        var existing = CurrentSession;
        if (existing is { IsAlive: true } && existing.Ready.IsCompletedSuccessfully) return existing;
        existing?.Shutdown();

        var spec = await _launcher.PrepareAsync(_context, ct);
        DisplayName = spec.DisplayName;
        var session = new Session(this);
        try
        {
            session.Start(spec.Process, _processes);
        }
        catch (ProcessStartException ex)
        {
            throw new KernelUnavailableException(ex.Message);
        }

        lock (_sessionLock) _session = session;
        try
        {
            await session.Ready.WaitAsync(_startupTimeout, ct);
        }
        catch (TimeoutException)
        {
            session.Shutdown();
            throw new KernelUnavailableException($"The {DisplayName} kernel didn't start within {_startupTimeout.TotalSeconds:0} seconds.");
        }
        catch (KernelStoppedException ex)
        {
            throw new KernelUnavailableException(ex.Message);
        }

        _executed = false;
        bool noteLostState;
        lock (_sessionLock)
        {
            noteLostState = _stateLost;
            _stateLost = false;
        }

        if (noteLostState) onConsole?.Invoke($"(A new {DisplayName} kernel was started: the variables from before are gone.)\n");
        return session;
    }

    private static async Task<JsonElement> RequestAsync(Session session, Func<string, object> message, CancellationToken ct)
    {
        var id = NewId();
        var reply = session.Expect(id);
        await session.SendAsync(message(id));
        return await reply.WaitAsync(RequestTimeout, ct);
    }

    // Stop: ask the program to interrupt the cell, and end it if it hasn't within the grace period.
    private async Task InterruptAsync(Session session, Execution execution)
    {
        if (execution.Finished) return;
        execution.InterruptRequested = true;
        try
        {
            await session.SendAsync(new { type = "interrupt" });
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
        }

        await Task.Delay(_interruptGrace);
        if (!execution.Finished) session.Kill();
    }

    private void OnSessionEnded(Session session, bool unexpected)
    {
        lock (_sessionLock)
        {
            if (!ReferenceEquals(_session, session)) return;
            _session = null;
            if (unexpected) _stateLost = true;
        }

        _executed = false;
    }

    private static string NewId() => Guid.NewGuid().ToString("N")[..12];

    private static string Text(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private sealed class KernelStoppedException(string message) : Exception(message);
}
