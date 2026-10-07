using System.Threading;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Activities;

/// <summary>
/// "Latest wins" for one kind of request: <see cref="Begin"/> cancels the request before it. Opening file B while file A
/// is still loading cancels A, so A can never finish later and take the editor away from B. A request checks its token
/// after every await before it touches the screen.
/// </summary>
public sealed class LatestOperation
{
    private readonly object _gate = new();
    private CancellationTokenSource? _current;

    /// <summary>Starts a new request and cancels the previous one. The token is cancelled when the next request begins.</summary>
    public CancellationToken Begin()
    {
        var next = new CancellationTokenSource();
        CancellationTokenSource? previous;
        lock (_gate)
        {
            previous = _current;
            _current = next;
        }

        // Not disposed: the previous request may still be awaiting with its token, and an unlinked source holds nothing.
        previous?.Cancel();
        return next.Token;
    }

    /// <summary>Cancels the running request, if any, without starting another.</summary>
    public void CancelCurrent()
    {
        CancellationTokenSource? current;
        lock (_gate)
        {
            current = _current;
            _current = null;
        }

        current?.Cancel();
    }

    /// <summary>Whether <paramref name="token"/> belongs to the newest request and it was not cancelled.</summary>
    public bool IsLatest(CancellationToken token)
    {
        lock (_gate)
        {
            return _current != null && _current.Token == token && !token.IsCancellationRequested;
        }
    }
}
