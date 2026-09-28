using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.CSharp;

/// <summary>
/// Finds the installed .NET SDKs (<c>dotnet</c>) on this machine, prioritizing user selection,
/// DOTNET_ROOT, and system PATH.
/// </summary>
public sealed partial class CSharpToolchainProvider : IToolchainProvider
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    private readonly IHostEnvironment _host;
    private readonly IProcessLauncher _launcher;
    private readonly ToolchainSettingsStore _settings;
    private readonly ConcurrentDictionary<string, Lazy<Task<ProbeResult?>>> _probes = new(StringComparer.Ordinal);

    [GeneratedRegex(@"^(?<ver>\d+\.\d+\.\d+)")]
    private static partial Regex VersionRegex();

    public CSharpToolchainProvider(IHostEnvironment host, IProcessLauncher launcher, ToolchainSettingsStore settings)
    {
        _host = host;
        _launcher = launcher;
        _settings = settings;
    }

    public string LanguageId => LanguageIds.CSharp;
    public string ToolName => ".NET SDK";

    public string? SelectedPath => _settings.GetSelectedPath(LanguageIds.CSharp);

    public IReadOnlyList<ToolchainAction> Actions => Array.Empty<ToolchainAction>();

    public void Select(string? executablePath) => _settings.SetSelectedPath(LanguageIds.CSharp, executablePath);

    public void Refresh() => _probes.Clear();

    public async Task<ToolchainResolution> ResolveAsync(ToolchainQuery query, CancellationToken ct = default)
    {
        await foreach (var candidate in CandidatesAsync(query, ct))
        {
            var probe = await ProbeAsync(candidate.Path, ct);
            if (probe == null) continue;

            return ToolchainResolution.Found(new ToolchainInfo
            {
                LanguageId = LanguageIds.CSharp,
                ExecutablePath = candidate.Path,
                DisplayName = $".NET SDK {probe.Version}",
                Source = candidate.Source,
                Version = probe.Version
            });
        }

        return ToolchainResolution.NotFound(Guidance());
    }

    public async Task<IReadOnlyList<ToolchainInfo>> ListAsync(ToolchainQuery query, CancellationToken ct = default)
    {
        var list = new List<ToolchainInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await foreach (var candidate in CandidatesAsync(query, ct))
        {
            if (!seen.Add(candidate.Path)) continue;

            var probe = await ProbeAsync(candidate.Path, ct);
            if (probe == null) continue;

            list.Add(new ToolchainInfo
            {
                LanguageId = LanguageIds.CSharp,
                ExecutablePath = candidate.Path,
                DisplayName = $".NET SDK {probe.Version}",
                Source = candidate.Source,
                Version = probe.Version
            });
        }

        return list;
    }

    public Task<ToolchainActionResult> RunActionAsync(string actionId, ToolchainQuery query, Action<string> output, CancellationToken ct = default)
    {
        return Task.FromResult(new ToolchainActionResult(false, "No actions available."));
    }

    private async IAsyncEnumerable<Candidate> CandidatesAsync(ToolchainQuery query, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        if (SelectedPath is { Length: > 0 } selected && _host.FileExists(selected))
        {
            yield return new Candidate(selected, "Saved choice");
        }

        var dotnetRoot = _host.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrEmpty(dotnetRoot))
        {
            var rootDotnet = Path.Combine(dotnetRoot, _host.IsWindows ? "dotnet.exe" : "dotnet");
            if (_host.FileExists(rootDotnet))
            {
                yield return new Candidate(rootDotnet, "DOTNET_ROOT");
            }
        }

        var path = await _host.GetLoginShellPathAsync(ct) ?? _host.GetEnvironmentVariable("PATH");
        var folders = ExecutableSearch.SplitPath(path, _host.IsWindows);
        foreach (var candidate in ExecutableSearch.FindAll(_host, folders, ["dotnet"]))
        {
            yield return new Candidate(candidate, "PATH");
        }

        var home = _host.GetEnvironmentVariable("HOME") ?? _host.GetEnvironmentVariable("USERPROFILE") ?? string.Empty;
        var wellKnown = _host.IsWindows
            ? [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe")]
            : new[]
            {
                "/usr/local/share/dotnet/dotnet",
                "/opt/homebrew/bin/dotnet",
                "/usr/bin/dotnet",
                Path.Combine(home, ".dotnet", "dotnet")
            };

        foreach (var p in wellKnown)
        {
            if (_host.FileExists(p))
            {
                yield return new Candidate(p, "Standard installation");
            }
        }
    }

    private Task<ProbeResult?> ProbeAsync(string path, CancellationToken ct)
    {
        return _probes.GetOrAdd(path, p => new Lazy<Task<ProbeResult?>>(() => RunProbeAsync(p, ct))).Value;
    }

    private async Task<ProbeResult?> RunProbeAsync(string executablePath, CancellationToken ct)
    {
        try
        {
            var result = await _host.RunAsync(executablePath, ["--version"], ProbeTimeout, ct);
            ct.ThrowIfCancellationRequested();
            if (!result.Succeeded) return null;

            var match = VersionRegex().Match(result.StandardOutput.Trim());
            if (match.Success && Version.TryParse(match.Groups["ver"].Value, out var ver))
            {
                return new ProbeResult(ver);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private MissingToolchainGuidance Guidance()
    {
        var (cmd, url) = _host.IsWindows
            ? ("winget install Microsoft.DotNet.SDK.10", "https://dotnet.microsoft.com/download")
            : _host.IsMacOS
                ? ("brew install dotnet-sdk", "https://dotnet.microsoft.com/download")
                : ("sudo apt-get install -y dotnet-sdk-10.0", "https://dotnet.microsoft.com/download");

        return new MissingToolchainGuidance(
            ".NET SDK wasn't found",
            $".NET SDK is required to run C# code with the external CLI runner. Install it from {url}.",
            [cmd]);
    }

    private sealed record Candidate(string Path, string Source);
    private sealed record ProbeResult(Version Version);
}
