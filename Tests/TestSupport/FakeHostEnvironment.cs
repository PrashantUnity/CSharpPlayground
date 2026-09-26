using System.Collections.Concurrent;
using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;

public enum FakeOs
{
    MacOS,
    Linux,
    Windows
}

/// <summary>
/// A pretend computer for toolchain discovery: which OS it is, which files exist, what PATH says, and which paths are
/// Python interpreters (answering the studio's probe with the version and facts given). Paths are compared with
/// either slash, so Windows layouts can be described on any test machine.
/// </summary>
public sealed class FakeHostEnvironment : IHostEnvironment
{
    private readonly FakeOs _os;
    private readonly HashSet<string> _files;
    private readonly HashSet<string> _directories;
    private readonly Dictionary<string, FakePython> _pythons;

    public FakeHostEnvironment(FakeOs os = FakeOs.MacOS)
    {
        _os = os;
        var comparer = os == FakeOs.Linux ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        _files = new HashSet<string>(comparer);
        _directories = new HashSet<string>(comparer);
        _pythons = new Dictionary<string, FakePython>(comparer);
        HomeDirectory = os == FakeOs.Windows ? @"C:\Users\test" : os == FakeOs.MacOS ? "/Users/test" : "/home/test";
    }

    public bool IsWindows => _os == FakeOs.Windows;
    public bool IsMacOS => _os == FakeOs.MacOS;
    public bool IsLinux => _os == FakeOs.Linux;
    public string HomeDirectory { get; set; }

    public Dictionary<string, string> Variables { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>PATH as the login shell reports it, or null when there's no login shell (Windows) or it failed.</summary>
    public string? LoginShellPath { get; set; }

    /// <summary>Whether <c>xcode-select -p</c> succeeds (Apple's Command Line Tools are installed).</summary>
    public bool CommandLineToolsInstalled { get; set; }

    /// <summary>Answers other commands, e.g. <c>py -0p</c>; null means "not found".</summary>
    public Func<string, IReadOnlyList<string>, CommandResult?>? OnCommand { get; set; }

    /// <summary>Every command run, in order.</summary>
    public ConcurrentQueue<(string FileName, IReadOnlyList<string> Arguments)> Commands { get; } = new();

    public IEnumerable<string> ProbedPaths => Commands.Where(c => c.Arguments.FirstOrDefault() == "-c").Select(c => c.FileName);

    public static string Normalize(string path) => path.Replace('\\', '/').TrimEnd('/');

    public void AddFile(string path)
    {
        var normalized = Normalize(path);
        _files.Add(normalized);
        var parent = ParentOf(normalized);
        while (parent != null)
        {
            _directories.Add(parent);
            parent = ParentOf(parent);
        }
    }

    public void AddDirectory(string path) => AddFile(Normalize(path) + "/.keep");

    /// <summary>Makes <paramref name="path"/> a Python interpreter that answers the studio's probe.</summary>
    public FakePython AddPython(string path, string version, string? prefix = null, string? basePrefix = null,
        bool externallyManaged = false, bool pip = true, bool venv = true, string? sitePackages = null, string noise = "")
    {
        AddFile(path);
        var home = prefix ?? ParentOf(ParentOf(Normalize(path)) ?? "/") ?? "/";
        var python = new FakePython(version, home, basePrefix, externallyManaged, pip, venv, sitePackages ?? home + "/lib/site-packages")
        {
            Noise = noise
        };
        _pythons[Normalize(path)] = python;
        return python;
    }

    /// <summary>A path that exists but isn't a working Python (exits with <paramref name="exitCode"/>), like a broken venv or a Store shortcut.</summary>
    public void AddBrokenPython(string path, int exitCode = 1)
    {
        AddFile(path);
        _pythons[Normalize(path)] = new FakePython("0.0.0", "/", null, false, false, false, string.Empty) { ExitCode = exitCode };
    }

    public string? GetEnvironmentVariable(string name) => Variables.TryGetValue(name, out var value) ? value : null;

    public bool FileExists(string path) => _files.Contains(Normalize(path));

    public bool DirectoryExists(string path) => _directories.Contains(Normalize(path));

    public IReadOnlyList<string> GetDirectories(string path)
    {
        var parent = Normalize(path);
        return _directories.Where(d => string.Equals(ParentOf(d), parent, IsLinux ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d, StringComparer.Ordinal)
            .ToArray();
    }

    public Task<string?> GetLoginShellPathAsync(CancellationToken ct = default) => Task.FromResult(IsWindows ? null : LoginShellPath);

    public Task<CommandResult> RunAsync(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken ct = default)
    {
        Commands.Enqueue((fileName, arguments));

        if (fileName == "/usr/bin/xcode-select")
        {
            return Task.FromResult(CommandLineToolsInstalled
                ? new CommandResult(0, "/Library/Developer/CommandLineTools\n", string.Empty, false)
                : new CommandResult(2, string.Empty, "xcode-select: error: unable to get active developer directory", false));
        }

        if (arguments.Count > 0 && arguments[0] == "-c" && _pythons.TryGetValue(Normalize(fileName), out var python))
        {
            return Task.FromResult(python.ProbeAnswer());
        }

        return Task.FromResult(OnCommand?.Invoke(fileName, arguments) ?? new CommandResult(-1, string.Empty, $"{fileName}: not found", false));
    }

    private static string? ParentOf(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash > 0 ? path[..slash] : slash == 0 && path.Length > 1 ? "/" : null;
    }
}

public sealed record FakePython(string Version, string Prefix, string? BasePrefix, bool ExternallyManaged, bool Pip, bool Venv, string SitePackages)
{
    public int ExitCode { get; init; }

    /// <summary>Text printed before the probe's answer, like a sitecustomize that talks.</summary>
    public string Noise { get; init; } = string.Empty;

    public CommandResult ProbeAnswer()
    {
        if (ExitCode != 0) return new CommandResult(ExitCode, string.Empty, "not a working Python", false);
        var json = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["version"] = Version,
            ["implementation"] = "cpython",
            ["executable"] = Prefix + "/bin/python",
            ["prefix"] = Prefix,
            ["base_prefix"] = BasePrefix ?? Prefix,
            ["externally_managed"] = ExternallyManaged,
            ["pip"] = Pip,
            ["venv"] = Venv,
            ["site_packages"] = SitePackages
        });
        return new CommandResult(0, Noise + "__FRY_PROBE__" + json + "\n", string.Empty, false);
    }
}
