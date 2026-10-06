using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Common;

/// <summary>
/// Helper to safely dispatch UI operations. In a live application (desktop or single-view lifetime),
/// cross-thread calls are posted to the Avalonia UI dispatcher. In test runners or headless sessions
/// without an active message pump, callbacks are executed inline to prevent deadlocks and queued-callback starvation.
/// </summary>
public static class UiDispatchHelper
{
    public static bool HasLiveUiLifetime =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime or ISingleViewApplicationLifetime;

    /// <summary>
    /// Posts <paramref name="action"/> to the UI thread if called from a background thread in a live UI lifetime.
    /// In headless / test environments the action is called inline.
    /// </summary>
    public static void RunOnUi(Action action)
    {
        if (HasLiveUiLifetime && !Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(action);
        }
        else
        {
            action();
        }
    }

    /// <summary>
    /// Awaitable equivalent of <see cref="RunOnUi"/>. Returns a completed task in headless environments
    /// so callers can use <c>await UiDispatchHelper.InvokeAsync(...)</c> without deadlocking in tests.
    /// </summary>
    public static async Task InvokeAsync(Action action)
    {
        if (HasLiveUiLifetime && !Dispatcher.UIThread.CheckAccess())
        {
            await Dispatcher.UIThread.InvokeAsync(action);
        }
        else
        {
            action();
        }
    }

    /// <summary>
    /// Awaitable equivalent for async lambdas. In headless environments the action is awaited inline.
    /// </summary>
    public static async Task InvokeAsync(Func<Task> asyncAction)
    {
        if (HasLiveUiLifetime && !Dispatcher.UIThread.CheckAccess())
        {
            await Dispatcher.UIThread.InvokeAsync(asyncAction);
        }
        else
        {
            await asyncAction();
        }
    }

    /// <summary>
    /// Gets or sets the active dispatcher implementation. Defaults to <see cref="AvaloniaUiDispatcher"/>.
    /// In headless web / ASP.NET Core environments, can be set to <see cref="ImmediateDispatcher"/>.
    /// </summary>
    public static IDispatcher Current { get; set; } = new AvaloniaUiDispatcher();

    /// <summary>
    /// Invokes <paramref name="function"/> and returns its result, running on the UI thread when appropriate.
    /// </summary>
    public static async Task<T> InvokeAsync<T>(Func<T> function)
    {
        if (HasLiveUiLifetime && !Dispatcher.UIThread.CheckAccess())
        {
            return await Dispatcher.UIThread.InvokeAsync(function);
        }
        else
        {
            return function();
        }
    }

    /// <summary>
    /// Invokes asynchronous <paramref name="asyncFunction"/> and returns its result, running on the UI thread when appropriate.
    /// </summary>
    public static async Task<T> InvokeAsync<T>(Func<Task<T>> asyncFunction)
    {
        if (HasLiveUiLifetime && !Dispatcher.UIThread.CheckAccess())
        {
            return await Dispatcher.UIThread.InvokeAsync(asyncFunction);
        }
        else
        {
            return await asyncFunction();
        }
    }
}

/// <summary>
/// Default <see cref="IDispatcher"/> implementation delegating to Avalonia's <see cref="Dispatcher.UIThread"/>
/// when in a live UI application lifetime, and executing inline otherwise.
/// </summary>
public sealed class AvaloniaUiDispatcher : IDispatcher
{
    public bool HasLiveUiLifetime => UiDispatchHelper.HasLiveUiLifetime;
    public void RunOnUi(Action action) => UiDispatchHelper.RunOnUi(action);
    public Task InvokeAsync(Action action) => UiDispatchHelper.InvokeAsync(action);
    public Task InvokeAsync(Func<Task> asyncAction) => UiDispatchHelper.InvokeAsync(asyncAction);
    public Task<T> InvokeAsync<T>(Func<T> function) => UiDispatchHelper.InvokeAsync(function);
    public Task<T> InvokeAsync<T>(Func<Task<T>> asyncFunction) => UiDispatchHelper.InvokeAsync(asyncFunction);
}

/// <summary>
/// Headless / web <see cref="IDispatcher"/> implementation that executes all actions synchronously or inline.
/// Ideal for CLI tools, unit test runners, and ASP.NET Core servers.
/// </summary>
public sealed class ImmediateDispatcher : IDispatcher
{
    public bool HasLiveUiLifetime => false;
    public void RunOnUi(Action action) => action();
    public Task InvokeAsync(Action action) { action(); return Task.CompletedTask; }
    public Task InvokeAsync(Func<Task> asyncAction) => asyncAction();
    public Task<T> InvokeAsync<T>(Func<T> function) => Task.FromResult(function());
    public Task<T> InvokeAsync<T>(Func<Task<T>> asyncFunction) => asyncFunction();
}
