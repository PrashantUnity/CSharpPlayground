namespace PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;

/// <summary>
/// What a C# cell declared with <c>using</c> (see <see cref="CellUsingDeclarations"/>): disposed when the cell has run,
/// last first, as a using declaration would at the end of its scope; also when the cell fails. The kernel opens a scope
/// around each cell; the cell's code adds to it.
/// </summary>
public static class CellDisposables
{
    private static readonly AsyncLocal<Scope?> Current = new();

    /// <summary>Starts collecting a cell's disposables; disposing the scope disposes them.</summary>
    public static Scope EnterScope()
    {
        var scope = new Scope(Current.Value);
        Current.Value = scope;
        return scope;
    }

    /// <summary>Called by a cell's rewritten <c>using var</c>: disposes <paramref name="value"/> when the cell has run.</summary>
    public static T Track<T>(T value)
    {
        if (value is IAsyncDisposable or IDisposable) Current.Value?.Add(value);
        return value;
    }

    public sealed class Scope(Scope? outer) : IAsyncDisposable
    {
        private readonly Stack<object> _owned = new();

        internal void Add(object value)
        {
            lock (_owned) _owned.Push(value);
        }

        public async ValueTask DisposeAsync()
        {
            Current.Value = outer;
            while (true)
            {
                object next;
                lock (_owned)
                {
                    if (!_owned.TryPop(out next!)) return;
                }

                if (next is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                else ((IDisposable)next).Dispose();
            }
        }
    }
}
