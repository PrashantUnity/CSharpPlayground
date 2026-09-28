using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Python;

/// <summary>
/// Finds the Python interpreters on this computer and picks the one a document runs with, in this order: the one the
/// user picked; the project's own virtual environment (a <c>.venv</c>, <c>venv</c> or <c>env</c> folder with a
/// <c>pyvenv.cfg</c>, from the document's folder up to the workspace root); the studio environment; then every
/// <c>python3</c>/<c>python</c> on the login shell's PATH, the studio's PATH and the usual install folders. Each is
/// run once to learn its version and whether pip and venv work, and only Python 3.9+ is used.
/// </summary>
public sealed partial class PythonToolchainProvider : IToolchainProvider
{
    public const string CreateStudioEnvironmentAction = "python.create-studio-environment";
    public const string RecreateStudioEnvironmentAction = "python.recreate-studio-environment";

    // Properties recorded for each interpreter found.
    public const string PrefixProperty = "prefix";
    public const string BasePrefixProperty = "basePrefix";
    public const string VirtualEnvironmentProperty = "virtualEnvironment";
    public const string ExternallyManagedProperty = "externallyManaged";
    public const string PipProperty = "pip";
    public const string VenvProperty = "venv";
    public const string SitePackagesProperty = "sitePackages";
    public const string StudioEnvironmentProperty = "studioEnvironment";

    public static readonly Version MinimumVersion = new(3, 9);

    private const string ProbeMarker = "__FRY_PROBE__";
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(8);
    private static readonly string[] ProjectEnvironmentFolders = [".venv", "venv", "env"];

    // A single line, so it survives any platform's argument quoting. Reads everything the studio needs in one start.
    private const string ProbeScript =
        "import sys, os, json, sysconfig, importlib.util as u; p = sysconfig.get_paths(); " +
        "print('" + ProbeMarker + "' + json.dumps({'version': '%d.%d.%d' % sys.version_info[:3], " +
        "'implementation': sys.implementation.name, 'executable': sys.executable, 'prefix': sys.prefix, " +
        "'base_prefix': getattr(sys, 'base_prefix', sys.prefix), " +
        "'externally_managed': os.path.exists(os.path.join(p.get('stdlib', ''), 'EXTERNALLY-MANAGED')), " +
        "'pip': u.find_spec('pip') is not None, 'venv': u.find_spec('venv') is not None and u.find_spec('ensurepip') is not None, " +
        "'site_packages': p.get('purelib', '')}))";

    private readonly IHostEnvironment _host;
    private readonly IProcessLauncher _launcher;
    private readonly ToolchainSettingsStore _settings;
    private readonly string _pythonRoot;
    private readonly ConcurrentDictionary<string, Lazy<Task<ProbeResult?>>> _probes = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _environmentLock = new(1, 1);
    private Lazy<Task<bool>>? _commandLineToolsInstalled;

    /// <param name="pythonRoot">The plugin's Python folder: studio environments go in <c>envs/</c> under it.</param>
    public PythonToolchainProvider(IHostEnvironment host, IProcessLauncher launcher, ToolchainSettingsStore settings, string pythonRoot)
    {
        _host = host;
        _launcher = launcher;
        _settings = settings;
        _pythonRoot = pythonRoot;
    }

    public string LanguageId => LanguageIds.Python;
    public string ToolName => "Python";

    public string StudioEnvironmentsFolder => Path.Combine(_pythonRoot, "envs");

    public string? SelectedPath => _settings.GetSelectedPath(LanguageIds.Python);

    private static readonly ToolchainAction CreateEnvironment = new(CreateStudioEnvironmentAction, "Create studio environment",
        "A virtual environment of the studio's own, for installing packages without touching your Python");

    private static readonly ToolchainAction RecreateEnvironment = new(RecreateStudioEnvironmentAction, "Recreate studio environment",
        "Start the studio environment again, e.g. after a Python upgrade broke it");

    /// <summary>Create the studio environment, or recreate it once there is one.</summary>
    public IReadOnlyList<ToolchainAction> Actions => StudioEnvironments().Any() ? [RecreateEnvironment] : [CreateEnvironment];

    public void Select(string? executablePath) => _settings.SetSelectedPath(LanguageIds.Python, executablePath);

    public void Refresh()
    {
        _probes.Clear();
        _commandLineToolsInstalled = null;
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
                tooOld.Add($"Python {probe.Version} ({candidate.Path})");
                continue;
            }

