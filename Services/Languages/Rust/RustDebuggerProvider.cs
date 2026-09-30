using System.Text;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging.Dap;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;

/// <summary>
/// Debugs Rust with <c>lldb-dap</c>: builds the file's package in cargo's dev profile (which keeps debug info and turns
/// optimization off), then starts the debug adapter on the program. LLDB is taught to print <c>String</c>, <c>Vec</c>,
/// <c>Option</c> and the other standard types the way Rust's own <c>rust-lldb</c> does, by loading the pretty-printers that
/// ship in the toolchain, and can show the standard library's source when the <c>rust-src</c> component is installed.
/// </summary>
public sealed class RustDebuggerProvider : IDebuggerProvider, IDapAdapterRegistration
{
    private static readonly TimeSpan SysrootTimeout = TimeSpan.FromSeconds(8);

    private readonly string _rustRoot;
    private readonly RustToolchainProvider _toolchain;
    private readonly IProcessLauncher _processes;
    private readonly IHostEnvironment _host;
    private readonly DapAdapterManager _adapterManager;

    public RustDebuggerProvider(
        string rustRoot,
        RustToolchainProvider toolchain,
        IProcessLauncher processes,
        IHostEnvironment host,
        DapAdapterManager? adapterManager = null)
    {
        _rustRoot = rustRoot;
        _toolchain = toolchain ?? throw new ArgumentNullException(nameof(toolchain));
        _processes = processes ?? throw new ArgumentNullException(nameof(processes));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _adapterManager = adapterManager ?? new DapAdapterManager(processes, host);
        _adapterManager.RegisterAdapter(this);
    }

    public string LanguageId => LanguageIds.Rust;
    public string AdapterName => "lldb-dap (LLDB Debug Adapter Protocol)";

    // Only the summary reaches the person (as "Failed to launch debugger: …"), so it carries the fix.
    private MissingToolchainGuidance MissingGuidance()
    {
        var install = _host.IsMacOS ? "brew install llvm (or xcode-select --install)"
            : _host.IsWindows ? "winget install LLVM.LLVM"
            : "sudo apt install lldb";
        return new MissingToolchainGuidance(
            "Rust Debugger (lldb-dap) not found",
            $"Debugging Rust needs lldb-dap. Install it with: {install}.",
            [install],
            "https://lldb.llvm.org");
    }

    public async ValueTask<DebuggerResolution> ResolveDebuggerAsync(ToolchainResolution? toolchain, CancellationToken ct = default) =>
        await LldbDapLocator.ResolveAsync(_host, Array.Empty<string?>(), MissingGuidance(), ct).ConfigureAwait(false);

    public async Task<IDebugSession> LaunchAsync(DebugLaunchContext context, CancellationToken ct = default)
    {
        var docFolder = string.IsNullOrEmpty(context.SourceFilePath) ? null : Path.GetDirectoryName(context.SourceFilePath);
        var resolved = context.Toolchain ?? await _toolchain.ResolveAsync(new ToolchainQuery(docFolder), ct).ConfigureAwait(false);
        if (!resolved.IsFound || resolved.Toolchain == null)
        {
            throw new InvalidOperationException(resolved.Missing?.Summary ?? "The Rust toolchain wasn't found.");
        }

        var toolchain = resolved.Toolchain;

        var scriptFile = context.SourceFilePath;
        if (string.IsNullOrEmpty(scriptFile) || !File.Exists(scriptFile))
        {
            var folder = Path.Combine(Path.GetTempPath(), "FryStudio", "staged_scripts");
            Directory.CreateDirectory(folder);
            scriptFile = Path.Combine(folder, $"script_{Guid.NewGuid():N}.rs");
            await File.WriteAllTextAsync(scriptFile, context.SourceCode, ct).ConfigureAwait(false);
        }

        scriptFile = Path.GetFullPath(scriptFile);
        var workingDir = Path.GetDirectoryName(scriptFile) ?? Directory.GetCurrentDirectory();

        // Debug info and stepping need the dev profile, whatever the file asks for when it runs.
        var asked = RustDirectives.Parse(string.IsNullOrEmpty(context.SourceCode) ? await File.ReadAllTextAsync(scriptFile, ct).ConfigureAwait(false) : context.SourceCode);
        var directives = new RustDirectives { Crates = asked.Crates, Edition = asked.Edition, Release = false };
        var displayCrate = await RustDisplayRuntime.EnsureCrateAsync(_rustRoot, ct).ConfigureAwait(false);
        var stage = RustProjectStager.Prepare(_rustRoot, scriptFile, directives, displayCrate, _host.IsWindows);
        var environment = await RustProcessEnvironment.ForAsync(_host, toolchain, stage.TargetDir, ct).ConfigureAwait(false);

        await BuildAsync(toolchain, stage, scriptFile, workingDir, environment, context, ct).ConfigureAwait(false);

        // Only now that the program is built: is there a debugger to run it under?
        var debugger = await ResolveDebuggerAsync(resolved, ct).ConfigureAwait(false);
        if (!debugger.IsAvailable || string.IsNullOrEmpty(debugger.ExecutablePath))
        {
            throw new InvalidOperationException(debugger.MissingGuidance?.Summary ?? "The Rust debugger (lldb-dap) wasn't found.");
        }

        var launchArguments = await LaunchArgumentsAsync(toolchain, stage, workingDir, ct).ConfigureAwait(false);
        var spec = new ProcessStartSpec
        {
            FileName = debugger.ExecutablePath,
            Arguments = Array.Empty<string>(),
            WorkingDirectory = workingDir,
            Environment = environment
        };

        return await _adapterManager.LaunchStdioAdapterAsync(
            LanguageIds.Rust,
            spec,
            context,
            postHandshake: async dapClient =>
            {
                var launched = await dapClient.SendRequestAsync("launch", launchArguments, ct).ConfigureAwait(false);
                if (!launched.Success) throw new DapException(launched.Message ?? "lldb-dap couldn't start the program.", "launch", launched);
            },
            ct,
            DapHandshake.Standard).ConfigureAwait(false);
    }

