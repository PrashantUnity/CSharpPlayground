using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using PdfEditorApp.Plugins.CSharpEditor.Services.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.JavaScript;

/// <summary>
/// Installs JavaScript packages with npm, as <c>%npm install lodash</c> does in notebooks.
/// </summary>
public sealed partial class NpmPackageManager(IProcessLauncher launcher, IHostEnvironment host) : IPackageManager
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex(@"^[%!]npm\s+(?<args>.+)$")]
    private static partial Regex NpmDirective();

    public string ToolName => "npm";

    public bool TryParseDirective(string line, out PackageCommand command)
    {
        var match = NpmDirective().Match(line.Trim());
        if (!match.Success)
        {
            command = null!;
            return false;
        }

        command = new PackageCommand(line.Trim(), SplitArguments(match.Groups["args"].Value));
        return true;
    }

    public PackageCommand InstallCommand(string package) => new($"%npm install {package}", ["install", package]);

    public string PackageForMissingDependency(string missingName) =>
        JavaScriptPackageMap.PackageFor(missingName) ?? missingName;

    public async Task<PackageCommandResult> RunAsync(PackageCommand command, ToolchainInfo toolchain, Action<string> output, CancellationToken ct = default)
    {
        if (command.Arguments.Count == 0) return new PackageCommandResult(false, "Tell npm what to do, e.g. %npm install lodash.");

        var npmPath = ResolveNpmPath(toolchain);
        if (string.IsNullOrEmpty(npmPath))
        {
            var msg = "npm executable could not be found next to Node.js or on PATH.";
            output(msg + "\n");
            return new PackageCommandResult(false, msg);
        }

        var gate = _locks.GetOrAdd(npmPath, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        int exitCode;
        try
        {
            var environment = await JavaScriptProcessEnvironment.ForAsync(host, toolchain, ct);
            using var process = launcher.Start(new ProcessStartSpec
            {
                FileName = npmPath,
                Arguments = command.Arguments,
                Environment = environment
            }, output, output);

            using (ct.Register(process.Kill))
            {
                exitCode = await process.Completion;
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

        return exitCode == 0
            ? new PackageCommandResult(true, "npm finished successfully.")
            : new PackageCommandResult(false, $"npm exited with code {exitCode}.");
    }

    private string? ResolveNpmPath(ToolchainInfo toolchain)
    {
        var binDir = Path.GetDirectoryName(toolchain.ExecutablePath);
        if (!string.IsNullOrEmpty(binDir))
        {
            var candidate = Path.Combine(binDir, host.IsWindows ? "npm.cmd" : "npm");
            if (host.FileExists(candidate)) return candidate;
            if (host.IsWindows)
            {
                candidate = Path.Combine(binDir, "npm.exe");
                if (host.FileExists(candidate)) return candidate;
            }
        }

        var names = host.IsWindows ? new[] { "npm.cmd", "npm.exe", "npm" } : new[] { "npm" };
        var pathEnv = host.GetEnvironmentVariable("PATH");
        var folders = ExecutableSearch.SplitPath(pathEnv, host.IsWindows);
        return ExecutableSearch.FindAll(host, folders, names).FirstOrDefault();
    }

    private static IReadOnlyList<string> SplitArguments(string text)
    {
        var arguments = new List<string>();
        var current = new StringBuilder();
        char? quote = null;
        var inArgument = false;

        foreach (var c in text)
        {
            if (quote != null)
            {
                if (c == quote) quote = null;
                else current.Append(c);
                continue;
            }

            if (c is '"' or '\'')
            {
                quote = c;
                inArgument = true;
            }
            else if (char.IsWhiteSpace(c))
            {
                if (inArgument) arguments.Add(current.ToString());
                current.Clear();
                inArgument = false;
            }
            else
            {
                current.Append(c);
                inArgument = true;
            }
        }

        if (inArgument) arguments.Add(current.ToString());
        return arguments;
    }
}
