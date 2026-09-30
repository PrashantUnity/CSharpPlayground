using System.Diagnostics;
using System.Text;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Interaction;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;

/// <summary>
/// Polyglot interactive notebook kernel for Go. Compiles and executes Go cell snippets,
/// accumulates imports and top-level functions/structs across cells, supports interactive visual dumps,
/// and enables cross-kernel variable sharing.
/// </summary>
public sealed partial class GoNotebookKernel : INotebookKernel
{
    private readonly GoToolchainProvider _toolchain;
    private readonly IProcessLauncher _processes;
    private readonly IHostEnvironment _host;
    private readonly KernelCreationContext _context;

    private readonly HashSet<string> _cumulativeImports = new(StringComparer.Ordinal)
    {
        "\"fmt\"",
        "\"strings\"",
        "\"time\"",
        "\"os\"",
        "\"encoding/json\"",
        "\"fry\""
    };

    private readonly Dictionary<string, string> _cumulativeTopLevel = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string TypeName, string JsonValue)> _sharedVariables = new(StringComparer.Ordinal);
    private int _executionCount;
    private bool _isDisposed;

    public GoNotebookKernel(
        GoToolchainProvider toolchain,
        IProcessLauncher processes,
        IHostEnvironment host,
        KernelCreationContext context)
    {
        _toolchain = toolchain ?? throw new ArgumentNullException(nameof(toolchain));
        _processes = processes ?? throw new ArgumentNullException(nameof(processes));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public string LanguageId => LanguageIds.Go;
    public string DisplayName => "Go (gc toolchain)";
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
            var guidance = resolution.Missing ?? GoGuidance.NotInstalled(_host);
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
        ExtractImportsAndTopLevel(code, out var imports, out var topLevelItems, out var body);

        foreach (var imp in imports) _cumulativeImports.Add(imp);

        var hash = Math.Abs((workingFolder + "_go_cell_" + _executionCount).GetHashCode(StringComparison.OrdinalIgnoreCase)).ToString("x8");
        var outDir = Path.Combine(Path.GetTempPath(), "FryStudio", "go_cells", hash);
        Directory.CreateDirectory(outDir);

        await GoDisplayRuntime.EnsureDisplayPackageAsync(outDir, ct).ConfigureAwait(false);

        var fullProgram = BuildCellProgram(body, topLevelItems);
        var sourcePath = Path.Combine(outDir, "cell.go");
        await File.WriteAllTextAsync(sourcePath, fullProgram, ct).ConfigureAwait(false);

        var goExecutable = resolution.Toolchain.ExecutablePath;
        var binName = _host.IsWindows ? "cell.exe" : "cell";
        var binPath = Path.Combine(outDir, binName);

        var buildArgs = new List<string> { "build", "-o", binPath, sourcePath };
        var buildEnv = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["GOCACHE"] = Path.Combine(outDir, ".gocache")
        };

        var buildOutput = new StringBuilder();
        int buildExitCode;
        try
        {
            using var buildProcess = _processes.Start(new ProcessStartSpec
            {
                FileName = goExecutable,
                Arguments = buildArgs,
                WorkingDirectory = outDir,
                Environment = buildEnv
            }, outText => buildOutput.Append(outText), errText => buildOutput.Append(errText));

            // No limit of the kernel's own: how long a cell may take is the studio's ExecutionTimeoutSeconds setting, which
            // arrives as the cancellation token.
            buildExitCode = await buildProcess.WaitForExitOrKillAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return Cancelled(clock);
        }

        if (buildExitCode != 0)
        {
            var err = buildOutput.ToString().Trim();
            request.OnConsole?.Invoke(err + "\n");
            return new KernelExecutionResult
            {
                Success = false,
                ErrorMessage = err,
                ConsoleOutput = err,
                Elapsed = clock.Elapsed
            };
        }

        // Commit top-level items on successful compilation
        for (int i = 0; i < topLevelItems.Count; i++)
        {
            _cumulativeTopLevel[$"item_{_executionCount}_{i}"] = topLevelItems[i];
        }

        var spec = new ProcessStartSpec
        {
            FileName = binPath,
            Arguments = Array.Empty<string>(),
            WorkingDirectory = workingFolder
        };

        // The cell's visuals, and the events on them its code listens to while it runs (over the event socket).
        using var visuals = new ExternalVisualSession();
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
            },
            visuals: visuals.Visuals);

        using var managedProcess = _processes.Start(
            visuals.Apply(spec),
            onStandardOutput: text => processor.ProcessChunk(text),
            onStandardError: err => processor.ProcessChunk(err));

        // A cell can't be typed into, so a program that reads its input sees the end of it rather than waiting for good.
        managedProcess.CloseInput();

        try
        {
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
                Kernel = "Go"
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
        throw new KernelValueException($"Go kernel has no variable named '{name}'.");
    }

    public Task SetValueFromJsonAsync(string name, string json, CancellationToken ct)
    {
        var typeName = "interface{}";
        var trimmed = json.Trim();
        if (trimmed == "true" || trimmed == "false") typeName = "bool";
        else if (long.TryParse(trimmed, out _)) typeName = "int64";
        else if (double.TryParse(trimmed, System.Globalization.CultureInfo.InvariantCulture, out _)) typeName = "float64";
        else if (trimmed.StartsWith('"')) typeName = "string";
        else if (trimmed.StartsWith('[')) typeName = "[]interface{}";

        _sharedVariables[name] = (typeName, json);
        return Task.CompletedTask;
    }

    public void HardReset()
    {
        _sharedVariables.Clear();
        _cumulativeTopLevel.Clear();
        _cumulativeImports.Clear();
        _cumulativeImports.Add("\"fmt\"");
        _cumulativeImports.Add("\"strings\"");
        _cumulativeImports.Add("\"time\"");
        _cumulativeImports.Add("\"os\"");
        _cumulativeImports.Add("\"encoding/json\"");
        _cumulativeImports.Add("\"fry\"");
        _executionCount = 0;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        HardReset();
    }
}
