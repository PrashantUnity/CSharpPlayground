using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Interaction;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Go;

/// <summary>
/// Notebook kernel for Go. Each cell is built with the Go toolchain as a small program and run: the functions, types, constants and
/// variables earlier cells declared are written in front of it, the variables of plain data they made are given back their values, and
/// the ones this cell makes (<c>x := ...</c>) are handed on as JSON, with their types, for the next one (see <see cref="GoCellProgramBuilder"/>).
/// The last expression of a cell is shown, errors are on the cell's own lines, and values move to and from other languages as JSON.
/// </summary>
public sealed partial class GoNotebookKernel : INotebookKernel
{
    internal const string ProgramFileName = "main.go";

    // Packages a cell may use without importing them: each is written into the program only when the cell uses it.
    private static readonly string[] DefaultImports =
    [
        "\"fmt\"", "\"strings\"", "\"time\"", "\"os\"", "\"encoding/json\"", "\"math\"", "\"sort\"", "\"strconv\"", "\"errors\"", "\"sync\"", "\"fry\""
    ];

    [GeneratedRegex(@"\bfry\s*\.\s*(?:Input|Password)\b")]
    private static partial Regex AsksForInput();

    [GeneratedRegex(@"undefined: (?<name>[A-Za-z_]\w*)")]
    private static partial Regex UndefinedName();

    [GeneratedRegex(@"no required module provides package (?<package>[^\s;:]+)")]
    private static partial Regex MissingModule();

    private readonly GoToolchainProvider _toolchain;
    private readonly IProcessLauncher _processes;
    private readonly IHostEnvironment _host;
    private readonly string _goRoot;
    private readonly GoNotebookDependencies _dependencies;
    private readonly KernelCreationContext _context;
    private readonly string _moduleDirectory;
    private readonly object _stateLock = new();

    private readonly GoItemStore _items = new();
    private readonly List<string> _imports = [];
    private readonly Dictionary<string, GoVariable> _variables = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _unkept = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _shared = new(StringComparer.Ordinal);
    private int _executionCount;
    private bool _isDisposed;

    public GoNotebookKernel(
        GoToolchainProvider toolchain,
        IProcessLauncher processes,
        IHostEnvironment host,
        string goRoot,
        GoNotebookDependencies dependencies,
        KernelCreationContext context)
    {
        _toolchain = toolchain ?? throw new ArgumentNullException(nameof(toolchain));
        _processes = processes ?? throw new ArgumentNullException(nameof(processes));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _goRoot = goRoot ?? throw new ArgumentNullException(nameof(goRoot));
        _dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _moduleDirectory = Path.Combine(goRoot, "notebook", "cells", Guid.NewGuid().ToString("N")[..12]);
    }

    public string LanguageId => LanguageIds.Go;
    public string DisplayName => "Go (gc toolchain)";
    public bool CanForceStop => true;

    public bool IsSessionActive
    {
        get
        {
            lock (_stateLock) return _executionCount > 0 || _variables.Count > 0 || _items.Items.Count > 0;
        }
    }

    private sealed record Build(GoCellProgram Program, string BinaryPath, IReadOnlyList<string> Dropped);

