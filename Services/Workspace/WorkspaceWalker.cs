using System.Diagnostics;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Workspace;

/// <summary>
/// Walks a workspace folder the way the Explorer should see it: without the folders tools fill with thousands of files
/// nobody opens by hand (a Python virtual environment, <c>__pycache__</c>, <c>node_modules</c>, <c>.git</c>) and without
/// following links, which could lead outside the workspace or round in a circle.
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
        "__pycache__", "site-packages", "node_modules", ".git", ".hg", ".svn", ".mypy_cache", ".pytest_cache", ".ruff_cache", ".ipynb_checkpoints", ".vs", ".idea"
    };

    private static readonly HashSet<string> EnvironmentFolderNames = new(StringComparer.OrdinalIgnoreCase) { ".venv", "venv", "env" };

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
    public static List<string> Folders(string root, out bool truncated, int maxFolders = MaxFiles)
    {
        var walk = new WalkState(maxFolders);
        var folders = Walk(root, walk).Skip(1).ToList();
        truncated = walk.Truncated;
        return folders;
    }

    private sealed class WalkState(int maxFolders)
    {
        public int MaxFolders { get; } = maxFolders;
        public bool Truncated { get; set; }
    }

    /// <summary>
    /// True when <paramref name="path"/> is, or lies inside, a folder the walk leaves out (.git, node_modules, ...), judged by
    /// name alone with no file access, so it is cheap enough to run for every file-system event.
    /// </summary>
    public static bool IsInsideSkippedFolder(string path, string root)
    {
        var relative = Path.GetRelativePath(root, path);
        if (relative.StartsWith("..", StringComparison.Ordinal)) return false;

        foreach (var segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (SkippedFolders.Contains(segment) || EnvironmentFolderNames.Contains(segment)) return true;
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
            }
        }
    }
}
