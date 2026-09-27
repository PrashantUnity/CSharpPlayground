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
            "NetCoreDbg provides full CoreCLR process debugging with thread and async frame inspection.",
            OperatingSystem.IsMacOS()
                ? ["brew install netcoredbg"]
                : ["Download binary from https://github.com/Samsung/netcoredbg/releases"],
            "https://github.com/Samsung/netcoredbg");

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

        return await _adapterManager.LaunchStdioAdapterAsync(
            LanguageIds.CSharp,
            spec,
            context,
            postHandshake: async dapClient =>
            {
                await dapClient.SendRequestAsync("launch", new
                {
                    program = "dotnet",
                    args = new[] { dllPath },
                    cwd = outDir,
                    stopAtEntry = false
                }, ct).ConfigureAwait(false);
            },
            ct).ConfigureAwait(false);
    }

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

    private async Task<string?> FindNetCoreDbgAsync(CancellationToken ct)
    {
        // 1. Check PATH
        var pathEnv = await _host.GetLoginShellPathAsync(ct).ConfigureAwait(false);
        var folders = ExecutableSearch.SplitPath(pathEnv, _host.IsWindows);
        var foundOnPath = ExecutableSearch.FindAll(_host, folders, ["netcoredbg"]).FirstOrDefault();
        if (!string.IsNullOrEmpty(foundOnPath)) return foundOnPath;

        // 2. Check standard Homebrew / Unix paths
        string[] candidates = OperatingSystem.IsMacOS()
            ? ["/opt/homebrew/bin/netcoredbg", "/usr/local/bin/netcoredbg"]
            : ["/usr/bin/netcoredbg", "/usr/local/bin/netcoredbg"];

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
