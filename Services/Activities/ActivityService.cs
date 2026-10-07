using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Activities;

/// <summary>
/// The studio's <see cref="IActivityService"/>: a thread-safe list of running activities. Every change raises
/// <see cref="Changed"/> outside the lock, on the thread that made it; presenters marshal and coalesce.
/// </summary>
public sealed class ActivityService : IActivityService
{
    private readonly object _gate = new();
    private readonly List<RunningActivity> _running = new();
    private long _nextId;
    private long _version;

    public ActivityService(TimeProvider? time = null)
    {
        Time = time ?? TimeProvider.System;
    }

    public TimeProvider Time { get; }

    public long Version => Interlocked.Read(ref _version);

    public event Action? Changed;

    public event Action<ActivitySnapshot, Exception>? Failed;

    public IActivity Start(ActivityOptions options, CancellationToken linked = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var activity = new RunningActivity(this, Interlocked.Increment(ref _nextId), options, Time.GetTimestamp(), linked);
        lock (_gate)
        {
            _running.Add(activity);
        }

        RaiseChanged();
        return activity;
    }

    public IReadOnlyList<ActivitySnapshot> Snapshot()
    {
        lock (_gate)
        {
            return _running.Select(a => a.ToSnapshot()).ToArray();
        }
    }

    public void Cancel(long id)
    {
        RunningActivity? target;
        lock (_gate)
        {
            target = _running.FirstOrDefault(a => a.Id == id);
        }

        target?.RequestCancel();
    }

    private bool Remove(RunningActivity activity)
    {
        bool removed;
        lock (_gate)
        {
            removed = _running.Remove(activity);
        }

        if (removed) RaiseChanged();
        return removed;
    }

    private void RaiseChanged()
    {
        Interlocked.Increment(ref _version);
        try
        {
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            // A presenter's bug must never break the work that reported.
            Debug.WriteLine($"[ActivityService] Changed handler failed: {ex}");
        }
    }

    private void RaiseFailed(ActivitySnapshot snapshot, Exception error)
    {
        Debug.WriteLine($"[ActivityService] '{snapshot.Title}' failed: {error}");
        try
        {
            Failed?.Invoke(snapshot, error);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ActivityService] Failed handler failed: {ex}");
        }
    }

    private sealed class RunningActivity : IActivity
    {
        private readonly ActivityService _owner;
        private readonly ActivityOptions _options;
        private readonly long _started;
        private readonly CancellationTokenSource _cts;
        private readonly object _fractionGate = new();
        private string? _detail;
        private double? _fraction;
        private int _ended;

        public RunningActivity(ActivityService owner, long id, ActivityOptions options, long started, CancellationToken linked)
        {
            _owner = owner;
            Id = id;
            _options = options;
            _started = started;
            _detail = options.Detail;
            _cts = linked.CanBeCanceled ? CancellationTokenSource.CreateLinkedTokenSource(linked) : new CancellationTokenSource();
            Token = _cts.Token;
        }

        public long Id { get; }

        public CancellationToken Token { get; }

        public ActivitySnapshot ToSnapshot() =>
            new(Id, _options.Title, Volatile.Read(ref _detail), _options.Location, ReadFraction(), _options.Cancellable,
                _options.Blocking, _options.YieldAfter, _started);

        private double? ReadFraction()
        {
            lock (_fractionGate)
            {
                return _fraction;
            }
        }

        public void Report(double? fraction = null, string? detail = null)
        {
            if (Volatile.Read(ref _ended) != 0) return;
            if (fraction is { } f)
            {
                lock (_fractionGate)
                {
                    _fraction = double.IsFinite(f) ? Math.Clamp(f, 0, 1) : null;
                }
            }

            if (detail != null) Volatile.Write(ref _detail, detail);
            _owner.RaiseChanged();
        }

        public void RequestCancel()
        {
            if (Volatile.Read(ref _ended) != 0) return;
            try
            {
                _cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Ended while the Cancel button was pressed.
            }
        }

        public void Fail(Exception error)
        {
            ArgumentNullException.ThrowIfNull(error);
            if (Interlocked.Exchange(ref _ended, 1) != 0) return;
            var snapshot = ToSnapshot();
            _owner.Remove(this);
            // Stopping because it was asked to is not something to report as an error.
            if (!(error is OperationCanceledException && Token.IsCancellationRequested)) _owner.RaiseFailed(snapshot, error);
            _cts.Dispose();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _ended, 1) != 0) return;
            _owner.Remove(this);
            _cts.Dispose();
        }
    }
}

/// <summary>
/// Reports nothing: the default for view models and services built without a studio host (tests, tools). Cancellation
/// through the linked token still works.
/// </summary>
public sealed class NullActivityService : IActivityService
{
    public static NullActivityService Instance { get; } = new();

    private long _nextId;

    public TimeProvider Time => TimeProvider.System;

    public long Version => 0;

    public IActivity Start(ActivityOptions options, CancellationToken linked = default) =>
        new NullActivity(Interlocked.Increment(ref _nextId), linked);

    public IReadOnlyList<ActivitySnapshot> Snapshot() => Array.Empty<ActivitySnapshot>();

    public void Cancel(long id)
    {
    }

    public event Action? Changed
    {
        add { }
        remove { }
    }

    public event Action<ActivitySnapshot, Exception>? Failed
    {
        add { }
        remove { }
    }

    private sealed class NullActivity(long id, CancellationToken token) : IActivity
    {
        public long Id { get; } = id;
        public CancellationToken Token { get; } = token;
        public void Report(double? fraction = null, string? detail = null) { }
        public void Fail(Exception error) => Debug.WriteLine($"[NullActivityService] activity failed: {error}");
        public void Dispose() { }
    }
}
