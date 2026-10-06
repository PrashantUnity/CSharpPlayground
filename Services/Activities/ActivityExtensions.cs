using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Activities;

/// <summary>Ways to run work as an activity without repeating start / report-failure / dispose at every call site.</summary>
public static class ActivityExtensions
{
    /// <summary>
    /// Runs <paramref name="work"/> as an activity. A failure is reported to the user (an error toast) and not rethrown,
    /// which is what a command or a click wants; cancellation ends it quietly. Returns whether it completed.
    /// </summary>
    public static async Task<bool> RunAsync(this IActivityService service, ActivityOptions options, Func<IActivity, Task> work, CancellationToken linked = default)
    {
        using var activity = service.Start(options, linked);
        try
        {
            await work(activity);
            return true;
        }
        catch (OperationCanceledException) when (activity.Token.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            activity.Fail(ex);
            return false;
        }
    }

    /// <summary>Like <see cref="RunAsync(IActivityService, ActivityOptions, Func{IActivity, Task}, CancellationToken)"/>, with a result (<c>default</c> when it failed or was cancelled).</summary>
    public static async Task<T?> RunAsync<T>(this IActivityService service, ActivityOptions options, Func<IActivity, Task<T>> work, CancellationToken linked = default)
    {
        using var activity = service.Start(options, linked);
        try
        {
            return await work(activity);
        }
        catch (OperationCanceledException) when (activity.Token.IsCancellationRequested)
        {
            return default;
        }
        catch (Exception ex)
        {
            activity.Fail(ex);
            return default;
        }
    }

    /// <summary>
    /// Lets a task run on without awaiting it, but never silently: a failure is reported through
    /// <see cref="IActivityService.Failed"/> as an error with <paramref name="what"/> as its title. Use it instead of
    /// <c>_ = SomethingAsync()</c> and instead of <c>async void</c>.
    /// </summary>
    public static void FireAndForget(this Task task, IActivityService service, string what)
    {
        if (task.IsCompletedSuccessfully) return;
        _ = Observe(task, service, what);
    }

    private static async Task Observe(Task task, IActivityService service, string what)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancelled on purpose.
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Activities] '{what}' failed: {ex}");
            using var activity = service.Start(new ActivityOptions(what));
            activity.Fail(ex);
        }
    }
}
