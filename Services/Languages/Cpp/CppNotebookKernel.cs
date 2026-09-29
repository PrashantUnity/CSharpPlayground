using System.Diagnostics;
using System.Text;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;

/// <summary>
/// Polyglot interactive notebook kernel for C++. Compiles and executes C++ cell snippets,
/// accumulates headers and functions, supports interactive visual dumps, and enables cross-kernel variable sharing.
/// </summary>
public sealed partial class CppNotebookKernel : INotebookKernel
{
    private readonly CppToolchainProvider _toolchain;
    private readonly IProcessLauncher _processes;
    private readonly IHostEnvironment _host;
    private readonly KernelCreationContext _context;

    private readonly HashSet<string> _cumulativeIncludes = new(StringComparer.Ordinal)
    {
        "#include <iostream>",
        "#include <vector>",
        "#include <string>",
        "#include <memory>",
        "#include <algorithm>",
        "#include <fry/display.hpp>"
    };

    private readonly Dictionary<string, string> _cumulativeTopLevel = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string TypeName, string JsonValue)> _sharedVariables = new(StringComparer.Ordinal);
    private int _executionCount;
    private bool _isDisposed;

    public CppNotebookKernel(
        CppToolchainProvider toolchain,
        IProcessLauncher processes,
        IHostEnvironment host,
        KernelCreationContext context)
    {
        _toolchain = toolchain ?? throw new ArgumentNullException(nameof(toolchain));
        _processes = processes ?? throw new ArgumentNullException(nameof(processes));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public string LanguageId => LanguageIds.Cpp;
    public string DisplayName => "C++20 (Clang / GCC / MSVC)";
    public bool IsSessionActive => _executionCount > 0 || _sharedVariables.Count > 0 || _cumulativeTopLevel.Count > 0;
    public bool CanForceStop => true;

    public async Task<KernelExecutionResult> ExecuteAsync(KernelExecutionRequest request, CancellationToken ct)
    {
        var clock = Stopwatch.StartNew();
        _executionCount++;

        var workingFolder = _context.WorkingDirectory();
        if (string.IsNullOrWhiteSpace(workingFolder) || !Directory.Exists(workingFolder))
        {
            workingFolder = _host.HomeDirectory;
        }

        var resolution = await _toolchain.ResolveAsync(new ToolchainQuery(workingFolder, _context.WorkspaceRoot?.Invoke()), ct).ConfigureAwait(false);
        if (!resolution.IsFound || resolution.Toolchain == null)
        {
            var guidance = resolution.Missing ?? CppGuidance.NotInstalled(_host);
            var err = guidance.ToText();
            request.OnConsole?.Invoke(err + "\n");
            return new KernelExecutionResult
            {
                Success = false,
                ErrorMessage = guidance.Summary,
                ConsoleOutput = err,
                Elapsed = clock.Elapsed
            };
        }

        var code = request.Code.Trim();
        ExtractIncludesAndTopLevel(code, out var includes, out var topLevelItems, out var body);

        foreach (var inc in includes) _cumulativeIncludes.Add(inc);

        var hash = Math.Abs((workingFolder + "_cpp_cell_" + _executionCount).GetHashCode(StringComparison.OrdinalIgnoreCase)).ToString("x8");
        var outDir = Path.Combine(Path.GetTempPath(), "FryStudio", "cpp_cells", hash);
        Directory.CreateDirectory(outDir);

        var includeDir = await CppDisplayRuntime.EnsureIncludeDirectoryAsync(outDir, ct).ConfigureAwait(false);

        var fullProgram = BuildCellProgram(body, topLevelItems);
        var sourcePath = Path.Combine(outDir, "cell.cpp");
        await File.WriteAllTextAsync(sourcePath, fullProgram, ct).ConfigureAwait(false);

        var compilerPath = resolution.Toolchain.ExecutablePath;
        var fileName = Path.GetFileName(compilerPath.Replace('\\', '/'));
        var isCl = fileName.Equals("cl.exe", StringComparison.OrdinalIgnoreCase) || fileName.Equals("cl", StringComparison.OrdinalIgnoreCase);

        var binName = (_host.IsWindows || isCl) ? "cell.exe" : "cell";
        var binPath = Path.Combine(outDir, binName);

        var compileArgs = new List<string>();
        if (isCl)
        {
            compileArgs.Add("/std:c++20");
            compileArgs.Add("/EHsc");
            compileArgs.Add("/W4");
            compileArgs.Add($"/Fe:{binPath}");
            compileArgs.Add($"/Fo:{outDir}\\");
            compileArgs.Add($"/I{workingFolder}");
            compileArgs.Add($"/I{includeDir}");
            compileArgs.Add(sourcePath);
        }
        else
        {
            compileArgs.Add("-std=c++20");
            compileArgs.Add("-O2");
            compileArgs.Add("-Wall");
            compileArgs.Add($"-I{workingFolder}");
            compileArgs.Add($"-I{includeDir}");
            compileArgs.Add("-o");
            compileArgs.Add(binPath);
            compileArgs.Add(sourcePath);
        }

        var compileResult = await _host.RunAsync(compilerPath, compileArgs, TimeSpan.FromSeconds(30), ct).ConfigureAwait(false);
        if (ct.IsCancellationRequested) return Cancelled(clock);
        if (compileResult.ExitCode != 0)
        {
            var parser = new ClangGccDiagnosticParser();
            var parseResult = parser.Parse(compileResult.StandardOutput + "\n" + compileResult.StandardError, sourcePath);
            var errMessage = !string.IsNullOrWhiteSpace(compileResult.StandardError) ? compileResult.StandardError : compileResult.StandardOutput;
            request.OnConsole?.Invoke(errMessage);
            return new KernelExecutionResult
            {
                Success = false,
                ErrorMessage = "Compilation failed",
                ConsoleOutput = errMessage,
                Diagnostics = parseResult.Diagnostics,
                Elapsed = clock.Elapsed
            };
        }

        // On successful compilation, register top-level declarations into cumulative state
        foreach (var top in topLevelItems)
        {
            var key = ExtractDeclarationKey(top);
            _cumulativeTopLevel[key] = top;
        }

        var env = await CppProcessEnvironment.ForAsync(_host, resolution.Toolchain, ct).ConfigureAwait(false);
        var spec = new ProcessStartSpec
        {
            FileName = binPath,
            Arguments = Array.Empty<string>(),
            WorkingDirectory = workingFolder,
            Environment = env
        };

        var consoleBuilder = new StringBuilder();
        var processor = new ExternalOutputProcessor(
            onConsoleText: text =>
            {
                consoleBuilder.Append(text);
                request.OnConsole?.Invoke(text);
            },
            onRichOutput: bundle =>
            {
                request.OnRichOutput?.Invoke(bundle);
            });

        using var managedProcess = _processes.Start(
            spec,
            onStandardOutput: text => processor.ProcessChunk(text),
            onStandardError: err => processor.ProcessChunk(err));

        // A cell can't be typed into, so a program that reads its input sees the end of it rather than waiting for good.
        managedProcess.CloseInput();

        try
        {
            // No limit of the kernel's own: how long a cell may take is the studio's ExecutionTimeoutSeconds setting, which
            // arrives as the cancellation token.
            var exitCode = await managedProcess.WaitForExitOrKillAsync(ct).ConfigureAwait(false);
            processor.Flush();

            return new KernelExecutionResult
            {
                Success = exitCode == 0,
                ConsoleOutput = consoleBuilder.ToString(),
                ErrorMessage = exitCode == 0 ? string.Empty : $"Program exited with code {exitCode}",
                Elapsed = clock.Elapsed
            };
        }
        catch (OperationCanceledException)
        {
            return Cancelled(clock);
        }
    }

    private static KernelExecutionResult Cancelled(Stopwatch clock) => new()
    {
        WasCancelled = true,
        ErrorMessage = "Cell execution was cancelled.",
        Elapsed = clock.Elapsed
    };

    public Task<IReadOnlyList<NotebookVariableInfo>> GetVariablesAsync(CancellationToken ct)
    {
        var list = new List<NotebookVariableInfo>();
        foreach (var (k, v) in _sharedVariables)
        {
            list.Add(new NotebookVariableInfo
            {
                Name = k,
                TypeName = v.TypeName,
                ValueDisplay = v.JsonValue,
                Kind = "Variable",
                Kernel = "C++"
            });
        }
        return Task.FromResult<IReadOnlyList<NotebookVariableInfo>>(list);
    }

    public Task<string> GetValueJsonAsync(string name, CancellationToken ct)
    {
        if (_sharedVariables.TryGetValue(name, out var val))
        {
            return Task.FromResult(val.JsonValue);
        }
        throw new KernelValueException($"C++ kernel has no variable named '{name}'.");
    }

    public Task SetValueFromJsonAsync(string name, string json, CancellationToken ct)
    {
        var typeName = "auto";
        var trimmed = json.Trim();
        if (trimmed == "true" || trimmed == "false") typeName = "bool";
        else if (long.TryParse(trimmed, out _)) typeName = "long long";
        else if (double.TryParse(trimmed, System.Globalization.CultureInfo.InvariantCulture, out _)) typeName = "double";
        else if (trimmed.StartsWith('"')) typeName = "std::string";
        else if (trimmed.StartsWith('[')) typeName = "std::vector";

        _sharedVariables[name] = (typeName, json);
        return Task.CompletedTask;
    }

    public void HardReset()
    {
        _sharedVariables.Clear();
        _cumulativeTopLevel.Clear();
        _cumulativeIncludes.Clear();
        _cumulativeIncludes.Add("#include <iostream>");
        _cumulativeIncludes.Add("#include <vector>");
        _cumulativeIncludes.Add("#include <string>");
        _cumulativeIncludes.Add("#include <memory>");
        _cumulativeIncludes.Add("#include <algorithm>");
        _cumulativeIncludes.Add("#include <fry/display.hpp>");
        _executionCount = 0;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        HardReset();
    }
}
