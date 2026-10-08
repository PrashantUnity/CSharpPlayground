using System.Collections.Concurrent;
using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace CSharpEditorPlugin.Tests.TestSupport;

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
    private readonly Dictionary<string, FakeNode> _nodes;
    private readonly Dictionary<string, FakeJava> _javas;
    private readonly Dictionary<string, FakeCpp> _cpps;
    private readonly Dictionary<string, FakeGo> _gos;
    private readonly Dictionary<string, FakeFSharp> _fsharps;
    private readonly Dictionary<string, FakeSql> _sqls;
    private readonly Dictionary<string, FakeRust> _rusts;
    private readonly Dictionary<string, FakeRust> _rustcs;

    public FakeHostEnvironment(FakeOs os = FakeOs.MacOS)
    {
        _os = os;
        var comparer = os == FakeOs.Linux ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        _files = new HashSet<string>(comparer);
        _directories = new HashSet<string>(comparer);
        _pythons = new Dictionary<string, FakePython>(comparer);
        _nodes = new Dictionary<string, FakeNode>(comparer);
        _javas = new Dictionary<string, FakeJava>(comparer);
        _cpps = new Dictionary<string, FakeCpp>(comparer);
        _gos = new Dictionary<string, FakeGo>(comparer);
        _fsharps = new Dictionary<string, FakeFSharp>(comparer);
        _sqls = new Dictionary<string, FakeSql>(comparer);
        _rusts = new Dictionary<string, FakeRust>(comparer);
        _rustcs = new Dictionary<string, FakeRust>(comparer);
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

    /// <summary>Whether <c>git --version</c> succeeds (Git CLI is installed).</summary>
    public bool GitInstalled { get; set; } = true;
    public string GitVersion { get; set; } = "git version 2.44.0";

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

    /// <summary>Makes <paramref name="path"/> a Node.js runtime that answers the studio's probe.</summary>
    public FakeNode AddNode(string path, string version, string? executable = null, int exitCode = 0)
    {
        AddFile(path);
        var node = new FakeNode(version, executable ?? path) { ExitCode = exitCode };
        _nodes[Normalize(path)] = node;
        return node;
    }

    /// <summary>Makes <paramref name="path"/> a Java JDK runtime that answers the studio's probe.</summary>
    public FakeJava AddJava(string path, string version, bool hasCompiler = true, int exitCode = 0)
    {
        AddFile(path);
        if (hasCompiler)
        {
            var bin = ParentOf(Normalize(path));
            var javacName = IsWindows ? "javac.exe" : "javac";
            var javacPath = !string.IsNullOrEmpty(bin) ? $"{bin}/{javacName}" : javacName;
            AddFile(javacPath);
        }
        var java = new FakeJava(version, path, hasCompiler) { ExitCode = exitCode };
        _javas[Normalize(path)] = java;
        return java;
    }

    /// <summary>Makes <paramref name="path"/> a C++ compiler that answers the studio's probe.</summary>
    public FakeCpp AddCpp(string path, string version, string vendor = "clang", int exitCode = 0)
    {
        AddFile(path);
        var cpp = new FakeCpp(version, path, vendor) { ExitCode = exitCode };
        _cpps[Normalize(path)] = cpp;
        return cpp;
    }

    /// <summary>Makes <paramref name="path"/> a Go toolchain runtime that answers the studio's probe.</summary>
    public FakeGo AddGo(string path, string version, int exitCode = 0)
    {
        AddFile(path);
        var go = new FakeGo(version, path) { ExitCode = exitCode };
        _gos[Normalize(path)] = go;
        return go;
    }

    /// <summary>Makes <paramref name="path"/> an F# / .NET toolchain runtime that answers the studio's probe.</summary>
    public FakeFSharp AddFSharp(string path, string version, int exitCode = 0)
    {
        AddFile(path);
        var fs = new FakeFSharp(version, path) { ExitCode = exitCode };
        _fsharps[Normalize(path)] = fs;
        return fs;
    }

    /// <summary>Makes <paramref name="path"/> a SQLite toolchain runtime that answers the studio's probe.</summary>
    public FakeSql AddSql(string path, string version = "3.51.0", int exitCode = 0)
    {
        AddFile(path);
        var sql = new FakeSql(version, path) { ExitCode = exitCode };
        _sqls[Normalize(path)] = sql;
        return sql;
    }

    /// <summary>
    /// Makes <paramref name="cargoPath"/> a Rust toolchain: cargo answers <c>--version</c>, and a sibling rustc (unless
    /// <paramref name="hasRustc"/> is false) answers <c>-vV</c>. <paramref name="cargoError"/> makes cargo fail the way a
    /// rustup proxy does when no toolchain is installed.
    /// </summary>
    public FakeRust AddRust(string cargoPath, string version, string channel = "stable", string host = "aarch64-apple-darwin",
        bool hasRustc = true, int exitCode = 0, string? cargoError = null)
    {
        AddFile(cargoPath);
        var bin = ParentOf(Normalize(cargoPath));
        var rustcName = IsWindows ? "rustc.exe" : "rustc";
        var rustcPath = !string.IsNullOrEmpty(bin) ? $"{bin}/{rustcName}" : rustcName;
        var rust = new FakeRust(version, cargoPath, rustcPath, channel, host) { ExitCode = exitCode, CargoError = cargoError };
        _rusts[Normalize(cargoPath)] = rust;
        if (hasRustc)
        {
            AddFile(rustcPath);
            _rustcs[Normalize(rustcPath)] = rust;
        }

        return rust;
    }

    /// <summary>A path that exists but isn't a working Node.js runtime.</summary>
    public void AddBrokenNode(string path, int exitCode = 1)
    {
        AddFile(path);
        _nodes[Normalize(path)] = new FakeNode("0.0.0", path) { ExitCode = exitCode };
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

        if (arguments.Count > 0 && arguments[0] == "-e" && _nodes.TryGetValue(Normalize(fileName), out var node))
        {
            return Task.FromResult(node.ProbeAnswer());
        }

        if (arguments.Count > 0 && arguments[0] == "-version" && _javas.TryGetValue(Normalize(fileName), out var java))
        {
            return Task.FromResult(java.ProbeAnswer());
        }

        if (arguments.Count > 0 && (arguments[0] == "--version" || arguments[0] == "/?") && _cpps.TryGetValue(Normalize(fileName), out var cpp))
        {
            return Task.FromResult(cpp.ProbeAnswer());
        }

        if (arguments.Count > 0 && arguments[0] == "--version" && _rusts.TryGetValue(Normalize(fileName), out var rust))
        {
            return Task.FromResult(rust.CargoProbeAnswer());
        }

        if (arguments.Count > 0 && arguments[0] == "-vV" && _rustcs.TryGetValue(Normalize(fileName), out var rustc))
        {
            return Task.FromResult(rustc.RustcProbeAnswer());
        }

        if (arguments.Count > 0 && arguments[0] == "version" && _gos.TryGetValue(Normalize(fileName), out var go))
        {
            return Task.FromResult(go.ProbeAnswer());
        }

        if (arguments.Count > 0 && ((arguments[0] == "--version") || (arguments[0] == "fsi" && arguments.Count > 1 && arguments[1] == "--version")) && _fsharps.TryGetValue(Normalize(fileName), out var fsharp))
        {
            return Task.FromResult(fsharp.ProbeAnswer());
        }

        if (arguments.Count > 0 && arguments[0] == "--version" && _sqls.TryGetValue(Normalize(fileName), out var sql))
        {
            return Task.FromResult(sql.ProbeAnswer());
        }

        if (arguments.Count > 0 && arguments[0] == "--version" && (fileName == "git" || fileName.EndsWith("/git", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith("\\git.exe", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith("git.exe", StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult(GitInstalled
                ? new CommandResult(0, GitVersion + "\n", string.Empty, false)
                : new CommandResult(-1, string.Empty, "git: not found", false));
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

public sealed record FakeNode(string Version, string Executable)
{
    public int ExitCode { get; init; }

    public CommandResult ProbeAnswer()
    {
        if (ExitCode != 0) return new CommandResult(ExitCode, string.Empty, "not a working Node.js", false);
        var json = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["version"] = Version,
            ["executable"] = Executable
        });
        return new CommandResult(0, "__FRY_PROBE__" + json + "\n", string.Empty, false);
    }
}

public sealed record FakeJava(string Version, string Executable, bool HasCompiler = true)
{
    public int ExitCode { get; init; }

    public CommandResult ProbeAnswer()
    {
        if (ExitCode != 0) return new CommandResult(ExitCode, string.Empty, "not a working Java", false);
        return new CommandResult(0, string.Empty, $"openjdk version \"{Version}\"\nOpenJDK Runtime Environment\n", false);
    }
}

public sealed record FakeCpp(string Version, string Executable, string Vendor = "clang")
{
    public int ExitCode { get; init; }

    public CommandResult ProbeAnswer()
    {
        if (ExitCode != 0) return new CommandResult(ExitCode, string.Empty, "not a working compiler", false);
        string output = Vendor.ToLowerInvariant() switch
        {
            "apple" or "appleclang" => $"Apple clang version {Version} (clang-1500.0.40.1)\nTarget: arm64-apple-darwin23.4.0\nThread model: posix",
            "gcc" or "g++" => $"g++ (Ubuntu {Version}-1ubuntu1) {Version}\nCopyright (C) 2023 Free Software Foundation, Inc.",
            "msvc" or "cl" => $"Microsoft (R) C/C++ Optimizing Compiler Version {Version} for x64\nCopyright (C) Microsoft Corporation.",
            _ => $"clang version {Version} (Homebrew LLVM {Version})\nTarget: arm64-apple-darwin25.5.0\nThread model: posix"
        };
        return new CommandResult(0, output, string.Empty, false);
    }
}

public sealed record FakeGo(string Version, string Executable)
{
    public int ExitCode { get; init; }

    public CommandResult ProbeAnswer()
    {
        if (ExitCode != 0) return new CommandResult(ExitCode, string.Empty, "not a working go", false);
        return new CommandResult(0, $"go version go{Version} darwin/arm64\n", string.Empty, false);
    }
}

public sealed record FakeFSharp(string Version, string Executable)
{
    public int ExitCode { get; init; }

    public CommandResult ProbeAnswer()
    {
        if (ExitCode != 0) return new CommandResult(ExitCode, string.Empty, "not a working F# runtime", false);
        return new CommandResult(0, $"Microsoft (R) F# Interactive version 15.2.400.0 for F# {Version}\n", string.Empty, false);
    }
}

public sealed record FakeSql(string Version, string Executable)
{
    public int ExitCode { get; init; }

    public CommandResult ProbeAnswer()
    {
        if (ExitCode != 0) return new CommandResult(ExitCode, string.Empty, "not a working sqlite3", false);
        return new CommandResult(0, $"{Version} 2025-06-12 13:14:41 (64-bit)\n", string.Empty, false);
    }
}

public sealed record FakeRust(string Version, string Cargo, string Rustc, string Channel = "stable", string Host = "aarch64-apple-darwin")
{
    public int ExitCode { get; init; }

    /// <summary>What cargo prints when it can't run at all (e.g. a rustup proxy with no default toolchain).</summary>
    public string? CargoError { get; init; }

    private string Full => Channel == "stable" ? Version : $"{Version}-{Channel}";

    // The same compiler always has the same commit, so a rustup proxy and the toolchain folder it forwards to look alike.
    private string CommitHash => (string.Concat((Version + Channel).Select(c => (c % 16).ToString("x"))) + new string('a', 40))[..40];

    public CommandResult CargoProbeAnswer()
    {
        if (CargoError != null) return new CommandResult(1, string.Empty, CargoError, false);
        if (ExitCode != 0) return new CommandResult(ExitCode, string.Empty, "not a working cargo", false);
        return new CommandResult(0, $"cargo {Full} (85eff7c80 2026-01-15)\n", string.Empty, false);
    }

    public CommandResult RustcProbeAnswer()
    {
        if (ExitCode != 0) return new CommandResult(ExitCode, string.Empty, "not a working rustc", false);
        return new CommandResult(0,
            $"rustc {Full} (4a4ef493e 2026-03-02)\nbinary: rustc\ncommit-hash: {CommitHash}\ncommit-date: 2026-03-02\nhost: {Host}\nrelease: {Full}\nLLVM version: 21.1.8\n",
            string.Empty, false);
    }
}
