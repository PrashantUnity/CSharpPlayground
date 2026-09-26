using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.JavaScript;

public sealed partial class JavaScriptToolchainProvider
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

        foreach (var projectCandidate in ProjectCandidates(query))
        {
            if (yielded.Add(projectCandidate.Path)) yield return projectCandidate;
        }

        var names = _host.IsWindows ? new[] { "node.exe", "node" } : new[] { "node" };
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
            var packageJson = Path.Combine(folder, "package.json");
            var localBin = Path.Combine(folder, "node_modules", ".bin", _host.IsWindows ? "node.exe" : "node");
            if (_host.FileExists(localBin))
            {
                yield return new Candidate(localBin, "Project local");
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
            var appData = _host.GetEnvironmentVariable("APPDATA");
            var localAppData = _host.GetEnvironmentVariable("LOCALAPPDATA");
            var userProfile = _host.GetEnvironmentVariable("USERPROFILE") ?? _host.HomeDirectory;

            yield return Path.Combine(programFiles, "nodejs");
            yield return Path.Combine(programFilesX86, "nodejs");
            if (!string.IsNullOrEmpty(appData)) yield return Path.Combine(appData, "npm");
            if (!string.IsNullOrEmpty(userProfile)) yield return Path.Combine(userProfile, "AppData", "Roaming", "npm");
            if (!string.IsNullOrEmpty(localAppData)) yield return Path.Combine(localAppData, "Programs", "node");
        }
        else if (_host.IsMacOS)
        {
            yield return "/opt/homebrew/bin";
            yield return "/usr/local/bin";
            yield return "/usr/bin";
            yield return "/bin";

            var home = _host.HomeDirectory;
            if (!string.IsNullOrEmpty(home))
            {
                var nvmVersions = Path.Combine(home, ".nvm", "versions", "node");
                if (_host.DirectoryExists(nvmVersions))
                {
                    foreach (var versionDir in _host.GetDirectories(nvmVersions))
                    {
                        yield return Path.Combine(versionDir, "bin");
                    }
                }

                yield return Path.Combine(home, ".fnm", "current", "bin");
                yield return Path.Combine(home, ".volta", "bin");
                yield return Path.Combine(home, ".asdf", "shims");
            }
        }
        else
        {
            yield return "/usr/local/bin";
            yield return "/usr/bin";
            yield return "/bin";
            yield return "/home/linuxbrew/.linuxbrew/bin";

            var home = _host.HomeDirectory;
            if (!string.IsNullOrEmpty(home))
            {
                var nvmVersions = Path.Combine(home, ".nvm", "versions", "node");
                if (_host.DirectoryExists(nvmVersions))
                {
                    foreach (var versionDir in _host.GetDirectories(nvmVersions))
                    {
                        yield return Path.Combine(versionDir, "bin");
                    }
                }

                yield return Path.Combine(home, ".fnm", "current", "bin");
                yield return Path.Combine(home, ".volta", "bin");
                yield return Path.Combine(home, ".asdf", "shims");
            }
        }
    }

    private static string Slashes(string path) => path.Replace('\\', '/').TrimEnd('/');

    private static bool SameFolder(string a, string b) => string.Equals(Slashes(a), Slashes(b), StringComparison.OrdinalIgnoreCase);

    private static bool IsUnder(string folder, string root)
    {
        var f = Slashes(folder);
        var r = Slashes(root);
        return f.Equals(r, StringComparison.OrdinalIgnoreCase) || f.StartsWith(r + "/", StringComparison.OrdinalIgnoreCase);
    }
}
