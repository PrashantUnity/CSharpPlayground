using System.Diagnostics;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

/// <summary>
/// Walks a workspace folder the way the Explorer should see it: without the folders tools fill with thousands of files
/// nobody opens by hand (a Python virtual environment, <c>__pycache__</c>, <c>node_modules</c>, <c>.git</c>) and without
/// following links, which could lead outside the workspace or round in a circle.
/// </summary>
internal static class WorkspaceWalker
{
    /// <summary>Enough for any real workspace; stops a walk of a huge folder (someone's home) from taking forever.</summary>
    public const int MaxFiles = 5000;

    private static readonly HashSet<string> SkippedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "__pycache__", "site-packages", "node_modules", ".git", ".hg", ".svn", ".mypy_cache", ".pytest_cache", ".ruff_cache", ".ipynb_checkpoints"
    };

    private static readonly HashSet<string> EnvironmentFolderNames = new(StringComparer.OrdinalIgnoreCase) { ".venv", "venv", "env" };

    /// <summary>The files under <paramref name="root"/> that <paramref name="wanted"/> accepts, at most <paramref name="maxFiles"/>.</summary>
    public static List<string> Files(string root, Func<string, bool> wanted, int maxFiles = MaxFiles)
    {
        var found = new List<string>();
        foreach (var folder in Walk(root))
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
                if (found.Count >= maxFiles) return found;
            }
        }

        return found;
    }

    /// <summary>Every folder under <paramref name="root"/> (not the root itself) the Explorer shows.</summary>
    public static List<string> Folders(string root) => Walk(root).Skip(1).ToList();

    /// <summary>True for a folder the walk leaves out.</summary>
    public static bool IsSkipped(string folder)
    {
        var name = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (SkippedFolders.Contains(name)) return true;
        // Only an actual virtual environment: a folder of the user's own that happens to be called "env" stays visible.
        if (EnvironmentFolderNames.Contains(name) && File.Exists(Path.Combine(folder, "pyvenv.cfg"))) return true;

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
    private static IEnumerable<string> Walk(string root)
    {
        if (!Directory.Exists(root)) yield break;

        var pending = new Queue<string>();
        pending.Enqueue(root);
        var visited = 0;
        while (pending.Count > 0)
        {
            var folder = pending.Dequeue();
            yield return folder;
            if (++visited > MaxFiles)
            {
                Debug.WriteLine($"[CSharpEditorPlugin] Stopped walking '{root}' after {MaxFiles} folders.");
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
