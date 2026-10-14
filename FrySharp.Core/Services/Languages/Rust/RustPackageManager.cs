using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using PdfEditorApp.Plugins.CSharpEditor.Services.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;

/// <summary>
/// Adds crates for Rust files and notebooks. Cargo has no global install, so a crate belongs to what uses it: a file names
/// its crates in <c>// #crate: rand = "0.8"</c> comments, and a notebook adds them with <c>%cargo add rand</c>. Adding one
/// downloads it with <c>cargo fetch</c> (which proves the name and version exist, and leaves the crate in cargo's cache for
/// the build), and hands back the comment to write into the file.
/// </summary>
public sealed partial class RustPackageManager : IPackageManager
{
    private readonly RustToolchainProvider? _toolchains;
    private readonly IProcessLauncher _launcher;
    private readonly IHostEnvironment _host;
    private readonly string _rustRoot;
    private readonly RustNotebookDependencies _dependencies;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

    // %cargo add rand   %cargo add serde --features derive   %crate rand@0.8   !cargo add tokio -F full
    [GeneratedRegex(@"^[%#!](?:cargo\s+add|crate)\s+(?<spec>\S.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex MagicRegex();

    [GeneratedRegex(@"^\s*#r\s+""crate:\s*(?<spec>[^""]+?)\s*""\s*;?$", RegexOptions.IgnoreCase)]
    private static partial Regex RDirectiveRegex();

    [GeneratedRegex(@"name = ""(?<name>[^""]+)""\r?\nversion = ""(?<version>[^""]+)""")]
    private static partial Regex LockedPackageRegex();

    /// <param name="rustRoot">The studio's Rust folder; the scratch package that checks a crate lives under it.</param>
    public RustPackageManager(
        string rustRoot,
        RustToolchainProvider? toolchains = null,
        IProcessLauncher? launcher = null,
        IHostEnvironment? host = null)
    {
        _rustRoot = rustRoot;
        _toolchains = toolchains;
        _launcher = launcher ?? new ProcessLauncher();
        _host = host ?? new HostEnvironment();
        _dependencies = new RustNotebookDependencies(rustRoot);
    }

    /// <summary>The crates notebook cells have added.</summary>
    public RustNotebookDependencies NotebookDependencies => _dependencies;

    public string ToolName => "cargo";

    public bool TryParseDirective(string line, out PackageCommand command)
    {
        var trimmed = line.Trim();
        command = null!;

        RustCrate? crate = null;
        var magic = MagicRegex().Match(trimmed);
        if (magic.Success)
        {
            if (TryParseMagic(magic.Groups["spec"].Value, out var parsed)) crate = parsed;
        }
        else if (RustDirectives.TryParseCrateLine(trimmed, out var comment))
        {
            crate = comment;
        }
        else if (RDirectiveRegex().Match(trimmed) is { Success: true } r && RustDirectives.TryParseCrate(r.Groups["spec"].Value, out var fromR))
        {
            crate = fromR;
        }

        if (crate == null) return false;
        command = new PackageCommand(trimmed, ["add", crate.ToManifestLine()]);
        return true;
    }

    public PackageCommand InstallCommand(string package)
    {
        var name = package.Trim();
        var crate = new RustCrate(name, RustPackageMap.DefaultValue(name));
        return new PackageCommand($"// #crate: {crate.ToManifestLine()}", ["add", crate.ToManifestLine()]);
    }

    public string PackageForMissingDependency(string missingName) => RustPackageMap.PackageFor(missingName);

    public async Task<PackageCommandResult> RunAsync(
        PackageCommand command,
        ToolchainInfo toolchain,
        Action<string> output,
        CancellationToken ct = default)
    {
        if (command.Arguments.Count < 2 || !RustDirectives.TryParseCrate(command.Arguments[1], out var crate))
        {
            return new PackageCommandResult(false, "Name a crate to add, e.g. %cargo add rand");
        }

        ToolchainInfo? active = toolchain;
        var cargo = active?.ExecutablePath;
        if ((string.IsNullOrEmpty(cargo) || !_host.FileExists(cargo)) && _toolchains != null)
        {
            active = (await _toolchains.ResolveAsync(new ToolchainQuery(), ct).ConfigureAwait(false)).Toolchain;
            cargo = active?.ExecutablePath;
        }

        if (active == null || string.IsNullOrEmpty(cargo) || !_host.FileExists(cargo))
        {
            output("❌ The Rust toolchain wasn't found.\n");
            output(RustGuidance.NotInstalled(_host).ToText() + "\n");
            return new PackageCommandResult(false, "Rust (cargo) wasn't found.");
        }

        var gate = _locks.GetOrAdd(cargo, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var scratch = Path.Combine(_rustRoot, "notebook", "deps");
            WriteScratchPackage(scratch, _dependencies.Load().Where(c => c.Name != crate.Name).Append(crate));

            output($"▶ cargo fetch  ({crate.ToManifestLine()})\n");
            var environment = await RustProcessEnvironment.ForAsync(_host, active, Path.Combine(_rustRoot, "target"), ct).ConfigureAwait(false);
            int exitCode;
            using (var process = _launcher.Start(new ProcessStartSpec
            {
                FileName = cargo,
                Arguments = ["fetch", "--manifest-path", Path.Combine(scratch, "Cargo.toml"), "--color", "never"],
                WorkingDirectory = scratch,
                Environment = environment
            }, output, output))
            {
                using (ct.Register(process.Kill))
                {
                    exitCode = await process.Completion.ConfigureAwait(false);
                }
            }

            ct.ThrowIfCancellationRequested();
            if (exitCode != 0)
            {
                output($"❌ cargo couldn't fetch {crate.Name} (exit code {exitCode}).\n");
                return new PackageCommandResult(false, $"cargo couldn't fetch '{crate.Name}': check its name and version.");
            }

            // "any version" is written down as the one that was found, the way cargo add does.
            var version = ResolvedVersion(scratch, crate.Name);
            var added = crate.TomlValue == "\"*\"" && version != null ? crate with { TomlValue = $"\"{version}\"" } : crate;
            _dependencies.Add(added);

            output($"✓ Added {added.Name}{(version != null ? " " + version : string.Empty)}\n");
            return new PackageCommandResult(true, DirectiveToInsert: $"// #crate: {added.ToManifestLine()}");
        }
        catch (ProcessStartException ex)
        {
            output($"❌ {ex.Message}\n");
            return new PackageCommandResult(false, ex.Message);
        }
        finally
        {
            gate.Release();
        }
    }

    // A package with no code of its own that depends on every crate asked for: cargo fetch resolves and downloads them all.
    private static void WriteScratchPackage(string scratch, IEnumerable<RustCrate> crates)
    {
        Directory.CreateDirectory(scratch);
        var manifest = new StringBuilder();
        manifest.Append("[package]\nname = \"fry_deps\"\nversion = \"0.1.0\"\nedition = \"2021\"\npublish = false\n\n");
        manifest.Append("[lib]\npath = \"lib.rs\"\n\n[dependencies]\n");
        foreach (var crate in crates) manifest.Append(crate.ToManifestLine()).Append('\n');
        manifest.Append("\n[workspace]\n");

        var manifestPath = Path.Combine(scratch, "Cargo.toml");
        if (!File.Exists(manifestPath) || File.ReadAllText(manifestPath) != manifest.ToString())
        {
            File.WriteAllText(manifestPath, manifest.ToString(), new UTF8Encoding(false));
        }

        var lib = Path.Combine(scratch, "lib.rs");
        if (!File.Exists(lib)) File.WriteAllText(lib, string.Empty);
    }

    // The version cargo locked for a crate, read from the scratch package's Cargo.lock.
    private static string? ResolvedVersion(string scratch, string name)
    {
        try
        {
            var lockFile = Path.Combine(scratch, "Cargo.lock");
            if (!File.Exists(lockFile)) return null;
            foreach (Match match in LockedPackageRegex().Matches(File.ReadAllText(lockFile)))
            {
                if (match.Groups["name"].Value == name) return match.Groups["version"].Value;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Unreadable: keep the version as asked.
        }

        return null;
    }

    // rand   rand@0.8   serde --features derive,std   tokio -F full --no-default-features   name = "1"
    private static bool TryParseMagic(string spec, out RustCrate crate)
    {
        crate = null!;
        if (spec.Contains('=') && !spec.Contains("--")) return RustDirectives.TryParseCrate(spec, out crate);

        var tokens = spec.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0 || tokens[0].StartsWith('-')) return false;
        if (!RustDirectives.TryParseCrate(tokens[0], out var basic)) return false;

        var features = new List<string>();
        var defaultFeatures = true;
        for (var i = 1; i < tokens.Length; i++)
        {
            var token = tokens[i];
            if (token is "--features" or "-F" or "--feature" && i + 1 < tokens.Length)
            {
                features.AddRange(tokens[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
            else if (token.StartsWith("--features=", StringComparison.Ordinal))
            {
                features.AddRange(token["--features=".Length..].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
            else if (token == "--no-default-features")
            {
                defaultFeatures = false;
            }
            else
            {
                return false;
            }
        }

        if (features.Count == 0 && defaultFeatures)
        {
            crate = basic;
            return true;
        }

        if (features.Any(f => !FeatureNameRegex().IsMatch(f))) return false;

        var parts = new List<string> { $"version = {basic.TomlValue}" };
        if (features.Count > 0) parts.Add($"features = [{string.Join(", ", features.Select(f => $"\"{f}\""))}]");
        if (!defaultFeatures) parts.Add("default-features = false");
        crate = new RustCrate(basic.Name, "{ " + string.Join(", ", parts) + " }");
        return true;
    }

    [GeneratedRegex(@"^[A-Za-z0-9_\-/+]+$")]
    private static partial Regex FeatureNameRegex();
}
