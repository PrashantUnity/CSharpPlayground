using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;

public enum CppCompilerVendor
{
    Clang,
    AppleClang,
    Gcc,
    Msvc,
    Generic
}

/// <summary>
/// Finds C++ compilers (clang++, g++, cl.exe) on this computer and picks the one a C++ source file compiles and runs with,
/// prioritizing user selection, project root, and system PATH.
/// </summary>
public sealed partial class CppToolchainProvider : IToolchainProvider
{
    public static readonly Version MinimumVersion = new(7, 0, 0);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(8);

    private readonly IHostEnvironment _host;
    private readonly IProcessLauncher _launcher;
    private readonly ToolchainSettingsStore _settings;
    private readonly string _cppRoot;
    private readonly ConcurrentDictionary<string, Lazy<Task<ProbeResult?>>> _probes = new(StringComparer.Ordinal);

    public CppToolchainProvider(IHostEnvironment host, IProcessLauncher launcher, ToolchainSettingsStore settings, string cppRoot)
    {
        _host = host;
        _launcher = launcher;
        _settings = settings;
        _cppRoot = cppRoot;
    }

    public string LanguageId => LanguageIds.Cpp;
    public string ToolName => "C++ Compiler";

    public string? SelectedPath => _settings.GetSelectedPath(LanguageIds.Cpp);

    public IReadOnlyList<ToolchainAction> Actions
    {
        get
        {
            var actions = new List<ToolchainAction>();
            if (_host.IsWindows)
            {
                actions.Add(new ToolchainAction("winget-install-winlibs", "Install WinLibs (GCC & Clang with C++ STL)", "Runs 'winget install BrechtSanders.WinLibs.POSIX.UCRT' for standalone C++ compiler with full standard library."));
                actions.Add(new ToolchainAction("winget-install-vs", "Install Visual Studio C++ Build Tools", "Runs 'winget install Microsoft.VisualStudio.2022.BuildTools' (with C++ workload)."));
                actions.Add(new ToolchainAction("winget-install-llvm", "Install LLVM Clang via winget", "Runs 'winget install LLVM.LLVM' (requires Visual Studio or MinGW for C++ standard library)."));
                actions.Add(new ToolchainAction("open-download", "Download Visual C++ Build Tools", "Opens Microsoft Visual C++ Build Tools download page in browser."));
            }
            else if (_host.IsMacOS)
            {
                actions.Add(new ToolchainAction("install-xcode-clt", "Install Apple Command Line Tools", "Runs 'xcode-select --install' to install Apple Clang and macOS SDK headers."));
                actions.Add(new ToolchainAction("brew-install-llvm", "Install LLVM Clang via Homebrew", "Runs 'brew install llvm' to install Homebrew Clang compiler."));
                actions.Add(new ToolchainAction("open-download", "Open Apple Developer Tools Page", "Opens Apple Developer website in browser."));
            }
            else
            {
                actions.Add(new ToolchainAction("open-download", "Open GCC / Clang Documentation", "Opens GCC documentation and package guides in browser."));
            }
            return actions;
        }
    }

    public void Select(string? executablePath) => _settings.SetSelectedPath(LanguageIds.Cpp, executablePath);

    public void Refresh()
    {
        _probes.Clear();
    }

    public async Task<ToolchainResolution> ResolveAsync(ToolchainQuery query, CancellationToken ct = default)
    {
        var tooOld = new List<string>();

        await foreach (var candidate in CandidatesAsync(query, ct))
        {
            var probe = await ProbeAsync(candidate.Path, ct);
            if (probe == null) continue;

            if (probe.Version < MinimumVersion)
            {
                tooOld.Add($"{probe.VendorName} {probe.Version} ({candidate.Path})");
                continue;
            }

            return ToolchainResolution.Found(ToInfo(candidate, probe));
        }

        return ToolchainResolution.NotFound(CppGuidance.NotInstalled(_host, tooOld));
    }

    public async Task<IReadOnlyList<ToolchainInfo>> ListAsync(ToolchainQuery query, CancellationToken ct = default)
    {
        var candidates = new List<Candidate>();
        await foreach (var candidate in CandidatesAsync(query, ct)) candidates.Add(candidate);

        var probes = await Task.WhenAll(candidates.Select(c => ProbeAsync(c.Path, ct)));
        var found = new List<ToolchainInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < candidates.Count; i++)
        {
            var probe = probes[i];
            if (probe == null || probe.Version < MinimumVersion) continue;
            if (!seen.Add($"{probe.Executable}|{probe.Version}|{probe.Vendor}")) continue;
            found.Add(ToInfo(candidates[i], probe));
        }

