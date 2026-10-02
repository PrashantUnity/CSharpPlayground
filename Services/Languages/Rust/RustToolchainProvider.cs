using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using PdfEditorApp.Plugins.CSharpEditor.Services.Common;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;

/// <summary>
/// Finds the Rust toolchain on this machine (<c>cargo</c>, with its sibling <c>rustc</c>), probes its version and host
/// target, and resolves the one a Rust source file builds with. rustup's proxies in <c>~/.cargo/bin</c> come first, then
/// PATH and the usual install folders; <see cref="ListAsync"/> also offers each toolchain rustup has installed.
/// </summary>
public sealed partial class RustToolchainProvider : IToolchainProvider
{
    public static readonly Version MinimumVersion = new(1, 70, 0);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(8);

    private const string InstallScript = "curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh -s -- -y";

    private readonly IHostEnvironment _host;
    private readonly IProcessLauncher _launcher;
    private readonly ToolchainSettingsStore _settings;
    private readonly string _rustRoot;
    private readonly ConcurrentDictionary<string, Lazy<Task<ProbeResult?>>> _probes = new(StringComparer.Ordinal);
    private volatile bool _sawRustupWithoutToolchain;

    [GeneratedRegex(@"cargo\s+(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?:-(?<pre>[A-Za-z]+))?", RegexOptions.IgnoreCase)]
    private static partial Regex CargoVersionRegex();

    [GeneratedRegex(@"^host:\s*(?<host>\S+)\s*$", RegexOptions.Multiline)]
    private static partial Regex RustcHostRegex();

    [GeneratedRegex(@"^release:\s*(?<release>\S+)\s*$", RegexOptions.Multiline)]
    private static partial Regex RustcReleaseRegex();

    [GeneratedRegex(@"^LLVM version:\s*(?<llvm>\S+)\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex RustcLlvmRegex();

    [GeneratedRegex(@"rustup could not choose|no default is configured|toolchain '[^']+' is not installed", RegexOptions.IgnoreCase)]
    private static partial Regex RustupWithoutToolchainRegex();

    /// <param name="rustRoot">The studio's Rust folder (<c>…/rust</c>): staged projects, the shared build cache and the display crate.</param>
    public RustToolchainProvider(IHostEnvironment host, IProcessLauncher launcher, ToolchainSettingsStore settings, string rustRoot)
    {
        _host = host;
        _launcher = launcher;
        _settings = settings;
        _rustRoot = rustRoot;
    }

    public string LanguageId => LanguageIds.Rust;
    public string ToolName => "Rust Toolchain";

    public string? SelectedPath => _settings.GetSelectedPath(LanguageIds.Rust);

    /// <summary>Where every Rust build's artifacts go, so a crate compiles once for all scripts.</summary>
    public string BuildCacheFolder => Path.Combine(_rustRoot, "target");

    public IReadOnlyList<ToolchainAction> Actions
    {
        get
        {
            var actions = new List<ToolchainAction>();
            if (_host.IsWindows)
            {
                actions.Add(new ToolchainAction("winget-install-rustup", "Install Rust via winget", "Runs 'winget install Rustlang.Rustup' to install rustup, cargo and rustc."));
                actions.Add(new ToolchainAction("winget-install-vs", "Install Visual Studio C++ Build Tools", "Runs 'winget install Microsoft.VisualStudio.2022.BuildTools' (with the C++ workload); Rust links with the MSVC linker."));
                actions.Add(new ToolchainAction("open-download", "Download rustup", "Opens https://rustup.rs in your browser."));
            }
            else if (_host.IsMacOS)
            {
                actions.Add(new ToolchainAction("rustup-init", "Install Rust via rustup", $"Runs the official installer: {InstallScript}"));
                actions.Add(new ToolchainAction("install-xcode-clt", "Install Apple Command Line Tools", "Runs 'xcode-select --install'; Rust links with Apple's tools."));
                actions.Add(new ToolchainAction("open-download", "Download rustup", "Opens https://rustup.rs in your browser."));
            }
            else
            {
                actions.Add(new ToolchainAction("rustup-init", "Install Rust via rustup", $"Runs the official installer: {InstallScript}"));
                actions.Add(new ToolchainAction("open-download", "Download rustup", "Opens https://rustup.rs in your browser."));
            }

            if (RustupPath() != null)
            {
                actions.Add(new ToolchainAction("rustup-update", "Update Rust (stable)", "Runs 'rustup update stable' and 'rustup default stable'."));
            }

            if (Directory.Exists(BuildCacheFolder))
            {
                actions.Add(new ToolchainAction("clear-cache", "Clear Rust build cache", "Deletes the shared build folder; the next run recompiles every crate."));
            }

            return actions;
        }
    }

    public void Select(string? executablePath) => _settings.SetSelectedPath(LanguageIds.Rust, executablePath);

    public void Refresh()
    {
        _probes.Clear();
        _sawRustupWithoutToolchain = false;
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
                tooOld.Add($"Rust {probe.Version} ({candidate.Path})");
                continue;
            }

            return ToolchainResolution.Found(ToInfo(candidate, probe));
        }

        return ToolchainResolution.NotFound(RustGuidance.NotInstalled(_host, tooOld, _sawRustupWithoutToolchain));
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

            // A rustup proxy and the toolchain folder it forwards to are the same compiler: list it once.
            var identity = probe.CommitHash != null
                ? $"{probe.CommitHash}|{probe.HostTriple}"
                : $"{probe.Executable}|{probe.Version}";
            if (!seen.Add(identity)) continue;
            found.Add(ToInfo(candidates[i], probe));
        }

