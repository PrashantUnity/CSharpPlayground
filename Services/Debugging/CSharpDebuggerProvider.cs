using System;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging.Dap;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;

public sealed class CSharpDebuggerProvider : IDebuggerProvider, IDapAdapterRegistration
{
    private readonly RoslynCompilerService _compilerService;
    private readonly ScriptDebuggerService _debuggerService;
    private readonly ScriptExecutionEngine _executionEngine;
    private readonly IProcessLauncher _processLauncher;
    private readonly IHostEnvironment _host;
    private readonly CSharpCoreClrCompiler _coreClrCompiler;
    private readonly string _cacheDirectory;
    private readonly DapAdapterManager _adapterManager;

    public CSharpDebuggerProvider(
        RoslynCompilerService compilerService,
        ScriptDebuggerService debuggerService,
        ScriptExecutionEngine executionEngine,
        IProcessLauncher processLauncher,
        IHostEnvironment host,
        string? cacheDirectory = null,
        DapAdapterManager? adapterManager = null)
    {
        _compilerService = compilerService ?? throw new ArgumentNullException(nameof(compilerService));
        _debuggerService = debuggerService ?? throw new ArgumentNullException(nameof(debuggerService));
        _executionEngine = executionEngine ?? throw new ArgumentNullException(nameof(executionEngine));
        _processLauncher = processLauncher ?? throw new ArgumentNullException(nameof(processLauncher));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _coreClrCompiler = new CSharpCoreClrCompiler(compilerService);
        _cacheDirectory = cacheDirectory ?? Path.Combine(Path.GetTempPath(), "frysharp_debug");
        _adapterManager = adapterManager ?? new DapAdapterManager(processLauncher, host);
        _adapterManager.RegisterAdapter(this);
    }

    public string LanguageId => LanguageIds.CSharp;
    public string AdapterName => "CoreCLR (netcoredbg)";

    public async ValueTask<DebuggerResolution> ResolveDebuggerAsync(ToolchainResolution? toolchain, CancellationToken ct = default)
    {
        var netcoredbgPath = await FindNetCoreDbgAsync(ct).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(netcoredbgPath))
        {
            return new DebuggerResolution(
                IsAvailable: true,
                DebuggerName: "CoreCLR (netcoredbg)",
                ExecutablePath: netcoredbgPath,
                Version: "DAP .NET Core",
                MissingGuidance: null);
        }

        var missingGuidance = new MissingToolchainGuidance(
            "NetCoreDbg isn't installed",
            "NetCoreDbg provides full CoreCLR process debugging with thread and async frame inspection. Without it the built-in debugger is used.",
            NetCoreDbgInstallSteps(),
            "https://github.com/Samsung/netcoredbg/releases");

