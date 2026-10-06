namespace CSharpEditorPlugin.Tests.TestSupport;

/// <summary>
/// A clock that only moves when a test says so. Its timers fire on the test's thread, from <see cref="Advance"/>, so
/// nothing a test builds is ever touched by a thread-pool callback.
/// </summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private readonly List<ManualTimer> _timers = new();
    private long _ticks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => Interlocked.Read(ref _ticks);

    public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(GetTimestamp());

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state, dueTime == Timeout.InfiniteTimeSpan ? null : GetTimestamp() + dueTime.Ticks, period);
        lock (_timers) _timers.Add(timer);
        return timer;
    }

    /// <summary>How many timers are running (a presenter must stop its timer when idle).</summary>
    public int ActiveTimers
    {
        get
        {
            lock (_timers) return _timers.Count;
        }
    }

    public void Advance(TimeSpan by)
    {
        long now = Interlocked.Add(ref _ticks, by.Ticks);
        ManualTimer[] due;
        lock (_timers) due = _timers.Where(t => t.DueAt is { } at && at <= now).ToArray();
        foreach (var timer in due) timer.Fire(now);
    }

    public void AdvanceMs(double milliseconds) => Advance(TimeSpan.FromMilliseconds(milliseconds));

    private void Remove(ManualTimer timer)
    {
        lock (_timers) _timers.Remove(timer);
    }

    private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state, long? dueAt, TimeSpan period) : ITimer
    {
        public long? DueAt { get; private set; } = dueAt;

        public void Fire(long now)
        {
            DueAt = period > TimeSpan.Zero && period != Timeout.InfiniteTimeSpan ? now + period.Ticks : null;
            callback(state);
        }

        public bool Change(TimeSpan dueTime, TimeSpan newPeriod)
        {
            DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : owner.GetTimestamp() + dueTime.Ticks;
            return true;
        }

        public void Dispose() => owner.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
