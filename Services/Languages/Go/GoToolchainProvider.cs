using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using PdfEditorApp.Plugins.CSharpEditor.Services.Common;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;

/// <summary>
/// Finds the Go toolchain (go) on this machine, probes its version, and resolves the runtime for Go source files and notebooks.
/// </summary>
public sealed partial class GoToolchainProvider : IToolchainProvider
{
    public static readonly Version MinimumVersion = new(1, 18, 0);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(8);

    private readonly IHostEnvironment _host;
    private readonly IProcessLauncher _launcher;
    private readonly ToolchainSettingsStore _settings;
    private readonly string _goRoot;
    private readonly ConcurrentDictionary<string, Lazy<Task<ProbeResult?>>> _probes = new(StringComparer.Ordinal);

    [GeneratedRegex(@"go\s+version\s+go(?<major>\d+)\.(?<minor>\d+)(?:\.(?<patch>\d+))?", RegexOptions.IgnoreCase)]
    private static partial Regex GoVersionRegex();

    public GoToolchainProvider(IHostEnvironment host, IProcessLauncher launcher, ToolchainSettingsStore settings, string goRoot)
    {
        _host = host;
        _launcher = launcher;
        _settings = settings;
        _goRoot = goRoot;
    }

    public string LanguageId => LanguageIds.Go;
    public string ToolName => "Go Toolchain";

    public string? SelectedPath => _settings.GetSelectedPath(LanguageIds.Go);

    public IReadOnlyList<ToolchainAction> Actions
    {
        get
        {
            var actions = new List<ToolchainAction>();
            if (_host.IsWindows)
            {
                actions.Add(new ToolchainAction("winget-install-go", "Install Go via winget", "Runs 'winget install GoLang.Go' to install official Go distribution."));
                actions.Add(new ToolchainAction("open-download", "Download Go Installer", "Opens https://go.dev/dl/ in browser."));
            }
            else if (_host.IsMacOS)
            {
                actions.Add(new ToolchainAction("brew-install-go", "Install Go via Homebrew", "Runs 'brew install go' to install Go compiler and tools."));
                actions.Add(new ToolchainAction("open-download", "Download Go for macOS", "Opens https://go.dev/dl/ in browser."));
            }
            else
            {
                actions.Add(new ToolchainAction("open-download", "Download Go Distribution", "Opens https://go.dev/dl/ in browser."));
            }
            return actions;
        }
    }

    public void Select(string? executablePath) => _settings.SetSelectedPath(LanguageIds.Go, executablePath);

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
                tooOld.Add($"Go {probe.Version} ({candidate.Path})");
                continue;
            }

            return ToolchainResolution.Found(ToInfo(candidate, probe));
        }

        return ToolchainResolution.NotFound(GoGuidance.NotInstalled(_host, tooOld));
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
                BrowserLauncher.Open("https://go.dev/dl/");
                return new ToolchainActionResult(true, "Opened https://go.dev/dl/ in browser.");

            case "brew-install-go":
                output("Installing Go via Homebrew (brew install go)…\n");
                var brewOk = await ToolchainSetupRunner.ExecuteAsync("brew install go", _host, _launcher, output, ct);
                return new ToolchainActionResult(brewOk, brewOk ? "Go installation completed." : "Homebrew installation failed or was cancelled.");

            case "winget-install-go":
                output("Installing Go via winget (winget install GoLang.Go)…\n");
                var wingetOk = await ToolchainSetupRunner.ExecuteAsync("winget install GoLang.Go --accept-source-agreements --accept-package-agreements", _host, _launcher, output, ct);
                return new ToolchainActionResult(wingetOk, wingetOk ? "Go installation completed." : "winget installation failed or was cancelled.");

            default:
                return new ToolchainActionResult(false, $"Unknown action '{actionId}'.");
        }
    }

    private sealed record ProbeResult(Version Version, string Executable, string RawOutput);

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
            var result = await _host.RunAsync(path, ["version"], ProbeTimeout, ct).ConfigureAwait(false);
            if (result.ExitCode != 0 && string.IsNullOrWhiteSpace(result.StandardOutput)) return null;

            var outText = (result.StandardOutput + " " + result.StandardError).Trim();
            var match = GoVersionRegex().Match(outText);
            if (!match.Success) return null;

            int major = int.Parse(match.Groups["major"].Value);
            int minor = int.Parse(match.Groups["minor"].Value);
            int patch = match.Groups["patch"].Success ? int.Parse(match.Groups["patch"].Value) : 0;
            var ver = new Version(major, minor, patch);

            return new ProbeResult(ver, path, outText);
        }
        catch
        {
            return null;
        }
    }

    private static ToolchainInfo ToInfo(Candidate candidate, ProbeResult probe)
    {
        return new ToolchainInfo
        {
            LanguageId = LanguageIds.Go,
            ExecutablePath = candidate.Path,
            Version = probe.Version,
            DisplayName = $"Go {probe.Version}",
            Source = candidate.Source
        };
    }
}
