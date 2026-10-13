namespace FrySharp.Sidecar.Protocol;

/// <summary>
/// Keeps every file the sidecar touches safely inside the workspace. A path from the client is relative to the workspace root,
/// with '/' separators; it is refused when it is absolute, climbs out with "..", or reaches outside through a symbolic
/// link anywhere along the way.
/// </summary>
public sealed class WorkspacePaths
{
    private const int MaxLinkDepth = 40;
    private static readonly char Separator = Path.DirectorySeparatorChar;

    public WorkspacePaths(string root)
    {
        Root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        RealRoot = RealPath(Root, 0);
    }

    /// <summary>The workspace folder as the user named it.</summary>
    public string Root { get; }

    /// <summary>The workspace folder with every link along its path resolved.</summary>
    public string RealRoot { get; }

    /// <summary>
    /// The full path of <paramref name="relativePath"/> inside the workspace, or false when it isn't inside. The root
    /// itself counts only when <paramref name="allowRoot"/> is set: nothing may delete or rename the workspace.
    /// </summary>
    public bool TryResolve(string? relativePath, out string fullPath, bool allowRoot = false)
    {
        fullPath = string.Empty;
        if (relativePath == null || relativePath.Contains('\0')) return false;

        var normalized = relativePath.Replace('\\', '/');
        if (normalized.StartsWith('/') || Path.IsPathRooted(normalized) || (OperatingSystem.IsWindows() && normalized.Contains(':')))
        {
            return false;
        }

        normalized = normalized.TrimEnd('/');
        if (normalized.Length == 0 || normalized == ".")
        {
            fullPath = Root;
            return allowRoot;
        }

        string candidate;
        try
        {
            candidate = Path.GetFullPath(Path.Combine(Root, normalized.Replace('/', Separator)));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        if (string.Equals(candidate, Root, StringComparison.Ordinal))
        {
            fullPath = Root;
            return allowRoot;
        }

        if (!candidate.StartsWith(Root + Separator, StringComparison.Ordinal)) return false;

        var real = RealPath(candidate, 0);
        if (!string.Equals(real, RealRoot, StringComparison.Ordinal) && !real.StartsWith(RealRoot + Separator, StringComparison.Ordinal))
        {
            return false;
        }

        fullPath = candidate;
        return true;
    }

    /// <summary>The path relative to the workspace root, with '/' separators ("" for the root).</summary>
    public string ToRelative(string fullPath)
    {
        var relative = Path.GetRelativePath(Root, fullPath);
        return relative == "." ? string.Empty : relative.Replace(Separator, '/');
    }

    private static string RealPath(string fullPath, int depth)
    {
        if (depth > MaxLinkDepth) throw new IOException($"Too many symbolic links resolving '{fullPath}'.");

        var root = Path.GetPathRoot(fullPath) ?? string.Empty;
        var parts = fullPath[root.Length..].Split(Separator, StringSplitOptions.RemoveEmptyEntries);
        var current = root;
        for (var i = 0; i < parts.Length; i++)
        {
            var next = Path.Combine(current, parts[i]);
            FileSystemInfo info = new FileInfo(next);
            if (!info.Exists)
            {
                info = new DirectoryInfo(next);
                if (!info.Exists && info.LinkTarget == null)
                {
                    return Path.Combine([next, .. parts[(i + 1)..]]);
                }
            }

            if (info.LinkTarget != null)
            {
                var target = info.ResolveLinkTarget(returnFinalTarget: true);
                next = target == null
                    ? Path.GetFullPath(info.LinkTarget, current)
                    : RealPath(target.FullName, depth + 1);
            }

            current = next;
        }

        return Path.TrimEndingDirectorySeparator(current);
    }
}
