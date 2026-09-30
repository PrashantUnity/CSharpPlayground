using System.Text.RegularExpressions;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.CSharp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.FSharp;

/// <summary>
/// Provides NuGet package resolution, directive parsing, and installation for F# scripts and notebook cells.
/// Supports inline <c>#r "nuget: ..."</c> directives and package quick-fixes.
/// </summary>
public sealed partial class FSharpPackageManager : IPackageManager
{
    private readonly NuGetPackageManager _inner;

    [GeneratedRegex(@"^\s*#r\s+""nuget:\s*(?<id>[a-zA-Z0-9_\-\.]+)(?:,\s*(?<ver>[^""]+))?""\s*;?$", RegexOptions.IgnoreCase)]
    private static partial Regex FSharpNuGetDirectiveRegex();

    [GeneratedRegex(@"^[%#!]nuget\s+(?:install|add)\s+(?<id>[a-zA-Z0-9_\-\.]+)(?:\s+(?<ver>[^\s]+))?$", RegexOptions.IgnoreCase)]
    private static partial Regex NuGetMagicRegex();

    public FSharpPackageManager(NuGetPackageManager? inner = null)
    {
        _inner = inner ?? new NuGetPackageManager();
    }

    public string ToolName => "NuGet";

    public bool TryParseDirective(string line, out PackageCommand command)
    {
        var trimmed = line.Trim();
        var matchDirective = FSharpNuGetDirectiveRegex().Match(trimmed);
        if (matchDirective.Success)
        {
            var id = matchDirective.Groups["id"].Value;
            var ver = matchDirective.Groups["ver"].Success ? matchDirective.Groups["ver"].Value.Trim() : null;
            var args = ver != null ? new[] { "install", id, ver } : new[] { "install", id };
            command = new PackageCommand(trimmed, args);
            return true;
        }

        var matchMagic = NuGetMagicRegex().Match(trimmed);
        if (matchMagic.Success)
        {
            var id = matchMagic.Groups["id"].Value;
            var ver = matchMagic.Groups["ver"].Success ? matchMagic.Groups["ver"].Value.Trim() : null;
            var args = ver != null ? new[] { "install", id, ver } : new[] { "install", id };
            command = new PackageCommand(trimmed, args);
            return true;
        }

        return _inner.TryParseDirective(line, out command);
    }

    public PackageCommand InstallCommand(string package)
    {
        var trimmed = package.Trim();
        return new PackageCommand($"#r \"nuget: {trimmed}\"", ["install", trimmed]);
    }

    public string PackageForMissingDependency(string missingName) =>
        _inner.PackageForMissingDependency(missingName);

    public Task<PackageCommandResult> RunAsync(
        PackageCommand command,
        ToolchainInfo toolchain,
        Action<string> output,
        CancellationToken ct = default) =>
        _inner.RunAsync(command, toolchain, output, ct);
}
