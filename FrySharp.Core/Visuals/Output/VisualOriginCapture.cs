namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Output;

/// <summary>
/// For tests and diagnostics: while one is open, it keeps the C# model each visual shown in its scope was made from (a
/// tracker's options, a parsed chart), so the spec can be checked against it. It flows with the code that opened it,
/// as the display scope does, and nothing is kept when none is open.
/// </summary>
internal sealed class VisualOriginCapture : IDisposable
{
    private static readonly AsyncLocal<VisualOriginCapture?> Current = new();

    private readonly VisualOriginCapture? _previous;
    private readonly Dictionary<VisualOutput, object> _origins = new(ReferenceEqualityComparer.Instance);
    private bool _disposed;

    private VisualOriginCapture(VisualOriginCapture? previous) => _previous = previous;

    public static VisualOriginCapture Begin()
    {
        var capture = new VisualOriginCapture(Current.Value);
        Current.Value = capture;
        return capture;
    }

    internal static void Record(VisualOutput output, object origin)
    {
        if (Current.Value is not { } capture) return;
        lock (capture._origins) capture._origins[output] = origin;
    }

    /// <summary>The model <paramref name="output"/> was made from, if it came from one of type <typeparamref name="T"/>.</summary>
    public T? OriginOf<T>(VisualOutput? output) where T : class
    {
        if (output == null) return null;
        lock (_origins) return _origins.TryGetValue(output, out var origin) ? origin as T : null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Current.Value = _previous;
    }
}
