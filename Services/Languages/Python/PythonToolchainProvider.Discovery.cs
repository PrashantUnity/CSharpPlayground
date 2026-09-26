using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Python;

// Where Python may be: the project's virtual environment, the studio environments, PATH (the login shell's too),
// the usual install folders, and the Windows launcher. Each candidate is probed before it's used.
public sealed partial class PythonToolchainProvider
{
    private sealed record Candidate(string Path, string Source, bool IsStudioEnvironment = false);

    private async IAsyncEnumerable<Candidate> CandidatesAsync(ToolchainQuery query, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var yielded = new HashSet<string>(_host.IsWindows || _host.IsMacOS ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        var selected = SelectedPath;
        if (!string.IsNullOrWhiteSpace(selected) && _host.FileExists(selected) && yielded.Add(selected))
        {
            yield return new Candidate(selected, "Selected", IsInStudioEnvironments(selected));
        }

        foreach (var projectEnvironment in ProjectEnvironments(query))
        {
            if (yielded.Add(projectEnvironment.Path)) yield return projectEnvironment;
        }

        foreach (var studio in StudioEnvironments())
        {
            if (yielded.Add(studio)) yield return new Candidate(studio, "Studio environment", IsStudioEnvironment: true);
        }

        var names = _host.IsWindows ? new[] { "python", "python3" } : new[] { "python3", "python" };
        var loginPath = await _host.GetLoginShellPathAsync(ct);
        var folders = ExecutableSearch.SplitPath(loginPath, _host.IsWindows)
            .Concat(ExecutableSearch.SplitPath(_host.GetEnvironmentVariable("PATH"), _host.IsWindows))
            .Concat(WellKnownFolders());

        foreach (var path in ExecutableSearch.FindAll(_host, folders, names))
        {
            if (!await IsWorthProbingAsync(path, ct)) continue;
            if (yielded.Add(path)) yield return new Candidate(path, "PATH");
        }

        if (_host.IsWindows)
        {
            foreach (var path in await PythonLauncherInterpretersAsync(ct))
            {
                if (yielded.Add(path)) yield return new Candidate(path, "Python launcher");
            }
        }
    }

    private IEnumerable<Candidate> ProjectEnvironments(ToolchainQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.DocumentFolder)) yield break;

        // The folders come from real paths, already absolute: they're walked as given.
        string? folder = Path.TrimEndingDirectorySeparator(query.DocumentFolder);
        var root = string.IsNullOrWhiteSpace(query.WorkspaceRoot) ? null : Path.TrimEndingDirectorySeparator(query.WorkspaceRoot);

