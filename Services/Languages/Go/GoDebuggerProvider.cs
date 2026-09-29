using System.Text.RegularExpressions;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging.Dap;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;

/// <summary>
/// Discovers the Delve Go debugger (<c>dlv</c>), compiles Go source with debug symbols (<c>-gcflags="all=-N -l"</c>),
/// and orchestrates interactive DAP debug sessions supporting breakpoints, stepping, call stacks, and locals inspection.
/// </summary>
public sealed class GoDebuggerProvider : IDebuggerProvider, IDapAdapterRegistration
{
    private readonly GoToolchainProvider _toolchain;
    private readonly IProcessLauncher _processes;
    private readonly IHostEnvironment _host;
    private readonly DapAdapterManager _adapterManager;

    public GoDebuggerProvider(
        GoToolchainProvider toolchain,
        IProcessLauncher processes,
        IHostEnvironment host,
        DapAdapterManager? adapterManager = null)
    {
        _toolchain = toolchain ?? throw new ArgumentNullException(nameof(toolchain));
        _processes = processes ?? throw new ArgumentNullException(nameof(processes));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _adapterManager = adapterManager ?? new DapAdapterManager(processes, host);
        _adapterManager.RegisterAdapter(this);
    }

    public string LanguageId => LanguageIds.Go;
    public string AdapterName => "dlv (Delve Go Debugger)";

