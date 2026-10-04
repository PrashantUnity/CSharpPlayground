using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging.Dap;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Python;

public sealed class PythonDebuggerProvider : IDebuggerProvider, IDapAdapterRegistration
{
    private readonly PythonToolchainProvider _toolchain;
    private readonly IProcessLauncher _processes;
    private readonly IHostEnvironment _host;
    private readonly DapAdapterManager _adapterManager;

    public PythonDebuggerProvider(
        PythonToolchainProvider toolchain,
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

    public string LanguageId => LanguageIds.Python;
    public string AdapterName => "debugpy";

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
            // Probe failed or timed out
        }

        var missingGuidance = new MissingToolchainGuidance(
            "debugpy isn't installed in this Python environment",
            "FrySharp uses debugpy for interactive Python debugging with breakpoints, locals and expression evaluation.",
            [
                $"{pythonExe} -m pip install debugpy",
                "Or run in bottom panel terminal: pip install debugpy"
            ],
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
        int port = DapAdapterManager.GetAvailablePort();

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

        return await _adapterManager.LaunchSocketAdapterAsync(
            LanguageIds.Python,
            spec,
            port,
            context,
            postHandshake: async dapClient =>
            {
                var attached = await dapClient.SendRequestAsync("attach", new
                {
                    name = "Python: Attach",
                    type = "python",
                    request = "attach",
                    connect = new { host = "127.0.0.1", port }
                }, ct).ConfigureAwait(false);
                if (!attached.Success) throw new DapException(attached.Message ?? "debugpy refused to attach.", "attach", attached);
            },
            ct,
            DapHandshake.Standard).ConfigureAwait(false);
    }

    Task<IDebugSession> IDapAdapterRegistration.LaunchAsync(DapAdapterManager manager, DebugLaunchContext context, CancellationToken ct) =>
        LaunchAsync(context, ct);
}