        // Up from the document's folder: to the workspace root when the document is inside it, else a few levels.
        var insideRoot = root != null && IsUnder(folder, root);
        for (var level = 0; !string.IsNullOrEmpty(folder) && (insideRoot || level < 4); level++)
        {
            foreach (var name in ProjectEnvironmentFolders)
            {
                var environment = Path.Combine(folder, name);
                if (!_host.FileExists(Path.Combine(environment, "pyvenv.cfg"))) continue;
                var executable = EnvironmentInterpreter(environment);
                if (_host.FileExists(executable)) yield return new Candidate(executable, $"Project {name}");
            }

            if (insideRoot && SameFolder(folder, root!)) break;
            folder = Path.GetDirectoryName(folder);
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

    // Newest Python version first.
    private IEnumerable<string> StudioEnvironments() =>
        _host.GetDirectories(StudioEnvironmentsFolder)
            .OrderByDescending(d => Version.TryParse(Path.GetFileName(d), out var v) ? v : new Version(0, 0))
            .Select(EnvironmentInterpreter)
            .Where(_host.FileExists);

    private bool IsInStudioEnvironments(string path) =>
        path.StartsWith(StudioEnvironmentsFolder, StringComparison.OrdinalIgnoreCase);

    private IEnumerable<string> WellKnownFolders()
    {
        var home = _host.HomeDirectory;
        if (_host.IsWindows)
        {
            var localAppData = _host.GetEnvironmentVariable("LOCALAPPDATA") ?? Path.Combine(home, "AppData", "Local");
            var programFiles = _host.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
            return NewestFirst(Path.Combine(localAppData, "Programs", "Python"), "Python")
                .Concat(NewestFirst(programFiles, "Python"))
                .Concat(NewestFirst(@"C:\", "Python"))
                .Concat(
                [
                    Path.Combine(home, "miniconda3"),
                    Path.Combine(home, "anaconda3"),
                    Path.Combine(home, "miniforge3"),
                    Path.Combine(home, "scoop", "apps", "python", "current")
                ]);
        }

        var folders = new List<string>();
        if (_host.IsMacOS)
        {
            folders.Add("/opt/homebrew/bin");
            folders.Add("/usr/local/bin");
            folders.AddRange(NewestFirst("/Library/Frameworks/Python.framework/Versions", string.Empty).Select(v => Path.Combine(v, "bin")));
        }
        else
        {
            folders.Add("/usr/local/bin");
            folders.Add("/home/linuxbrew/.linuxbrew/bin");
        }

        folders.Add(Path.Combine(home, ".pyenv", "shims"));
        folders.AddRange(NewestFirst(Path.Combine(home, ".local", "share", "uv", "python"), "cpython-").Select(v => Path.Combine(v, "bin")));
        foreach (var conda in new[] { "miniconda3", "anaconda3", "miniforge3", "mambaforge" })
        {
            folders.Add(Path.Combine(home, conda, "bin"));
            folders.Add(Path.Combine("/opt", conda, "bin"));
        }

        folders.Add("/usr/bin");
        return folders;
    }

    // Subfolders whose name starts with a prefix, the highest version-looking name first ("Python313" before "Python39").
    private IEnumerable<string> NewestFirst(string parent, string prefix) =>
        _host.GetDirectories(parent)
            .Where(d => Path.GetFileName(d).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(d => VersionKey(Path.GetFileName(d)[prefix.Length..]));

    // "3.14" → 3.14, "313" (Python313) → 3.13, "cpython-3.12.4-macos-aarch64-none" → 3.12.4; anything else sorts last.
    private static Version VersionKey(string text)
    {
        var dotted = System.Text.RegularExpressions.Regex.Match(text, @"\d+(?:\.\d+)+");
        if (dotted.Success && Version.TryParse(dotted.Value, out var version)) return version;
        var digits = System.Text.RegularExpressions.Regex.Match(text, @"\d+");
        if (digits.Success && digits.Value.Length > 1 && Version.TryParse($"{digits.Value[0]}.{digits.Value[1..]}", out version)) return version;
        return new Version(0, 0);
    }

    private async Task<bool> IsWorthProbingAsync(string path, CancellationToken ct)
    {
        // Apple's /usr/bin/python3 is a stub until the Command Line Tools are installed; running it then opens an
        // "install the developer tools" dialog, so it's only tried when they're there.
        if (_host.IsMacOS && path == "/usr/bin/python3")
        {
            _commandLineToolsInstalled ??= new Lazy<Task<bool>>(async () =>
                (await _host.RunAsync("/usr/bin/xcode-select", ["-p"], TimeSpan.FromSeconds(3), ct)).Succeeded);
            return await _commandLineToolsInstalled.Value;
        }

        if (_host.IsMacOS && path == "/usr/bin/python") return false; // Python 2's old spot: gone or ancient

        return true;
    }

    // "py -0p" lists every python.org install on Windows, e.g. " -V:3.13 *        C:\...\Python313\python.exe".
    private async Task<IReadOnlyList<string>> PythonLauncherInterpretersAsync(CancellationToken ct)
    {
        var launcher = ExecutableSearch.FindAll(_host, ExecutableSearch.SplitPath(_host.GetEnvironmentVariable("PATH"), true), ["py"]).FirstOrDefault()
                       ?? Path.Combine(_host.GetEnvironmentVariable("WINDIR") ?? @"C:\Windows", "py.exe");
        if (!_host.FileExists(launcher)) return Array.Empty<string>();

        var result = await _host.RunAsync(launcher, ["-0p"], TimeSpan.FromSeconds(5), ct);
        if (!result.Succeeded) return Array.Empty<string>();

        return result.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.IndexOf(":\\", StringComparison.Ordinal) is var drive and > 0 ? line[(drive - 1)..].Trim() : null)
            .Where(path => path != null && path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && _host.FileExists(path))
            .Select(path => path!)
            .ToArray();
    }
}