    public async ValueTask<DebuggerResolution> ResolveDebuggerAsync(ToolchainResolution? toolchain, CancellationToken ct = default)
    {
        var resolved = toolchain ?? await _toolchain.ResolveAsync(new ToolchainQuery(), ct).ConfigureAwait(false);

        var candidatePaths = new List<string>();

        if (resolved.IsFound && resolved.Toolchain != null)
        {
            var binDir = Path.GetDirectoryName(resolved.Toolchain.ExecutablePath);
            if (!string.IsNullOrEmpty(binDir))
            {
                candidatePaths.Add(Path.Combine(binDir, _host.IsWindows ? "dlv.exe" : "dlv"));
            }
        }

        var home = _host.HomeDirectory;
        if (!string.IsNullOrEmpty(home))
        {
            candidatePaths.Add(Path.Combine(home, "go", "bin", _host.IsWindows ? "dlv.exe" : "dlv"));
        }

        var gopath = _host.GetEnvironmentVariable("GOPATH");
        if (!string.IsNullOrEmpty(gopath))
        {
            candidatePaths.Add(Path.Combine(gopath, "bin", _host.IsWindows ? "dlv.exe" : "dlv"));
        }

        if (_host.IsMacOS)
        {
            candidatePaths.Add("/opt/homebrew/bin/dlv");
            candidatePaths.Add("/usr/local/bin/dlv");
            candidatePaths.Add("/usr/local/go/bin/dlv");
        }
        else if (!_host.IsWindows)
        {
            candidatePaths.Add("/usr/bin/dlv");
            candidatePaths.Add("/usr/local/bin/dlv");
            candidatePaths.Add("/snap/bin/dlv");
        }
        else
        {
            candidatePaths.Add(@"C:\Go\bin\dlv.exe");
            var userProf = _host.GetEnvironmentVariable("USERPROFILE");
            if (!string.IsNullOrEmpty(userProf))
            {
                candidatePaths.Add(Path.Combine(userProf, "go", "bin", "dlv.exe"));
            }
        }

        candidatePaths.Add(_host.IsWindows ? "dlv.exe" : "dlv");

        foreach (var path in candidatePaths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            try
            {
                var result = await _host.RunAsync(path, ["version"], TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
                if (result.ExitCode == 0 || !string.IsNullOrWhiteSpace(result.StandardOutput))
                {
                    var versionText = (result.StandardOutput + " " + result.StandardError).Trim();
                    var match = Regex.Match(versionText, @"(?:Version:\s*|delve\s+)(?<ver>\d+(?:\.\d+)+)", RegexOptions.IgnoreCase);
                    var ver = match.Success ? match.Groups["ver"].Value : "1.0.0";

                    return new DebuggerResolution(
                        IsAvailable: true,
                        DebuggerName: "dlv",
                        ExecutablePath: path,
                        Version: ver,
                        MissingGuidance: null);
                }
            }
            catch
            {
                // Try next candidate
            }
        }

        var missing = new MissingToolchainGuidance(
            "Go Debugger (dlv / Delve) not found",
            "C# Code Studio uses Delve (dlv dap) for interactive Go debugging with breakpoints, call stacks, and variable inspection.",
            [
                "Install Delve via Go: go install github.com/go-delve/delve/cmd/dlv@latest",
                _host.IsMacOS ? "Install via Homebrew: brew install delve" :
                _host.IsWindows ? "Install via Chocolatey: choco install delve" :
                "Install via package manager: sudo apt install delve",
                "Ensure '$GOPATH/bin' or '~/go/bin' is included in your PATH.",
                "Click Refresh in the toolchain picker or restart C# Code Studio."
            ],
            DownloadUrl: "https://github.com/go-delve/delve");

        return new DebuggerResolution(
            IsAvailable: false,
            DebuggerName: "dlv",
            ExecutablePath: null,
            Version: null,
            MissingGuidance: missing);
    }

    public async Task<IDebugSession> LaunchAsync(DebugLaunchContext context, CancellationToken ct = default)
    {
        var resolved = await _toolchain.ResolveAsync(new ToolchainQuery(), ct).ConfigureAwait(false);
        if (!resolved.IsFound || resolved.Toolchain == null)
        {
            throw new InvalidOperationException(resolved.Missing?.Summary ?? "Go toolchain not found.");
        }

        string scriptFile = context.SourceFilePath;
        if (string.IsNullOrEmpty(scriptFile) || !File.Exists(scriptFile))
        {
            scriptFile = Path.Combine(Path.GetTempPath(), $"script_{Guid.NewGuid():N}.go");
            await File.WriteAllTextAsync(scriptFile, context.SourceCode, ct).ConfigureAwait(false);
        }
        scriptFile = Path.GetFullPath(scriptFile);

        var goExecutable = resolved.Toolchain.ExecutablePath;
        var hash = Math.Abs(scriptFile.GetHashCode(StringComparison.OrdinalIgnoreCase)).ToString("x8");
        var outDir = Path.Combine(Path.GetTempPath(), "FryStudio", "go_debug", hash);
        Directory.CreateDirectory(outDir);

        var fileBaseName = Path.GetFileNameWithoutExtension(scriptFile.Replace('\\', '/'));
        var binName = _host.IsWindows ? $"{fileBaseName}.exe" : fileBaseName;
        var binPath = Path.Combine(outDir, binName);
        var workingDir = Path.GetDirectoryName(scriptFile) ?? Directory.GetCurrentDirectory();

        // Compile with debug symbols and disabled optimizations (-gcflags="all=-N -l")
        var compileArgs = new List<string>
        {
            "build",
            "-gcflags=all=-N -l",
            "-o",
            binPath,
            scriptFile
        };

        var compileResult = await _host.RunAsync(goExecutable, compileArgs, TimeSpan.FromSeconds(30), ct).ConfigureAwait(false);
        if (compileResult.ExitCode != 0)
        {
            var parser = new GoCompilerDiagnosticParser();
            var parseResult = parser.Parse(compileResult.StandardOutput + "\n" + compileResult.StandardError, scriptFile);
            throw new DebugCompilationException(parseResult.Diagnostics);
        }

        var debugger = await ResolveDebuggerAsync(resolved, ct).ConfigureAwait(false);
        if (!debugger.IsAvailable || string.IsNullOrEmpty(debugger.ExecutablePath))
        {
            throw new InvalidOperationException(debugger.MissingGuidance?.Summary ?? "Go debugger (dlv) not found.");
        }

        // Delve's DAP server is a TCP server and has no stdio mode: started plain it listens on a port of its own choosing and
        // never reads its input, so the session has to say where to listen and connect there.
        int port = DapAdapterManager.GetAvailablePort();
        var spec = new ProcessStartSpec
        {
            FileName = debugger.ExecutablePath,
            Arguments = ["dap", $"--listen=127.0.0.1:{port}"],
            WorkingDirectory = workingDir
        };

        return await _adapterManager.LaunchSocketAdapterAsync(
            LanguageIds.Go,
            spec,
            port,
            context with { OnLiveOutput = WithoutListeningBanner(context.OnLiveOutput) },
            postHandshake: async dapClient =>
            {
                var launched = await dapClient.SendRequestAsync("launch", LaunchArguments(binPath, workingDir), ct).ConfigureAwait(false);
                if (!launched.Success) throw new DapException(launched.Message ?? "Delve refused to launch the program.", "launch", launched);
            },
            ct,
            // Delve refuses setBreakpoints and configurationDone until it has been asked to launch ("No debug session started").
            DapHandshake.Standard).ConfigureAwait(false);
    }

    /// <summary>What Delve is asked to run: the binary just built, stopping only at the user's breakpoints.</summary>
    internal static object LaunchArguments(string binaryPath, string workingDirectory) => new
    {
        mode = "exec",
        program = binaryPath,
        cwd = workingDirectory,
        stopOnEntry = false
    };

    private static readonly Regex ListeningBanner = new(@"^DAP server listening at: [^\r\n]*(\r?\n)?", RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>Delve announces its port on standard output; the session already knows it, so the line isn't shown as the program's output.</summary>
    internal static Action<string>? WithoutListeningBanner(Action<string>? sink)
    {
        if (sink == null) return null;
        return text =>
        {
            var rest = ListeningBanner.Replace(text, string.Empty);
            if (rest.Length > 0) sink(rest);
        };
    }

    Task<IDebugSession> IDapAdapterRegistration.LaunchAsync(DapAdapterManager manager, DebugLaunchContext context, CancellationToken ct) =>
        LaunchAsync(context, ct);
}
