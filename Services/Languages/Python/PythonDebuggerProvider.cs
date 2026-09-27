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

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Python;

public sealed class PythonDebuggerProvider : IDebuggerProvider
{
    private readonly PythonToolchainProvider _toolchain;
    private readonly IProcessLauncher _processes;
    private readonly IHostEnvironment _host;

    public PythonDebuggerProvider(PythonToolchainProvider toolchain, IProcessLauncher processes, IHostEnvironment host)
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
                DebuggerName: "debugpy (Python)",
                ExecutablePath: null,
                Version: null,
                MissingGuidance: resolved.Missing);
        }

        var pythonExe = resolved.Toolchain.ExecutablePath;

        // Probe for debugpy
        try
        {
            var probeSpec = new ProcessStartSpec
            {
                FileName = pythonExe,
                Arguments = ["-m", "debugpy", "--version"]
            };

            string versionOutput = string.Empty;
            using var process = _processes.Start(
                probeSpec,
                onStandardOutput: text => versionOutput += text,
                onStandardError: _ => { });

            int exitCode = await process.Completion.WaitAsync(TimeSpan.FromSeconds(3), ct).ConfigureAwait(false);
            if (exitCode == 0 && !string.IsNullOrWhiteSpace(versionOutput))
            {
                return new DebuggerResolution(
                    IsAvailable: true,
                    DebuggerName: "debugpy",
                    ExecutablePath: pythonExe,
                    Version: versionOutput.Trim(),
                    MissingGuidance: null);
            }
        }
        catch
        {
            // debugpy not installed or probe failed
        }

        var missingGuidance = new MissingToolchainGuidance(
            "debugpy isn't installed",
            "debugpy is required for Python interactive debugging.",
            [$"{pythonExe} -m pip install debugpy"],
            "https://github.com/microsoft/debugpy");

        return new DebuggerResolution(
            IsAvailable: false,
            DebuggerName: "debugpy",
            ExecutablePath: pythonExe,
            Version: null,
            MissingGuidance: missingGuidance);
    }

    public async Task<IDebugSession> LaunchAsync(DebugLaunchContext context, CancellationToken ct = default)
    {
        var docFolder = string.IsNullOrEmpty(context.SourceFilePath) ? null : Path.GetDirectoryName(context.SourceFilePath);
        var resolved = context.Toolchain ?? await _toolchain.ResolveAsync(new ToolchainQuery(docFolder), ct).ConfigureAwait(false);
        if (!resolved.IsFound || resolved.Toolchain == null)
        {
            throw new InvalidOperationException(resolved.Missing?.Summary ?? "Python runtime not found.");
        }

        var pythonExe = resolved.Toolchain.ExecutablePath;
        int port = GetAvailablePort();

        var workingDir = string.IsNullOrEmpty(context.SourceFilePath)
            ? Directory.GetCurrentDirectory()
            : Path.GetDirectoryName(context.SourceFilePath) ?? Directory.GetCurrentDirectory();

        // Write source code to temporary file if not already on disk
        string scriptFile = context.SourceFilePath;
        if (string.IsNullOrEmpty(scriptFile) || !File.Exists(scriptFile))
        {
            scriptFile = Path.Combine(Path.GetTempPath(), $"script_{Guid.NewGuid():N}.py");
            await File.WriteAllTextAsync(scriptFile, context.SourceCode, ct).ConfigureAwait(false);
        }
        scriptFile = Path.GetFullPath(scriptFile);

        var spec = new ProcessStartSpec
        {
            FileName = pythonExe,
            Arguments = ["-u", "-m", "debugpy", "--listen", $"127.0.0.1:{port}", "--wait-for-client", scriptFile],
            WorkingDirectory = workingDir,
            Environment = new Dictionary<string, string?>
            {
                ["PYTHONUNBUFFERED"] = "1",
                ["PYDEVD_DISABLE_FILE_VALIDATION"] = "1"
            }
        };

        var managedProcess = _processes.Start(
            spec,
            onStandardOutput: text => context.OnLiveOutput?.Invoke(text),
            onStandardError: text => context.OnLiveOutput?.Invoke(text));

        // Wait for debugpy to open port and connect
        TcpClient? tcpClient = null;
        for (int i = 0; i < 20; i++)
        {
            if (managedProcess.HasExited)
            {
                throw new InvalidOperationException("Python debuggee process exited before debugger could attach.");
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
            throw new TimeoutException($"Timed out connecting to debugpy on port {port}.");
        }

        var stream = tcpClient.GetStream();
        var dapClient = new DapClient(stream, stream);
        dapClient.Start();

        var session = new DapDebugSession(LanguageIds.Python, dapClient, managedProcess);

        // DAP Handshake:
        // 1. Initialize
        await dapClient.SendRequestAsync("initialize", new
        {
            clientID = "frysharp",
            clientName = "C# Code Studio",
            adapterID = "debugpy",
            pathFormat = "path",
            linesStartAt1 = true,
            columnsStartAt1 = true
        }, ct).ConfigureAwait(false);

        // 2. Start attach in background (debugpy only completes attach after configurationDone)
        var attachTask = dapClient.SendRequestAsync("attach", new
        {
            name = "Python: Attach",
            type = "python",
            request = "attach",
            connect = new { host = "127.0.0.1", port }
        }, ct);

        // 3. Set breakpoints
        if (context.Breakpoints.Count > 0)
        {
            await session.SetBreakpointsAsync(scriptFile, context.Breakpoints, ct).ConfigureAwait(false);
        }

        // 4. Configuration done
        await dapClient.SendRequestAsync("configurationDone", null, ct).ConfigureAwait(false);

        // 5. Complete attach handshake
        await attachTask.ConfigureAwait(false);

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
