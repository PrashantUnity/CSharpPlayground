using System.Diagnostics;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

/// <summary>A file found by <see cref="WorkspaceFileIndex.Find"/>.</summary>
/// <param name="RelativePath">Path from the workspace root, always with <c>/</c> separators.</param>
/// <param name="Name">The file name alone.</param>
public readonly record struct WorkspaceFileMatch(string RelativePath, string Name);

/// <summary>
/// The names of every file in the workspace, held in memory so "Go to File" can search a project of any size at once, the way
/// an IDE's index does. It is built by a background walk that can be cancelled, publishes what it has found so far while a big
/// folder is still being walked (so the first matches don't wait for the last folder), and is rebuilt when the workspace changes.
/// Only names and paths are kept: the index costs about a hundred bytes per file, and searching it never touches the disk.
/// </summary>
public sealed class WorkspaceFileIndex
{
    /// <summary>What a search sees: a finished, unchanging list. A new one replaces it whole, so a search never sees a half-updated list.</summary>
    private sealed class Snapshot(string[] paths, int[] nameStarts, bool truncated)
    {
        public static readonly Snapshot Empty = new([], [], false);

        public string[] Paths { get; } = paths;
        public int[] NameStarts { get; } = nameStarts;
        public bool Truncated { get; } = truncated;
    }

    private const int PublishEvery = 5_000;

    private readonly Func<string, bool> _wanted;
    private readonly int _limit;
    private readonly object _gate = new();
    private CancellationTokenSource? _build;
    private volatile Snapshot _snapshot = Snapshot.Empty;
    private volatile bool _isBuilding;
    private string? _root;

    /// <param name="wanted">Which files belong in the index (what the Explorer lists).</param>
    /// <param name="limit">How many files the index holds at most; a bigger workspace is reported as <see cref="IsTruncated"/>.</param>
    public WorkspaceFileIndex(Func<string, bool> wanted, int limit = 250_000)
    {
        _wanted = wanted;
        _limit = limit;
    }

    /// <summary>The folder the index describes (null before the first build).</summary>
    public string? Root => _root;

    /// <summary>True while a walk is running: the files found so far can already be searched.</summary>
    public bool IsBuilding => _isBuilding;

    /// <summary>How many files are searchable right now.</summary>
    public int Count => _snapshot.Paths.Length;

    /// <summary>True when the workspace holds more files than the index keeps.</summary>
    public bool IsTruncated => _snapshot.Truncated;

    /// <summary>Raised on a background thread whenever a new list is published, and when a walk ends.</summary>
    public event Action? Updated;

    /// <summary>
    /// Starts walking <paramref name="root"/> in the background, replacing any walk still running. The returned task ends when
    /// the walk has (it never faults; a cancelled walk just ends), so a caller may ignore it or wait for it.
    /// </summary>
    public Task RebuildAsync(string root)
    {
        var walk = new CancellationTokenSource();
        CancellationTokenSource? previous;
        bool sameRoot;
        lock (_gate)
        {
            previous = _build;
            _build = walk;
            sameRoot = string.Equals(_root, root, StringComparison.Ordinal);
            _root = root;
            _isBuilding = true;

            // Another folder's files are wrong for this one; the same folder's old list is still a good answer meanwhile.
            if (!sameRoot) _snapshot = Snapshot.Empty;
        }

        previous?.Cancel();
        return Task.Run(() => Build(root, sameRoot, walk));
    }

    private void Build(string root, bool keepOldUntilDone, CancellationTokenSource walk)
    {
        var paths = new List<string>();
        var starts = new List<int>();
        var status = new WorkspaceWalker.WalkStatus();
        try
        {
            foreach (var file in WorkspaceWalker.EnumerateFiles(root, _wanted, _limit, status, walk.Token))
            {
                var relative = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
                paths.Add(relative);
                starts.Add(relative.LastIndexOf('/') + 1);

                // A new folder shows its first matches as soon as they exist; a rebuild of the same folder waits for the end,
                // so the list never shrinks to a fragment in between.
                if (!keepOldUntilDone && paths.Count % PublishEvery == 0) Publish(walk, paths, starts, truncated: false);
            }

            Publish(walk, paths, starts, status.Truncated);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer walk, which reports for itself.
            return;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] Indexing '{root}' failed: {ex.Message}");
        }