        return new DebuggerResolution(
            IsAvailable: true, // In-process Roslyn debugger is always available
            DebuggerName: "In-Process Roslyn (Instant)",
            ExecutablePath: null,
            Version: ".NET 10 Roslyn In-Process",
            MissingGuidance: missingGuidance);
    }

    public async Task<IDebugSession> LaunchAsync(DebugLaunchContext context, CancellationToken ct = default)
    {
        var netcoredbgPath = await FindNetCoreDbgAsync(ct).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(netcoredbgPath))
        {
            try
            {
                return await LaunchCoreClrDapSessionAsync(netcoredbgPath, context, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                context.OnLiveOutput?.Invoke($"⚠️ CoreCLR debugger failed to launch ({ex.Message}). Falling back to In-Process Roslyn debugger...\n");
            }
        }

        // Fallback: fast in-process Roslyn debugger
        return LaunchInProcessSession(context);
    }

    private async Task<IDebugSession> LaunchCoreClrDapSessionAsync(string netcoredbgPath, DebugLaunchContext context, CancellationToken ct)
    {
        var runId = Guid.NewGuid().ToString("N")[..8];
        var outDir = Path.Combine(_cacheDirectory, runId);

        var (success, dllPath, diagnostics) = _coreClrCompiler.CompileToStandaloneBinary(
            context.SourceCode,
            outDir,
            assemblyName: "script");

        if (!success || string.IsNullOrEmpty(dllPath))
        {
            throw new DebugCompilationException(diagnostics);
        }

        var spec = new ProcessStartSpec
        {
            FileName = netcoredbgPath,
            Arguments = ["--interpreter=vscode"],
            WorkingDirectory = outDir
        };

        // The debug build names its one source file script.cs (the #line the script is wrapped in), whatever the document is
        // called: a breakpoint the debugger can match to nothing never binds, and the program runs straight through.
        var session = await _adapterManager.LaunchStdioAdapterAsync(
            LanguageIds.CSharp,
            spec,
            context with { SourceFilePath = ScriptSourceName },
            postHandshake: async dapClient =>
            {
                var launched = await dapClient.SendRequestAsync("launch", LaunchArguments(dllPath, outDir), ct).ConfigureAwait(false);
                if (!launched.Success) throw new DapException(launched.Message ?? "netcoredbg refused to launch the program.", "launch", launched);
            },
            ct,
            // The order the specification gives (and the one every other adapter is driven in); netcoredbg answers launch at once.
            DapHandshake.Standard).ConfigureAwait(false);

        session.SourcePathOverride = ScriptSourceName;
        return session;
    }

    /// <summary>The file name the debug build records for the script, and so the only source name a breakpoint can be matched by.</summary>
    internal const string ScriptSourceName = "script.cs";

    /// <summary>What netcoredbg is asked to run: the script's assembly, through <c>dotnet</c>.</summary>
    internal static object LaunchArguments(string dllPath, string workingDirectory) => new
    {
        program = "dotnet",
        args = new[] { dllPath },
        cwd = workingDirectory,
        stopAtEntry = false
    };

    Task<IDebugSession> IDapAdapterRegistration.LaunchAsync(DapAdapterManager manager, DebugLaunchContext context, CancellationToken ct) =>
        LaunchAsync(context, ct);

    private IDebugSession LaunchInProcessSession(DebugLaunchContext context)
    {
        var (compileOk, bytes, diagnostics) = _debuggerService.CompileForDebugging(
            context.SourceCode,
            ExecutionLanguageMode.Statements);

        if (!compileOk || bytes == null)
        {
            throw new DebugCompilationException(diagnostics);
        }

        var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        var roslynSession = ScriptDebugSession.BeginSession(context.Breakpoints, cts, context.ScriptId);

        var debugSession = new InProcessRoslynDebugSession(
            roslynSession,
            _debuggerService,
            _executionEngine,
            cts,
            Path.GetFileName(context.SourceFilePath));

        debugSession.StartExecution(bytes);
        return debugSession;
    }

    /// <summary>The folder in the user's home a downloaded netcoredbg is unzipped into: no package manager has it (Homebrew has no formula).</summary>
    internal const string HomeFolderName = ".netcoredbg";

    private IReadOnlyList<string> NetCoreDbgInstallSteps() =>
    [
        _host.IsMacOS ? "Download netcoredbg-osx-arm64.zip (Apple Silicon; there is no Intel build) from the latest release on GitHub." :
        _host.IsWindows ? "Download netcoredbg-win64.zip from the latest release on GitHub." :
        "Download netcoredbg-linux-amd64.tar.gz (or netcoredbg-linux-arm64.tar.gz) from the latest release on GitHub.",
        $"Unpack it into {(_host.IsWindows ? @"%USERPROFILE%\" : "~/")}{HomeFolderName} so that the netcoredbg program sits directly in that folder, or put its folder on your PATH.",
        "Click Refresh in the toolchain picker or restart C# Code Studio."
    ];

    private async Task<string?> FindNetCoreDbgAsync(CancellationToken ct)
    {
        var fileName = _host.IsWindows ? "netcoredbg.exe" : "netcoredbg";

        // 1. Check PATH
        var pathEnv = await _host.GetLoginShellPathAsync(ct).ConfigureAwait(false);
        var folders = ExecutableSearch.SplitPath(pathEnv, _host.IsWindows);
        var foundOnPath = ExecutableSearch.FindAll(_host, folders, ["netcoredbg"]).FirstOrDefault();
        if (!string.IsNullOrEmpty(foundOnPath)) return foundOnPath;

        // 2. The folder a release is unpacked into, then the standard system folders
        var candidates = new List<string>();
        if (!string.IsNullOrEmpty(_host.HomeDirectory)) candidates.Add(Path.Combine(_host.HomeDirectory, HomeFolderName, fileName));
        if (_host.IsMacOS) candidates.AddRange(["/opt/homebrew/bin/netcoredbg", "/usr/local/bin/netcoredbg"]);
        else if (!_host.IsWindows) candidates.AddRange(["/usr/bin/netcoredbg", "/usr/local/bin/netcoredbg"]);

        foreach (var c in candidates)
        {
            if (_host.FileExists(c)) return c;
        }

        return null;
    }
}

public sealed class DebugCompilationException : Exception
{
    public IReadOnlyList<DiagnosticItem> Diagnostics { get; }

    public DebugCompilationException(IReadOnlyList<DiagnosticItem> diagnostics)
        : base("Compilation for debugging failed.")
    {
        Diagnostics = diagnostics;
    }
}
