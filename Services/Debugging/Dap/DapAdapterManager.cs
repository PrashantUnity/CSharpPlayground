using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Debugging.Dap;

/// <summary>
/// Registration interface for language-specific DAP adapters.
/// </summary>
public interface IDapAdapterRegistration
{
    string LanguageId { get; }
    string AdapterName { get; }
    ValueTask<DebuggerResolution> ResolveDebuggerAsync(ToolchainResolution? toolchain, CancellationToken ct = default);
    Task<IDebugSession> LaunchAsync(DapAdapterManager manager, DebugLaunchContext context, CancellationToken ct = default);
}

/// <summary>
/// Unified orchestrator and registry for Debug Adapter Protocol (DAP) adapters.
/// Consolidates external toolchain resolution, adapter process spawning, socket/stdio piping,
/// and protocol initialization handshakes across all supported languages.
/// </summary>
public sealed class DapAdapterManager
{
    private readonly ConcurrentDictionary<string, IDapAdapterRegistration> _adapters = new(StringComparer.OrdinalIgnoreCase);
    private readonly IProcessLauncher _processes;
    private readonly IHostEnvironment _host;

    public DapAdapterManager(IProcessLauncher processes, IHostEnvironment host)
    {
        _processes = processes ?? throw new ArgumentNullException(nameof(processes));
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public IProcessLauncher Processes => _processes;
    public IHostEnvironment Host => _host;

    public void RegisterAdapter(IDapAdapterRegistration adapter)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        _adapters[adapter.LanguageId] = adapter;
    }

    public bool HasAdapter(string languageId) => _adapters.ContainsKey(languageId);

    public IDapAdapterRegistration? GetAdapter(string languageId)
    {
        _adapters.TryGetValue(languageId, out var adapter);
        return adapter;
    }

    public async ValueTask<DebuggerResolution> ResolveDebuggerAsync(string languageId, ToolchainResolution? toolchain, CancellationToken ct = default)
    {
        if (_adapters.TryGetValue(languageId, out var adapter))
        {
            return await adapter.ResolveDebuggerAsync(toolchain, ct).ConfigureAwait(false);
        }

        return new DebuggerResolution(
            IsAvailable: false,
            DebuggerName: $"{languageId} Debugger",
            ExecutablePath: null,
            Version: null,
            MissingGuidance: null);
    }

    public async Task<IDebugSession> LaunchSessionAsync(string languageId, DebugLaunchContext context, CancellationToken ct = default)
    {
        if (_adapters.TryGetValue(languageId, out var adapter))
        {
            return await adapter.LaunchAsync(this, context, ct).ConfigureAwait(false);
        }

        throw new NotSupportedException($"No DAP adapter registered for language '{languageId}'.");
    }

