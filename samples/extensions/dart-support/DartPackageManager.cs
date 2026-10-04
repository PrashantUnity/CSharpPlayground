#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Services.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace DartSupportExtension;

/// <summary>
/// Manages Dart packages and dependencies via <c>dart pub</c>.
/// Supports %pub add &lt;package&gt;, %dart add &lt;package&gt;, and // #dart: &lt;package&gt; directives.
/// </summary>
public sealed class DartPackageManager : IPackageManager
{
    private readonly DartToolchainProvider _toolchains;
    private readonly IProcessLauncher _launcher;
    private readonly IHostEnvironment _host;

    private static readonly Regex PubMagicRegex = new(
        @"^[%#!](?:dart\s+)?(?:pub\s+)?(?:add|get|install)\s+(?<pkg>[a-zA-Z0-9_\-]+)(?:\s+(?<args>.*))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DartCommentRegex = new(
        @"^\s*//\s*#(?:dart|pub):\s*(?:add\s+)?(?<pkg>[a-zA-Z0-9_\-]+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public DartPackageManager(DartToolchainProvider toolchains, IProcessLauncher launcher, IHostEnvironment host)
    {
        _toolchains = toolchains ?? throw new ArgumentNullException(nameof(toolchains));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public string ToolName => "dart pub";

    public bool TryParseDirective(string line, out PackageCommand command)
    {
        var trimmed = line.Trim();

        var matchMagic = PubMagicRegex.Match(trimmed);
        if (matchMagic.Success)
        {
            var pkg = matchMagic.Groups["pkg"].Value;
            command = new PackageCommand(trimmed, ["pub", "add", pkg]);
            return true;
        }

        var matchComment = DartCommentRegex.Match(trimmed);
        if (matchComment.Success)
        {
            var pkg = matchComment.Groups["pkg"].Value;
            command = new PackageCommand(trimmed, ["pub", "add", pkg]);
            return true;
        }

        command = new PackageCommand(string.Empty, Array.Empty<string>());
        return false;
    }

    public PackageCommand InstallCommand(string package)
    {
        var trimmed = package.Trim();
        return new PackageCommand($"%pub add {trimmed}", ["pub", "add", trimmed]);
    }

    public string PackageForMissingDependency(string missingName) => missingName;

    public async Task<PackageCommandResult> RunAsync(
        PackageCommand command,
        ToolchainInfo toolchain,
        Action<string> output,
        CancellationToken ct = default)
    {
        var executable = toolchain?.ExecutablePath;
        if (string.IsNullOrEmpty(executable) || !File.Exists(executable))
        {
            var res = await _toolchains.ResolveAsync(new ToolchainQuery(), ct).ConfigureAwait(false);
            if (res.IsFound && res.Toolchain != null)
            {
                executable = res.Toolchain.ExecutablePath;
            }
        }

        if (string.IsNullOrEmpty(executable) || !File.Exists(executable))
        {
            output("Dart SDK executable could not be found.\n");
            return new PackageCommandResult(false, "Dart executable not found.");
        }

        var args = command.Arguments.Count > 0 ? command.Arguments : ["pub", "add"];
        output($"▶ Running: dart {string.Join(" ", args)}...\n");

        int exitCode;
        try
        {
            using var proc = _launcher.Start(new ProcessStartSpec
            {
                FileName = executable,
                Arguments = args,
                WorkingDirectory = _host.HomeDirectory
            },
            outText => output(outText),
            errText => output(errText));

            exitCode = await proc.WaitForExitOrKillAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new PackageCommandResult(false, "Package install cancelled.");
        }

        return new PackageCommandResult(exitCode == 0, exitCode == 0 ? "Package operation completed successfully." : $"dart pub exited with code {exitCode}");
    }
}
