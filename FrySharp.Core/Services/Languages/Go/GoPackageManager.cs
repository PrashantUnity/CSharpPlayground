using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using PdfEditorApp.Plugins.CSharpEditor.Services.Packages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;

/// <summary>
/// Adds Go modules for notebooks with <c>go get</c>: <c>%go get &lt;package&gt;</c>, <c>%get &lt;package&gt;</c>, or a <c>// #go: &lt;package&gt;</c> comment.
/// A module is downloaded into a scratch module of its own (see <see cref="GoNotebookDependencies"/>), which every notebook build then
/// requires, since <c>go get</c> only works inside a module and a notebook cell isn't one until it is built.
/// </summary>
public sealed partial class GoPackageManager : IPackageManager
{
    private readonly GoToolchainProvider? _toolchains;
    private readonly IProcessLauncher _launcher;
    private readonly IHostEnvironment _host;
    private readonly string _goRoot;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="goRoot">The studio's Go folder: the shared build cache, and the scratch module that downloads modules.</param>
    public GoPackageManager(
        string goRoot,
        GoToolchainProvider? toolchains = null,
        IProcessLauncher? launcher = null,
        IHostEnvironment? host = null)
    {
        _goRoot = goRoot;
        _toolchains = toolchains;
        _launcher = launcher ?? new ProcessLauncher();
        _host = host ?? new HostEnvironment();
        NotebookDependencies = new GoNotebookDependencies(goRoot);
    }

    /// <summary>The modules notebook cells have added.</summary>
    public GoNotebookDependencies NotebookDependencies { get; }

    public GoToolchainProvider? Toolchains => _toolchains;

    [GeneratedRegex(@"^[%#!](?:go\s+)?(?:get|install|add)\s+(?<pkg>[a-zA-Z0-9_\-\./@]+)(?:\s+(?<args>.*))?$", RegexOptions.IgnoreCase)]
    private static partial Regex GoMagicRegex();

    [GeneratedRegex(@"^\s*//\s*#(?:go|golang):\s*(?:get\s+)?(?<pkg>[a-zA-Z0-9_\-\./@]+)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex GoCommentRegex();

    [GeneratedRegex(@"^\s*#r\s+""(?:go|golang):\s*(?<pkg>[a-zA-Z0-9_\-\./@]+)\s*""\s*;?$", RegexOptions.IgnoreCase)]
    private static partial Regex GoRDirectiveRegex();

    public string ToolName => "go get";

    public bool TryParseDirective(string line, out PackageCommand command)
    {
        var trimmed = line.Trim();

        var matchMagic = GoMagicRegex().Match(trimmed);
        if (matchMagic.Success)
        {
            var pkg = matchMagic.Groups["pkg"].Value;
            command = new PackageCommand(trimmed, ["get", pkg]);
            return true;
        }

        var matchComment = GoCommentRegex().Match(trimmed);
        if (matchComment.Success)
        {
            var pkg = matchComment.Groups["pkg"].Value;
            command = new PackageCommand(trimmed, ["get", pkg]);
            return true;
        }

        var matchR = GoRDirectiveRegex().Match(trimmed);
        if (matchR.Success)
        {
            var pkg = matchR.Groups["pkg"].Value;
            command = new PackageCommand(trimmed, ["get", pkg]);
            return true;
        }

        command = null!;
        return false;
    }

    public PackageCommand InstallCommand(string package)
    {
        var trimmed = package.Trim();
        return new PackageCommand($"// #go: {trimmed}", ["get", trimmed]);
    }

    public string PackageForMissingDependency(string missingName) => GoPackageMap.PackageFor(missingName);

    public async Task<PackageCommandResult> RunAsync(
        PackageCommand command,
        ToolchainInfo toolchain,
        Action<string> output,
        CancellationToken ct = default)
    {
        if (command.Arguments.Count < 2)
        {
            return new PackageCommandResult(false, "Specify a Go package to install, e.g. %go get github.com/google/uuid");
        }

        var rawPkg = command.Arguments[1];
        var pkg = GoPackageMap.PackageFor(rawPkg);

        var goExecutable = toolchain?.ExecutablePath;
        if (string.IsNullOrEmpty(goExecutable) || !_host.FileExists(goExecutable))
        {
            if (_toolchains != null)
            {
                var resolved = await _toolchains.ResolveAsync(new ToolchainQuery(), ct).ConfigureAwait(false);
                if (resolved.IsFound && resolved.Toolchain != null)
                {
                    goExecutable = resolved.Toolchain.ExecutablePath;
                }
            }
        }

        if (string.IsNullOrEmpty(goExecutable) || !_host.FileExists(goExecutable))
        {
            output("❌ Go toolchain executable could not be found.\n");
            output(GoGuidance.NotInstalled(_host).ToText() + "\n");
            return new PackageCommandResult(false, "Go executable not found.");
        }

        var gate = _locks.GetOrAdd(goExecutable, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);

        int exitCode;
        try
        {
            output($"▶ Running go get {pkg}...\n");

            var version = toolchain?.Version ?? new Version(1, 21);
            NotebookDependencies.EnsureModule(version);
            var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["GOCACHE"] = Path.Combine(_goRoot, "cache"),
                ["GOTOOLCHAIN"] = "local",
                ["GOWORK"] = "off",
                ["GOFLAGS"] = "-mod=mod",
                ["GO111MODULE"] = "on"
            };

            using var process = _launcher.Start(new ProcessStartSpec
            {
                FileName = goExecutable,
                Arguments = ["get", pkg],
                WorkingDirectory = NotebookDependencies.Directory,
                Environment = environment
            }, output, output);

            using (ct.Register(process.Kill))
            {
                exitCode = await process.Completion.ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            output($"❌ Failed to execute go get: {ex.Message}\n");
            return new PackageCommandResult(false, ex.Message);
        }
        finally
        {
            gate.Release();
        }

        if (exitCode != 0)
        {
            output($"❌ go get failed with exit code {exitCode}.\n");
            return new PackageCommandResult(false, $"go get exited with code {exitCode}");
        }

        output($"✓ Added {pkg}: notebook cells can import it.\n");
        return new PackageCommandResult(true);
    }
}
