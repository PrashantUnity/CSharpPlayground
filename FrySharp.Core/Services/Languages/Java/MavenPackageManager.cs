using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Services.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Java;

/// <summary>
/// Manages Maven dependencies for Java source scripts and notebook cells.
/// Supports JBang-style <c>//DEPS groupId:artifactId:version</c> and notebook magic <c>%maven install ...</c>.
/// Resolves artifacts directly from Maven Central into local cache with zero external tool dependencies.
/// </summary>
public sealed partial class MavenPackageManager : IPackageManager
{
    private readonly MavenCentralResolver _resolver;

    [GeneratedRegex(@"^\s*//\s*DEPS\s+(?<coord>[a-zA-Z0-9_\-\.]+:[a-zA-Z0-9_\-\.]+(:[a-zA-Z0-9_\-\.]+)?)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex DepsDirectiveRegex();

    [GeneratedRegex(@"^[%#!]m(a)?v(e)?n\s+(?:install|add)\s+(?<coord>[a-zA-Z0-9_\-\.]+:[a-zA-Z0-9_\-\.]+(:[a-zA-Z0-9_\-\.]+)?)$", RegexOptions.IgnoreCase)]
    private static partial Regex MavenMagicRegex();

    [GeneratedRegex(@"^\s*#r\s+""maven:\s*(?<coord>[a-zA-Z0-9_\-\.]+:[a-zA-Z0-9_\-\.]+(?:[:,\s]+[a-zA-Z0-9_\-\.]+)?)\s*""\s*;?$", RegexOptions.IgnoreCase)]
    private static partial Regex MavenRDirectiveRegex();

    public MavenPackageManager(MavenCentralResolver? resolver = null, IHostEnvironment? host = null)
    {
        _resolver = resolver ?? new MavenCentralResolver(host ?? new HostEnvironment());
    }

    public string ToolName => "Maven";

    public bool TryParseDirective(string line, out PackageCommand command)
    {
        var trimmed = line.Trim();

        var matchDeps = DepsDirectiveRegex().Match(trimmed);
        if (matchDeps.Success)
        {
            var coord = matchDeps.Groups["coord"].Value;
            command = new PackageCommand(trimmed, ["install", coord]);
            return true;
        }

        var matchMagic = MavenMagicRegex().Match(trimmed);
        if (matchMagic.Success)
        {
            var coord = matchMagic.Groups["coord"].Value;
            command = new PackageCommand(trimmed, ["install", coord]);
            return true;
        }

        var matchR = MavenRDirectiveRegex().Match(trimmed);
        if (matchR.Success)
        {
            var rawCoord = matchR.Groups["coord"].Value;
            var coord = Regex.Replace(rawCoord, @"[,\s]+", ":");
            command = new PackageCommand(trimmed, ["install", coord]);
            return true;
        }

        command = null!;
        return false;
    }

    public PackageCommand InstallCommand(string package)
    {
        var trimmed = package.Trim();
        return new PackageCommand($"//DEPS {trimmed}", ["install", trimmed]);
    }

    public string PackageForMissingDependency(string missingName) => MavenPackageMap.PackageFor(missingName);

    public async Task<PackageCommandResult> RunAsync(
        PackageCommand command,
        ToolchainInfo toolchain,
        Action<string> output,
        CancellationToken ct = default)
    {
        if (command.Arguments.Count < 2)
        {
            return new PackageCommandResult(false, "Specify a Maven coordinate to install, e.g. //DEPS com.google.code.gson:gson:2.11.0");
        }

        var rawCoord = command.Arguments[1];
        if (!MavenArtifactCoordinate.TryParse(rawCoord, out var coord))
        {
            var msg = $"Invalid Maven coordinate: '{rawCoord}'. Expected 'groupId:artifactId[:version]'.";
            output(msg + "\n");
            return new PackageCommandResult(false, msg);
        }

        try
        {
            var jarPath = await _resolver.DownloadArtifactAsync(coord, output, ct).ConfigureAwait(false);
            return new PackageCommandResult(
                Success: true,
                Message: $"Successfully restored {coord.Canonical}.",
                AddedSearchPath: jarPath);
        }
        catch (OperationCanceledException)
        {
            return new PackageCommandResult(false, "Maven artifact download cancelled.");
        }
        catch (Exception ex)
        {
            output($"❌ Error restoring Maven package '{coord.Canonical}': {ex.Message}\n");
            return new PackageCommandResult(false, ex.Message);
        }
    }
}
