using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaEdit.Document;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;

/// <summary>
/// Keeps a Roslyn syntax tree of an editor's C# document, parsed off the UI thread. Each edit is recorded on the UI
/// thread (a few fields), and the background reparses incrementally (Roslyn reuses everything the edit didn't touch),
/// so the work per keystroke doesn't grow with the file. Until the tree catches up with the latest edits, roles are
/// read from the last tree and moved to where that text is now; <see cref="Updated"/> says when a newer tree is ready.
/// </summary>
public sealed class CSharpLiveSyntax : IDisposable
{
    private sealed record Snapshot(SyntaxTree Tree, SourceText Text, ITextSourceVersion Version, CSharpDeclarations Names);

    private readonly TextDocument _document;
    private readonly object _gate = new();
    private readonly List<TextChange> _pending = new();
    private ITextSourceVersion? _pendingVersion;
    private ITextSource? _initialText;
    private Snapshot? _snapshot;
    private Task _worker = Task.CompletedTask;
    private bool _running;
    private bool _disposed;
    private int _editsSinceFullCollect; // the background worker's own

    // Below this a whole-file pass is cheap (well under a millisecond per thousand lines); every so many edits one is
    // made anyway.
    private const int FullCollectLength = 100_000;
    private const int FullCollectEvery = 64;

    public CSharpLiveSyntax(TextDocument document)
    {
        _document = document;
        _document.Changed += OnChanged;
        lock (_gate)
        {
            _initialText = document.CreateSnapshot();
            _pendingVersion = document.Version;
            Schedule();
        }
    }

    /// <summary>A newer tree is ready (raised on a background thread).</summary>
    public event Action? Updated;

    /// <summary>How long the last parse took on its background thread (tools and tests; waiting to start isn't counted).</summary>
    public TimeSpan LastParseDuration { get; private set; }

    /// <summary>Whether a tree exists yet.</summary>
    public bool HasTree => Volatile.Read(ref _snapshot) != null;

    /// <summary>Completes when every edit made so far is parsed (tests and tools).</summary>
    public Task WhenIdle()
    {
        lock (_gate) return _worker;
    }

    /// <summary>
    /// Waits until every edit made so far is parsed, for tools and tests on the document's own thread (an await would
    /// come back on another thread, and the document only answers its own). Returns false on timeout.
    /// </summary>
    public bool WaitUntilIdle(TimeSpan timeout) => WhenIdle().Wait(timeout);

    private void OnChanged(object? sender, DocumentChangeEventArgs e)
    {
        lock (_gate)
        {
            if (_disposed) return;
            _pending.Add(new TextChange(new TextSpan(e.Offset, e.RemovalLength), e.InsertedText.Text));
            _pendingVersion = _document.Version;
            Schedule();
        }
    }

    // Under _gate.
    private void Schedule()
    {
        if (_running) return;
        _running = true;
        _worker = Task.Run(Work);
    }

    private void Work()
    {
        try
        {
            Parse();
        }
        catch (Exception)
        {
            // A tree that can't be made leaves the grammar's colours; the next edit tries again.
            lock (_gate) _running = false;
        }
    }

    private void Parse()
    {
        while (true)
        {
            ITextSource? initial;
            TextChange[] changes;
            ITextSourceVersion? version;
            lock (_gate)
            {
                if (_disposed || (_initialText == null && _pending.Count == 0))
                {
                    _running = false;
                    return;
                }

                initial = _initialText;
                _initialText = null;
                changes = _pending.ToArray();
                _pending.Clear();
                version = _pendingVersion;
            }

            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            var previous = Volatile.Read(ref _snapshot);
            SourceText text;
            SyntaxTree tree;
            if (initial != null || previous == null)
            {
                // The first parse: the text when the document was attached, then the edits made since.
                text = SourceText.From(initial?.Text ?? string.Empty);
                foreach (var change in changes) text = text.WithChanges(change);
                tree = CSharpClassifier.Parse(text);
            }
            else
            {
                text = previous.Text;
                foreach (var change in changes) text = text.WithChanges(change);
                tree = previous.Tree.WithChangedText(text);
            }

            // The names the file declares: from the edited parts only (the work follows the edit, not the file), with a
            // full pass now and then so names that were removed don't linger, and always for a small file.
            var root = tree.GetRoot();
            CSharpDeclarations names;
            if (previous == null || initial != null || text.Length < FullCollectLength || ++_editsSinceFullCollect >= FullCollectEvery)
            {
                names = CSharpDeclarations.Collect(root);
                _editsSinceFullCollect = 0;
            }
            else
            {
                names = previous.Names.With(root, tree.GetChangedSpans(previous.Tree));
            }

            Volatile.Write(ref _snapshot, new Snapshot(tree, text, version!, names));
            LastParseDuration = System.Diagnostics.Stopwatch.GetElapsedTime(started);
            Updated?.Invoke();
        }
    }

    /// <summary>
    /// The roles in the document range [<paramref name="start"/>, <paramref name="end"/>) of the document as it is now,
    /// in order, never overlapping (empty until the first tree is ready). UI thread.
    /// </summary>
    public List<CSharpClassifiedSpan> Classify(int start, int end)
    {
        var result = new List<CSharpClassifiedSpan>();
        var snapshot = Volatile.Read(ref _snapshot);
        if (snapshot == null || end <= start) return result;

        var current = _document.Version;
        var fresh = current == null || snapshot.Version.CompareAge(current) == 0;
        var oldStart = fresh ? start : current!.MoveOffsetTo(snapshot.Version, start, AnchorMovementType.BeforeInsertion);
        var oldEnd = fresh ? end : current!.MoveOffsetTo(snapshot.Version, end, AnchorMovementType.AfterInsertion);
        oldStart = Math.Clamp(oldStart, 0, snapshot.Text.Length);
        oldEnd = Math.Clamp(oldEnd, oldStart, snapshot.Text.Length);
        if (oldEnd <= oldStart) return result;

        var spans = CSharpClassifier.Classify(snapshot.Tree, TextSpan.FromBounds(oldStart, oldEnd), snapshot.Names);
        if (fresh) return spans;

        // Older tree: move each run to where its text is now (text typed since keeps the grammar's colour meanwhile).
        foreach (var span in spans)
        {
            var newStart = Math.Max(start, snapshot.Version.MoveOffsetTo(current!, span.Start, AnchorMovementType.AfterInsertion));
            var newEnd = Math.Min(end, snapshot.Version.MoveOffsetTo(current!, span.End, AnchorMovementType.BeforeInsertion));
            if (newEnd - newStart != span.Length) continue; // the run was edited: wait for the new tree
            if (result.Count > 0 && result[^1].End > newStart) continue;
            result.Add(span with { Start = newStart });
        }

        return result;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _pending.Clear();
        }

        _document.Changed -= OnChanged;
    }
}
