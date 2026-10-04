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

/// <summary>The order the requests that start a debug session are sent in.</summary>
public enum DapHandshake
{
    /// <summary><c>initialize</c>, <c>setBreakpoints</c>, <c>configurationDone</c>, then the provider's <c>launch</c> or <c>attach</c>: what the older providers were written against.</summary>
    Legacy,

    /// <summary>
    /// The order the DAP specification gives: <c>initialize</c>, then <c>launch</c> (not waited for: many adapters answer it only after
    /// <c>configurationDone</c>), then the adapter's <c>initialized</c> event, then <c>setBreakpoints</c> and <c>configurationDone</c>.
    /// <c>lldb-dap</c> needs it: sent <c>configurationDone</c> first, it refuses, and the program never starts.
    /// </summary>
    Standard
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
        CancellationToken ct = default,
        DapHandshake handshake = DapHandshake.Legacy)
    {
        var managedProcess = _processes.Start(
            spec,
            onStandardOutput: text => context.OnLiveOutput?.Invoke(text),
            onStandardError: text => context.OnLiveOutput?.Invoke(text));

        // Ten seconds to listen: a debugger started for the first time (a fresh download, the first run after an update) can be
        // slow, and a debugger that has died is noticed at the next try, so waiting longer costs nothing when all is well.
        TcpClient? tcpClient = null;
        try
        {
            for (int i = 0; i < 100 && tcpClient == null; i++)
            {
                if (managedProcess.HasExited)
                {
                    throw new InvalidOperationException($"The {languageId} debug adapter exited (code {managedProcess.Completion.Result}) before the debugger could connect to it. Anything it printed is in the Output.");
                }

                var attempt = new TcpClient();
                try
                {
                    await attempt.ConnectAsync(IPAddress.Loopback, port, ct).ConfigureAwait(false);
                    tcpClient = attempt;
                }
                catch (SocketException)
                {
                    attempt.Dispose();
                    await Task.WhenAny(managedProcess.Completion, Task.Delay(100, ct)).ConfigureAwait(false);
                }
                catch
                {
                    attempt.Dispose();
                    throw;
                }
            }

            if (tcpClient == null)
            {
                if (managedProcess.HasExited)
                {
                    throw new InvalidOperationException($"The {languageId} debug adapter exited (code {managedProcess.Completion.Result}) before the debugger could connect to it. Anything it printed is in the Output.");
                }
                throw new TimeoutException($"Timed out connecting to {languageId} DAP adapter on port {port}.");
            }
        }
        catch
        {
            // Stopped while waiting, or nothing to connect to: the adapter must not be left running.
            managedProcess.Kill();
            throw;
        }

        var stream = tcpClient.GetStream();
        return await StartSessionAsync(languageId, new DapClient(stream, stream), managedProcess, context, postHandshake, handshake, ct).ConfigureAwait(false);
    }

    public async Task<DapDebugSession> LaunchStdioAdapterAsync(
        string languageId,
        ProcessStartSpec spec,
        DebugLaunchContext context,
        Func<DapClient, Task>? postHandshake = null,
        CancellationToken ct = default,
        DapHandshake handshake = DapHandshake.Legacy)
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

        // The launcher reports the exit only once the adapter's output is all delivered. Ending the stream then lets the client
        // see the adapter is gone: a request still waiting for its answer fails, and the session ends instead of waiting for good.
        _ = managedProcess.Completion.ContinueWith(_ =>
        {
            try { serverOut.Dispose(); } catch (Exception) { }
        }, TaskScheduler.Default);

        return await StartSessionAsync(languageId, new DapClient(clientIn, clientOut), managedProcess, context, postHandshake, handshake, ct).ConfigureAwait(false);
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
        return await StartSessionAsync(languageId, new DapClient(inputStream, outputStream), process, context, postHandshake, DapHandshake.Legacy, ct).ConfigureAwait(false);
    }

    // The session is made before anything is sent, so it hears every event from the first: a breakpoint hit right after
    // configurationDone would otherwise be lost, and the debugger would never pause. A launch that fails takes the adapter down with it.
    internal static async Task<DapDebugSession> StartSessionAsync(
        string languageId, DapClient client, IManagedProcess? process, DebugLaunchContext context,
        Func<DapClient, Task>? launch, DapHandshake handshake, CancellationToken ct, TimeSpan? initializeTimeout = null)
    {
        var session = new DapDebugSession(languageId, client, process);
        client.Start();

        // An adapter that dies while the session starts (its port was taken, a library is missing) would leave the request it
        // was sent unanswered for good; stop waiting for it and say what happened.
        using var starting = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (process != null)
        {
            _ = process.Completion.ContinueWith(_ =>
            {
                try { starting.Cancel(); } catch (ObjectDisposedException) { }
            }, TaskScheduler.Default);
        }

        try
        {
            await PerformHandshakeAsync(client, context, launch, handshake, initializedTimeout: null, starting.Token, initializeTimeout).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw new IOException(process is { HasExited: true }
                ? $"The {languageId} debug adapter exited before the session started (exit code {process.Completion.Result}). Anything it printed is in the Output."
                : $"The {languageId} debug adapter closed the connection before the session started.");
        }
        catch
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return session;
    }

    private static readonly TimeSpan DefaultInitializedTimeout = TimeSpan.FromSeconds(15);

    // A program that never speaks the Debug Adapter Protocol on the connection (a debugger started in a mode that isn't DAP,
    // a port that belongs to something else) would otherwise leave the session starting for good.
    private static readonly TimeSpan DefaultInitializeTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Starts a session: <paramref name="launch"/> is the provider's <c>launch</c> or <c>attach</c> request.</summary>
    /// <param name="initializedTimeout">How long <see cref="DapHandshake.Standard"/> waits for the adapter's <c>initialized</c> event before carrying on without it.</param>
    /// <param name="initializeTimeout">How long the adapter has to answer <c>initialize</c>; if it doesn't, the start fails with a <see cref="TimeoutException"/>.</param>
    internal static async Task PerformHandshakeAsync(
        DapClient client, DebugLaunchContext context, Func<DapClient, Task>? launch, DapHandshake handshake,
        TimeSpan? initializedTimeout, CancellationToken ct, TimeSpan? initializeTimeout = null)
    {
        var initializeLimit = initializeTimeout ?? DefaultInitializeTimeout;
        if (handshake == DapHandshake.Legacy)
        {
            await InitializeAsync(client, context, initializeLimit, ct).ConfigureAwait(false);
            await SetBreakpointsAsync(client, context, ct).ConfigureAwait(false);
            try
            {
                await client.SendRequestAsync("configurationDone", null, ct).ConfigureAwait(false);
            }
            catch
            {
                // Some adapters auto-configure or don't require configurationDone
            }

            if (launch != null) await launch(client).ConfigureAwait(false);
            return;
        }

        // Standard: the adapter may say "initialized" before or after it answers launch, so listen from before initialize.
        var initialized = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task OnEvent(DapEvent evt)
        {
            if (string.Equals(evt.Event, "initialized", StringComparison.OrdinalIgnoreCase)) initialized.TrySetResult();
            return Task.CompletedTask;
        }

        client.EventReceived += OnEvent;
        try
        {
            await InitializeAsync(client, context, initializeLimit, ct).ConfigureAwait(false);

            // Started, not waited for: lldb-dap and debugpy answer it only once configurationDone has been sent.
            var launching = launch?.Invoke(client);
            if (launching != null && await Task.WhenAny(initialized.Task, launching).ConfigureAwait(false) == launching)
            {
                await launching.ConfigureAwait(false); // it answered first, or failed
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            await Task.WhenAny(initialized.Task, Task.Delay(initializedTimeout ?? DefaultInitializedTimeout, timeoutCts.Token)).ConfigureAwait(false);
            timeoutCts.Cancel();

            await SetBreakpointsAsync(client, context, ct).ConfigureAwait(false);

            var done = await client.SendRequestAsync("configurationDone", null, ct).ConfigureAwait(false);
            if (!done.Success) throw new DapException(done.Message ?? "The debug adapter refused configurationDone.", "configurationDone", done);

            if (launching != null) await launching.ConfigureAwait(false);
        }
        finally
        {
            client.EventReceived -= OnEvent;
        }
    }

    private static async Task InitializeAsync(DapClient client, DebugLaunchContext context, TimeSpan timeout, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        DapResponse response;
        try
        {
            response = await client.SendRequestAsync("initialize", new
            {
                clientID = "FrySharp",
                clientName = "FrySharp",
                adapterID = context.ScriptId,
                linesStartAt1 = true,
                columnsStartAt1 = true,
                pathFormat = "path",
                supportsRunInTerminalRequest = false
            }, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            throw new TimeoutException($"The debug adapter didn't answer \"initialize\" within {timeout.TotalSeconds:0} seconds. It may not be speaking the Debug Adapter Protocol on this connection.");
        }

        if (!response.Success) throw new DapException(response.Message ?? "The debug adapter refused to initialize.", "initialize", response);
    }

    private static async Task SetBreakpointsAsync(DapClient client, DebugLaunchContext context, CancellationToken ct)
    {
        if (context.Breakpoints.Count == 0 || string.IsNullOrEmpty(context.SourceFilePath)) return;

        var response = await client.SendRequestAsync("setBreakpoints", DapDebugSession.BreakpointsRequest(context.SourceFilePath, context.Breakpoints), ct).ConfigureAwait(false);
        DapDebugSession.ApplyBreakpointResults(context.Breakpoints, response);

        // Debugging goes on without them, but the user has to be told: a breakpoint that never binds is a program that runs straight through.
        if (!response.Success) context.OnLiveOutput?.Invoke($"⚠️ The debugger could not set the breakpoints: {response.Message}\n");
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