        lock (_gate)
        {
            if (ReferenceEquals(_build, walk)) _isBuilding = false;
        }

        Updated?.Invoke();
    }

    private void Publish(CancellationTokenSource walk, List<string> paths, List<int> starts, bool truncated)
    {
        lock (_gate)
        {
            // A walk that has been replaced must not overwrite the newer one's list.
            if (!ReferenceEquals(_build, walk)) return;
            _snapshot = new Snapshot(paths.ToArray(), starts.ToArray(), truncated);
        }

        Updated?.Invoke();
    }

    /// <summary>
    /// The files whose names best match <paramref name="query"/>, best first: an exact name, then a name that starts with it, a
    /// name that contains it, a name that has its letters in order (<c>wsi</c> finds <c>WorkspaceIndex.cs</c>), and last a path
    /// that contains it (so <c>src/models</c> narrows to a folder). Case doesn't matter. Nothing is read from disk.
    /// </summary>
    public IReadOnlyList<WorkspaceFileMatch> Find(string query, int limit = 30)
    {
        query = query.Trim().Replace('\\', '/');
        if (query.Length == 0 || limit <= 0) return [];

        var snapshot = _snapshot;
        var paths = snapshot.Paths;
        var starts = snapshot.NameStarts;
        var q = query.AsSpan();
        var byPath = query.Contains('/');

        // The best `limit` so far, kept sorted best first: `limit` is small, so inserting is cheaper than sorting everything.
        var best = new List<(long Key, int Index)>(limit + 1);
        for (var i = 0; i < paths.Length; i++)
        {
            var key = Score(paths[i], starts[i], q, byPath);
            if (key < 0) continue;
            if (best.Count == limit && key <= best[^1].Key) continue;

            var at = best.Count;
            while (at > 0 && best[at - 1].Key < key) at--;
            best.Insert(at, (key, i));
            if (best.Count > limit) best.RemoveAt(best.Count - 1);
        }

        var result = new List<WorkspaceFileMatch>(best.Count);
        foreach (var (_, index) in best)
        {
            result.Add(new WorkspaceFileMatch(paths[index], paths[index][starts[index]..]));
        }

        return result;
    }

    // A score for one file, or -1 for no match. Shorter paths win ties, so `Program.cs` beats `Program.cs.bak.orig`.
    private static long Score(string path, int nameStart, ReadOnlySpan<char> q, bool byPath)
    {
        var name = path.AsSpan(nameStart);
        long tier;
        if (byPath)
        {
            var at = path.AsSpan().IndexOf(q, StringComparison.OrdinalIgnoreCase);
            if (at < 0) return -1;
            tier = path.AsSpan().EndsWith(q, StringComparison.OrdinalIgnoreCase) ? 600 : 200 - Math.Min(at, 100);
        }
        else if (name.Equals(q, StringComparison.OrdinalIgnoreCase))
        {
            tier = 1000;
        }
        else if (name.StartsWith(q, StringComparison.OrdinalIgnoreCase))
        {
            tier = 800;
        }
        else
        {
            var at = name.IndexOf(q, StringComparison.OrdinalIgnoreCase);
            if (at >= 0)
            {
                tier = 600 - Math.Min(at, 100);
            }
            else if (IsSubsequence(name, q))
            {
                tier = 300;
            }
            else
            {
                var inPath = path.AsSpan().IndexOf(q, StringComparison.OrdinalIgnoreCase);
                if (inPath < 0) return -1;
                tier = 100;
            }
        }

        return tier * 10_000 - Math.Min(path.Length, 9_999);
    }

    private static bool IsSubsequence(ReadOnlySpan<char> text, ReadOnlySpan<char> letters)
    {
        var next = 0;
        foreach (var c in text)
        {
            if (char.ToLowerInvariant(c) == char.ToLowerInvariant(letters[next]) && ++next == letters.Length) return true;
        }

        return false;
    }
}
