using System.Diagnostics;
using System.Text;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.FSharp;

/// <summary>
/// Polyglot interactive notebook kernel for F#. Executes F# cell snippets via <c>dotnet fsi</c>,
/// accumulates open statements and top-level definitions across cells, supports interactive visual dumps,
/// and enables cross-kernel variable sharing.
/// </summary>
public sealed partial class FSharpNotebookKernel : INotebookKernel
{
    private readonly FSharpToolchainProvider _toolchain;
    private readonly IProcessLauncher _processes;
    private readonly IHostEnvironment _host;
    private readonly KernelCreationContext _context;

    private readonly HashSet<string> _cumulativeOpens = new(StringComparer.Ordinal)
    {
        "open System",
        "open System.IO",
        "open System.Collections.Generic"
    };

    private readonly HashSet<string> _cumulativeDirectives = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _cumulativeTopLevel = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string TypeName, string JsonValue)> _sharedVariables = new(StringComparer.Ordinal);
    private int _executionCount;
    private bool _isDisposed;

    public FSharpNotebookKernel(
        FSharpToolchainProvider toolchain,
        IProcessLauncher processes,
        IHostEnvironment host,
        KernelCreationContext context)
    {
        _toolchain = toolchain ?? throw new ArgumentNullException(nameof(toolchain));
        _processes = processes ?? throw new ArgumentNullException(nameof(processes));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public string LanguageId => LanguageIds.FSharp;
    public string DisplayName => "F# (dotnet fsi)";
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
            var guidance = resolution.Missing ?? FSharpGuidance.NotInstalled(_host);
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
        ExtractDirectivesAndDeclarations(code, out var directives, out var opens, out var topLevelItems, out var body);

        foreach (var d in directives) _cumulativeDirectives.Add(d);
        foreach (var o in opens) _cumulativeOpens.Add(o);

        var hash = Math.Abs((workingFolder + "_fs_cell_" + _executionCount).GetHashCode(StringComparison.OrdinalIgnoreCase)).ToString("x8");
        var outDir = Path.Combine(Path.GetTempPath(), "FryStudio", "fsharp_cells", hash);
        Directory.CreateDirectory(outDir);

        await FSharpDisplayRuntime.EnsureDisplayPackageAsync(outDir, ct).ConfigureAwait(false);

        var fullProgram = BuildCellProgram(outDir, body, topLevelItems);
        var sourcePath = Path.Combine(outDir, "cell.fsx");
        await File.WriteAllTextAsync(sourcePath, fullProgram, ct).ConfigureAwait(false);

        var executable = resolution.Toolchain.ExecutablePath;
        var isDirectFsi = Path.GetFileNameWithoutExtension(executable).Equals("fsi", StringComparison.OrdinalIgnoreCase);

        var runArgs = isDirectFsi
            ? new List<string> { "--nologo", "--exec", sourcePath }
            : new List<string> { "fsi", "--nologo", "--exec", sourcePath };

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

        var spec = new ProcessStartSpec
        {
            FileName = executable,
            Arguments = runArgs,
            WorkingDirectory = outDir
        };

        using var managedProcess = _processes.Start(
            spec,
            onStandardOutput: text => processor.ProcessChunk(text),
            onStandardError: err => processor.ProcessChunk(err));

        try
        {
            // No limit of the kernel's own: how long a cell may take is the studio's ExecutionTimeoutSeconds setting, which
            // arrives as the cancellation token.
            var exitCode = await managedProcess.WaitForExitOrKillAsync(ct).ConfigureAwait(false);
            processor.Flush();

            if (exitCode == 0)
            {
                for (var i = 0; i < topLevelItems.Count; i++)
                {
                    _cumulativeTopLevel[$"decl_{_executionCount}_{i}"] = topLevelItems[i];
                }
            }

            var fullConsole = consoleBuilder.ToString();
            var parser = new FSharpCompilerDiagnosticParser();
            var parseResult = parser.Parse(fullConsole, sourcePath);
            var hasErrors = parseResult.Diagnostics.Any(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);

            return new KernelExecutionResult
            {
                Success = exitCode == 0 && !hasErrors,
                ErrorMessage = exitCode != 0 ? (parseResult.Diagnostics.FirstOrDefault(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)?.Message ?? $"F# execution failed with exit code {exitCode}.") : string.Empty,
                ConsoleOutput = fullConsole,
                Diagnostics = parseResult.Diagnostics,
                Elapsed = clock.Elapsed
            };
        }
        catch (OperationCanceledException)
        {
            return new KernelExecutionResult
            {
                WasCancelled = true,
                ErrorMessage = "Cell execution was cancelled.",
                Elapsed = clock.Elapsed
            };
        }
    }

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
                Kernel = "F#"
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
        throw new KernelValueException($"F# kernel has no variable named '{name}'.");
    }

    public Task SetValueFromJsonAsync(string name, string json, CancellationToken ct)
    {
        var typeName = "obj";
        var trimmed = json.Trim();
        if (trimmed == "true" || trimmed == "false") typeName = "bool";
        else if (long.TryParse(trimmed, out _)) typeName = "int64";
        else if (double.TryParse(trimmed, System.Globalization.CultureInfo.InvariantCulture, out _)) typeName = "float";
        else if (trimmed.StartsWith('"')) typeName = "string";

        _sharedVariables[name] = (typeName, json);
        return Task.CompletedTask;
    }

    public void HardReset()
    {
        _sharedVariables.Clear();
        _cumulativeTopLevel.Clear();
        _cumulativeDirectives.Clear();
        _cumulativeOpens.Clear();
        _cumulativeOpens.Add("open System");
        _cumulativeOpens.Add("open System.IO");
        _cumulativeOpens.Add("open System.Collections.Generic");
        _executionCount = 0;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        HardReset();
    }
}
