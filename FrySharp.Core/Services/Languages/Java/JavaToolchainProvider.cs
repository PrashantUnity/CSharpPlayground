using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using PdfEditorApp.Plugins.CSharpEditor.Services.Common;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;

/// <summary>
/// Finds the Java Development Kits (JDK) on this computer and picks the one a Java source file runs with,
/// prioritizing user selection, JAVA_HOME, project root, and PATH. Requires JDK 11.0.0+ with javac compiler.
/// </summary>
public sealed partial class JavaToolchainProvider : IToolchainProvider
{
    public static readonly Version MinimumVersion = new(11, 0, 0);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(8);

    private readonly IHostEnvironment _host;
    private readonly IProcessLauncher _launcher;
    private readonly ToolchainSettingsStore _settings;
    private readonly string _javaRoot;
    private readonly ConcurrentDictionary<string, Lazy<Task<ProbeResult?>>> _probes = new(StringComparer.Ordinal);

    public JavaToolchainProvider(IHostEnvironment host, IProcessLauncher launcher, ToolchainSettingsStore settings, string javaRoot)
    {
        _host = host;
        _launcher = launcher;
        _settings = settings;
        _javaRoot = javaRoot;
    }

    public string LanguageId => LanguageIds.Java;
    public string ToolName => "Java JDK";

    public string? SelectedPath => _settings.GetSelectedPath(LanguageIds.Java);

    public IReadOnlyList<ToolchainAction> Actions
    {
        get
        {
            var actions = new List<ToolchainAction>();
            if (_host.IsWindows)
            {
                actions.Add(new ToolchainAction("winget-install-openjdk", "Install Microsoft OpenJDK 21 via winget", "Runs 'winget install Microsoft.OpenJDK.21' to install JDK 21."));
                actions.Add(new ToolchainAction("open-download", "Download Eclipse Temurin JDK", "Opens adoptium.net in browser."));
            }
            else if (_host.IsMacOS)
            {
                actions.Add(new ToolchainAction("brew-install-openjdk", "Install OpenJDK via Homebrew", "Runs 'brew install openjdk' via Homebrew."));
                actions.Add(new ToolchainAction("open-download", "Download Eclipse Temurin JDK", "Opens adoptium.net in browser."));
            }
            else
            {
                actions.Add(new ToolchainAction("open-download", "Download Eclipse Temurin JDK", "Opens adoptium.net in browser."));
            }
            return actions;
        }
    }

    public void Select(string? executablePath) => _settings.SetSelectedPath(LanguageIds.Java, executablePath);

    public void Refresh()
    {
        _probes.Clear();
    }

    public async Task<ToolchainResolution> ResolveAsync(ToolchainQuery query, CancellationToken ct = default)
    {
        var tooOld = new List<string>();
        var hadJreWithoutCompiler = false;

        await foreach (var candidate in CandidatesAsync(query, ct))
        {
            var probe = await ProbeAsync(candidate.Path, ct);
            if (probe == null) continue;

            if (probe.Version < MinimumVersion)
            {
                tooOld.Add($"Java {probe.Version} ({candidate.Path})");
                continue;
            }

            if (!probe.HasCompiler)
            {
                hadJreWithoutCompiler = true;
                continue;
            }

            return ToolchainResolution.Found(ToInfo(candidate, probe));
        }

        return ToolchainResolution.NotFound(JavaGuidance.NotInstalled(_host, tooOld, hadJreWithoutCompiler));
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
            if (probe == null || probe.Version < MinimumVersion || !probe.HasCompiler) continue;
            if (!seen.Add($"{probe.Executable}|{probe.Version}")) continue;
            found.Add(ToInfo(candidates[i], probe));
        }

