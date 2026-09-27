using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging.Dap;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.JavaScript;

public sealed class JavaScriptDebuggerProvider : IDebuggerProvider
{
    private readonly JavaScriptToolchainProvider _toolchain;
    private readonly IProcessLauncher _processes;
    private readonly IHostEnvironment _host;

    public JavaScriptDebuggerProvider(JavaScriptToolchainProvider toolchain, IProcessLauncher processes, IHostEnvironment host)
    {
        _toolchain = toolchain ?? throw new ArgumentNullException(nameof(toolchain));
        _processes = processes ?? throw new ArgumentNullException(nameof(processes));
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

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
        int port = GetAvailablePort();

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

        var managedProcess = _processes.Start(
            spec,
            onStandardOutput: text => context.OnLiveOutput?.Invoke(text),
            onStandardError: text => context.OnLiveOutput?.Invoke(text));

        // Connect over local socket
        TcpClient? tcpClient = null;
        for (int i = 0; i < 20; i++)
        {
            if (managedProcess.HasExited)
            {
                throw new InvalidOperationException("Node.js debuggee process exited unexpectedly.");
            }

            try
            {
                var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, port, ct).ConfigureAwait(false);
                tcpClient = client;
                break;
            }
            catch (SocketException)
            {
                await Task.Delay(100, ct).ConfigureAwait(false);
            }
        }

        if (tcpClient == null)
        {
            managedProcess.Kill();
            throw new TimeoutException($"Timed out connecting to Node.js inspector on port {port}.");
        }

        var stream = tcpClient.GetStream();
        var dapClient = new DapClient(stream, stream);
        dapClient.Start();

        var session = new DapDebugSession(LanguageIds.JavaScript, dapClient, managedProcess);
        return session;
    }

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