    public async Task<KernelExecutionResult> ExecuteAsync(KernelExecutionRequest request, CancellationToken ct)
    {
        var clock = Stopwatch.StartNew();
        lock (_stateLock) _executionCount++;

        var workingFolder = _context.WorkingDirectory();
        if (string.IsNullOrWhiteSpace(workingFolder) || !Directory.Exists(workingFolder)) workingFolder = _host.HomeDirectory;

        var resolution = await _toolchain.ResolveAsync(new ToolchainQuery(workingFolder, _context.WorkspaceRoot?.Invoke()), ct).ConfigureAwait(false);
        if (!resolution.IsFound || resolution.Toolchain == null)
        {
            var guidance = resolution.Missing ?? GoGuidance.NotInstalled(_host);
            var text = guidance.ToText();
            request.OnConsole?.Invoke(text + "\n");
            return new KernelExecutionResult { Success = false, ErrorMessage = guidance.Summary, ConsoleOutput = text, Elapsed = clock.Elapsed };
        }

        var cell = GoCellSplitter.Split(request.Code);
        try
        {
            var fryFolder = await GoDisplayRuntime.EnsureSharedModuleAsync(_goRoot, ct).ConfigureAwait(false);
            Directory.CreateDirectory(_moduleDirectory);
            WriteModule(fryFolder, resolution.Toolchain.Version);

            var outcome = await BuildAsync(cell, resolution.Toolchain, request, clock, ct).ConfigureAwait(false);
            if (outcome.Failure != null)
            {
                if (!outcome.Failure.WasCancelled && outcome.Failure.ConsoleOutput.Length > 0) request.OnConsole?.Invoke(outcome.Failure.ConsoleOutput + "\n");
                return outcome.Failure;
            }

            return await RunProgramAsync(request, cell, outcome.Build!, workingFolder!, clock, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return Cancelled(clock);
        }
        catch (ProcessStartException ex)
        {
            request.OnConsole?.Invoke(ex.Message + "\n");
            return Failure(ex.Message, ex.Message, clock);
        }
    }

    // The module the cells are built in: fry from the shared folder, and the modules %go get added.
    private void WriteModule(string fryFolder, Version goVersion)
    {
        var module = new StringBuilder();
        module.Append("module cell\n\n").Append($"go {goVersion.Major}.{goVersion.Minor}\n\n");
        module.Append("require (\n\tfry v0.0.0\n");
        foreach (var requirement in _dependencies.Requirements()) module.Append('\t').Append(requirement).Append('\n');
        module.Append(")\n\n").Append($"replace fry => \"{fryFolder.Replace('\\', '/')}\"\n");

        WriteIfChanged(Path.Combine(_moduleDirectory, "go.mod"), module.ToString());
        if (_dependencies.GoSum() is { } sums) WriteIfChanged(Path.Combine(_moduleDirectory, "go.sum"), sums);
    }

    private static void WriteIfChanged(string path, string content)
    {
        if (File.Exists(path) && File.ReadAllText(path) == content) return;
        File.WriteAllText(path, content, new UTF8Encoding(false));
    }

    private Dictionary<string, string?> ToolEnvironment() => new(StringComparer.Ordinal)
    {
        // One build cache for every cell and notebook: the standard library and the fry package are built once.
        ["GOCACHE"] = Path.Combine(_goRoot, "cache"),
        ["GOTOOLCHAIN"] = "local",
        ["GOWORK"] = "off",
        ["GOFLAGS"] = "-mod=mod",
        ["GO111MODULE"] = "on"
    };

    private sealed record BuildOutcome(Build? Build, KernelExecutionResult? Failure);

    private async Task<BuildOutcome> BuildAsync(GoCell cell, ToolchainInfo toolchain, KernelExecutionRequest request, Stopwatch clock, CancellationToken ct)
    {
        var mainPath = Path.Combine(_moduleDirectory, ProgramFileName);
        var binaryPath = Path.Combine(_moduleDirectory, _host.IsWindows ? "cell.exe" : "cell");

        var imports = new List<string>();
        lock (_stateLock)
        {
            foreach (var import in cell.Imports) if (!_imports.Contains(import)) _imports.Add(import);
            imports.AddRange(_imports);
        }

        foreach (var import in DefaultImports) if (!imports.Any(i => i.EndsWith(import, StringComparison.Ordinal))) imports.Add(import);

        var showLast = true;
        var dropped = new HashSet<string>(StringComparer.Ordinal);
        for (var attempt = 0; attempt < 4; attempt++)
        {
            GoItem[] items;
            GoVariable[] variables;
            lock (_stateLock)
            {
                items = [.. _items.Items];
                variables = _variables.Values.Where(v => !dropped.Contains(v.Name)).ToArray();
            }

            var program = GoCellProgramBuilder.Build(cell, items, imports, variables, showLast);
            if (program.Error != null) return new BuildOutcome(null, Failure(program.Error, program.Error, clock));

            await File.WriteAllTextAsync(mainPath, program.Source, new UTF8Encoding(false), ct).ConfigureAwait(false);
            var output = new StringBuilder();
            int exitCode;
            using (var process = _processes.Start(new ProcessStartSpec
            {
                FileName = toolchain.ExecutablePath,
                Arguments = ["build", "-o", binaryPath, "."],
                WorkingDirectory = _moduleDirectory,
                Environment = ToolEnvironment(),
                MergeStandardError = true
            }, text => { lock (output) output.Append(text); }, text => { lock (output) output.Append(text); }))
            {
                // No limit of the kernel's own: how long a cell may take is the studio's ExecutionTimeoutSeconds setting, which arrives as the cancellation token.
                exitCode = await process.WaitForExitOrKillAsync(ct).ConfigureAwait(false);
            }

            string buildText;
            lock (output) buildText = output.ToString();
            if (exitCode == 0)
            {
                lock (_stateLock)
                {
                    foreach (var name in dropped)
                    {
                        _variables.Remove(name);
                        _unkept[name] = "its value couldn't be given back to the cell that follows";
                    }

                    _items.Apply(cell.Items);
                }

                return new BuildOutcome(new Build(program, binaryPath, [.. dropped]), null);
            }

            var parsed = new GoCompilerDiagnosticParser().Parse(buildText, mainPath);
            var errors = parsed.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

            // The cell's last expression couldn't be shown (it has no value: a call that returns nothing): run it as it is.
            if (showLast && program.ShownStatement is { } shown && errors.Any(d => program.CellLineOf(d.Line) is { } line && line >= shown.First && line <= shown.Last))
            {
                showLast = false;
                continue;
            }

            // A variable an earlier cell made that can't be given back (its type isn't one this cell can write): the cell goes on without it.
            var unrestorable = errors.Select(d => program.RestoreLines.GetValueOrDefault(d.Line)).Where(n => n != null && !dropped.Contains(n)).Select(n => n!).Distinct().ToList();
            if (unrestorable.Count > 0)
            {
                dropped.UnionWith(unrestorable);
                continue;
            }

            return new BuildOutcome(null, BuildFailure(program, buildText, parsed, clock));
        }

        return new BuildOutcome(null, Failure("The cell didn't build.", "The cell didn't build.", clock));
    }

    private KernelExecutionResult BuildFailure(GoCellProgram program, string output, DiagnosticParseResult parsed, Stopwatch clock)
    {
        var shown = GoConsoleText.Build(output, program) + UnkeptHint(output) + MissingModuleHint(output);
        var diagnostics = MapToCell(parsed.Diagnostics, program);
        var summary = diagnostics.FirstOrDefault(d => d.Severity == DiagnosticSeverity.Error)?.Message
            ?? shown.Split('\n').FirstOrDefault(l => l.Length > 0)
            ?? "The build failed";
        return new KernelExecutionResult
        {
            Success = false,
            ErrorMessage = summary,
            ConsoleOutput = shown,
            Diagnostics = diagnostics,
            MissingDependency = parsed.MissingDependency,
            Elapsed = clock.Elapsed
        };
    }

    // "undefined: x" about a variable an earlier cell made but the notebook couldn't keep says why it's gone.
    private string UnkeptHint(string output)
    {
        List<(string Name, string Why)> found;
        lock (_stateLock)
        {
            found = UndefinedName().Matches(output).Select(m => m.Groups["name"].Value).Distinct().Where(_unkept.ContainsKey).Select(n => (n, _unkept[n])).ToList();
        }

        return string.Concat(found.Select(f => $"\nnote: {f.Name} was made in an earlier cell, but the notebook couldn't keep it: {f.Why}. Make it again in this cell, or share it as data with fry.Share(\"{f.Name}\", {f.Name})."));
    }

    private static string MissingModuleHint(string output) =>
        MissingModule().Match(output) is { Success: true } m ? $"\nnote: add the module with %go get {m.Groups["package"].Value}" : string.Empty;

    // Puts a message on the line of the cell it is about; one from the scaffolding or an earlier cell's code has no line here.
    private static IReadOnlyList<DiagnosticItem> MapToCell(IReadOnlyList<DiagnosticItem> diagnostics, GoCellProgram program)
    {
        var mapped = new List<DiagnosticItem>(diagnostics.Count);
        foreach (var d in diagnostics)
        {
            var line = program.CellLineOf(d.Line);
            var endLine = program.CellLineOf(d.EndLine);
            mapped.Add(new DiagnosticItem
            {
                Id = d.Id,
                Severity = d.Severity,
                Message = line == null ? "In code from an earlier cell: " + d.Message : d.Message,
                Line = line ?? 1,
                Column = line == null ? 1 : d.Column,
                EndLine = endLine ?? line ?? 1,
                EndColumn = line == null ? 1 : d.EndColumn
            });
        }

        return mapped;
    }

    private async Task<KernelExecutionResult> RunProgramAsync(KernelExecutionRequest request, GoCell cell, Build build, string workingFolder, Stopwatch clock, CancellationToken ct)
    {
        // What the program printed as it printed it (a panic names the generated file: the diagnostics are read from this), and as the cell is shown it.
        var console = new StringBuilder();
        var shownConsole = new StringBuilder();
        var pendingText = new StringBuilder();
        void Emit(string text)
        {
            var shown = GoConsoleText.Positions(text, build.Program);
            lock (console)
            {
                console.Append(text);
                shownConsole.Append(shown);
            }

            request.OnConsole?.Invoke(shown);
        }

        var started = new TaskCompletionSource<IManagedProcess>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var visuals = new ExternalVisualSession();
        var processor = new ExternalOutputProcessor(
            onConsoleText: text =>
            {
                if (pendingText.Length > 0 || (text.Contains(ProgramFileName, StringComparison.Ordinal) && !text.EndsWith('\n')))
                {
                    pendingText.Append(text);
                    if (text.EndsWith('\n'))
                    {
                        var full = pendingText.ToString();
                        pendingText.Clear();
                        Emit(full);
                    }
                }
                else
                {
                    Emit(text);
                }
            },
            onRichOutput: bundle => request.OnRichOutput?.Invoke(bundle),
            onShare: (name, json) =>
            {
                lock (_stateLock) _shared[name] = json;
            },
            visuals: visuals.Visuals,
            onInputRequest: (prompt, password) => _ = AnswerInputAsync(started.Task, request, prompt, password, ct));

        // The variables the cells before gave, and the ones this cell hands on, go through two files.
        var varsIn = Path.Combine(_moduleDirectory, "variables-in.jsonl");
        var varsOut = Path.Combine(_moduleDirectory, "variables-out.jsonl");
        WriteVariablesIn(varsIn, build.Dropped);
        TryDelete(varsOut);

        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["FRY_VARS_IN"] = varsIn,
            ["FRY_VARS_OUT"] = varsOut,
            ["FRY_INPUT_REQUESTS"] = "1"
        };

        IManagedProcess? running = null;
        try
        {
            running = _processes.Start(visuals.Apply(new ProcessStartSpec
            {
                FileName = build.BinaryPath,
                Arguments = Array.Empty<string>(),
                WorkingDirectory = workingFolder,
                Environment = environment,
                // fmt.Println and println in one cell read in the order they ran.
                MergeStandardError = true
            }), processor.ProcessChunk, processor.ProcessChunk);
            started.SetResult(running);

            // A plain read of os.Stdin can't ask anyone for a line, so a program that does only that sees the end of its input rather than
            // waiting for good; one that calls fry.Input is asked for each line as it comes.
            if (!(request.OnInputRequest != null && AsksForInput().IsMatch(build.Program.Source))) running.CloseInput();

            var exitCode = await running.WaitForExitOrKillAsync(ct).ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();
            processor.Flush();
            if (pendingText.Length > 0)
            {
                Emit(pendingText.ToString());
                pendingText.Clear();
            }

            string consoleText, rawConsole;
            lock (console)
            {
                consoleText = shownConsole.ToString();
                rawConsole = console.ToString();
            }

            if (exitCode == 0)
            {
                var kept = KeepVariables(varsOut, build.Dropped);
                if (kept.Length > 0)
                {
                    request.OnConsole?.Invoke(kept);
                    consoleText += kept;
                }

                return new KernelExecutionResult { Success = true, ConsoleOutput = consoleText, Elapsed = clock.Elapsed };
            }

            var parsed = new GoCompilerDiagnosticParser().Parse(rawConsole, Path.Combine(_moduleDirectory, ProgramFileName));
            var diagnostics = MapToCell(parsed.Diagnostics, build.Program);
            return new KernelExecutionResult
            {
                Success = false,
                ConsoleOutput = GoConsoleText.Positions(rawConsole, build.Program),
                ErrorMessage = diagnostics.FirstOrDefault(d => d.Id == "PANIC")?.Message ?? $"The program exited with code {exitCode}",
                Diagnostics = diagnostics,
                Elapsed = clock.Elapsed
            };
        }
        catch (OperationCanceledException)
        {
            return Cancelled(clock);
        }
        finally
        {
            running?.Dispose();
        }
    }

