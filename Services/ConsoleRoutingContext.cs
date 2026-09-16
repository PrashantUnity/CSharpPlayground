using System;
using System.IO;
using System.Text;
using System.Threading;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

/// <summary>
/// Routes Console.Out/Error through an AsyncLocal-scoped sink instead of a raw Console.SetOut/SetError
/// swap-and-restore. Mirrors InteractiveDisplayContext exactly: a single routing TextWriter is
/// installed on Console.Out/Error once, and each execution enters its own AsyncLocal-scoped sink,
/// which flows naturally with that execution's own async call stack. Since AsyncLocal correctly
/// isolates concurrent, unrelated executions from each other, no cross-execution lock is needed —
/// this replaces the old ConsoleRedirectionGate, which had to hold a process-wide semaphore for a
/// cell's *entire* execution (not just the swap) to stay correct, and as a result serialized every
/// notebook tab and the Code Studio behind whichever one happened to be running.
///
/// Known, accepted trade-off: a raw `new Thread(...)` or `ThreadPool.UnsafeQueueUserWorkItem` started
/// *by user script code* does not inherit the flowed AsyncLocal, so Console output from such a thread
/// falls through to the real console captured at install time (a no-op sink in the standalone Runner,
/// which has no attached console). This is the same pre-existing limitation InteractiveDisplayContext/
/// Display.* already has for raw threads — not a new regression.
/// </summary>
internal static class ConsoleRoutingContext
{
    private static readonly AsyncLocal<TextWriter?> _activeSink = new();
    private static int _installed;

    public static void EnsureInstalled()
    {
        if (Interlocked.Exchange(ref _installed, 1) != 0) return;

        var realOut = Console.Out;
        var realError = Console.Error;
        Console.SetOut(new RoutingWriter(() => _activeSink.Value ?? realOut));
        Console.SetError(new RoutingWriter(() => _activeSink.Value ?? realError));
    }

    /// <summary>Routes Console.Out/Error to <paramref name="sink"/> for the duration of the returned
    /// scope, within this async flow only. Dispose restores whatever sink (if any) was active before.</summary>
    public static IDisposable EnterScope(TextWriter sink)
    {
        EnsureInstalled();
        var previous = _activeSink.Value;
        _activeSink.Value = sink;
        return new Scope(() => _activeSink.Value = previous);
    }

    private sealed class RoutingWriter : TextWriter
    {
        private readonly Func<TextWriter> _resolveCurrent;

        public RoutingWriter(Func<TextWriter> resolveCurrent) => _resolveCurrent = resolveCurrent;

        public override Encoding Encoding => _resolveCurrent().Encoding;
        public override void Write(char value) => _resolveCurrent().Write(value);
        public override void Write(string? value) => _resolveCurrent().Write(value);
        public override void Write(char[] buffer, int index, int count) => _resolveCurrent().Write(buffer, index, count);
        public override void WriteLine() => _resolveCurrent().WriteLine();
        public override void WriteLine(string? value) => _resolveCurrent().WriteLine(value);
        public override void Flush() => _resolveCurrent().Flush();
    }

    private sealed class Scope : IDisposable
    {
        private readonly Action _onDispose;
        private bool _disposed;

        public Scope(Action onDispose) => _onDispose = onDispose;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _onDispose();
        }
    }
}
