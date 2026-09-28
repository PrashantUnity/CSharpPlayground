using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.JavaScript;

/// <summary>
/// Finds the Node.js runtimes on this computer and picks the one a JavaScript script or notebook cell runs with,
/// prioritizing user selection, project root, and PATH (login shell PATH included). Only Node.js 18.0.0+ is used.
/// </summary>
public sealed partial class JavaScriptToolchainProvider : IToolchainProvider
{
    public static readonly Version MinimumVersion = new(18, 0, 0);

    private const string ProbeMarker = "__FRY_PROBE__";
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(8);

    private const string ProbeScript =
        "const v = process.versions.node; console.log('" + ProbeMarker + "' + JSON.stringify({ version: v, executable: process.execPath }));";

    private readonly IHostEnvironment _host;
    private readonly IProcessLauncher _launcher;
    private readonly ToolchainSettingsStore _settings;
    private readonly string _javascriptRoot;
    private readonly ConcurrentDictionary<string, Lazy<Task<ProbeResult?>>> _probes = new(StringComparer.Ordinal);

    public JavaScriptToolchainProvider(IHostEnvironment host, IProcessLauncher launcher, ToolchainSettingsStore settings, string javascriptRoot)
    {
        _host = host;
        _launcher = launcher;
        _settings = settings;
        _javascriptRoot = javascriptRoot;
    }

    public string LanguageId => LanguageIds.JavaScript;
    public string ToolName => "Node.js";

    public string? SelectedPath => _settings.GetSelectedPath(LanguageIds.JavaScript);

    public IReadOnlyList<ToolchainAction> Actions => Array.Empty<ToolchainAction>();

    public void Select(string? executablePath) => _settings.SetSelectedPath(LanguageIds.JavaScript, executablePath);

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
                tooOld.Add($"Node.js {probe.Version} ({candidate.Path})");
                continue;
            }

            return ToolchainResolution.Found(ToInfo(candidate, probe));
        }

        return ToolchainResolution.NotFound(JavaScriptGuidance.NotInstalled(_host, tooOld));
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

    public Task<ToolchainActionResult> RunActionAsync(string actionId, ToolchainQuery query, Action<string> output, CancellationToken ct = default) =>
        Task.FromResult(new ToolchainActionResult(false, $"Unknown action '{actionId}'."));

    private sealed record ProbeResult(Version Version, string Executable);

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
        var result = await _host.RunAsync(path, ["-e", ProbeScript], ProbeTimeout, ct);
        ct.ThrowIfCancellationRequested();
        if (!result.Succeeded)
        {
            Debug.WriteLine($"[CSharpEditorPlugin] {path} isn't a usable Node.js (exit {result.ExitCode}{(result.TimedOut ? ", timed out" : string.Empty)}).");
            return null;
        }

        var line = result.StandardOutput.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.StartsWith(ProbeMarker, StringComparison.Ordinal));
        if (line == null) return null;

        try
        {
            using var json = JsonDocument.Parse(line[ProbeMarker.Length..]);
            var root = json.RootElement;
            if (!Version.TryParse(root.GetProperty("version").GetString(), out var version)) return null;
            var executable = root.TryGetProperty("executable", out var execProp) ? execProp.GetString() ?? path : path;
            return new ProbeResult(version, executable);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }

    private ToolchainInfo ToInfo(Candidate candidate, ProbeResult probe)
    {
        return new ToolchainInfo
        {
            LanguageId = LanguageIds.JavaScript,
            ExecutablePath = candidate.Path,
            Version = probe.Version,
            DisplayName = $"Node.js {probe.Version}",
            Source = candidate.Source.StartsWith("Project", StringComparison.Ordinal) ? candidate.Source : SourceOf(candidate.Path, candidate.Source)
        };
    }

    private static string SourceOf(string path, string fallback)
    {
        var normalized = path.Replace('\\', '/');
        if (normalized.Contains("/opt/homebrew/", StringComparison.Ordinal) || normalized.Contains("/Cellar/", StringComparison.Ordinal) || normalized.Contains("linuxbrew", StringComparison.Ordinal)) return "Homebrew";
        if (normalized.Contains("/.nvm/", StringComparison.Ordinal) || normalized.Contains("/nvm/", StringComparison.OrdinalIgnoreCase)) return "NVM";
        if (normalized.Contains("/.fnm/", StringComparison.Ordinal) || normalized.Contains("/fnm/", StringComparison.OrdinalIgnoreCase)) return "fnm";
        if (normalized.Contains("/.volta/", StringComparison.Ordinal) || normalized.Contains("/volta/", StringComparison.OrdinalIgnoreCase)) return "Volta";
        if (normalized.Contains("/.asdf/", StringComparison.Ordinal)) return "asdf";
        if (normalized.Contains("Program Files/nodejs", StringComparison.OrdinalIgnoreCase)) return "nodejs.org";
        if (normalized.StartsWith("/usr/bin/", StringComparison.Ordinal)) return "System";
        return fallback;
    }
}