    public async Task<DapDebugSession> LaunchSocketAdapterAsync(
        string languageId,
        ProcessStartSpec spec,
        int port,
        DebugLaunchContext context,
        Func<DapClient, Task>? postHandshake = null,
        CancellationToken ct = default)
    {
        var managedProcess = _processes.Start(
            spec,
            onStandardOutput: text => context.OnLiveOutput?.Invoke(text),
            onStandardError: text => context.OnLiveOutput?.Invoke(text));

        TcpClient? tcpClient = null;
        for (int i = 0; i < 30; i++)
        {
            if (managedProcess.HasExited)
            {
                throw new InvalidOperationException($"{languageId} debuggee process exited before debugger could attach.");
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
            throw new TimeoutException($"Timed out connecting to {languageId} DAP adapter on port {port}.");
        }

        var stream = tcpClient.GetStream();
        var dapClient = new DapClient(stream, stream);
        dapClient.Start();

        await PerformStandardHandshakeAsync(dapClient, context, ct).ConfigureAwait(false);

        if (postHandshake != null)
        {
            await postHandshake(dapClient).ConfigureAwait(false);
        }

        return new DapDebugSession(languageId, dapClient, managedProcess);
    }

    public async Task<DapDebugSession> LaunchStdioAdapterAsync(
        string languageId,
        ProcessStartSpec spec,
        DebugLaunchContext context,
        Func<DapClient, Task>? postHandshake = null,
        CancellationToken ct = default)
    {
        var serverToClientPipe = new Pipe();
        var clientToServerPipe = new Pipe();

        var clientIn = serverToClientPipe.Reader.AsStream();
        var serverOut = serverToClientPipe.Writer.AsStream();

        var clientOut = clientToServerPipe.Writer.AsStream();
        var serverIn = clientToServerPipe.Reader.AsStream();

        var managedProcess = _processes.Start(
            spec,
            onStandardOutput: text =>
            {
                var bytes = Encoding.UTF8.GetBytes(text);
                serverOut.Write(bytes, 0, bytes.Length);
                serverOut.Flush();
            },
            onStandardError: err => context.OnLiveOutput?.Invoke(err));

        _ = Task.Run(async () =>
        {
            var buffer = new byte[4096];
            try
            {
                while (!managedProcess.HasExited)
                {
                    int read = await serverIn.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                    if (read == 0) break;
                    var text = Encoding.UTF8.GetString(buffer, 0, read);
                    await managedProcess.WriteInputAsync(text).ConfigureAwait(false);
                }
            }
            catch
            {
            }
        });

        var dapClient = new DapClient(clientIn, clientOut);
        dapClient.Start();

        await PerformStandardHandshakeAsync(dapClient, context, ct).ConfigureAwait(false);

        if (postHandshake != null)
        {
            await postHandshake(dapClient).ConfigureAwait(false);
        }

        return new DapDebugSession(languageId, dapClient, managedProcess);
    }

    public async Task<DapDebugSession> LaunchBridgeAdapterAsync(
        string languageId,
        Stream inputStream,
        Stream outputStream,
        IManagedProcess? process,
        DebugLaunchContext context,
        Func<DapClient, Task>? postHandshake = null,
        CancellationToken ct = default)
    {
        var dapClient = new DapClient(inputStream, outputStream);
        dapClient.Start();

        await PerformStandardHandshakeAsync(dapClient, context, ct).ConfigureAwait(false);

        if (postHandshake != null)
        {
            await postHandshake(dapClient).ConfigureAwait(false);
        }

        return new DapDebugSession(languageId, dapClient, process);
    }

    private static async Task PerformStandardHandshakeAsync(DapClient client, DebugLaunchContext context, CancellationToken ct)
    {
        // 1. Initialize
        await client.SendRequestAsync("initialize", new
        {
            clientID = "FryStudio",
            clientName = "C# Code Studio",
            adapterID = context.ScriptId,
            linesStartAt1 = true,
            columnsStartAt1 = true,
            pathFormat = "path",
            supportsRunInTerminalRequest = false
        }, ct).ConfigureAwait(false);

        // 2. Set Breakpoints
        if (context.Breakpoints.Count > 0 && !string.IsNullOrEmpty(context.SourceFilePath))
        {
            var enabledBps = new List<object>();
            foreach (var b in context.Breakpoints)
            {
                if (b.IsEnabled)
                {
                    enabledBps.Add(new { line = b.LineNumber, condition = b.Condition });
                }
            }

            await client.SendRequestAsync("setBreakpoints", new
            {
                source = new
                {
                    path = context.SourceFilePath,
                    name = Path.GetFileName(context.SourceFilePath)
                },
                breakpoints = enabledBps,
                lines = context.Breakpoints.Where(b => b.IsEnabled).Select(b => b.LineNumber).ToList()
            }, ct).ConfigureAwait(false);
        }

        // 3. Configuration Done
        try
        {
            await client.SendRequestAsync("configurationDone", null, ct).ConfigureAwait(false);
        }
        catch
        {
            // Some adapters auto-configure or don't require configurationDone
        }
    }

    public static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
