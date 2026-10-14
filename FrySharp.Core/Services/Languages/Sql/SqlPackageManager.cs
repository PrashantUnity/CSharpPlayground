using System.Text.RegularExpressions;
using PdfEditorApp.Plugins.CSharpEditor.Services.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Sql;

/// <summary>
/// Provides package/extension directive parsing for SQL scripts and notebooks (e.g. <c>.load &lt;ext&gt;</c> or <c>%load &lt;ext&gt;</c>).
/// </summary>
public sealed partial class SqlPackageManager : IPackageManager
{
    [GeneratedRegex(@"^\s*(?:\.load|%load|#load)\s+(?<id>[^\s;]+)", RegexOptions.IgnoreCase)]
    private static partial Regex LoadExtensionRegex();

    public string ToolName => "SQLite Extensions";

    public bool TryParseDirective(string line, out PackageCommand command)
    {
        var trimmed = line.Trim();
        var match = LoadExtensionRegex().Match(trimmed);
        if (match.Success)
        {
            var id = match.Groups["id"].Value.Trim('\'', '"');
            command = new PackageCommand(trimmed, ["load", id]);
            return true;
        }

        command = new PackageCommand(string.Empty, Array.Empty<string>());
        return false;
    }

    public PackageCommand InstallCommand(string package)
    {
        var trimmed = package.Trim();
        return new PackageCommand($".load {trimmed}", ["load", trimmed]);
    }

    public string PackageForMissingDependency(string missingName) => missingName;

    public Task<PackageCommandResult> RunAsync(
        PackageCommand command,
        ToolchainInfo toolchain,
        Action<string> output,
        CancellationToken ct = default)
    {
        output($"SQLite directive: {command.Text}\n");
        return Task.FromResult(new PackageCommandResult(true, "Loaded extension."));
    }
}
