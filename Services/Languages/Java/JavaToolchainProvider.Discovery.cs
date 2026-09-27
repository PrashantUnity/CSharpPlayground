using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;

public sealed partial class JavaToolchainProvider
{
    private sealed record Candidate(string Path, string Source);

    private async IAsyncEnumerable<Candidate> CandidatesAsync(ToolchainQuery query, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var yielded = new HashSet<string>(_host.IsWindows || _host.IsMacOS ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        var selected = SelectedPath;
        if (!string.IsNullOrWhiteSpace(selected) && _host.FileExists(selected) && yielded.Add(selected))
        {
            yield return new Candidate(selected, "Selected");
        }

        var javaHome = _host.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrWhiteSpace(javaHome))
        {
            var javaHomeBin = Path.Combine(javaHome, "bin", _host.IsWindows ? "java.exe" : "java");
            if (_host.FileExists(javaHomeBin) && yielded.Add(javaHomeBin))
            {
                yield return new Candidate(javaHomeBin, "JAVA_HOME");
            }
        }

        foreach (var projectCandidate in ProjectCandidates(query))
        {
            if (yielded.Add(projectCandidate.Path)) yield return projectCandidate;
        }

        var names = _host.IsWindows ? new[] { "java.exe", "java" } : new[] { "java" };
        var loginPath = await _host.GetLoginShellPathAsync(ct);
        var folders = ExecutableSearch.SplitPath(loginPath, _host.IsWindows)
            .Concat(ExecutableSearch.SplitPath(_host.GetEnvironmentVariable("PATH"), _host.IsWindows))
            .Concat(WellKnownFolders());

        foreach (var path in ExecutableSearch.FindAll(_host, folders, names))
        {
            if (yielded.Add(path)) yield return new Candidate(path, "PATH");
        }
    }

    private IEnumerable<Candidate> ProjectCandidates(ToolchainQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.DocumentFolder)) yield break;

        string? folder = Path.TrimEndingDirectorySeparator(query.DocumentFolder);
        var root = string.IsNullOrWhiteSpace(query.WorkspaceRoot) ? null : Path.TrimEndingDirectorySeparator(query.WorkspaceRoot);
        var insideRoot = root != null && IsUnder(folder, root);

        for (var level = 0; !string.IsNullOrEmpty(folder) && (insideRoot || level < 4); level++)
        {
            var localJdkBin = Path.Combine(folder, ".jdk", "bin", _host.IsWindows ? "java.exe" : "java");
            if (_host.FileExists(localJdkBin))
            {
                yield return new Candidate(localJdkBin, "Project local");
            }

            if (insideRoot && SameFolder(folder, root!)) break;
            folder = Path.GetDirectoryName(folder);
        }
    }

    private IEnumerable<string> WellKnownFolders()
    {
        if (_host.IsWindows)
        {
            var programFiles = _host.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
            var programFilesX86 = _host.GetEnvironmentVariable("ProgramFiles(x86)") ?? @"C:\Program Files (x86)";
            var localAppData = _host.GetEnvironmentVariable("LOCALAPPDATA");

            foreach (var baseDir in new[] { Path.Combine(programFiles, "Java"), Path.Combine(programFiles, "Eclipse Adoptium"), Path.Combine(programFiles, "Microsoft") })
            {
                if (_host.DirectoryExists(baseDir))
                {
                    foreach (var sub in _host.GetDirectories(baseDir))
                    {
                        yield return Path.Combine(sub, "bin");
                    }
                }
            }

            if (!string.IsNullOrEmpty(localAppData))
            {
                var adoptium = Path.Combine(localAppData, "Programs", "Eclipse Adoptium");
                if (_host.DirectoryExists(adoptium))
                {
                    foreach (var sub in _host.GetDirectories(adoptium))
                    {
                        yield return Path.Combine(sub, "bin");
                    }
                }
            }
        }
        else if (_host.IsMacOS)
        {
            const string jvmDir = "/Library/Java/JavaVirtualMachines";
            if (_host.DirectoryExists(jvmDir))
            {
                foreach (var jvm in _host.GetDirectories(jvmDir))
                {
                    yield return Path.Combine(jvm, "Contents", "Home", "bin");
                }
            }

            yield return "/opt/homebrew/opt/openjdk/bin";
            yield return "/usr/local/opt/openjdk/bin";
            yield return "/opt/homebrew/bin";
            yield return "/usr/local/bin";
            yield return "/usr/bin";

            var home = _host.HomeDirectory;
            if (!string.IsNullOrEmpty(home))
            {
                var sdkman = Path.Combine(home, ".sdkman", "candidates", "java");
                if (_host.DirectoryExists(sdkman))
                {
                    foreach (var candidate in _host.GetDirectories(sdkman))
                    {
                        yield return Path.Combine(candidate, "bin");
                    }
                }

                var asdf = Path.Combine(home, ".asdf", "installs", "java");
                if (_host.DirectoryExists(asdf))
                {
                    foreach (var candidate in _host.GetDirectories(asdf))
                    {
                        yield return Path.Combine(candidate, "bin");
                    }
                }
            }
        }
        else
        {
            const string jvmDir = "/usr/lib/jvm";
            if (_host.DirectoryExists(jvmDir))
            {
                foreach (var jvm in _host.GetDirectories(jvmDir))
                {
                    yield return Path.Combine(jvm, "bin");
                }
            }

            yield return "/usr/local/bin";
            yield return "/usr/bin";

            var home = _host.HomeDirectory;
            if (!string.IsNullOrEmpty(home))
            {
                var sdkman = Path.Combine(home, ".sdkman", "candidates", "java");
                if (_host.DirectoryExists(sdkman))
                {
                    foreach (var candidate in _host.GetDirectories(sdkman))
                    {
                        yield return Path.Combine(candidate, "bin");
                    }
                }
            }
        }
    }

    private static bool IsUnder(string path, string parent)
    {
        var p = Path.TrimEndingDirectorySeparator(path);
        var r = Path.TrimEndingDirectorySeparator(parent);
        return p.Length > r.Length && p.StartsWith(r, StringComparison.OrdinalIgnoreCase) &&
               (p[r.Length] == Path.DirectorySeparatorChar || p[r.Length] == Path.AltDirectorySeparatorChar);
    }

    private static bool SameFolder(string a, string b) =>
        string.Equals(Path.TrimEndingDirectorySeparator(a), Path.TrimEndingDirectorySeparator(b), StringComparison.OrdinalIgnoreCase);
}
