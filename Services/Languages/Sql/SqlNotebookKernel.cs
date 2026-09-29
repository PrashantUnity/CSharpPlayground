using System.Diagnostics;
using System.Text;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Sql;

/// <summary>
/// Polyglot interactive notebook kernel for SQL. Executes SQL cell queries and DDL against a persistent
/// session database (<c>notebook.db</c>) via <c>sqlite3</c>, outputs formatted tables, and enables cross-kernel
/// variable sharing (injecting shared tables and extracting tables as JSON).
/// </summary>
public sealed partial class SqlNotebookKernel : INotebookKernel
{
    private readonly SqlToolchainProvider _toolchain;
    private readonly IProcessLauncher _processes;
    private readonly IHostEnvironment _host;
    private readonly KernelCreationContext _context;

    private readonly string _sessionDir;
    private readonly string _dbFilePath;
    private readonly Dictionary<string, (string TypeName, string JsonValue)> _sharedVariables = new(StringComparer.OrdinalIgnoreCase);
    private int _executionCount;
    private bool _isDisposed;

    public SqlNotebookKernel(
        SqlToolchainProvider toolchain,
        IProcessLauncher processes,
        IHostEnvironment host,
        KernelCreationContext context)
    {
        _toolchain = toolchain ?? throw new ArgumentNullException(nameof(toolchain));
        _processes = processes ?? throw new ArgumentNullException(nameof(processes));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _context = context ?? throw new ArgumentNullException(nameof(context));

        var hash = Guid.NewGuid().ToString("N")[..8];
        _sessionDir = Path.Combine(Path.GetTempPath(), "FryStudio", "sql_kernel", hash);
        try
        {
            Directory.CreateDirectory(_sessionDir);
        }
        catch { }

        _dbFilePath = Path.Combine(_sessionDir, "notebook.db");
    }

    public string LanguageId => LanguageIds.Sql;
    public string DisplayName => "SQL (SQLite 3)";
    public bool IsSessionActive => _executionCount > 0 || _sharedVariables.Count > 0;
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
            var guidance = resolution.Missing ?? SqlGuidance.NotInstalled(_host);
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

        var cellCode = CleanCode(request.Code, out var shareDirectives);

        // Prepend table definitions for any shared variables
        var preSql = new StringBuilder();
        foreach (var (varName, (_, jsonVal)) in _sharedVariables)
        {
            var tableSql = GenerateSharedTableSql(varName, jsonVal);
            if (!string.IsNullOrEmpty(tableSql))
            {
                preSql.AppendLine(tableSql);
            }
        }
        _sharedVariables.Clear();

        var fullSql = (preSql.ToString() + "\n" + cellCode).Trim();
        var cellScriptPath = Path.Combine(_sessionDir, $"cell_{_executionCount}.sql");
        await File.WriteAllTextAsync(cellScriptPath, fullSql, ct).ConfigureAwait(false);

        var executable = resolution.Toolchain.ExecutablePath;
        var runArgs = new List<string> { "-header", "-table", _dbFilePath, $".read {cellScriptPath}" };

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
            WorkingDirectory = _sessionDir
        };

        var managedProcess = _processes.Start(
            spec,
            onStandardOutput: text => processor.ProcessChunk(text),
            onStandardError: err => processor.ProcessChunk(err));

        try
        {
            var exitCode = await managedProcess.Completion.WaitAsync(TimeSpan.FromSeconds(45), ct).ConfigureAwait(false);
            processor.Flush();

            var fullConsole = consoleBuilder.ToString();
            var parser = new SqlCompilerDiagnosticParser();
            var parseResult = parser.Parse(fullConsole, cellScriptPath);
            var hasErrors = parseResult.Diagnostics.Any(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);

            return new KernelExecutionResult
            {
                Success = exitCode == 0 && !hasErrors,
                ErrorMessage = exitCode != 0 ? (parseResult.Diagnostics.FirstOrDefault(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)?.Message ?? $"SQL execution failed with exit code {exitCode}.") : string.Empty,
                ConsoleOutput = fullConsole,
                Diagnostics = parseResult.Diagnostics,
                Elapsed = clock.Elapsed
            };
        }
        catch (OperationCanceledException)
        {
            managedProcess.Kill();
            return new KernelExecutionResult
            {
                WasCancelled = true,
                ErrorMessage = "Cell execution was cancelled.",
                Elapsed = clock.Elapsed
            };
        }
    }

    public async Task<IReadOnlyList<NotebookVariableInfo>> GetVariablesAsync(CancellationToken ct)
    {
        var list = new List<NotebookVariableInfo>();

        var resolution = await _toolchain.ResolveAsync(new ToolchainQuery(), ct).ConfigureAwait(false);
        if (resolution.IsFound && resolution.Toolchain != null && File.Exists(_dbFilePath))
        {
            try
            {
                var run = await _host.RunAsync(
                    resolution.Toolchain.ExecutablePath,
                    [_dbFilePath, "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';"],
                    TimeSpan.FromSeconds(5),
                    ct).ConfigureAwait(false);

                var tables = run.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var table in tables)
                {
                    list.Add(new NotebookVariableInfo
                    {
                        Name = table,
                        TypeName = "Table",
                        ValueDisplay = $"SQLite Table '{table}'",
                        Kind = "Table",
                        Kernel = "SQL"
                    });
                }
            }
            catch { }
        }

        foreach (var (k, v) in _sharedVariables)
        {
            list.Add(new NotebookVariableInfo
            {
                Name = k,
                TypeName = v.TypeName,
                ValueDisplay = v.JsonValue,
                Kind = "Variable",
                Kernel = "SQL"
            });
        }

        return list;
    }

    public async Task<string> GetValueJsonAsync(string name, CancellationToken ct)
    {
        if (_sharedVariables.TryGetValue(name, out var val))
        {
            return val.JsonValue;
        }

        var resolution = await _toolchain.ResolveAsync(new ToolchainQuery(), ct).ConfigureAwait(false);
        if (resolution.IsFound && resolution.Toolchain != null && File.Exists(_dbFilePath))
        {
            try
            {
                var run = await _host.RunAsync(
                    resolution.Toolchain.ExecutablePath,
                    ["-json", _dbFilePath, $"SELECT * FROM \"{name.Replace("\"", "\"\"")}\";"],
                    TimeSpan.FromSeconds(5),
                    ct).ConfigureAwait(false);

                if (run.ExitCode == 0 && !string.IsNullOrWhiteSpace(run.StandardOutput))
                {
                    return run.StandardOutput.Trim();
                }
            }
            catch { }
        }

        throw new KernelValueException($"SQL kernel has no table or variable named '{name}'.");
    }

    public Task SetValueFromJsonAsync(string name, string json, CancellationToken ct)
    {
        _sharedVariables[name] = ("Table", json);
        return Task.CompletedTask;
    }

    public void HardReset()
    {
        _executionCount = 0;
        _sharedVariables.Clear();
        try
        {
            if (File.Exists(_dbFilePath)) File.Delete(_dbFilePath);
        }
        catch { }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        HardReset();
        try
        {
            if (Directory.Exists(_sessionDir)) Directory.Delete(_sessionDir, recursive: true);
        }
        catch { }
    }
}
