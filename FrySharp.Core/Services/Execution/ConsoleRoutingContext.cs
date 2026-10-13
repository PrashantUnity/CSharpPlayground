using System.Text;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Execution;

/// <summary>
/// Routes Console.Out/Error through an AsyncLocal-scoped sink per execution flow.
/// </summary>
internal static class ConsoleRoutingContext
{
    private static readonly AsyncLocal<TextWriter?> _activeSink = new();
    private static readonly AsyncLocal<TextReader?> _activeSource = new();
    private static int _installed;

    public static void EnsureInstalled()
    {
        if (Interlocked.Exchange(ref _installed, 1) != 0) return;

        var realOut = Console.Out;
        var realError = Console.Error;
        var realIn = Console.In;
        Console.SetOut(new RoutingWriter(() => _activeSink.Value ?? realOut));
        Console.SetError(new RoutingWriter(() => _activeSink.Value ?? realError));
        Console.SetIn(new RoutingReader(() => _activeSource.Value ?? realIn));
    }

    /// <summary>Routes Console.Out/Error to <paramref name="sink"/>, and optionally Console.In to <paramref name="source"/>,
    /// for the duration of the returned scope, within this async flow only. Dispose restores whatever was active before.</summary>
    public static IDisposable EnterScope(TextWriter sink, TextReader? source = null)
    {
        EnsureInstalled();
        var previousSink = _activeSink.Value;
        var previousSource = _activeSource.Value;
        _activeSink.Value = sink;
        if (source != null)
        {
            _activeSource.Value = source;
        }

        return new Scope(() =>
        {
            _activeSink.Value = previousSink;
            if (source != null)
            {
                _activeSource.Value = previousSource;
            }
        });
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

    private sealed class RoutingReader : TextReader
    {
        private readonly Func<TextReader> _resolveCurrent;

        public RoutingReader(Func<TextReader> resolveCurrent) => _resolveCurrent = resolveCurrent;

        public override int Read() => _resolveCurrent().Read();
        public override int Read(char[] buffer, int index, int count) => _resolveCurrent().Read(buffer, index, count);
        public override string? ReadLine() => _resolveCurrent().ReadLine();
        public override Task<string?> ReadLineAsync() => _resolveCurrent().ReadLineAsync();
        public override string ReadToEnd() => _resolveCurrent().ReadToEnd();
        public override Task<string> ReadToEndAsync() => _resolveCurrent().ReadToEndAsync();
        public override int Peek() => _resolveCurrent().Peek();
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
