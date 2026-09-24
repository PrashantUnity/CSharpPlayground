using System;
using System.Threading;
using System.Threading.Tasks;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

/// <summary>
/// Roslyn scripting (and a compiled Main() invoked via reflection) only observes a
/// CancellationToken between top-level statements/submissions — it can never preempt a single
/// statement that's synchronously blocked mid-call (e.g. `httpClient.GetStringAsync(url).Result`
/// against a server that never responds). So "Stop" can't always make a running execution actually
/// finish; the best a host can do is stop *waiting* for it. This mirrors how every interactive
/// scripting tool in this category behaves (LINQPad, dotnet-interactive) — a stuck call leaks one
/// thread-pool thread until it naturally returns or the process exits, since .NET has no safe way
/// to force-kill a thread.
/// </summary>
public static class ExecutionAbandonment
{
    public static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Waits for <paramref name="task"/>, but gives up early if <paramref name="token"/> is
    /// cancelled and the task still hasn't finished after a short grace period (long enough for the
    /// common, fast, cooperative-cancellation case — cancelled between statements — to complete
    /// normally instead of being misreported as abandoned).
    /// Returns true if the task completed (the caller should await it to get its result/exception);
    /// false if it's being abandoned (still running in the background, unobserved).
    /// </summary>
    public static async Task<bool> WaitWithGraceAsync(Task task, CancellationToken token)
    {
        if (!token.CanBeCanceled)
        {
            await task;
            return true;
        }

        var cancelled = new TaskCompletionSource<bool>();
        await using var registration = token.Register(() => cancelled.TrySetResult(true));

        if (await Task.WhenAny(task, cancelled.Task) == task)
        {
            return true;
        }

        return await Task.WhenAny(task, Task.Delay(GracePeriod)) == task;
    }
}
