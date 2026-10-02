using System;
using System.Text;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

/// <summary>
/// Carries one run's console output from the running script to the Terminal panel, each piece exactly once and in the
/// order it was written. The script writes on its own thread (<see cref="Write"/>) and every write queues a
/// <see cref="Flush"/> for the UI thread; when the run ends, <see cref="Complete"/> shows whatever is still waiting, so
/// the queued flushes that run after it find nothing left to add.
/// </summary>
/// <remarks>
/// The end of a run can't just count on its queued writes having been shown: Avalonia resumes an awaiting method at
/// Normal priority, ahead of work that <c>Dispatcher.UIThread.Post</c> queued at Default, so a script that prints and
/// then finishes reaches its completion code before its last lines are on screen.
/// </remarks>
public sealed class RunConsoleRelay
{
    private readonly object _gate = new();
    private readonly StringBuilder _written = new();
    private readonly Action<string> _show;
    private readonly Action<Action> _queue;
    private int _shown;

    /// <param name="show">Adds text to the Terminal. Gets every piece once, in order, one call at a time.</param>
    /// <param name="queue">Runs an action on the UI thread: later when called from elsewhere, or right away.</param>
    public RunConsoleRelay(Action<string> show, Action<Action> queue)
    {
        _show = show;
        _queue = queue;
    }

    /// <summary>For the script's live console callback: keeps <paramref name="text"/> and queues showing it.</summary>
    public void Write(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        lock (_gate)
        {
            _written.Append(text);
        }
        _queue(Flush);
    }

    /// <summary>Shows everything written that isn't shown yet; calling it again, from any thread, adds nothing twice.</summary>
    public void Flush()
    {
        lock (_gate)
        {
            if (_shown == _written.Length) return;
            string pending = _written.ToString(_shown, _written.Length - _shown);
            _shown = _written.Length;
            _show(pending);
        }
    }

    /// <summary>
    /// The run has ended: shows what's still waiting, then <paramref name="finalOutput"/> (the run's whole console output,
    /// if the runner reports one) when it holds text that never came through <see cref="Write"/>.
    /// </summary>
    public void Complete(string? finalOutput = null)
    {
        lock (_gate)
        {
            Flush();
            if (string.IsNullOrEmpty(finalOutput) || _written.ToString().Contains(finalOutput, StringComparison.Ordinal)) return;
            _written.Append(finalOutput);
            _shown = _written.Length;
            _show(finalOutput);
        }
    }
}