        return found;
    }

    public async Task<ToolchainActionResult> RunActionAsync(string actionId, ToolchainQuery query, Action<string> output, CancellationToken ct = default)
    {
        switch (actionId)
        {
            case "open-download":
                BrowserLauncher.Open(RustGuidance.DownloadUrl);
                return new ToolchainActionResult(true, $"Opened {RustGuidance.DownloadUrl} in browser.");

            case "rustup-init":
                output("Installing Rust with the official rustup installer…\n");
                return await InstallAsync(InstallScript, "Rust installation completed.", "The rustup installer failed or was cancelled.", output, ct);

            case "winget-install-rustup":
                output("Installing Rust via winget (winget install Rustlang.Rustup)…\n");
                return await InstallAsync("winget install Rustlang.Rustup --accept-source-agreements --accept-package-agreements", "Rust installation completed.", "winget installation failed or was cancelled.", output, ct);

            case "winget-install-vs":
                output("Installing Visual Studio C++ Build Tools (with the C++ workload) via winget…\n");
                return await InstallAsync("winget install Microsoft.VisualStudio.2022.BuildTools --override \"--passive --wait --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended\" --accept-source-agreements --accept-package-agreements", "Visual Studio Build Tools installation completed.", "winget installation failed or was cancelled.", output, ct);

            case "install-xcode-clt":
                output("Launching Apple Command Line Tools installer (xcode-select --install)…\n");
                var xcodeOk = await ToolchainSetupRunner.ExecuteAsync("xcode-select --install", _host, _launcher, output, ct);
                return new ToolchainActionResult(xcodeOk, xcodeOk ? "Apple Command Line Tools installer requested." : "Failed to launch the xcode-select installer.");

            case "rustup-update":
                output("Updating the stable Rust toolchain…\n");
                return await InstallAsync("rustup update stable && rustup default stable", "Rust is up to date.", "rustup update failed or was cancelled.", output, ct);

            case "clear-cache":
                return ClearBuildCache(output);

            default:
                return new ToolchainActionResult(false, $"Unknown action '{actionId}'.");
        }
    }

    private async Task<ToolchainActionResult> InstallAsync(string command, string success, string failure, Action<string> output, CancellationToken ct)
    {
        var ok = await ToolchainSetupRunner.ExecuteAsync(command, _host, _launcher, output, ct);
        if (ok) Refresh();
        return new ToolchainActionResult(ok, ok ? success : failure);
    }

    private ToolchainActionResult ClearBuildCache(Action<string> output)
    {
        try
        {
            if (Directory.Exists(BuildCacheFolder)) Directory.Delete(BuildCacheFolder, recursive: true);
            output($"Deleted {BuildCacheFolder}\n");
            return new ToolchainActionResult(true, "Rust build cache cleared.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ToolchainActionResult(false, $"Couldn't clear the Rust build cache: {ex.Message}");
        }
    }

    private sealed record ProbeResult(
        Version Version,
        string Executable,
        string Channel,
        string? RustcPath,
        string? HostTriple,
        string? LlvmVersion,
        string? CommitHash);

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
        try
        {
            var result = await _host.RunAsync(path, ["--version"], ProbeTimeout, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();

            var text = (result.StandardOutput + "\n" + result.StandardError).Trim();
            if (RustupWithoutToolchainRegex().IsMatch(text)) _sawRustupWithoutToolchain = true;
            if (result.ExitCode != 0 && string.IsNullOrWhiteSpace(result.StandardOutput)) return null;

            var match = CargoVersionRegex().Match(text);
            if (!match.Success) return null;

            var version = new Version(
                int.Parse(match.Groups["major"].Value),
                int.Parse(match.Groups["minor"].Value),
                int.Parse(match.Groups["patch"].Value));
            var channel = match.Groups["pre"].Success ? match.Groups["pre"].Value.ToLowerInvariant() : "stable";

            string? rustcPath = null, host = null, llvm = null, commit = null;
            var directory = DirectoryOf(path);
            if (directory != null)
            {
                var candidate = Join(directory, ExecutableSearch.ExecutableName("rustc", _host.IsWindows));
                if (_host.FileExists(candidate))
                {
                    rustcPath = candidate;
                    var verbose = await _host.RunAsync(candidate, ["-vV"], ProbeTimeout, ct).ConfigureAwait(false);
                    ct.ThrowIfCancellationRequested();
                    if (verbose.ExitCode == 0)
                    {
                        var verboseText = verbose.StandardOutput;
                        host = RustcHostRegex().Match(verboseText) is { Success: true } h ? h.Groups["host"].Value : null;
                        llvm = RustcLlvmRegex().Match(verboseText) is { Success: true } l ? l.Groups["llvm"].Value : null;
                        commit = CommitHashRegex().Match(verboseText) is { Success: true } c ? c.Groups["hash"].Value : null;
                    }
                }
            }

            return new ProbeResult(version, path, channel, rustcPath, host, llvm, commit);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    [GeneratedRegex(@"^commit-hash:\s*(?<hash>[0-9a-f]{7,40})\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex CommitHashRegex();

    private ToolchainInfo ToInfo(Candidate candidate, ProbeResult probe)
    {
        var properties = new Dictionary<string, string>
        {
            ["channel"] = probe.Channel,
            ["edition"] = $"{RustDirectives.DefaultEdition} (default)"
        };
        if (probe.RustcPath != null) properties["rustc"] = probe.RustcPath;
        if (probe.HostTriple != null) properties["hostTriple"] = probe.HostTriple;
        if (probe.LlvmVersion != null) properties["llvmVersion"] = probe.LlvmVersion;
        if (probe.CommitHash != null) properties["commitHash"] = probe.CommitHash;

        return new ToolchainInfo
        {
            LanguageId = LanguageIds.Rust,
            ExecutablePath = candidate.Path,
            Version = probe.Version,
            DisplayName = probe.Channel == "stable" ? $"Rust {probe.Version}" : $"Rust {probe.Version} ({probe.Channel})",
            Source = SourceOf(candidate),
            Properties = properties
        };
    }

    private static string SourceOf(Candidate candidate)
    {
        var normalized = candidate.Path.Replace('\\', '/');
        if (candidate.Source == "Selected") return "Selected";

        const string toolchains = "/.rustup/toolchains/";
        var at = normalized.IndexOf(toolchains, StringComparison.OrdinalIgnoreCase);
        if (at >= 0)
        {
            var name = normalized[(at + toolchains.Length)..].Split('/')[0];
            return $"rustup toolchain {name}";
        }

        if (normalized.Contains("/.cargo/bin/", StringComparison.OrdinalIgnoreCase)) return "rustup";
        if (normalized.Contains("/opt/homebrew/", StringComparison.Ordinal) || normalized.Contains("/Cellar/", StringComparison.Ordinal)) return "Homebrew";
        if (normalized.Contains("/.nix-profile/", StringComparison.Ordinal) || normalized.StartsWith("/nix/", StringComparison.Ordinal)) return "Nix";
        if (normalized.Contains("/scoop/", StringComparison.OrdinalIgnoreCase)) return "Scoop";
        if (normalized.StartsWith("/usr/bin/", StringComparison.Ordinal) || normalized.StartsWith("/usr/local/bin/", StringComparison.Ordinal)) return "System";
        return candidate.Source;
    }

    private string? RustupPath()
    {
        var name = ExecutableSearch.ExecutableName("rustup", _host.IsWindows);
        var cargoHome = _host.GetEnvironmentVariable("CARGO_HOME");
        if (!string.IsNullOrWhiteSpace(cargoHome))
        {
            var fromCargoHome = Join(Join(cargoHome, "bin"), name);
            if (_host.FileExists(fromCargoHome)) return fromCargoHome;
        }

        if (!string.IsNullOrEmpty(_host.HomeDirectory))
        {
            var fromHome = Join(Join(Join(_host.HomeDirectory, ".cargo"), "bin"), name);
            if (_host.FileExists(fromHome)) return fromHome;
        }

        return null;
    }

    private string? DirectoryOf(string path)
    {
        var slash = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
        return slash > 0 ? path[..slash] : null;
    }

    private string Join(string folder, string name) =>
        folder.TrimEnd('/', '\\') + (_host.IsWindows ? "\\" : "/") + name;
}
