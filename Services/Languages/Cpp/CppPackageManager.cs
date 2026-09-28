using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Services.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;

/// <summary>
/// Manages C++ packages via vcpkg for source files and notebooks.
/// Supports %vcpkg install &lt;package&gt; and // #vcpkg: &lt;package&gt; directives.
/// </summary>
public sealed partial class CppPackageManager : IPackageManager
{
    private readonly CppToolchainProvider? _toolchains;
    private readonly IProcessLauncher _launcher;
    private readonly IHostEnvironment _host;

    public CppPackageManager(
        CppToolchainProvider? toolchains = null,
        IProcessLauncher? launcher = null,
        IHostEnvironment? host = null)
    {
        _toolchains = toolchains;
        _launcher = launcher ?? new ProcessLauncher();
        _host = host ?? new HostEnvironment();
    }

    public CppToolchainProvider? Toolchains => _toolchains;

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex(@"^[%#!](?:vcpkg|pkg)\s+(?:install|add)\s+(?<pkg>[a-zA-Z0-9_\-\.]+)(?:\s+(?<args>.*))?$", RegexOptions.IgnoreCase)]
    private static partial Regex VcpkgMagicRegex();

    [GeneratedRegex(@"^\s*//\s*#(?:vcpkg|pkg):\s*(?<pkg>[a-zA-Z0-9_\-\.]+)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex VcpkgCommentRegex();

    [GeneratedRegex(@"^\s*#r\s+""vcpkg:\s*(?<pkg>[a-zA-Z0-9_\-\.]+)\s*""\s*;?$", RegexOptions.IgnoreCase)]
    private static partial Regex VcpkgRDirectiveRegex();

    public string ToolName => "vcpkg";

    public bool TryParseDirective(string line, out PackageCommand command)
    {
        var trimmed = line.Trim();

        var matchMagic = VcpkgMagicRegex().Match(trimmed);
        if (matchMagic.Success)
        {
            var pkg = matchMagic.Groups["pkg"].Value;
            command = new PackageCommand(trimmed, ["install", pkg]);
            return true;
        }

        var matchComment = VcpkgCommentRegex().Match(trimmed);
        if (matchComment.Success)
        {
            var pkg = matchComment.Groups["pkg"].Value;
            command = new PackageCommand(trimmed, ["install", pkg]);
            return true;
        }

        var matchR = VcpkgRDirectiveRegex().Match(trimmed);
        if (matchR.Success)
        {
            var pkg = matchR.Groups["pkg"].Value;
            command = new PackageCommand(trimmed, ["install", pkg]);
            return true;
        }

        command = null!;
        return false;
    }

    public PackageCommand InstallCommand(string package)
    {
        var trimmed = package.Trim();
        return new PackageCommand($"// #vcpkg: {trimmed}", ["install", trimmed]);
    }

    public string PackageForMissingDependency(string missingName) => CppPackageMap.PackageFor(missingName);

    public async Task<PackageCommandResult> RunAsync(
        PackageCommand command,
        ToolchainInfo toolchain,
        Action<string> output,
        CancellationToken ct = default)
    {
        if (command.Arguments.Count < 2)
        {
            return new PackageCommandResult(false, "Specify a C++ package to install with vcpkg, e.g. %vcpkg install nlohmann-json");
        }

        var pkgName = command.Arguments[1];
        var vcpkgPath = await ResolveVcpkgExecutableAsync(ct).ConfigureAwait(false);

        if (string.IsNullOrEmpty(vcpkgPath))
        {
            output("❌ vcpkg executable could not be found.\n");
            output("To install vcpkg:\n");
            if (_host.IsWindows)
            {
                output("  git clone https://github.com/microsoft/vcpkg.git\n");
                output("  .\\vcpkg\\bootstrap-vcpkg.bat\n");
            }
            else
            {
                output("  git clone https://github.com/microsoft/vcpkg.git\n");
                output("  ./vcpkg/bootstrap-vcpkg.sh\n");
            }
            return new PackageCommandResult(false, "vcpkg executable not found.");
        }

        var vcpkgRoot = Path.GetDirectoryName(vcpkgPath)!;
        var gate = _locks.GetOrAdd(vcpkgPath, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);

        int exitCode;
        try
        {
            output($"▶ Running vcpkg install {pkgName}...\n");
            var environment = await CppProcessEnvironment.ForAsync(_host, toolchain, ct).ConfigureAwait(false);

            using var process = _launcher.Start(new ProcessStartSpec
            {
                FileName = vcpkgPath,
                Arguments = ["install", pkgName],
                WorkingDirectory = vcpkgRoot,
                Environment = environment
            }, output, output);

            using (ct.Register(process.Kill))
            {
                exitCode = await process.Completion.ConfigureAwait(false);
            }
        }
        catch (ProcessStartException ex)
        {
            output(ex.Message + "\n");
            return new PackageCommandResult(false, ex.Message);
        }
        finally
        {
            gate.Release();
        }

        if (exitCode == 0)
        {
            var installedInclude = Path.Combine(vcpkgRoot, "installed");
            output($"✔ vcpkg installed {pkgName} successfully.\n");
            return new PackageCommandResult(true, $"Successfully installed {pkgName}.", AddedSearchPath: installedInclude);
        }

        output($"❌ vcpkg exited with code {exitCode}.\n");
        return new PackageCommandResult(false, $"vcpkg exited with code {exitCode}.");
    }

    public async Task<string?> ResolveVcpkgExecutableAsync(CancellationToken ct = default)
    {
        var exeName = _host.IsWindows ? "vcpkg.exe" : "vcpkg";

        // Check common vcpkg installation directories
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new[]
        {
            Path.Combine(userHome, ".vcpkg", exeName),
            Path.Combine(userHome, "vcpkg", exeName),
            Path.Combine(userHome, "Developer", "vcpkg", exeName),
            Path.Combine("/usr/local/bin", exeName),
            Path.Combine("/opt/homebrew/bin", exeName)
        };

        foreach (var candidate in candidates)
        {
            if (_host.FileExists(candidate)) return candidate;
        }

        // Search PATH
        var pathEnv = await _host.GetLoginShellPathAsync(ct).ConfigureAwait(false)
                      ?? _host.GetEnvironmentVariable("PATH") ?? string.Empty;

        var paths = pathEnv.Split(_host.IsWindows ? ';' : ':', StringSplitOptions.RemoveEmptyEntries);
        foreach (var dir in paths)
        {
            var p = Path.Combine(dir.Trim(), exeName);
            if (_host.FileExists(p)) return p;
        }

        return null;
    }
}
