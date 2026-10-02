using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using PdfEditorApp.Plugins.CSharpEditor.Services.Common;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Sql;

/// <summary>
/// Finds the SQLite 3 CLI toolchain (sqlite3) on this machine, probes its version,
/// and resolves the execution runtime for SQL scripts and polyglot notebook cells.
/// </summary>
public sealed partial class SqlToolchainProvider : IToolchainProvider
{
    public static readonly Version MinimumVersion = new(3, 0, 0);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(6);

    private readonly IHostEnvironment _host;
    private readonly IProcessLauncher _launcher;
    private readonly ToolchainSettingsStore _settings;
    private readonly ConcurrentDictionary<string, Lazy<Task<ProbeResult?>>> _probes = new(StringComparer.Ordinal);

    [GeneratedRegex(@"^(?:(?:SQLite\s+version\s+)|(?:\s*))?(?<major>\d+)\.(?<minor>\d+)(?:\.(?<patch>\d+))?", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex SqliteVersionRegex();

    public SqlToolchainProvider(IHostEnvironment host, IProcessLauncher launcher, ToolchainSettingsStore settings)
    {
        _host = host;
        _launcher = launcher;
        _settings = settings;
    }

    public string LanguageId => LanguageIds.Sql;
    public string ToolName => "SQLite CLI (sqlite3)";

    public string? SelectedPath => _settings.GetSelectedPath(LanguageIds.Sql);

    public IReadOnlyList<ToolchainAction> Actions
    {
        get
        {
            var actions = new List<ToolchainAction>();
            if (_host.IsWindows)
            {
                actions.Add(new ToolchainAction("winget-install-sqlite", "Install SQLite via winget", "Runs 'winget install SQLite.SQLite'."));
                actions.Add(new ToolchainAction("open-download", "Download SQLite Tools", "Opens https://www.sqlite.org/download.html in browser."));
            }
            else if (_host.IsMacOS)
            {
                actions.Add(new ToolchainAction("brew-install-sqlite", "Install SQLite via Homebrew", "Runs 'brew install sqlite' via Homebrew."));
                actions.Add(new ToolchainAction("open-download", "Download SQLite Tools", "Opens https://www.sqlite.org/download.html in browser."));
            }
            else
            {
                actions.Add(new ToolchainAction("open-download", "Download SQLite Tools", "Opens https://www.sqlite.org/download.html in browser."));
            }
            return actions;
        }
    }

    public void Select(string? executablePath) => _settings.SetSelectedPath(LanguageIds.Sql, executablePath);

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
                tooOld.Add($"SQLite {probe.Version} ({candidate.Path})");
                continue;
            }

            return ToolchainResolution.Found(ToInfo(candidate, probe));
        }

        return ToolchainResolution.NotFound(SqlGuidance.NotInstalled(_host, tooOld));
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
                BrowserLauncher.Open("https://www.sqlite.org/download.html");
                return new ToolchainActionResult(true, "Opened https://www.sqlite.org/download.html in browser.");

            case "brew-install-sqlite":
                output("Installing SQLite via Homebrew (brew install sqlite)…\n");
                var brewOk = await ToolchainSetupRunner.ExecuteAsync("brew install sqlite", _host, _launcher, output, ct);
                return new ToolchainActionResult(brewOk, brewOk ? "SQLite installation completed." : "Homebrew installation failed or was cancelled.");

            case "winget-install-sqlite":
                output("Installing SQLite via winget (winget install SQLite.SQLite)…\n");
                var wingetOk = await ToolchainSetupRunner.ExecuteAsync("winget install SQLite.SQLite --accept-source-agreements --accept-package-agreements", _host, _launcher, output, ct);
                return new ToolchainActionResult(wingetOk, wingetOk ? "SQLite installation completed." : "winget installation failed or was cancelled.");

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
            var result = await _host.RunAsync(path, ["--version"], ProbeTimeout, ct).ConfigureAwait(false);
            var outText = (result.StandardOutput + " " + result.StandardError).Trim();

            var match = SqliteVersionRegex().Match(outText);
            if (!match.Success) return null;

            var major = int.Parse(match.Groups["major"].Value);
            var minor = int.Parse(match.Groups["minor"].Value);
            var patch = match.Groups["patch"].Success ? int.Parse(match.Groups["patch"].Value) : 0;
            var version = new Version(major, minor, patch);

            return new ProbeResult(version, path, outText);
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
            LanguageId = LanguageIds.Sql,
            ExecutablePath = candidate.Path,
            Version = probe.Version,
            DisplayName = $"SQLite {probe.Version} ({Path.GetFileName(candidate.Path)})",
            Source = candidate.Source
        };
    }
}
