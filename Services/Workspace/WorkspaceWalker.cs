using System.Diagnostics;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Workspace;

/// <summary>
/// Walks a workspace folder without going into the folders tools fill with thousands of files nobody opens by hand (a
/// Python virtual environment, <c>__pycache__</c>, <c>node_modules</c>, <c>.git</c>) and without following links, which
/// could lead outside the workspace or round in a circle. The Explorer still shows those folders (all but VS Code's hidden
/// ones), closed: see <see cref="Folders(string, out bool, out List{string}, int)"/> and <see cref="IsHiddenInExplorer"/>.
/// </summary>
internal static class WorkspaceWalker
{
    /// <summary>
    /// Enough for a big project; stops a walk of a huge folder (someone's home) from taking forever. A folder bigger than this
    /// is shown cut off, and the Explorer says so (see the walks' <c>truncated</c> result): it must never be a silent limit.
    /// </summary>
    public const int MaxFiles = 20_000;

    private static readonly HashSet<string> SkippedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "__pycache__", "site-packages", "node_modules", ".git", ".hg", ".svn", ".mypy_cache", ".pytest_cache", ".ruff_cache", ".ipynb_checkpoints", ".vs", ".idea", ".frysharp", "bin", "obj"
    };

    private static readonly HashSet<string> EnvironmentFolderNames = new(StringComparer.OrdinalIgnoreCase) { ".venv", "venv", "env" };

    // What VS Code's Explorer hides by default (files.exclude): everything else in the folder is shown. The folders the
    // walk skips for speed (node_modules, bin, .venv, ...) are still shown, closed, and listed when opened.
    private static readonly HashSet<string> ExplorerHiddenFolders = new(StringComparer.OrdinalIgnoreCase) { ".git", ".svn", ".hg", "CVS" };
    private static readonly HashSet<string> ExplorerHiddenFiles = new(StringComparer.OrdinalIgnoreCase) { ".DS_Store", "Thumbs.db" };

    /// <summary>True for a folder VS Code's Explorer hides (.git, .svn, .hg, CVS).</summary>
    public static bool IsHiddenInExplorer(string folder) =>
        ExplorerHiddenFolders.Contains(Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));

    /// <summary>True for a file VS Code's Explorer hides (.DS_Store, Thumbs.db): every other file is shown.</summary>
    public static bool IsExplorerFile(string path) => !ExplorerHiddenFiles.Contains(Path.GetFileName(path));

    /// <summary>The files under <paramref name="root"/> that <paramref name="wanted"/> accepts, at most <paramref name="maxFiles"/>.</summary>
    public static List<string> Files(string root, Func<string, bool> wanted, int maxFiles = MaxFiles) =>
        Files(root, wanted, out _, maxFiles);

    /// <param name="truncated">True when the walk stopped early because the folder holds more than <paramref name="maxFiles"/> files (or folders), so the list is not everything.</param>
    public static List<string> Files(string root, Func<string, bool> wanted, out bool truncated, int maxFiles = MaxFiles)
    {
        var found = new List<string>();
        var walk = new WalkState(maxFiles);
        foreach (var folder in Walk(root, walk))
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(folder);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                if (!wanted(file)) continue;
                found.Add(file);

                // One more than the limit proves there was more: a folder of exactly the limit is not truncated.
                if (found.Count > maxFiles)
                {
                    found.RemoveAt(found.Count - 1);
                    truncated = true;
                    return found;
                }
            }
        }

        truncated = walk.Truncated;
        return found;
    }

    /// <summary>Whether a streamed walk stopped early; set by <see cref="EnumerateFiles"/> once it has finished.</summary>
    public sealed class WalkStatus
    {
        public bool Truncated { get; set; }
    }

    /// <summary>
    /// The files under <paramref name="root"/> that <paramref name="wanted"/> accepts, one at a time as the walk finds them, so a
    /// caller (the workspace file index) can use the first ones long before a huge folder has been walked to the end.
    /// Stops at <paramref name="limit"/> files (or folders), setting <see cref="WalkStatus.Truncated"/>.
    /// </summary>
    public static IEnumerable<string> EnumerateFiles(string root, Func<string, bool> wanted, int limit, WalkStatus status, CancellationToken cancellationToken = default)
    {
        var walk = new WalkState(limit);
        var count = 0;
        foreach (var folder in Walk(root, walk))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string[] files;
            try
            {
                files = Directory.GetFiles(folder);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                if (!wanted(file)) continue;
                if (++count > limit)
                {
                    status.Truncated = true;
                    yield break;
                }

                yield return file;
            }
        }

        if (walk.Truncated) status.Truncated = true;
    }

    /// <summary>Every folder under <paramref name="root"/> (not the root itself) the Explorer shows.</summary>
    public static List<string> Folders(string root) => Folders(root, out _);

    /// <param name="truncated">True when the walk stopped early because the folder holds more than <paramref name="maxFolders"/> folders, so the list is not everything.</param>
    public static List<string> Folders(string root, out bool truncated, int maxFolders = MaxFiles) =>
        Folders(root, out truncated, out _, maxFolders);

    /// <param name="closed">
    /// The folders the walk did not go into but the Explorer shows (node_modules, bin, a linked folder, ...): listed when
    /// they are opened. Found in the same pass, at no extra cost.
    /// </param>
    public static List<string> Folders(string root, out bool truncated, out List<string> closed, int maxFolders = MaxFiles)
    {
        var walk = new WalkState(maxFolders) { Closed = new List<string>() };
        var folders = Walk(root, walk).Skip(1).ToList();
        truncated = walk.Truncated;
        closed = walk.Closed;
        return folders;
    }

    private sealed class WalkState(int maxFolders)
    {
        public int MaxFolders { get; } = maxFolders;
        public bool Truncated { get; set; }

        // When set: the skipped folders the Explorer still shows.
        public List<string>? Closed { get; init; }
    }

    /// <summary>
    /// True when <paramref name="path"/> lies inside a folder the walk leaves out (.git, node_modules, ...), judged by name
    /// alone with no file access, so it is cheap enough to run for every file-system event. The folder itself is not inside
    /// it: the Explorer shows node_modules (closed), so its appearing or going matters.
    /// </summary>
    public static bool IsInsideSkippedFolder(string path, string root)
    {
        var relative = Path.GetRelativePath(root, path);
        if (relative.StartsWith("..", StringComparison.Ordinal)) return false;

        var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (SkippedFolders.Contains(segments[i]) || EnvironmentFolderNames.Contains(segments[i])) return true;
        }

        return false;
    }

    /// <summary>True for a folder the walk leaves out.</summary>
    public static bool IsSkipped(string folder)
    {
        var name = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (SkippedFolders.Contains(name)) return true;
        // Only an actual virtual environment: a folder of the user's own that happens to be called "env" stays visible.
        if (EnvironmentFolderNames.Contains(name) && File.Exists(Path.Combine(folder, "pyvenv.cfg"))) return true;
        // Cargo's build output: only where a Cargo.toml sits beside it, so a folder of the user's own called "target" stays visible.
        if (name.Equals("target", StringComparison.OrdinalIgnoreCase) &&
            Path.GetDirectoryName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) is { Length: > 0 } parent &&
            File.Exists(Path.Combine(parent, "Cargo.toml")))
        {
            return true;
        }

        try
        {
            return new DirectoryInfo(folder).Attributes.HasFlag(FileAttributes.ReparsePoint);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    // The root, then every folder below it that isn't skipped, parents before children.
    private static IEnumerable<string> Walk(string root, WalkState state)
    {
        if (!Directory.Exists(root)) yield break;

        var pending = new Queue<string>();
        pending.Enqueue(root);
        var visited = 0;
        while (pending.Count > 0)
        {
            var folder = pending.Dequeue();
            yield return folder;
            if (++visited > state.MaxFolders)
            {
                state.Truncated = true;
                Debug.WriteLine($"[CSharpEditorPlugin] Stopped walking '{root}' after {state.MaxFolders} folders.");
                yield break;
            }

            string[] children;
            try
            {
                children = Directory.GetDirectories(folder);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var child in children)
            {
                if (!IsSkipped(child)) pending.Enqueue(child);
                else if (state.Closed != null && !IsHiddenInExplorer(child)) state.Closed.Add(child);
            }
        }
    }
}