        return found;
    }

    public async Task<ToolchainActionResult> RunActionAsync(string actionId, ToolchainQuery query, Action<string> output, CancellationToken ct = default)
    {
        switch (actionId)
        {
            case "open-download":
                BrowserLauncher.Open("https://adoptium.net/");
                return new ToolchainActionResult(true, "Opened https://adoptium.net/ in browser.");

            case "brew-install-openjdk":
                output("Installing OpenJDK via Homebrew (brew install openjdk)…\n");
                var brewOk = await ToolchainSetupRunner.ExecuteAsync("brew install openjdk", _host, _launcher, output, ct);
                return new ToolchainActionResult(brewOk, brewOk ? "OpenJDK installation completed." : "Homebrew installation failed or was cancelled.");

            case "winget-install-openjdk":
                output("Installing Microsoft OpenJDK 21 via winget…\n");
                var wingetOk = await ToolchainSetupRunner.ExecuteAsync("winget install Microsoft.OpenJDK.21 --accept-source-agreements --accept-package-agreements", _host, _launcher, output, ct);
                return new ToolchainActionResult(wingetOk, wingetOk ? "OpenJDK installation completed." : "winget installation failed or was cancelled.");

            default:
                return new ToolchainActionResult(false, $"Unknown action '{actionId}'.");
        }
    }

    private sealed record ProbeResult(Version Version, string Executable, string CompilerPath, bool HasCompiler);

    private async Task<ProbeResult?> ProbeAsync(string path, CancellationToken ct)
    {
        // The probe starts a process, which can take a while: it starts on the pool, so the Lazy's lock is held only to make
        // the task, and everyone else asking for it waits without holding a thread.
        var probe = _probes.GetOrAdd(path, p => new Lazy<Task<ProbeResult?>>(() => Task.Run(() => RunProbeAsync(p, ct))));
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
        var result = await _host.RunAsync(path, ["-version"], ProbeTimeout, ct);
        ct.ThrowIfCancellationRequested();

        // java -version writes to stderr in Java 8-17, or stdout in some modern distributions
        var combined = (result.StandardError + "\n" + result.StandardOutput).Trim();
        if (string.IsNullOrWhiteSpace(combined))
        {
            Debug.WriteLine($"[CSharpEditorPlugin] {path} isn't a usable Java (exit {result.ExitCode}{(result.TimedOut ? ", timed out" : string.Empty)}).");
            return null;
        }

        var version = ParseJavaVersion(combined);
        if (version == null) return null;

        var binDir = ExecutableSearch.GetDirectoryName(path);
        var javacName = _host.IsWindows ? "javac.exe" : "javac";
        var compilerPath = !string.IsNullOrEmpty(binDir) ? ExecutableSearch.Combine(_host, binDir, javacName) : javacName;
        var hasCompiler = _host.FileExists(compilerPath);

        return new ProbeResult(version, path, compilerPath, hasCompiler);
    }

    public static Version? ParseJavaVersion(string output)
    {
        // Matches e.g. 'openjdk version "17.0.20"', 'java version "21.0.2"', 'version "11.0.22"'
        var match = Regex.Match(output, @"(?:openjdk|java)?\s*version\s*""(?<ver>[^""]+)""");
        if (!match.Success) return null;

        var raw = match.Groups["ver"].Value;
        // Normalize 1.8.0_392 -> 1.8.0, 21.0.2+13 -> 21.0.2, 21 -> 21.0
        var clean = Regex.Replace(raw, @"[-_+].*$", "");
        var parts = clean.Split('.');
        if (parts.Length == 1 && int.TryParse(parts[0], out var major))
        {
            return new Version(major, 0);
        }

        return Version.TryParse(clean, out var v) ? v : null;
    }

    private ToolchainInfo ToInfo(Candidate candidate, ProbeResult probe)
    {
        return new ToolchainInfo
        {
            LanguageId = LanguageIds.Java,
            ExecutablePath = candidate.Path,
            Version = probe.Version,
            DisplayName = $"Java {probe.Version}",
            Source = candidate.Source.StartsWith("Project", StringComparison.Ordinal) ? candidate.Source : SourceOf(candidate.Path, candidate.Source)
        };
    }

    private static string SourceOf(string path, string fallback)
    {
        var normalized = path.Replace('\\', '/');
        if (normalized.Contains("/opt/homebrew/", StringComparison.Ordinal) || normalized.Contains("/Cellar/", StringComparison.Ordinal)) return "Homebrew";
        if (normalized.Contains("/.sdkman/", StringComparison.Ordinal)) return "SDKMAN";
        if (normalized.Contains("/.asdf/", StringComparison.Ordinal)) return "asdf";
        if (normalized.Contains("Eclipse Adoptium", StringComparison.OrdinalIgnoreCase) || normalized.Contains("Temurin", StringComparison.OrdinalIgnoreCase)) return "Adoptium";
        if (normalized.Contains("Microsoft", StringComparison.OrdinalIgnoreCase)) return "Microsoft";
        if (normalized.StartsWith("/usr/bin/", StringComparison.Ordinal) || normalized.StartsWith("/usr/lib/jvm", StringComparison.Ordinal)) return "System";
        return fallback;
    }
}
