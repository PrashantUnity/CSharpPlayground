#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Services.Common;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace DartSupportExtension;

/// <summary>
/// Finds the Dart SDK / Flutter toolchain on this machine, probes its version,
/// and displays active toolchain status in the Status Bar.
/// </summary>
public sealed class DartToolchainProvider : IToolchainProvider
{
    public static readonly Version MinimumVersion = new(2, 12, 0);

    private readonly IHostEnvironment _host;
    private readonly IProcessLauncher _launcher;
    private readonly ToolchainSettingsStore _settings;
    private readonly ConcurrentDictionary<string, Lazy<Task<ProbeResult?>>> _probes = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Regex VersionRegex = new(
        @"Dart SDK version:\s+(?<version>\d+\.\d+\.\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public DartToolchainProvider(IHostEnvironment host, IProcessLauncher launcher, ToolchainSettingsStore settings)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public string LanguageId => "dart";
    public string ToolName => "Dart SDK";
    public string? SelectedPath => _settings.GetSelectedPath("dart");

    public IReadOnlyList<ToolchainAction> Actions
    {
        get
        {
            var actions = new List<ToolchainAction>();
            if (_host.IsMacOS)
            {
                actions.Add(new ToolchainAction("brew-install-dart", "Install Dart via Homebrew", "Runs 'brew install dart' to install official Dart SDK."));
                actions.Add(new ToolchainAction("open-download", "Download Dart SDK", "Opens https://dart.dev/get-dart in browser."));
            }
            else if (_host.IsWindows)
            {
                actions.Add(new ToolchainAction("choco-install-dart", "Install Dart via Chocolatey", "Runs 'choco install dart-sdk' in admin terminal."));
                actions.Add(new ToolchainAction("open-download", "Download Dart SDK for Windows", "Opens https://dart.dev/get-dart in browser."));
            }
            else
            {
                actions.Add(new ToolchainAction("open-download", "Download Dart SDK for Linux", "Opens https://dart.dev/get-dart in browser."));
            }
            return actions;
        }
    }

    public void Select(string? executablePath) => _settings.SetSelectedPath("dart", executablePath);

    public void Refresh() => _probes.Clear();

    public async Task<ToolchainResolution> ResolveAsync(ToolchainQuery query, CancellationToken ct = default)
    {
        var candidates = GetCandidates(query);

        foreach (var path in candidates)
        {
            var probe = await ProbeAsync(path, ct).ConfigureAwait(false);
            if (probe == null) continue;

            if (probe.Version < MinimumVersion)
            {
                continue;
            }

            return ToolchainResolution.Found(new ToolchainInfo
            {
                LanguageId = "dart",
                ExecutablePath = probe.Executable,
                DisplayName = $"Dart {probe.Version} ({Path.GetFileName(probe.Executable)})",
                Version = probe.Version,
                Source = "System"
            });
        }

        var guidance = new MissingToolchainGuidance(
            "Dart SDK Not Found",
            "Dart SDK is not installed or not available on PATH.",
            _host.IsMacOS
                ? ["Run 'brew install dart' in your terminal.", "Or download the Dart SDK from https://dart.dev/get-dart."]
                : _host.IsWindows
                    ? ["Run 'choco install dart-sdk' in an administrator prompt.", "Or download from https://dart.dev/get-dart."]
                    : ["Install via your distribution package manager (e.g. 'sudo apt-get install dart')."],
            "https://dart.dev/get-dart");

        return ToolchainResolution.NotFound(guidance);
    }

    public async Task<IReadOnlyList<ToolchainInfo>> ListAsync(ToolchainQuery query, CancellationToken ct = default)
    {
        var candidates = GetCandidates(query);
        var list = new List<ToolchainInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in candidates)
        {
            var probe = await ProbeAsync(path, ct).ConfigureAwait(false);
            if (probe == null || probe.Version < MinimumVersion) continue;

            if (seen.Add(probe.Executable))
            {
                list.Add(new ToolchainInfo
                {
                    LanguageId = "dart",
                    ExecutablePath = probe.Executable,
                    DisplayName = $"Dart {probe.Version}",
                    Version = probe.Version,
                    Source = "System"
                });
            }
        }

        return list;
    }

    public async Task<ToolchainActionResult> RunActionAsync(string actionId, ToolchainQuery query, Action<string> output, CancellationToken ct = default)
    {
        switch (actionId)
        {
            case "open-download":
                BrowserLauncher.Open("https://dart.dev/get-dart");
                return new ToolchainActionResult(true, "Opened https://dart.dev/get-dart in browser.");

            case "brew-install-dart":
                output("Installing Dart SDK via Homebrew (brew install dart)...\n");
                var brewResult = await ToolchainSetupRunner.ExecuteAsync("brew install dart", _host, _launcher, output, ct);
                Refresh();
                return new ToolchainActionResult(brewResult, brewResult ? "Dart SDK installed." : "Failed to install Dart via brew.");

            default:
                return new ToolchainActionResult(false, $"Unknown action {actionId}");
        }
    }

    private List<string> GetCandidates(ToolchainQuery query)
    {
        var list = new List<string>();

        var selected = SelectedPath;
        if (!string.IsNullOrWhiteSpace(selected) && File.Exists(selected))
        {
            list.Add(selected);
        }

        // 1. Common system paths
        if (_host.IsMacOS)
        {
            list.Add("/opt/homebrew/bin/dart");
            list.Add("/usr/local/bin/dart");
        }
        else if (_host.IsWindows)
        {
            list.Add(@"C:\tools\dart-sdk\bin\dart.exe");
            list.Add(@"C:\Program Files\Dart\dart-sdk\bin\dart.exe");
        }
        else
        {
            list.Add("/usr/bin/dart");
            list.Add("/usr/lib/dart/bin/dart");
        }

        // 2. PATH resolution
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var separator = _host.IsWindows ? ';' : ':';
        var binaryName = _host.IsWindows ? "dart.exe" : "dart";

        foreach (var dir in pathEnv.Split(separator, StringSplitOptions.RemoveEmptyEntries))
        {
            var full = Path.Combine(dir.Trim(), binaryName);
            if (File.Exists(full))
            {
                list.Add(full);
            }
        }

        return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private Task<ProbeResult?> ProbeAsync(string executable, CancellationToken ct)
    {
        return _probes.GetOrAdd(executable, p => new Lazy<Task<ProbeResult?>>(() => RunProbeAsync(p, ct))).Value;
    }

    private async Task<ProbeResult?> RunProbeAsync(string executable, CancellationToken ct)
    {
        if (!File.Exists(executable)) return null;

        var output = new System.Text.StringBuilder();
        try
        {
            using var proc = _launcher.Start(new ProcessStartSpec
            {
                FileName = executable,
                Arguments = ["--version"],
                WorkingDirectory = _host.HomeDirectory
            },
            s => output.Append(s),
            s => output.Append(s));

            var exit = await proc.WaitForExitOrKillAsync(ct).ConfigureAwait(false);
            var text = output.ToString();
            var match = VersionRegex.Match(text);
            if (match.Success && Version.TryParse(match.Groups["version"].Value, out var ver))
            {
                return new ProbeResult(executable, ver);
            }
        }
        catch { }

        return null;
    }

    private sealed record ProbeResult(string Executable, Version Version);
}