            return ToolchainResolution.Found(ToInfo(candidate, probe));
        }

        return ToolchainResolution.NotFound(PythonGuidance.NotInstalled(_host, tooOld));
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
            // Different paths to one interpreter (a symlink in /usr/local/bin and the framework's own bin) are one choice.
            if (!seen.Add($"{probe.Prefix}|{probe.Version}")) continue;
            found.Add(ToInfo(candidates[i], probe));
        }

        return found;
    }

    public async Task<ToolchainActionResult> RunActionAsync(string actionId, ToolchainQuery query, Action<string> output, CancellationToken ct = default)
    {
        if (actionId is not (CreateStudioEnvironmentAction or RecreateStudioEnvironmentAction))
        {
            return new ToolchainActionResult(false, $"Unknown action '{actionId}'.");
        }

        var base_ = await ResolveBaseInterpreterAsync(query, ct);
        if (base_ == null)
        {
            return new ToolchainActionResult(false, PythonGuidance.NotInstalled(_host).ToText());
        }

        try
        {
            var environment = await EnsureStudioEnvironmentAsync(base_, output, recreate: actionId == RecreateStudioEnvironmentAction, ct);
            Select(environment.ExecutablePath);
            return new ToolchainActionResult(true, $"Now using the studio environment ({environment.DisplayName}).", environment);
        }
        catch (StudioEnvironmentException ex)
        {
            return new ToolchainActionResult(false, ex.Message);
        }
    }

    /// <summary>
    /// The studio's own virtual environment for <paramref name="basePython"/>'s version, created on first use with
    /// <c>--system-site-packages</c> so packages already installed for that Python keep working in it. Broken ones (e.g.
    /// after an upgrade removed the Python it was made from) are made again.
    /// </summary>
    public async Task<ToolchainInfo> EnsureStudioEnvironmentAsync(ToolchainInfo basePython, Action<string> output, bool recreate = false, CancellationToken ct = default)
    {
        var folder = Path.Combine(StudioEnvironmentsFolder, $"{basePython.Version.Major}.{basePython.Version.Minor}");
        await _environmentLock.WaitAsync(ct);
        try
        {
            var executable = EnvironmentInterpreter(folder);
            if (!recreate && _host.FileExists(executable))
            {
                _probes.TryRemove(executable, out _);
                if (await ProbeAsync(executable, ct) is { } working)
                {
                    return ToInfo(new Candidate(executable, "Studio environment", IsStudioEnvironment: true), working);
                }

                output($"The studio environment in {folder} no longer works (was the Python it came from upgraded or removed?). Making it again…\n");
            }

            if (!basePython.Is(VenvProperty))
            {
                throw new StudioEnvironmentException(PythonGuidance.VenvMissing(_host, basePython));
            }

            if (Directory.Exists(folder))
            {
                try
                {
                    Directory.Delete(folder, recursive: true);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    throw new StudioEnvironmentException($"Couldn't remove the old studio environment in {folder}: {ex.Message}");
                }
            }

            Directory.CreateDirectory(StudioEnvironmentsFolder);
            output($"Creating the studio environment from {basePython.Label}…\n");
            var exitCode = await RunAsync(new ProcessStartSpec
            {
                FileName = basePython.ExecutablePath,
                Arguments = ["-m", "venv", "--system-site-packages", folder]
            }, output, ct);

            _probes.TryRemove(executable, out _);
            var probe = exitCode == 0 && _host.FileExists(executable) ? await ProbeAsync(executable, ct) : null;
            if (probe == null)
            {
                throw new StudioEnvironmentException(exitCode == 0
                    ? $"Python said it created the studio environment, but {executable} doesn't run."
                    : PythonGuidance.VenvMissing(_host, basePython));
            }

            output($"Studio environment ready: {folder}\n");
            return ToInfo(new Candidate(executable, "Studio environment", IsStudioEnvironment: true), probe);
        }
        finally
        {
            _environmentLock.Release();
        }
    }

    /// <summary>The Python a studio environment would be made from: the resolved one unless that's already a virtual environment.</summary>
    private async Task<ToolchainInfo?> ResolveBaseInterpreterAsync(ToolchainQuery query, CancellationToken ct)
    {
        var all = await ListAsync(query, ct);
        return all.FirstOrDefault(p => !p.Is(VirtualEnvironmentProperty)) ?? all.FirstOrDefault();
    }

    public string EnvironmentInterpreter(string environmentFolder) =>
        _host.IsWindows ? Path.Combine(environmentFolder, "Scripts", "python.exe") : Path.Combine(environmentFolder, "bin", "python");

    private async Task<int> RunAsync(ProcessStartSpec spec, Action<string> output, CancellationToken ct)
    {
        using var process = _launcher.Start(spec, output, output);
        using (ct.Register(process.Kill))
        {
            return await process.Completion;
        }
    }


    private sealed record ProbeResult(
        Version Version,
        string Prefix,
        string BasePrefix,
        bool ExternallyManaged,
        bool HasPip,
        bool HasVenv,
        string SitePackages);

    private async Task<ProbeResult?> ProbeAsync(string path, CancellationToken ct)
    {
        var probe = _probes.GetOrAdd(path, p => new Lazy<Task<ProbeResult?>>(() => RunProbeAsync(p, ct)));
        try
        {
            return await probe.Value;
        }
        catch (OperationCanceledException)
        {
            // Cancelled isn't an answer: ask again next time.
            _probes.TryRemove(new KeyValuePair<string, Lazy<Task<ProbeResult?>>>(path, probe));
            throw;
        }
    }

    private async Task<ProbeResult?> RunProbeAsync(string path, CancellationToken ct)
    {
        var result = await _host.RunAsync(path, ["-c", ProbeScript], ProbeTimeout, ct);
        ct.ThrowIfCancellationRequested();
        if (!result.Succeeded)
        {
            // A Windows Store shortcut exits with 9009; a broken venv can't find its Python. Neither is usable.
            Debug.WriteLine($"[CSharpEditorPlugin] {path} isn't a usable Python (exit {result.ExitCode}{(result.TimedOut ? ", timed out" : string.Empty)}).");
            return null;
        }

        var line = result.StandardOutput.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.StartsWith(ProbeMarker, StringComparison.Ordinal));
        if (line == null) return null;

        try
        {
            using var json = JsonDocument.Parse(line[ProbeMarker.Length..]);
            var root = json.RootElement;
            if (root.GetProperty("implementation").GetString() is not ("cpython" or "pypy")) return null;
            if (!Version.TryParse(root.GetProperty("version").GetString(), out var version)) return null;
            return new ProbeResult(
                version,
                root.GetProperty("prefix").GetString() ?? string.Empty,
                root.GetProperty("base_prefix").GetString() ?? string.Empty,
                root.GetProperty("externally_managed").GetBoolean(),
                root.GetProperty("pip").GetBoolean(),
                root.GetProperty("venv").GetBoolean(),
                root.GetProperty("site_packages").GetString() ?? string.Empty);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }

    private ToolchainInfo ToInfo(Candidate candidate, ProbeResult probe)
    {
        var isVirtualEnvironment = !string.Equals(probe.Prefix, probe.BasePrefix, StringComparison.Ordinal);
        return new ToolchainInfo
        {
            LanguageId = LanguageIds.Python,
            ExecutablePath = candidate.Path,
            Version = probe.Version,
            DisplayName = $"Python {probe.Version}",
            Source = candidate.IsStudioEnvironment || IsInStudioEnvironments(candidate.Path) ? "Studio environment"
                : candidate.Source.StartsWith("Project ", StringComparison.Ordinal) ? candidate.Source
                : SourceOf(candidate.Path, probe.BasePrefix, candidate.Source),
            Properties = new Dictionary<string, string>
            {
                [PrefixProperty] = probe.Prefix,
                [BasePrefixProperty] = probe.BasePrefix,
                [VirtualEnvironmentProperty] = isVirtualEnvironment ? "true" : "false",
                // A virtual environment accepts pip installs even when the Python it came from is externally managed.
                [ExternallyManagedProperty] = probe.ExternallyManaged && !isVirtualEnvironment ? "true" : "false",
                [PipProperty] = probe.HasPip ? "true" : "false",
                [VenvProperty] = probe.HasVenv ? "true" : "false",
                [SitePackagesProperty] = probe.SitePackages,
                [StudioEnvironmentProperty] = candidate.IsStudioEnvironment || IsInStudioEnvironments(candidate.Path) ? "true" : "false"
            }
        };
    }

    // Where an interpreter came from, for the picker ("Homebrew", "python.org", "pyenv"). The installation it belongs to
    // (base_prefix) says more than the path it was found at: /usr/local/bin/python3 is a symlink into one of them.
    private static string SourceOf(string path, string basePrefix, string fallback)
    {
        foreach (var where in new[] { basePrefix.Replace('\\', '/'), path.Replace('\\', '/') })
        {
            if (where.Length == 0) continue;
            if (where.Contains("/opt/homebrew/", StringComparison.Ordinal) || where.Contains("/Cellar/", StringComparison.Ordinal) || where.Contains("linuxbrew", StringComparison.Ordinal)) return "Homebrew";
            if (where.Contains("Xcode.app", StringComparison.Ordinal) || where.Contains("/CommandLineTools/", StringComparison.Ordinal)) return "Xcode Command Line Tools";
            if (where.StartsWith("/Library/Frameworks/Python.framework/", StringComparison.Ordinal)) return "python.org";
            if (where.Contains("/.pyenv/", StringComparison.Ordinal)) return "pyenv";
            if (where.Contains("conda", StringComparison.OrdinalIgnoreCase) || where.Contains("miniforge", StringComparison.OrdinalIgnoreCase) || where.Contains("mambaforge", StringComparison.OrdinalIgnoreCase)) return "Conda";
            if (where.Contains("/.local/share/uv/", StringComparison.Ordinal) || where.Contains("/uv/python/", StringComparison.OrdinalIgnoreCase)) return "uv";
            if (where.Contains("/WindowsApps/", StringComparison.OrdinalIgnoreCase)) return "Microsoft Store";
            if (where.Contains("/Programs/Python/", StringComparison.OrdinalIgnoreCase)) return "python.org";
        }

        return path.StartsWith("/usr/", StringComparison.Ordinal) && basePrefix.StartsWith("/usr", StringComparison.Ordinal) ? "System" : fallback;
    }

    private sealed class StudioEnvironmentException(string message) : Exception(message);
}
