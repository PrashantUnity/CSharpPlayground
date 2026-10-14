using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;

/// <summary>
/// Resolved Windows C++ SDK paths including MSVC headers/libraries, Windows 10/11 SDK headers/libraries,
/// and MinGW/WinLibs/MSYS2 sysroots.
/// </summary>
public sealed record CppWindowsSdkInfo(
    IReadOnlyList<string> IncludeDirectories,
    IReadOnlyList<string> LibDirectories,
    string? MinGwSysroot = null)
{
    public bool HasMsvcHeaders => IncludeDirectories.Count > 0;
    public bool HasMinGw => !string.IsNullOrEmpty(MinGwSysroot);
}

/// <summary>
/// Discovers Visual Studio MSVC include/lib directories, Windows Kits 10/11 SDK directories,
/// and MinGW-w64 / WinLibs / MSYS2 sysroots on Windows.
/// </summary>
public static class CppWindowsSdkResolver
{
    public static CppWindowsSdkInfo Resolve(IHostEnvironment host)
    {
        if (!host.IsWindows)
        {
            return new CppWindowsSdkInfo(Array.Empty<string>(), Array.Empty<string>());
        }

        var includeDirs = new List<string>();
        var libDirs = new List<string>();

        FindMsvcPaths(host, includeDirs, libDirs);
        FindWindowsSdkPaths(host, includeDirs, libDirs);
        var minGwSysroot = FindMinGwSysroot(host);

        return new CppWindowsSdkInfo(includeDirs, libDirs, minGwSysroot);
    }

    private static void FindMsvcPaths(IHostEnvironment host, List<string> includeDirs, List<string> libDirs)
    {
        var programFiles = host.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
        var programFilesX86 = host.GetEnvironmentVariable("ProgramFiles(x86)") ?? @"C:\Program Files (x86)";

        string? newestVersionDir = null;
        Version? highestVersion = null;

        foreach (var baseDir in new[] { Path.Combine(programFiles, "Microsoft Visual Studio"), Path.Combine(programFilesX86, "Microsoft Visual Studio") })
        {
            if (!host.DirectoryExists(baseDir)) continue;

            foreach (var yearDir in host.GetDirectories(baseDir))
            {
                foreach (var edition in host.GetDirectories(yearDir))
                {
                    var msvcBase = Path.Combine(edition, "VC", "Tools", "MSVC");
                    if (!host.DirectoryExists(msvcBase)) continue;

                    foreach (var versionDir in host.GetDirectories(msvcBase))
                    {
                        var folderName = Path.GetFileName(versionDir);
                        if (Version.TryParse(folderName, out var v))
                        {
                            if (highestVersion == null || v > highestVersion)
                            {
                                highestVersion = v;
                                newestVersionDir = versionDir;
                            }
                        }
                        else if (newestVersionDir == null)
                        {
                            newestVersionDir = versionDir;
                        }
                    }
                }
            }
        }

        if (newestVersionDir != null)
        {
            var inc = JoinPath(host, newestVersionDir, "include");
            if (host.DirectoryExists(inc))
            {
                includeDirs.Add(inc);
            }

            var libX64 = JoinPath(host, newestVersionDir, "lib", "x64");
            if (host.DirectoryExists(libX64))
            {
                libDirs.Add(libX64);
            }
        }
    }

    private static void FindWindowsSdkPaths(IHostEnvironment host, List<string> includeDirs, List<string> libDirs)
    {
        var programFilesX86 = host.GetEnvironmentVariable("ProgramFiles(x86)") ?? @"C:\Program Files (x86)";
        var programFiles = host.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";

        foreach (var baseDir in new[] { JoinPath(host, programFilesX86, "Windows Kits", "10"), JoinPath(host, programFiles, "Windows Kits", "10") })
        {
            if (!host.DirectoryExists(baseDir)) continue;

            var incBase = JoinPath(host, baseDir, "Include");
            if (host.DirectoryExists(incBase))
            {
                var sdkVersions = host.GetDirectories(incBase);
                var newestSdk = sdkVersions.OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
                if (newestSdk != null)
                {
                    var ucrt = JoinPath(host, newestSdk, "ucrt");
                    var shared = JoinPath(host, newestSdk, "shared");
                    var um = JoinPath(host, newestSdk, "um");

                    if (host.DirectoryExists(ucrt)) includeDirs.Add(ucrt);
                    if (host.DirectoryExists(shared)) includeDirs.Add(shared);
                    if (host.DirectoryExists(um)) includeDirs.Add(um);
                }
            }

            var libBase = JoinPath(host, baseDir, "Lib");
            if (host.DirectoryExists(libBase))
            {
                var sdkVersions = host.GetDirectories(libBase);
                var newestSdk = sdkVersions.OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
                if (newestSdk != null)
                {
                    var ucrtLib = JoinPath(host, newestSdk, "ucrt", "x64");
                    var umLib = JoinPath(host, newestSdk, "um", "x64");

                    if (host.DirectoryExists(ucrtLib)) libDirs.Add(ucrtLib);
                    if (host.DirectoryExists(umLib)) libDirs.Add(umLib);
                }
            }
        }
    }

    private static string JoinPath(IHostEnvironment host, params string[] parts)
    {
        var sep = host.IsWindows ? '\\' : Path.DirectorySeparatorChar;
        return string.Join(sep, parts.Select(p => p.TrimEnd('\\', '/')));
    }

    private static string? FindMinGwSysroot(IHostEnvironment host)
    {
        var localAppData = host.GetEnvironmentVariable("LocalAppData");
        var programFiles = host.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";

        var candidates = new List<string>
        {
            @"C:\msys64\ucrt64",
            @"C:\msys64\mingw64",
            @"C:\msys64\clang64",
            @"C:\MinGW",
            @"C:\mingw64",
            @"C:\winlibs",
            Path.Combine(programFiles, "winlibs")
        };

        if (!string.IsNullOrEmpty(localAppData))
        {
            candidates.Add(Path.Combine(localAppData, "Programs", "winlibs"));
        }

        foreach (var c in candidates)
        {
            if (IsValidMinGwRoot(host, c)) return c;
        }

        return null;
    }

    private static bool IsValidMinGwRoot(IHostEnvironment host, string root)
    {
        if (!host.DirectoryExists(root)) return false;
        return host.DirectoryExists(Path.Combine(root, "include")) ||
               host.DirectoryExists(Path.Combine(root, "include", "c++")) ||
               host.FileExists(Path.Combine(root, "bin", "g++.exe")) ||
               host.FileExists(Path.Combine(root, "bin", "gcc.exe"));
    }
}
