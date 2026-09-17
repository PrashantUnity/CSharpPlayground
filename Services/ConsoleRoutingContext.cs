using System;
using System.IO;
using System.Text;
using System.Threading;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

/// <summary>
/// Routes Console.Out/Error through an AsyncLocal-scoped sink per execution flow.
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
