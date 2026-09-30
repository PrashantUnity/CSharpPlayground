using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Debugging.Dap;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;

/// <summary>
/// Discovers C++ DAP debuggers (<c>lldb-dap</c>, <c>lldb-vscode</c>), compiles C++ source with debug symbols
/// (<c>-g -O0</c>), and orchestrates an interactive DAP debug session supporting breakpoints, stepping, and locals inspection.
/// </summary>
public sealed class CppDebuggerProvider : IDebuggerProvider, IDapAdapterRegistration
{
    private readonly CppToolchainProvider _toolchain;
    private readonly IProcessLauncher _processes;
    private readonly IHostEnvironment _host;
    private readonly DapAdapterManager _adapterManager;

    public CppDebuggerProvider(
        CppToolchainProvider toolchain,
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

    public string LanguageId => LanguageIds.Cpp;
    public string AdapterName => "lldb-dap (LLDB Debug Adapter Protocol)";

    public async ValueTask<DebuggerResolution> ResolveDebuggerAsync(ToolchainResolution? toolchain, CancellationToken ct = default)
    {
        var resolved = toolchain ?? await _toolchain.ResolveAsync(new ToolchainQuery(), ct).ConfigureAwait(false);

        var siblingFolders = new List<string?>();
        if (resolved.IsFound && resolved.Toolchain != null)
        {
            siblingFolders.Add(Path.GetDirectoryName(resolved.Toolchain.ExecutablePath));
        }

        var missing = new MissingToolchainGuidance(
            "C++ Debugger (lldb-dap) not found",
            "C# Code Studio uses lldb-dap for interactive C++ debugging with breakpoints, locals and expression evaluation.",
            [
                _host.IsMacOS ? "brew install llvm" : _host.IsWindows ? "winget install LLVM.LLVM" : "sudo apt install lldb"
            ],
            "https://lldb.llvm.org");

        return await LldbDapLocator.ResolveAsync(_host, siblingFolders, missing, ct).ConfigureAwait(false);
    }

    public async Task<IDebugSession> LaunchAsync(DebugLaunchContext context, CancellationToken ct = default)
    {
        var docFolder = string.IsNullOrEmpty(context.SourceFilePath) ? null : Path.GetDirectoryName(context.SourceFilePath);
        var resolved = context.Toolchain ?? await _toolchain.ResolveAsync(new ToolchainQuery(docFolder), ct).ConfigureAwait(false);
        if (!resolved.IsFound || resolved.Toolchain == null)
        {
            throw new InvalidOperationException(resolved.Missing?.Summary ?? "C++ compiler not found.");
        }

        string scriptFile = context.SourceFilePath;

        if (string.IsNullOrEmpty(scriptFile) || !File.Exists(scriptFile))
        {
            scriptFile = Path.Combine(Path.GetTempPath(), $"script_{Guid.NewGuid():N}.cpp");
            await File.WriteAllTextAsync(scriptFile, context.SourceCode, ct).ConfigureAwait(false);
        }
        scriptFile = Path.GetFullPath(scriptFile);

        var compilerPath = resolved.Toolchain.ExecutablePath;
        var fileName = Path.GetFileName(compilerPath.Replace('\\', '/'));
        var isCl = fileName.Equals("cl.exe", StringComparison.OrdinalIgnoreCase) || fileName.Equals("cl", StringComparison.OrdinalIgnoreCase);

        var hash = Math.Abs(scriptFile.GetHashCode(StringComparison.OrdinalIgnoreCase)).ToString("x8");
        var outDir = Path.Combine(Path.GetTempPath(), "FryStudio", "cpp_debug", hash);
        Directory.CreateDirectory(outDir);

        var fileBaseName = Path.GetFileNameWithoutExtension(scriptFile.Replace('\\', '/'));
        var binName = (_host.IsWindows || isCl) ? $"{fileBaseName}.exe" : fileBaseName;
        var binPath = Path.Combine(outDir, binName);
        var workingDir = Path.GetDirectoryName(scriptFile) ?? Directory.GetCurrentDirectory();

        var includeDir = await CppDisplayRuntime.EnsureIncludeDirectoryAsync(outDir, ct).ConfigureAwait(false);

        // Compile with debug symbols (-g -O0 or /Zi /Od)
        var compileArgs = new List<string>();
        if (isCl)
        {
            compileArgs.Add("/std:c++20");
            compileArgs.Add("/Zi");
            compileArgs.Add("/Od");
            compileArgs.Add("/EHsc");
            compileArgs.Add($"/Fe:{binPath}");
            compileArgs.Add($"/Fo:{outDir}\\");
            compileArgs.Add($"/I{workingDir}");
            compileArgs.Add($"/I{includeDir}");
            compileArgs.Add(scriptFile);
        }
        else
        {
            compileArgs.Add("-std=c++20");
            compileArgs.Add("-g");
            compileArgs.Add("-O0");
            compileArgs.Add("-Wall");
            compileArgs.Add($"-I{workingDir}");
            compileArgs.Add($"-I{includeDir}");
            compileArgs.Add("-o");
            compileArgs.Add(binPath);
            compileArgs.Add(scriptFile);
        }

        var compileResult = await _host.RunAsync(compilerPath, compileArgs, TimeSpan.FromSeconds(30), ct).ConfigureAwait(false);
        if (compileResult.ExitCode != 0)
        {
            var parser = new ClangGccDiagnosticParser();
            var parseResult = parser.Parse(compileResult.StandardOutput + "\n" + compileResult.StandardError, scriptFile);
            throw new DebugCompilationException(parseResult.Diagnostics);
        }

        // Now verify the debugger is available (after compilation succeeds)
        var debugger = await ResolveDebuggerAsync(resolved, ct).ConfigureAwait(false);
        if (!debugger.IsAvailable || string.IsNullOrEmpty(debugger.ExecutablePath))
        {
            throw new InvalidOperationException(debugger.MissingGuidance?.Summary ?? "C++ debugger (lldb-dap) not found.");
        }

        var env = await CppProcessEnvironment.ForAsync(_host, resolved.Toolchain, ct).ConfigureAwait(false);

        var spec = new ProcessStartSpec
        {
            FileName = debugger.ExecutablePath,
            Arguments = Array.Empty<string>(),
            WorkingDirectory = workingDir,
            Environment = env
        };

        return await _adapterManager.LaunchStdioAdapterAsync(
            LanguageIds.Cpp,
            spec,
            context,
            postHandshake: async dapClient =>
            {
                var launched = await dapClient.SendRequestAsync("launch", new
                {
                    program = binPath,
                    cwd = workingDir,
                    stopOnEntry = false
                }, ct).ConfigureAwait(false);
                if (!launched.Success) throw new DapException(launched.Message ?? "lldb-dap couldn't start the program.", "launch", launched);
            },
            ct,
            DapHandshake.Standard).ConfigureAwait(false);
    }

    Task<IDebugSession> IDapAdapterRegistration.LaunchAsync(DapAdapterManager manager, DebugLaunchContext context, CancellationToken ct) =>
        LaunchAsync(context, ct);
}
