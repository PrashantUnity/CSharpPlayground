using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Services.Activities;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Startup;

public enum EngineState
{
    Starting,
    Ready,
    Failed,
}

/// <summary>
/// Starts the C# engine in two stages, off the UI thread, so the studio is usable long before the engine is warm.
/// <list type="bullet">
/// <item><b>Core</b>: what a page needs to open a document (the compiler service and the studio view models). Opening a
/// script waits for this and nothing more.</item>
/// <item><b>Warm</b>: what only makes the first run faster (Roslyn's first compilation, the notebook kernel). It runs
/// after Core as a status-bar activity; nobody waits for it, running code simply finds it done.</item>
/// </list>
/// A failure is reported to the user and remembered; the next open that needs the engine starts it again.
/// </summary>
public sealed class EngineReadiness
{
    private readonly IActivityService _activities;
    private readonly Func<Task> _core;
    private readonly Func<Task> _warm;
    private readonly object _gate = new();
    private Task? _coreTask;
    private Task? _warmTask;
    private EngineState _state = EngineState.Starting;

    /// <param name="core">Builds what pages need; runs on a background thread.</param>
    /// <param name="warm">Warms what makes the first run fast; runs on a background thread after <paramref name="core"/>.</param>
    public EngineReadiness(IActivityService activities, Func<Task> core, Func<Task> warm)
    {
        _activities = activities;
        _core = core;
        _warm = warm;
    }

    public EngineState State
    {
        get
        {
            lock (_gate) return _state;
        }
    }

    /// <summary>Why the last start failed (<c>null</c> unless <see cref="State"/> is <see cref="EngineState.Failed"/>).</summary>
    public Exception? Error { get; private set; }

    /// <summary>How long the stages took (for the perf harness and the log).</summary>
    public TimeSpan CoreElapsed { get; private set; }

    public TimeSpan WarmElapsed { get; private set; }

    /// <summary>The state moved. Raised on a background thread.</summary>
    public event Action? Changed;

    /// <summary>One line for the Hub's status panel.</summary>
    public string StatusText => State switch
    {
        EngineState.Ready => "Ready • Roslyn 4.12 & C# 13",
        EngineState.Failed => $"Failed to start: {Error?.Message}",
        _ => "Starting • Roslyn 4.12 & C# 13",
    };

    /// <summary>Completes when the Core stage is done (faults if it failed).</summary>
    public Task CoreReady
    {
        get
        {
            lock (_gate) return _coreTask ?? Task.CompletedTask;
        }
    }

    /// <summary>Completes when both stages are done.</summary>
    public Task WarmReady
    {
        get
        {
            lock (_gate) return _warmTask ?? Task.CompletedTask;
        }
    }

    /// <summary>Starts the engine (once; later calls return the running start).</summary>
    public Task Start()
    {
        lock (_gate)
        {
            if (_coreTask != null) return _coreTask;
            _state = EngineState.Starting;
            Error = null;
            var core = Task.Run(RunCoreAsync);
            _coreTask = core;
            _warmTask = core.ContinueWith(t => t.IsCompletedSuccessfully ? Task.Run(RunWarmAsync) : t, TaskScheduler.Default).Unwrap();
        }

        Changed?.Invoke();
        return CoreReady;
    }

    /// <summary>
    /// Waits for the Core stage, starting the engine again first if the last start failed (so "try again" is simply
    /// opening the document again).
    /// </summary>
    public Task WhenCoreReadyAsync()
    {
        lock (_gate)
        {
            // Only a failed Core stage is started again: a failed warm-up leaves the pages working.
            if (_coreTask is { IsFaulted: true })
            {
                _coreTask = null;
                _warmTask = null;
            }
        }

        return Start();
    }

    private async Task RunCoreAsync()
    {
        using var activity = _activities.Start(new ActivityOptions("Starting C# engine"));
        var clock = Stopwatch.StartNew();
        try
        {
            await _core().ConfigureAwait(false);
            CoreElapsed = clock.Elapsed;
        }
        catch (Exception ex)
        {
            Fail(ex);
            activity.Fail(ex);
            throw;
        }
    }

    private async Task RunWarmAsync()
    {
        using var activity = _activities.Start(new ActivityOptions("Warming up C# engine"));
        var clock = Stopwatch.StartNew();
        try
        {
            await _warm().ConfigureAwait(false);
            WarmElapsed = clock.Elapsed;
            lock (_gate) _state = EngineState.Ready;
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            // The pages work without the warm-up; running code will report its own problem if the engine is broken.
            Debug.WriteLine($"[EngineReadiness] warm-up failed: {ex}");
            Fail(ex);
            activity.Fail(ex);
        }
    }

    private void Fail(Exception ex)
    {
        lock (_gate)
        {
            _state = EngineState.Failed;
            Error = ex;
        }

        Changed?.Invoke();
    }
}
