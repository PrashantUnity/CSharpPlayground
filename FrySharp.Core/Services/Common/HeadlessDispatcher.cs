namespace PdfEditorApp.Plugins.CSharpEditor.Services.Common;

/// <summary>
/// Headless implementation of <see cref="IDispatcher"/> for CLI tools, server sessions, and unit tests
/// where no Avalonia UI thread pump is active. All dispatches run synchronously or inline.
/// </summary>
public sealed class HeadlessDispatcher : IDispatcher
{
    public static readonly HeadlessDispatcher Instance = new();

    public bool HasLiveUiLifetime => false;

    public void RunOnUi(Action action) => action();

    // Like a UI dispatcher, a failure is reported through the returned task, never thrown at the caller.
    public Task InvokeAsync(Action action)
    {
        try
        {
            action();
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            return Task.FromException(ex);
        }
    }

    public Task InvokeAsync(Func<Task> asyncAction)
    {
        try
        {
            return asyncAction();
        }
        catch (Exception ex)
        {
            return Task.FromException(ex);
        }
    }

    public Task<T> InvokeAsync<T>(Func<T> function)
    {
        try
        {
            return Task.FromResult(function());
        }
        catch (Exception ex)
        {
            return Task.FromException<T>(ex);
        }
    }

    public Task<T> InvokeAsync<T>(Func<Task<T>> asyncFunction)
    {
        try
        {
            return asyncFunction();
        }
        catch (Exception ex)
        {
            return Task.FromException<T>(ex);
        }
    }
}
