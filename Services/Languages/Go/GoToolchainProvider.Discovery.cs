using System.Runtime.CompilerServices;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;

public sealed partial class GoToolchainProvider
{
    private sealed record Candidate(string Path, string Source);

    private async IAsyncEnumerable<Candidate> CandidatesAsync(ToolchainQuery query, [EnumeratorCancellation] CancellationToken ct)
    {
        var yielded = new HashSet<string>(_host.IsWindows || _host.IsMacOS ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        var selected = SelectedPath;
        if (!string.IsNullOrWhiteSpace(selected) && _host.FileExists(selected) && yielded.Add(selected))
        {
            yield return new Candidate(selected, "Selected");
        }

        var goroot = _host.GetEnvironmentVariable("GOROOT");
        if (!string.IsNullOrWhiteSpace(goroot))
        {
            var gorootBin = Path.Combine(goroot, "bin", _host.IsWindows ? "go.exe" : "go");
            if (_host.FileExists(gorootBin) && yielded.Add(gorootBin))
            {
                yield return new Candidate(gorootBin, "GOROOT");
            }
        }

        foreach (var projectCandidate in ProjectCandidates(query))
        {
            if (yielded.Add(projectCandidate.Path)) yield return projectCandidate;
        }

        var names = _host.IsWindows ? new[] { "go.exe", "go" } : new[] { "go" };
        var loginPath = await _host.GetLoginShellPathAsync(ct).ConfigureAwait(false);
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
            var localGoBin = Path.Combine(folder, "bin", _host.IsWindows ? "go.exe" : "go");
            if (_host.FileExists(localGoBin))
            {
                yield return new Candidate(localGoBin, "Project local");
            }

            var dotGoBin = Path.Combine(folder, ".go", "bin", _host.IsWindows ? "go.exe" : "go");
            if (_host.FileExists(dotGoBin))
            {
                yield return new Candidate(dotGoBin, "Project local (.go)");
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
            yield return Path.Combine(programFiles, "Go", "bin");
            yield return @"C:\Go\bin";

            var userProfile = _host.GetEnvironmentVariable("USERPROFILE");
            if (!string.IsNullOrEmpty(userProfile))
            {
                yield return Path.Combine(userProfile, "go", "bin");
            }

            var localAppData = _host.GetEnvironmentVariable("LOCALAPPDATA");
            if (!string.IsNullOrEmpty(localAppData))
            {
                yield return Path.Combine(localAppData, "Programs", "Go", "bin");
            }
        }
        else if (_host.IsMacOS)
        {
            yield return "/opt/homebrew/bin";
            yield return "/usr/local/bin";
            yield return "/usr/local/go/bin";

            var home = _host.HomeDirectory;
            if (!string.IsNullOrEmpty(home))
            {
                yield return Path.Combine(home, "go", "bin");
            }
        }
        else
        {
            yield return "/usr/local/go/bin";
            yield return "/usr/bin";
            yield return "/snap/bin";

            var home = _host.HomeDirectory;
            if (!string.IsNullOrEmpty(home))
            {
                yield return Path.Combine(home, "go", "bin");
                yield return Path.Combine(home, ".go", "bin");
            }
        }
    }

    private static bool IsUnder(string path, string parent) =>
        path.StartsWith(parent, StringComparison.OrdinalIgnoreCase) &&
        (path.Length == parent.Length ||
         path[parent.Length] == Path.DirectorySeparatorChar ||
         path[parent.Length] == Path.AltDirectorySeparatorChar);

    private static bool SameFolder(string a, string b) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(a),
            Path.TrimEndingDirectorySeparator(b),
            StringComparison.OrdinalIgnoreCase);
}