    private async Task BuildAsync(
        ToolchainInfo toolchain, RustStage stage, string scriptFile, string workingDir,
        IReadOnlyDictionary<string, string?> environment, DebugLaunchContext context, CancellationToken ct)
    {
        var output = new StringBuilder();
        void Receive(string text)
        {
            lock (output) output.Append(text);
            context.OnLiveOutput?.Invoke(text);
        }

        int exitCode;
        using (var process = _processes.Start(new ProcessStartSpec
        {
            FileName = toolchain.ExecutablePath,
            Arguments = ["build", "--manifest-path", stage.ManifestPath, "--color", "never"],
            WorkingDirectory = workingDir,
            Environment = environment
        }, Receive, Receive))
        {
            using (ct.Register(process.Kill))
            {
                exitCode = await process.Completion.ConfigureAwait(false);
            }
        }

        ct.ThrowIfCancellationRequested();
        if (exitCode == 0) return;

        string text;
        lock (output) text = output.ToString();
        var parsed = new RustDiagnosticParser().Parse(text, scriptFile);
        if (parsed.Diagnostics.Count > 0) throw new DebugCompilationException(parsed.Diagnostics);

        var tail = string.Join('\n', text.Split('\n').Where(l => l.Trim().Length > 0).TakeLast(3));
        throw new InvalidOperationException($"cargo build failed (exit code {exitCode}). {tail}".Trim());
    }

    // What lldb-dap is told about the program: where it is, the pretty-printers to load, and where the standard library's source is.
    private async Task<Dictionary<string, object>> LaunchArgumentsAsync(ToolchainInfo toolchain, RustStage stage, string workingDir, CancellationToken ct)
    {
        var arguments = new Dictionary<string, object>
        {
            ["program"] = stage.BinPath,
            ["cwd"] = workingDir,
            ["stopOnEntry"] = false
        };

        var rustc = toolchain.Get("rustc");
        if (rustc == null) return arguments;

        var sysroot = (await _host.RunAsync(rustc, ["--print", "sysroot"], SysrootTimeout, ct).ConfigureAwait(false)).StandardOutput.Trim();
        if (sysroot.Length == 0) return arguments;

        // The same two things rust-lldb does before it starts lldb.
        var etc = Path.Combine(sysroot, "lib", "rustlib", "etc");
        var lookup = Path.Combine(etc, "lldb_lookup.py");
        var commands = Path.Combine(etc, "lldb_commands");
        if (_host.FileExists(lookup) && _host.FileExists(commands))
        {
            var cmds = new[]
            {
                $"command script import \"{lookup}\"",
                $"command source -s 0 \"{commands}\""
            };
            arguments["initCommands"] = cmds;
            arguments["preRunCommands"] = cmds;
        }

        // std's debug info points at /rustc/<commit>/library/…; rust-src holds those files.
        var standardSource = Path.Combine(sysroot, "lib", "rustlib", "src", "rust");
        if (toolchain.Get("commitHash") is { Length: > 0 } commit && _host.DirectoryExists(standardSource))
        {
            arguments["sourceMap"] = new[] { new[] { $"/rustc/{commit}", standardSource } };
        }

        return arguments;
    }

    Task<IDebugSession> IDapAdapterRegistration.LaunchAsync(DapAdapterManager manager, DebugLaunchContext context, CancellationToken ct) =>
        LaunchAsync(context, ct);
}
