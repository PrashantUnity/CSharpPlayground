using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;

public sealed partial class CppToolchainProvider
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

        var names = _host.IsWindows
            ? new[] { "clang++.exe", "g++.exe", "c++.exe", "cl.exe", "clang.exe", "gcc.exe" }
            : new[] { "clang++", "g++", "c++", "clang", "gcc" };

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
            var binDirs = new[] { Path.Combine(folder, "bin"), Path.Combine(folder, ".tools", "bin"), Path.Combine(folder, "build") };
            foreach (var binDir in binDirs)
            {
                if (_host.DirectoryExists(binDir))
                {
                    foreach (var name in _host.IsWindows ? new[] { "clang++.exe", "g++.exe", "cl.exe" } : new[] { "clang++", "g++" })
                    {
                        var compiler = Path.Combine(binDir, name);
                        if (_host.FileExists(compiler))
                        {
                            yield return new Candidate(compiler, "Project local");
                        }
                    }
                }
            }

            if (insideRoot && SameFolder(folder, root!)) break;
            folder = Path.GetDirectoryName(folder);
        }
    }

    private IEnumerable<string> WellKnownFolders()
    {
        if (_host.IsWindows)
        {
            // Visual Studio MSVC & Build Tools
            var programFiles = _host.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
            var programFilesX86 = _host.GetEnvironmentVariable("ProgramFiles(x86)") ?? @"C:\Program Files (x86)";

            foreach (var baseDir in new[] { Path.Combine(programFiles, "Microsoft Visual Studio"), Path.Combine(programFilesX86, "Microsoft Visual Studio") })
            {
                if (_host.DirectoryExists(baseDir))
                {
                    // Scan versions (2022, 2019) -> editions (Enterprise, Professional, Community, BuildTools)
                    foreach (var yearDir in _host.GetDirectories(baseDir))
                    {
                        foreach (var edition in _host.GetDirectories(yearDir))
                        {
                            var msvcBase = Path.Combine(edition, "VC", "Tools", "MSVC");
                            if (_host.DirectoryExists(msvcBase))
                            {
                                foreach (var versionDir in _host.GetDirectories(msvcBase))
                                {
                                    yield return Path.Combine(versionDir, "bin", "Hostx64", "x64");
                                    yield return Path.Combine(versionDir, "bin", "Hostx86", "x64");
                                    yield return Path.Combine(versionDir, "bin", "Hostx86", "x86");
                                }
                            }
                        }
                    }
                }
            }

            // LLVM / Clang
            yield return Path.Combine(programFiles, "LLVM", "bin");
            yield return Path.Combine(programFilesX86, "LLVM", "bin");

            // MSYS2 / MinGW / WinLibs
            yield return @"C:\msys64\ucrt64\bin";
            yield return @"C:\msys64\mingw64\bin";
            yield return @"C:\msys64\clang64\bin";
            yield return @"C:\msys64\usr\bin";
            yield return @"C:\MinGW\bin";
            yield return @"C:\mingw64\bin";
            yield return @"C:\winlibs\bin";

            var localAppData = _host.GetEnvironmentVariable("LocalAppData");
            if (!string.IsNullOrEmpty(localAppData))
            {
                yield return Path.Combine(localAppData, "Programs", "winlibs", "bin");
            }
            yield return Path.Combine(programFiles, "winlibs", "bin");
        }
        else if (_host.IsMacOS)
        {
            // Homebrew LLVM & GCC
            yield return "/opt/homebrew/opt/llvm/bin";
            yield return "/opt/homebrew/bin";
            yield return "/usr/local/opt/llvm/bin";
            yield return "/usr/local/bin";

            // Apple Command Line Tools & Xcode
            yield return "/Library/Developer/CommandLineTools/usr/bin";
            yield return "/Applications/Xcode.app/Contents/Developer/Toolchains/XcodeDefault.xctoolchain/usr/bin";
            yield return "/usr/bin";
        }
        else
        {
            // Linux standard locations
            yield return "/usr/local/bin";
            yield return "/usr/bin";
            yield return "/bin";
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