        return found;
    }

    public async Task<ToolchainActionResult> RunActionAsync(string actionId, ToolchainQuery query, Action<string> output, CancellationToken ct = default)
    {
        switch (actionId)
        {
            case "open-download":
                var url = _host.IsWindows
                    ? "https://visualstudio.microsoft.com/visual-cpp-build-tools/"
                    : _host.IsMacOS ? "https://developer.apple.com/xcode/" : "https://gcc.gnu.org/";
                BrowserLauncher.Open(url);
                return new ToolchainActionResult(true, $"Opened {url} in browser.");

            case "install-xcode-clt":
                output("Launching Apple Command Line Tools installer (xcode-select --install)…\n");
                var xcodeOk = await ToolchainSetupRunner.ExecuteAsync("xcode-select --install", _host, _launcher, output, ct);
                return new ToolchainActionResult(xcodeOk, xcodeOk ? "Apple Command Line Tools installer requested." : "Failed to launch xcode-select installer.");

            case "brew-install-llvm":
                output("Installing LLVM Clang via Homebrew (brew install llvm)…\n");
                var brewOk = await ToolchainSetupRunner.ExecuteAsync("brew install llvm", _host, _launcher, output, ct);
                return new ToolchainActionResult(brewOk, brewOk ? "LLVM Clang installation completed." : "Homebrew installation failed or was cancelled.");

            case "winget-install-winlibs":
                output("Installing WinLibs (GCC & Clang with full C++ STL) via winget…\n");
                var winlibsOk = await ToolchainSetupRunner.ExecuteAsync("winget install BrechtSanders.WinLibs.POSIX.UCRT --accept-source-agreements --accept-package-agreements", _host, _launcher, output, ct);
                return new ToolchainActionResult(winlibsOk, winlibsOk ? "WinLibs installation completed." : "winget installation failed or was cancelled.");

            case "winget-install-llvm":
                output("Installing LLVM Clang via winget (winget install LLVM.LLVM)…\n");
                var wingetOk = await ToolchainSetupRunner.ExecuteAsync("winget install LLVM.LLVM --accept-source-agreements --accept-package-agreements", _host, _launcher, output, ct);
                return new ToolchainActionResult(wingetOk, wingetOk ? "LLVM Clang installation completed." : "winget installation failed or was cancelled.");

            case "winget-install-vs":
                output("Installing Visual Studio C++ Build Tools (with C++ Workload) via winget…\n");
                var vsOk = await ToolchainSetupRunner.ExecuteAsync("winget install Microsoft.VisualStudio.2022.BuildTools --override \"--passive --wait --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended\" --accept-source-agreements --accept-package-agreements", _host, _launcher, output, ct);
                return new ToolchainActionResult(vsOk, vsOk ? "Visual Studio Build Tools installation completed." : "winget installation failed or was cancelled.");

            default:
                return new ToolchainActionResult(false, $"Unknown action '{actionId}'.");
        }
    }

    public sealed record ProbeResult(Version Version, string Executable, CppCompilerVendor Vendor, string VendorName);

    private async Task<ProbeResult?> ProbeAsync(string path, CancellationToken ct)
    {
        var probe = _probes.GetOrAdd(path, p => new Lazy<Task<ProbeResult?>>(() => RunProbeAsync(p, ct)));
        try
        {
            return await probe.Value;
        }
        catch (OperationCanceledException)
        {
            _probes.TryRemove(new KeyValuePair<string, Lazy<Task<ProbeResult?>>>(path, probe));
            throw;
        }
    }

    private async Task<ProbeResult?> RunProbeAsync(string path, CancellationToken ct)
    {
        var fileName = Path.GetFileName(path);
        var isCl = fileName.Equals("cl.exe", StringComparison.OrdinalIgnoreCase) || fileName.Equals("cl", StringComparison.OrdinalIgnoreCase);
        var args = isCl ? new[] { "/?" } : new[] { "--version" };

        var result = await _host.RunAsync(path, args, ProbeTimeout, ct);
        ct.ThrowIfCancellationRequested();

        var combined = (result.StandardOutput + "\n" + result.StandardError).Trim();
        if (string.IsNullOrWhiteSpace(combined))
        {
            Debug.WriteLine($"[CSharpEditorPlugin] {path} isn't a usable C++ compiler (exit {result.ExitCode}{(result.TimedOut ? ", timed out" : string.Empty)}).");
            return null;
        }

        var (vendor, vendorName) = DetectVendor(combined, fileName);
        var version = ParseCompilerVersion(combined, vendor);
        if (version == null) return null;

        return new ProbeResult(version, path, vendor, vendorName);
    }

    public static (CppCompilerVendor Vendor, string VendorName) DetectVendor(string output, string fileName)
    {
        if (output.Contains("Apple clang", StringComparison.OrdinalIgnoreCase) || output.Contains("Apple LLVM", StringComparison.OrdinalIgnoreCase))
        {
            return (CppCompilerVendor.AppleClang, "Apple Clang");
        }

        if (output.Contains("clang", StringComparison.OrdinalIgnoreCase))
        {
            return (CppCompilerVendor.Clang, "Clang");
        }

        if (output.Contains("Free Software Foundation", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("g++", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("gcc", StringComparison.OrdinalIgnoreCase))
        {
            return (CppCompilerVendor.Gcc, "GCC");
        }

        if (output.Contains("Microsoft", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("Optimizing Compiler", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("cl", StringComparison.OrdinalIgnoreCase))
        {
            return (CppCompilerVendor.Msvc, "MSVC");
        }

        return (CppCompilerVendor.Generic, "C++");
    }

    public static Version? ParseCompilerVersion(string output, CppCompilerVendor vendor)
    {
        if (vendor == CppCompilerVendor.Msvc)
        {
            var msvcMatch = Regex.Match(output, @"Version\s+(?<ver>\d+(?:\.\d+)+)", RegexOptions.IgnoreCase);
            if (msvcMatch.Success && Version.TryParse(CleanVersion(msvcMatch.Groups["ver"].Value), out var msvcVer))
            {
                return msvcVer;
            }
        }

        var match = Regex.Match(output, @"(?:version|clang version|g\+\+.*?)\s*(?<ver>\d+(?:\.\d+)+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var raw = CleanVersion(match.Groups["ver"].Value);
            if (Version.TryParse(raw, out var v)) return v;
            var parts = raw.Split('.');
            if (parts.Length == 1 && int.TryParse(parts[0], out var major)) return new Version(major, 0);
        }

        return null;
    }

    private static string CleanVersion(string raw)
    {
        var clean = Regex.Replace(raw, @"[-_+~].*$", "");
        var parts = clean.Split('.');
        if (parts.Length > 4) clean = string.Join('.', parts.Take(4));
        return clean;
    }

    private ToolchainInfo ToInfo(Candidate candidate, ProbeResult probe)
    {
        return new ToolchainInfo
        {
            LanguageId = LanguageIds.Cpp,
            ExecutablePath = candidate.Path,
            Version = probe.Version,
            DisplayName = $"{probe.VendorName} {probe.Version}",
            Source = candidate.Source.StartsWith("Project", StringComparison.Ordinal) ? candidate.Source : SourceOf(candidate.Path, candidate.Source),
            Properties = new Dictionary<string, string>
            {
                ["compilerVendor"] = probe.VendorName,
                ["languageStandard"] = "C++20 (-std=c++20)",
                ["hostArchitecture"] = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString()
            }
        };
    }

    private static string SourceOf(string path, string fallback)
    {
        var normalized = path.Replace('\\', '/');
        if (normalized.Contains("/opt/homebrew/", StringComparison.Ordinal) || normalized.Contains("/Cellar/", StringComparison.Ordinal)) return "Homebrew";
        if (normalized.Contains("/CommandLineTools/", StringComparison.Ordinal)) return "Command Line Tools";
        if (normalized.Contains("/Xcode.app/", StringComparison.Ordinal)) return "Xcode";
        if (normalized.Contains("/msys64/", StringComparison.OrdinalIgnoreCase)) return "MSYS2";
        if (normalized.Contains("Microsoft Visual Studio", StringComparison.OrdinalIgnoreCase)) return "Visual Studio";
        if (normalized.Contains("/LLVM/", StringComparison.OrdinalIgnoreCase)) return "LLVM";
        if (normalized.StartsWith("/usr/bin/", StringComparison.Ordinal) || normalized.StartsWith("/usr/local/bin/", StringComparison.Ordinal)) return "System";
        return fallback;
    }
}
