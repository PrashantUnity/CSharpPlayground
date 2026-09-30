using System.Collections.Concurrent;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Interaction;

/// <summary>
/// Where C# callbacks for visual events run: one loop per kernel. A callback waits for the kernel to be free (it takes
/// the lock a cell takes, so it never runs beside a cell and sees the variables as the cells left them), or runs inside
/// the running code when that code asks: <c>Display.ProcessEvents()</c>, <c>Display.Wait()</c>. Callbacks run in the order
/// their events came.
/// </summary>
public sealed class VisualEventLoop
{
    private static readonly AsyncLocal<VisualEventLoop?> Ambient = new();

    private readonly ConcurrentQueue<Action> _pending = new();
    private readonly SemaphoreSlim _arrived = new(0);
    private readonly CancellationTokenSource _stopped = new();
    private readonly Func<CancellationToken, Task<IDisposable>>? _whenFree;
    private int _draining;

    /// <param name="whenFree">Takes the kernel's lock (waiting for a running cell to end); without it, callbacks run only when asked.</param>
    public VisualEventLoop(Func<CancellationToken, Task<IDisposable>>? whenFree = null) => _whenFree = whenFree;

    /// <summary>The loop of the code running here: its kernel's, or one of its own for a program that has no kernel.</summary>
    public static VisualEventLoop? Current => Ambient.Value;

    internal static VisualEventLoop CurrentOrOwn() => Ambient.Value ??= new VisualEventLoop();

    /// <summary>Makes this the loop of the code that runs inside (a cell: the kernel enters it for each).</summary>
    public IDisposable Enter()
    {
        var outer = Ambient.Value;
        Ambient.Value = this;
        return new Exit(() => Ambient.Value = outer);
    }

    /// <summary>Cancelled when the loop stops (the kernel restarted): a callback that runs long can watch it.</summary>
    public CancellationToken Stopping => _stopped.Token;

    public bool IsStopped => _stopped.IsCancellationRequested;

    /// <summary>A callback to run: now if the kernel is free, else when it is (or when the running code asks).</summary>
    public void Post(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (IsStopped) return;
        _pending.Enqueue(callback);
        _arrived.Release();
        if (_whenFree != null) _ = DrainWhenFreeAsync();
    }

    /// <summary>Runs every callback waiting, here and now; how many ran.</summary>
    public int RunPending()
    {
        var ran = 0;
        while (!IsStopped && _pending.TryDequeue(out var callback))
        {
            callback();
            ran++;
        }

        return ran;
    }

    /// <summary>Runs callbacks as their events come, until <paramref name="token"/> is cancelled (Stop) or the time is up.</summary>
    public void Wait(CancellationToken token, TimeSpan? timeout = null)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _stopped.Token);
        var deadline = timeout is { } limit ? DateTime.UtcNow + limit : DateTime.MaxValue;
        try
        {
            while (true)
            {
                RunPending();
                var left = deadline - DateTime.UtcNow;
                if (left <= TimeSpan.Zero) return;

                // Woken by each event that comes (the count only says "look again"), the stop, or the time running out.
                if (_pending.IsEmpty) _arrived.Wait(left > TimeSpan.FromDays(1) ? Timeout.InfiniteTimeSpan : left, linked.Token);
            }
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            // The loop stopped: nothing more will come.
        }
    }

    /// <summary>Stops running callbacks, and drops those waiting (the kernel restarted, so their variables are gone).</summary>
    public void Stop()
    {
        _stopped.Cancel();
        _pending.Clear();
    }

    // Takes the kernel's lock as a cell would, then runs what is waiting; once more if more came meanwhile.
    private async Task DrainWhenFreeAsync()
    {
        if (Interlocked.Exchange(ref _draining, 1) == 1) return;
        try
        {
            using (await _whenFree!(_stopped.Token).ConfigureAwait(false)) RunPending();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            Volatile.Write(ref _draining, 0);
        }

        if (!_pending.IsEmpty && !IsStopped) _ = DrainWhenFreeAsync();
    }

    private sealed class Exit(Action exit) : IDisposable
    {
        public void Dispose() => exit();
    }
}

/// <summary>
/// A C# callback for a visual's events: it runs on the loop of the code that asked for it, with that code's console and
/// display (so what it prints and shows goes to the cell that asked), and a failure is reported there, not thrown.
/// </summary>
internal sealed class CallbackSink(VisualEventLoop loop, Action<VisualEvent> callback) : IVisualEventSink
{
    private readonly ExecutionContext? _context = ExecutionContext.Capture();

    public void Deliver(string displayId, VisualEvent visualEvent) => loop.Post(() => Run(visualEvent));

    private void Run(VisualEvent visualEvent)
    {
        if (_context == null) Invoke(visualEvent);
        else ExecutionContext.Run(_context.CreateCopy(), _ => Invoke(visualEvent), null);
    }

    private void Invoke(VisualEvent visualEvent)
    {
        try
        {
            callback(visualEvent);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"⚠️ The {visualEvent.Kind} callback failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
