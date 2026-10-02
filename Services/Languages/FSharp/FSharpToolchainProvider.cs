using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using PdfEditorApp.Plugins.CSharpEditor.Services.Common;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.FSharp;

/// <summary>
/// Finds the F# toolchain (dotnet fsi / .NET SDK) on this machine, probes its version,
/// and resolves the runtime for F# scripts (.fsx) and polyglot notebooks.
/// </summary>
public sealed partial class FSharpToolchainProvider : IToolchainProvider
{
    public static readonly Version MinimumVersion = new(6, 0, 0);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(8);

    private readonly IHostEnvironment _host;
    private readonly IProcessLauncher _launcher;
    private readonly ToolchainSettingsStore _settings;
    private readonly ConcurrentDictionary<string, Lazy<Task<ProbeResult?>>> _probes = new(StringComparer.Ordinal);

    [GeneratedRegex(@"F#\s+(?:Interactive\s+version\s+[\d\.]+\s+for\s+F#\s+)?(?<major>\d+)\.(?<minor>\d+)(?:\.(?<patch>\d+))?", RegexOptions.IgnoreCase)]
    private static partial Regex FSharpVersionRegex();

    [GeneratedRegex(@"^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex DotNetVersionRegex();

    public FSharpToolchainProvider(IHostEnvironment host, IProcessLauncher launcher, ToolchainSettingsStore settings)
    {
        _host = host;
        _launcher = launcher;
        _settings = settings;
    }

    public string LanguageId => LanguageIds.FSharp;
    public string ToolName => "F# Interactive (dotnet fsi)";

    public string? SelectedPath => _settings.GetSelectedPath(LanguageIds.FSharp);

    public IReadOnlyList<ToolchainAction> Actions
    {
        get
        {
            var actions = new List<ToolchainAction>();
            if (_host.IsWindows)
            {
                actions.Add(new ToolchainAction("winget-install-dotnet", "Install .NET SDK via winget", "Runs 'winget install Microsoft.DotNet.SDK.10'."));
                actions.Add(new ToolchainAction("open-download", "Download .NET SDK", "Opens https://dotnet.microsoft.com/download in browser."));
            }
            else if (_host.IsMacOS)
            {
                actions.Add(new ToolchainAction("brew-install-dotnet", "Install .NET SDK via Homebrew", "Runs 'brew install --cask dotnet-sdk' via Homebrew."));
                actions.Add(new ToolchainAction("open-download", "Download .NET SDK for macOS", "Opens https://dotnet.microsoft.com/download in browser."));
            }
            else
            {
                actions.Add(new ToolchainAction("open-download", "Download .NET SDK", "Opens https://dotnet.microsoft.com/download in browser."));
            }
            return actions;
        }
    }

    public void Select(string? executablePath) => _settings.SetSelectedPath(LanguageIds.FSharp, executablePath);

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
                tooOld.Add($"F# / .NET {probe.Version} ({candidate.Path})");
                continue;
            }

            return ToolchainResolution.Found(ToInfo(candidate, probe));
        }

        return ToolchainResolution.NotFound(FSharpGuidance.NotInstalled(_host, tooOld));
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
                BrowserLauncher.Open("https://dotnet.microsoft.com/download");
                return new ToolchainActionResult(true, "Opened https://dotnet.microsoft.com/download in browser.");

            case "brew-install-dotnet":
                output("Installing .NET SDK via Homebrew (brew install --cask dotnet-sdk)…\n");
                var brewOk = await ToolchainSetupRunner.ExecuteAsync("brew install --cask dotnet-sdk", _host, _launcher, output, ct);
                return new ToolchainActionResult(brewOk, brewOk ? ".NET SDK installation completed." : "Homebrew installation failed or was cancelled.");

            case "winget-install-dotnet":
                output("Installing .NET SDK via winget (winget install Microsoft.DotNet.SDK.10)…\n");
                var wingetOk = await ToolchainSetupRunner.ExecuteAsync("winget install Microsoft.DotNet.SDK.10 --accept-source-agreements --accept-package-agreements", _host, _launcher, output, ct);
                return new ToolchainActionResult(wingetOk, wingetOk ? ".NET SDK installation completed." : "winget installation failed or was cancelled.");

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
            var isFsiDirect = Path.GetFileNameWithoutExtension(path).Equals("fsi", StringComparison.OrdinalIgnoreCase);
            var args = isFsiDirect ? new[] { "--version" } : new[] { "fsi", "--version" };

            var result = await _host.RunAsync(path, args, ProbeTimeout, ct).ConfigureAwait(false);
            var outText = (result.StandardOutput + " " + result.StandardError).Trim();

            var match = FSharpVersionRegex().Match(outText);
            if (match.Success)
            {
                int major = int.Parse(match.Groups["major"].Value);
                int minor = int.Parse(match.Groups["minor"].Value);
                int patch = match.Groups["patch"].Success ? int.Parse(match.Groups["patch"].Value) : 0;
                return new ProbeResult(new Version(major, minor, patch), path, outText);
            }

            // Fallback: probe `dotnet --version`
            if (!isFsiDirect)
            {
                var dotNetVerRes = await _host.RunAsync(path, ["--version"], ProbeTimeout, ct).ConfigureAwait(false);
                var dotNetOut = dotNetVerRes.StandardOutput.Trim();
                var dotNetMatch = DotNetVersionRegex().Match(dotNetOut);
                if (dotNetMatch.Success)
                {
                    int major = int.Parse(dotNetMatch.Groups["major"].Value);
                    int minor = int.Parse(dotNetMatch.Groups["minor"].Value);
                    int patch = int.Parse(dotNetMatch.Groups["patch"].Value);
                    return new ProbeResult(new Version(major, minor, patch), path, dotNetOut);
                }
            }

            return null;
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
            LanguageId = LanguageIds.FSharp,
            ExecutablePath = candidate.Path,
            Version = probe.Version,
            DisplayName = $"F# {probe.Version} ({Path.GetFileName(candidate.Path)})",
            Source = candidate.Source
        };
    }
}
