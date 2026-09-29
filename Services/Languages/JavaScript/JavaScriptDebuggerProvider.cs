using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging.Dap;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.JavaScript;

public sealed class JavaScriptDebuggerProvider : IDebuggerProvider, IDapAdapterRegistration
{
    private readonly JavaScriptToolchainProvider _toolchain;
    private readonly IProcessLauncher _processes;
    private readonly IHostEnvironment _host;
    private readonly DapAdapterManager _adapterManager;

    public JavaScriptDebuggerProvider(
        JavaScriptToolchainProvider toolchain,
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

    public string LanguageId => LanguageIds.JavaScript;
    public string AdapterName => "Node.js Inspector (V8)";

    public async ValueTask<DebuggerResolution> ResolveDebuggerAsync(ToolchainResolution? toolchainResolution, CancellationToken ct = default)
    {
        var resolved = toolchainResolution ?? await _toolchain.ResolveAsync(new ToolchainQuery(), ct).ConfigureAwait(false);
        if (!resolved.IsFound || resolved.Toolchain == null)
        {
            return new DebuggerResolution(
                IsAvailable: false,
                DebuggerName: "Node.js Inspector (V8)",
                ExecutablePath: null,
                Version: null,
                MissingGuidance: resolved.Missing);
        }

        return new DebuggerResolution(
            IsAvailable: true,
            DebuggerName: "Node.js Inspector (V8)",
            ExecutablePath: resolved.Toolchain.ExecutablePath,
            Version: resolved.Toolchain.DisplayName,
            MissingGuidance: null);
    }

    public async Task<IDebugSession> LaunchAsync(DebugLaunchContext context, CancellationToken ct = default)
    {
        var docFolder = string.IsNullOrEmpty(context.SourceFilePath) ? null : Path.GetDirectoryName(context.SourceFilePath);
        var resolved = context.Toolchain ?? await _toolchain.ResolveAsync(new ToolchainQuery(docFolder), ct).ConfigureAwait(false);
        if (!resolved.IsFound || resolved.Toolchain == null)
        {
            throw new InvalidOperationException(resolved.Missing?.Summary ?? "Node.js runtime not found.");
        }

        var nodeExe = resolved.Toolchain.ExecutablePath;
        int port = DapAdapterManager.GetAvailablePort();

        var workingDir = string.IsNullOrEmpty(context.SourceFilePath)
            ? Directory.GetCurrentDirectory()
            : Path.GetDirectoryName(context.SourceFilePath) ?? Directory.GetCurrentDirectory();

        string scriptFile = context.SourceFilePath;
        if (string.IsNullOrEmpty(scriptFile) || !File.Exists(scriptFile))
        {
            scriptFile = Path.Combine(Path.GetTempPath(), $"script_{Guid.NewGuid():N}.js");
            await File.WriteAllTextAsync(scriptFile, context.SourceCode, ct).ConfigureAwait(false);
        }

        var spec = new ProcessStartSpec
        {
            FileName = nodeExe,
            Arguments = [$"--inspect-brk=127.0.0.1:{port}", scriptFile],
            WorkingDirectory = workingDir
        };

        // --inspect-brk opens V8's inspector, which speaks the Chrome DevTools Protocol over a WebSocket, not the Debug Adapter Protocol:
        // the adapter is the bridge between them, and its streams are what the session's DapClient talks to.
        var earlyOutput = new List<string>();
        NodeInspectorDapAdapter? adapter = null;
        var gate = new object();
        void Output(string text)
        {
            lock (gate)
            {
                if (adapter != null) adapter.OnProcessOutput(text);
                else earlyOutput.Add(text);
            }
        }

        var process = _processes.Start(spec, Output, Output);
        adapter = new NodeInspectorDapAdapter(process, port, scriptFile, context.OnLiveOutput);
        lock (gate)
        {
            foreach (var text in earlyOutput) adapter.OnProcessOutput(text);
            earlyOutput.Clear();
        }

        try
        {
            return await _adapterManager.LaunchBridgeAdapterAsync(
                LanguageIds.JavaScript,
                adapter.ClientInputStream,
                adapter.ClientOutputStream,
                process,
                context,
                postHandshake: null,
                ct).ConfigureAwait(false);
        }
        catch
        {
            await adapter.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    Task<IDebugSession> IDapAdapterRegistration.LaunchAsync(DapAdapterManager manager, DebugLaunchContext context, CancellationToken ct) =>
        LaunchAsync(context, ct);
}
