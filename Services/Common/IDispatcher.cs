using System;
using System.Threading.Tasks;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Common;

/// <summary>
/// Abstraction over UI thread dispatching. Decouples headless backend services, notebooks, and extensions
/// from Avalonia's Dispatcher.UIThread so the exact same services can execute identically in desktop mode,
/// headless test suites, or an ASP.NET Core web host (where UI dispatch routes to WebSockets or no-ops).
/// </summary>
public interface IDispatcher
{
    /// <summary>
    /// Gets whether a live graphical UI application lifetime (desktop or single-view) is currently active.
    /// Returns false in CLI tools, unit test runners, headless servers, and ASP.NET Core hosts.
    /// </summary>
    bool HasLiveUiLifetime { get; }

    /// <summary>
    /// Posts <paramref name="action"/> to the UI thread if in a live UI lifetime; otherwise executes inline.
    /// </summary>
    void RunOnUi(Action action);

    /// <summary>
    /// Awaits execution of <paramref name="action"/> on the UI thread, or runs inline in headless mode.
    /// </summary>
    Task InvokeAsync(Action action);

    /// <summary>
    /// Awaits execution of an asynchronous <paramref name="asyncAction"/> on the UI thread, or runs inline in headless mode.
    /// </summary>
    Task InvokeAsync(Func<Task> asyncAction);

    /// <summary>
    /// Invokes <paramref name="function"/> and returns its result, running on the UI thread when appropriate.
    /// </summary>
    Task<T> InvokeAsync<T>(Func<T> function);

    /// <summary>
    /// Invokes asynchronous <paramref name="asyncFunction"/> and returns its result, running on the UI thread when appropriate.
    /// </summary>
    Task<T> InvokeAsync<T>(Func<Task<T>> asyncFunction);
}
