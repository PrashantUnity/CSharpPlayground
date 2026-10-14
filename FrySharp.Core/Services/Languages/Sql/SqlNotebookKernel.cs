using System.Diagnostics;
using System.Text;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
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

        try
        {
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

            var cellCode = CleanCode(request.Code);

            // Prepend table definitions for any shared variables
            var preSql = new StringBuilder();
            foreach (var (varName, (_, jsonVal)) in _sharedVariables)
            {
                var tableSql = GenerateSharedTableSql(varName, jsonVal);
                if (!string.IsNullOrEmpty(tableSql))
                {
                    preSql.AppendLine(tableSql.TrimEnd());
                }
            }
            _sharedVariables.Clear();

            // The cell's own lines come after the tables it was given, and sqlite3 numbers lines from the top of the file:
            // an error is shown on the cell's line, not on one of the generated ones.
            var prefixLines = preSql.ToString().Count(c => c == '\n');
            var cellScriptPath = Path.Combine(_sessionDir, $"cell_{_executionCount}.sql");
            await File.WriteAllTextAsync(cellScriptPath, preSql + cellCode, ct).ConfigureAwait(false);

            var executable = resolution.Toolchain.ExecutablePath;
            // -json: a result set arrives as rows, to be shown as a table; the text a script prints arrives as it is.
            var runArgs = new List<string> { "-json", _dbFilePath, $".read {cellScriptPath}" };

            var stdout = new StringBuilder();
            var stderr = new List<(int Offset, string Text)>();
            var gate = new object();
            var spec = new ProcessStartSpec
            {
                FileName = executable,
                Arguments = runArgs,
                WorkingDirectory = _sessionDir
            };

            using var managedProcess = _processes.Start(
                spec,
                onStandardOutput: text => { lock (gate) stdout.Append(text); },
                onStandardError: text => { lock (gate) stderr.Add((stdout.Length, text)); });

            // No limit of the kernel's own: how long a cell may take is the studio's ExecutionTimeoutSeconds setting, which
            // arrives as the cancellation token.
            var exitCode = await managedProcess.WaitForExitOrKillAsync(ct).ConfigureAwait(false);

            string output;
            List<(int Offset, string Text)> errors;
            lock (gate)
            {
                output = stdout.ToString().Replace("\r\n", "\n");
                errors = stderr.Select(e => (e.Offset, e.Text.Replace("\r\n", "\n"))).ToList();
            }

            var console = Present(SqlResultStream.Split(output), errors, request);
            var parseResult = new SqlCompilerDiagnosticParser().Parse(console, cellScriptPath);
            var diagnostics = parseResult.Diagnostics.Select(d => OnCellLines(d, prefixLines)).ToList();
            var hasErrors = diagnostics.Any(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);

            return new KernelExecutionResult
            {
                Success = exitCode == 0 && !hasErrors,
                ErrorMessage = exitCode != 0 ? (diagnostics.FirstOrDefault(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)?.Message ?? $"SQL execution failed with exit code {exitCode}.") : string.Empty,
                ConsoleOutput = console,
                Diagnostics = diagnostics,
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

    // Shows what the script printed in the order it printed it: its result sets as tables, its text (and sqlite3's errors,
    // which arrive on the other stream) as console text. Returns the text.
    private static string Present(IReadOnlyList<SqlOutputSegment> segments, List<(int Offset, string Text)> errors, KernelExecutionRequest request)
    {
        var console = new StringBuilder();
        var tables = segments.OfType<SqlTableSegment>().ToList();
        var next = 0;

        void Say(string text)
        {
            console.Append(text);
            request.OnConsole?.Invoke(text);
        }

        void SayErrorsBefore(int offset)
        {
            while (next < errors.Count && errors[next].Offset <= offset) Say(errors[next++].Text);
        }

        var tableNumber = 0;
        foreach (var segment in segments)
        {
            SayErrorsBefore(segment.Start);
            switch (segment)
            {
                case SqlTextSegment text:
                    Say(text.Text);
                    break;
                case SqlTableSegment result:
                    tableNumber++;
                    if (tables.Count > 1) result.Table.Title = $"Result {tableNumber}";
                    request.OnRichOutput?.Invoke(new RichCellOutput { Kind = CellOutputKind.Table, TableResult = result.Table });
                    break;
            }
        }

        SayErrorsBefore(int.MaxValue);
        return console.ToString();
    }

    // sqlite3 names a line of the file it was given; the cell's code starts after the lines of the shared tables.
    private static DiagnosticItem OnCellLines(DiagnosticItem item, int prefixLines)
    {
        var line = Math.Max(1, item.Line - prefixLines);
        var endLine = Math.Max(line, item.EndLine - prefixLines);
        return new DiagnosticItem
        {
            Id = item.Id,
            Message = item.Message,
            Severity = item.Severity,
            Line = line,
            Column = item.Column,
            EndLine = endLine,
            EndColumn = item.EndColumn
        };
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
                    ["-json", _dbFilePath, $"SELECT * FROM {Quote(name)};"],
                    TimeSpan.FromSeconds(5),
                    ct).ConfigureAwait(false);

                // A table without rows prints nothing, and is the empty list.
                if (run.ExitCode == 0) return string.IsNullOrWhiteSpace(run.StandardOutput) ? "[]" : run.StandardOutput.Trim();
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
