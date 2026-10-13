namespace PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

/// <summary>Finds programs by name in PATH-style folder lists and well-known install folders.</summary>
public static class ExecutableSearch
{
    /// <summary>The folders of a PATH value, in order, without blanks or repeats.</summary>
    public static IReadOnlyList<string> SplitPath(string? pathValue, IHostEnvironment host) =>
        SplitPath(pathValue, host.IsWindows);

    /// <summary>The folders of a PATH value, in order, without blanks or repeats.</summary>
    public static IReadOnlyList<string> SplitPath(string? pathValue, bool isWindows)
    {
        if (string.IsNullOrWhiteSpace(pathValue)) return Array.Empty<string>();
        var separator = isWindows ? ';' : ':';
        return pathValue
            .Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.Trim('"'))
            .Where(p => p.Length > 0)
            .Distinct(isWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary><c>python</c> → <c>python.exe</c> on Windows; unchanged elsewhere.</summary>
    public static string ExecutableName(string name, bool isWindows) =>
        isWindows && !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name + ".exe" : name;

    /// <summary>Returns the directory part of a path regardless of whether it uses forward or backward slashes.</summary>
    public static string? GetDirectoryName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var lastSlash = path.LastIndexOfAny(['/', '\\']);
        return lastSlash >= 0 ? path[..lastSlash] : null;
    }

    /// <summary>Returns the file name part of a path regardless of whether it uses forward or backward slashes.</summary>
    public static string? GetFileName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var lastSlash = path.LastIndexOfAny(['/', '\\']);
        return lastSlash >= 0 ? path[(lastSlash + 1)..] : path;
    }

    /// <summary>Combines directory components respecting the host environment's path convention.</summary>
    public static string Combine(IHostEnvironment host, string folder, string sub)
    {
        var sep = host.DirectorySeparatorChar;
        var alt = sep == '/' ? '\\' : '/';
        return $"{folder.TrimEnd('/', '\\')}{sep}{sub.TrimStart('/', '\\').Replace(alt, sep)}";
    }

    /// <summary>Combines directory components respecting the host environment's path convention.</summary>
    public static string Combine(IHostEnvironment host, string folder, string sub1, string sub2)
    {
        var sep = host.DirectorySeparatorChar;
        var alt = sep == '/' ? '\\' : '/';
        return $"{folder.TrimEnd('/', '\\')}{sep}{sub1.Trim('/', '\\').Replace(alt, sep)}{sep}{sub2.TrimStart('/', '\\').Replace(alt, sep)}";
    }

    /// <summary>Combines directory components respecting the host environment's path convention.</summary>
    public static string Combine(IHostEnvironment host, string folder, string sub1, string sub2, string sub3)
    {
        var sep = host.DirectorySeparatorChar;
        var alt = sep == '/' ? '\\' : '/';
        return $"{folder.TrimEnd('/', '\\')}{sep}{sub1.Trim('/', '\\').Replace(alt, sep)}{sep}{sub2.Trim('/', '\\').Replace(alt, sep)}{sep}{sub3.TrimStart('/', '\\').Replace(alt, sep)}";
    }

    /// <summary>Every existing <c>folder/name</c>, folders first then names, without repeats.</summary>
    public static IEnumerable<string> FindAll(IHostEnvironment host, IEnumerable<string> folders, IReadOnlyList<string> names)
    {
        var seen = new HashSet<string>(host.IsWindows || host.IsMacOS ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var folder in folders)
        {
            if (string.IsNullOrWhiteSpace(folder)) continue;
            foreach (var name in names)
            {
                string candidate;
                try
                {
                    candidate = Combine(host, folder, ExecutableName(name, host.IsWindows));
                }
                catch (ArgumentException)
                {
                    continue; // a PATH entry with characters no path can have
                }

                if (host.FileExists(candidate) && seen.Add(candidate)) yield return candidate;
            }
        }
    }
}
