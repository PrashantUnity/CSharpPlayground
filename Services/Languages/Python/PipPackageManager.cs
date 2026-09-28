using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using PdfEditorApp.Plugins.CSharpEditor.Services.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Python;

/// <summary>
/// Installs Python packages with pip, as <c>%pip install numpy</c> does in Jupyter. A Python that doesn't allow pip
/// installs (Homebrew's and Debian's are "externally managed") gets the studio environment instead: made from that
/// Python with its packages still visible, saved as the choice from then on, and never touching the Python itself.
/// </summary>
public sealed partial class PipPackageManager(PythonToolchainProvider toolchains, IProcessLauncher launcher, IHostEnvironment host) : IPackageManager
{
    private static readonly string[] ShowsProgress = ["install", "download", "wheel"];
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _environmentLocks = new(StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex(@"^[%!]pip3?\s+(?<args>.+)$")]
    private static partial Regex PipDirective();

    public string ToolName => "pip";

    public bool TryParseDirective(string line, out PackageCommand command)
    {
        var match = PipDirective().Match(line.Trim());
        if (!match.Success)
        {
            command = null!;
            return false;
        }

        command = new PackageCommand(line.Trim(), SplitArguments(match.Groups["args"].Value));
        return true;
    }

    public PackageCommand InstallCommand(string package) => new($"%pip install {package}", ["install", package]);

    public string PackageForMissingDependency(string missingName) => PythonPackageMap.PackageFor(missingName);

    public async Task<PackageCommandResult> RunAsync(PackageCommand command, ToolchainInfo toolchain, Action<string> output, CancellationToken ct = default)
    {
        if (command.Arguments.Count == 0) return new PackageCommandResult(false, "Tell pip what to do, e.g. %pip install numpy.");

        var python = toolchain;
        ToolchainInfo? switched = null;
        if (NeedsStudioEnvironment(python, command))
        {
            output($"{python.Label} doesn't allow installing packages into it (it's managed by {python.Source}), so they go into the studio's own environment.\n");
            try
            {
                python = await toolchains.EnsureStudioEnvironmentAsync(python, output, ct: ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                output(ex.Message + "\n");
                return new PackageCommandResult(false, ex.Message);
            }

            toolchains.Select(python.ExecutablePath);
            switched = python;
            output($"From now on Python runs with the studio environment ({python.ExecutablePath}). Pick another Python from the status bar any time.\n");
        }

        if (!python.Is(PythonToolchainProvider.PipProperty) && switched == null)
        {
            var message = $"{python.Label} has no pip. Install pip for it (python -m ensurepip), or pick another Python.";
            output(message + "\n");
            return new PackageCommandResult(false, message);
        }

        var gate = _environmentLocks.GetOrAdd(python.ExecutablePath, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        int exitCode;
        try
        {
            var environment = await PythonProcessEnvironment.ForAsync(host, python, ct);
            // Always "python -m pip", never the pip script: the right pip for this Python, and no trouble with the space
            // in the studio environment's path.
            using var process = launcher.Start(new ProcessStartSpec
            {
                FileName = python.ExecutablePath,
                Arguments = PipArguments(command.Arguments),
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
            return new PackageCommandResult(false, ex.Message, switched);
        }
        finally
        {
            gate.Release();
        }

        ct.ThrowIfCancellationRequested();
        var succeeded = exitCode == 0;
        return new PackageCommandResult(
            succeeded,
            succeeded ? null : $"pip stopped with exit code {exitCode}.",
            switched,
            switched?.Get(PythonToolchainProvider.SitePackagesProperty));
    }

    /// <summary>The arguments after <c>python</c>: <c>-m pip</c>, the command's own, and what keeps pip quiet and unattended.</summary>
    public static IReadOnlyList<string> PipArguments(IReadOnlyList<string> arguments)
    {
        var all = new List<string> { "-m", "pip" };
        all.AddRange(arguments);
        var subcommand = arguments.FirstOrDefault(a => !a.StartsWith('-'))?.ToLowerInvariant() ?? string.Empty;
        if (ShowsProgress.Contains(subcommand) && !arguments.Contains("--progress-bar")) all.AddRange(["--progress-bar", "off"]);
        // pip asks "Proceed (Y/n)?" before uninstalling, and nobody can answer it here.
        if (subcommand == "uninstall" && !arguments.Contains("-y") && !arguments.Contains("--yes")) all.Add("-y");
        all.Add("--disable-pip-version-check");
        all.Add("--no-input");
        return all;
    }

    // Only commands that change what's installed need a Python that allows it; "pip list" works anywhere.
    private static bool NeedsStudioEnvironment(ToolchainInfo python, PackageCommand command)
    {
        if (!python.Is(PythonToolchainProvider.ExternallyManagedProperty)) return false;
        var subcommand = command.Arguments.FirstOrDefault(a => !a.StartsWith('-'))?.ToLowerInvariant();
        return subcommand is "install" or "uninstall";
    }

    /// <summary>Splits like a shell: spaces separate, quotes group (<c>install "numpy&gt;=2"</c>).</summary>
    public static IReadOnlyList<string> SplitArguments(string text)
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