    // The program asked for a line: the notebook's answer is written to it, and no answer (cancelled, or nobody to ask) ends its input.
    private static async Task AnswerInputAsync(Task<IManagedProcess> started, KernelExecutionRequest request, string prompt, bool password, CancellationToken ct)
    {
        IManagedProcess process;
        try
        {
            process = await started.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return;
        }

        string? answer = null;
        try
        {
            if (request.OnInputRequest is { } ask) answer = await ask(prompt, password, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or InvalidOperationException)
        {
            // Stopped, or the host can't ask: the same as no answer.
        }

        if (answer == null) process.CloseInput();
        else await process.WriteInputAsync(answer + "\n", CancellationToken.None).ConfigureAwait(false);
    }

    private void WriteVariablesIn(string path, IReadOnlyCollection<string> dropped)
    {
        GoVariable[] variables;
        lock (_stateLock) variables = _variables.Values.Where(v => !dropped.Contains(v.Name)).ToArray();
        var lines = variables.Select(v => JsonSerializer.Serialize(new { name = v.Name, json = JsonDocument.Parse(v.Json).RootElement }));
        File.WriteAllText(path, string.Join('\n', lines) + "\n", new UTF8Encoding(false));
    }

    // The cell ran to its end: the variables it handed on replace the ones of the same names from before. Returns a note about any that
    // couldn't be kept and that the cell might have expected to be, or an empty string.
    private string KeepVariables(string varsOut, IReadOnlyCollection<string> dropped)
    {
        var notes = new StringBuilder();
        try
        {
            lock (_stateLock)
            {
                foreach (var line in File.Exists(varsOut) ? File.ReadAllLines(varsOut) : [])
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    using var entry = JsonDocument.Parse(line);
                    var root = entry.RootElement;
                    var name = root.GetProperty("name").GetString() ?? string.Empty;
                    if (root.TryGetProperty("skipped", out var skipped))
                    {
                        var wasKept = _variables.Remove(name);
                        var why = skipped.GetString() ?? "it can't be saved";
                        var quiet = root.TryGetProperty("quiet", out var q) && q.ValueKind == JsonValueKind.True;
                        var isNew = _unkept.TryAdd(name, why);
                        _unkept[name] = why;
                        if (!quiet && (wasKept || isNew)) notes.Append($"note: {name} isn't kept for later cells: {why}.\n");
                    }
                    else
                    {
                        _variables[name] = new GoVariable(name, root.GetProperty("type").GetString() ?? "any", root.GetProperty("json").GetRawText());
                        _unkept.Remove(name);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or KeyNotFoundException)
        {
            // A file that can't be read leaves the variables as they were before this cell.
        }

        return notes.ToString();
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
    }

    private static KernelExecutionResult Failure(string message, string console, Stopwatch clock) =>
        new() { Success = false, ErrorMessage = message, ConsoleOutput = console, Elapsed = clock.Elapsed };

    private static KernelExecutionResult Cancelled(Stopwatch clock) => new()
    {
        WasCancelled = true,
        ErrorMessage = "Cell execution was cancelled.",
        Elapsed = clock.Elapsed
    };

    public Task<IReadOnlyList<NotebookVariableInfo>> GetVariablesAsync(CancellationToken ct)
    {
        var list = new List<NotebookVariableInfo>();
        lock (_stateLock)
        {
            foreach (var variable in _variables.Values)
            {
                var type = variable.Type.Replace("main.", string.Empty, StringComparison.Ordinal);
                list.Add(new NotebookVariableInfo
                {
                    Name = variable.Name,
                    TypeName = type,
                    ValueDisplay = variable.Json.Length > 120 ? variable.Json[..120] + "…" : variable.Json,
                    Kind = "Variable",
                    Kernel = "Go"
                });
            }

            foreach (var item in _items.Items.Where(i => i.Kind != "var" && i.Kind != "const"))
            {
                foreach (var name in item.Names)
                {
                    list.Add(new NotebookVariableInfo
                    {
                        Name = name,
                        TypeName = item.Kind == "type" ? "type" : "func",
                        ValueDisplay = item.Text.Split('\n')[0].TrimEnd('{', ' '),
                        Kind = item.Kind == "type" ? "Type" : "Function",
                        Kernel = "Go"
                    });
                }
            }
        }

        return Task.FromResult<IReadOnlyList<NotebookVariableInfo>>(list);
    }

    public Task<string> GetValueJsonAsync(string name, CancellationToken ct)
    {
        lock (_stateLock)
        {
            if (_variables.TryGetValue(name, out var variable)) return Task.FromResult(variable.Json);
            if (_shared.TryGetValue(name, out var shared)) return Task.FromResult(shared);
        }

        throw new KernelValueException($"Go kernel has no variable named '{name}' that it can hand on. A variable of a type that can't be saved (a func, a struct with lowercase fields) can be shared as data with fry.Share(\"{name}\", {name}).");
    }

    public Task SetValueFromJsonAsync(string name, string json, CancellationToken ct)
    {
        if (!Regex.IsMatch(name, @"^[A-Za-z_]\w*$") || GoKeywords.Contains(name))
        {
            throw new KernelValueException($"'{name}' can't be a Go variable name.");
        }

        try
        {
            using var _ = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            throw new KernelValueException("The value isn't valid JSON.");
        }

        lock (_stateLock)
        {
            _variables[name] = new GoVariable(name, GoValueTypes.Infer(json), json);
            _unkept.Remove(name);
        }

        return Task.CompletedTask;
    }

    private static readonly HashSet<string> GoKeywords = new(StringComparer.Ordinal)
    {
        "break", "default", "func", "interface", "select", "case", "defer", "go", "map", "struct", "chan", "else", "goto", "package",
        "switch", "const", "fallthrough", "if", "range", "type", "continue", "for", "import", "return", "var", "_"
    };

    public void HardReset()
    {
        lock (_stateLock)
        {
            _items.Clear();
            _imports.Clear();
            _variables.Clear();
            _unkept.Clear();
            _shared.Clear();
            _executionCount = 0;
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        HardReset();
        try
        {
            if (Directory.Exists(_moduleDirectory)) Directory.Delete(_moduleDirectory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Left behind for the next clean-up.
        }
    }
}
