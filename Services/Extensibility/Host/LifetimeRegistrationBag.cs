using System;
using System.Collections.Generic;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Host;

/// <summary>
/// Tracks all IDisposable instances (commands, event hooks, UI contribution points)
/// registered by an extension or script session. When the session is unloaded or reloaded,
/// disposing this bag cleanly tears down all registrations without memory leaks.
/// </summary>
public sealed class LifetimeRegistrationBag : IDisposable
{
    private readonly List<IDisposable> _items = new();
    private readonly object _lock = new();
    private bool _isDisposed;

    public void Track(IDisposable disposable)
    {
        ArgumentNullException.ThrowIfNull(disposable);

        lock (_lock)
        {
            if (_isDisposed)
            {
                disposable.Dispose();
                return;
            }
            _items.Add(disposable);
        }
    }

    public void Track(Action disposeAction)
    {
        ArgumentNullException.ThrowIfNull(disposeAction);
        Track(new ActionDisposable(disposeAction));
    }

    public void Dispose()
    {
        List<IDisposable> toDispose;

        lock (_lock)
        {
            if (_isDisposed) return;
            _isDisposed = true;
            toDispose = new List<IDisposable>(_items);
            _items.Clear();
        }

        // Dispose in reverse order of registration
        for (int i = toDispose.Count - 1; i >= 0; i--)
        {
            try
            {
                toDispose[i].Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LifetimeRegistrationBag] Error disposing registration: {ex.Message}");
            }
        }
    }

    private sealed class ActionDisposable : IDisposable
    {
        private Action? _action;
        public ActionDisposable(Action action) => _action = action;
        public void Dispose()
        {
            var act = System.Threading.Interlocked.Exchange(ref _action, null);
            act?.Invoke();
        }
    }
}
