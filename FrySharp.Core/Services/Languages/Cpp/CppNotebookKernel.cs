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

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Cpp;

/// <summary>
/// Notebook kernel for C++. Each cell is built with the C++ compiler as a small program and run: the includes and declarations earlier cells made are
/// written in front of it, the variables of plain data they made are given back their values, and the ones this cell makes are handed on as JSON, with
/// their types, for the next one (see <see cref="CppCellProgramBuilder"/>). The last expression of a cell, when it has no <c>;</c>, is shown, errors are on
/// the cell's own lines, and values move to and from other languages as JSON.
/// </summary>
public sealed partial class CppNotebookKernel : INotebookKernel
{
    [GeneratedRegex(@"\bfry\s*::\s*(?:input|password)\b")]
    private static partial Regex AsksForInput();

    [GeneratedRegex(@"\b(?:error: )?use of undeclared identifier '(?<name>[A-Za-z_]\w*)'|'(?<name2>[A-Za-z_]\w*)' was not declared in this scope|identifier ""(?<name3>[A-Za-z_]\w*)"" is undefined")]
    private static partial Regex UndeclaredName();

    // libstdc++: "terminate called after throwing an instance of 'std::runtime_error'" then "  what():  boom"; libc++: "libc++abi: terminating due to uncaught exception of type std::runtime_error: boom"
    [GeneratedRegex(@"terminate called after throwing an instance of '(?<type>[^']+)'\s*(?:\r?\n\s*what\(\):\s*(?<what>.*))?|terminating due to uncaught exception of type (?<type2>[\w<>]+(?:::[\w<>]+)*)(?::\s*(?<what2>.*))?")]
    private static partial Regex UncaughtException();

    private readonly CppToolchainProvider _toolchain;
    private readonly IProcessLauncher _processes;
    private readonly IHostEnvironment _host;
    private readonly string _cppRoot;
    private readonly KernelCreationContext _context;
    private readonly string _moduleDirectory;
    private readonly object _stateLock = new();

    private readonly CppItemStore _items = new();
    private readonly List<string> _includes = [];
    private readonly Dictionary<string, CppVariable> _variables = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _unkept = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _shared = new(StringComparer.Ordinal);
    private int _executionCount;
    private bool _isDisposed;

    public CppNotebookKernel(
        CppToolchainProvider toolchain,
        IProcessLauncher processes,
        IHostEnvironment host,
        string cppRoot,
        KernelCreationContext context)
    {
        _toolchain = toolchain ?? throw new ArgumentNullException(nameof(toolchain));
        _processes = processes ?? throw new ArgumentNullException(nameof(processes));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _cppRoot = cppRoot ?? throw new ArgumentNullException(nameof(cppRoot));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _moduleDirectory = Path.Combine(cppRoot, "notebook", "cells", Guid.NewGuid().ToString("N")[..12]);
    }

    public string LanguageId => LanguageIds.Cpp;
    public string DisplayName => "C++20 (Clang / GCC / MSVC)";
    public bool CanForceStop => true;

    public bool IsSessionActive
    {
        get
        {
            lock (_stateLock) return _executionCount > 0 || _variables.Count > 0 || _items.Items.Count > 0;
        }
    }

    private sealed record Build(CppCellProgram Program, string BinaryPath, IReadOnlyList<string> Dropped, IReadOnlyDictionary<string, string?> Environment);

    private sealed record BuildOutcome(Build? Build, KernelExecutionResult? Failure);

    public async Task<KernelExecutionResult> ExecuteAsync(KernelExecutionRequest request, CancellationToken ct)
    {
        var clock = Stopwatch.StartNew();
        lock (_stateLock) _executionCount++;

        var workingFolder = _context.WorkingDirectory();
        if (string.IsNullOrWhiteSpace(workingFolder) || !Directory.Exists(workingFolder)) workingFolder = _host.HomeDirectory;

        var resolution = await _toolchain.ResolveAsync(new ToolchainQuery(workingFolder, _context.WorkspaceRoot?.Invoke()), ct).ConfigureAwait(false);
        if (!resolution.IsFound || resolution.Toolchain == null)
        {
            var guidance = resolution.Missing ?? CppGuidance.NotInstalled(_host);
            var text = guidance.ToText();
            request.OnConsole?.Invoke(text + "\n");
            return new KernelExecutionResult { Success = false, ErrorMessage = guidance.Summary, ConsoleOutput = text, Elapsed = clock.Elapsed };
        }

        var cell = CppCellSplitter.Split(request.Code);
        try
        {
            Directory.CreateDirectory(_moduleDirectory);
            var outcome = await BuildAsync(cell, resolution.Toolchain, workingFolder!, request, clock, ct).ConfigureAwait(false);
            if (outcome.Failure != null)
            {
                if (!outcome.Failure.WasCancelled && outcome.Failure.ConsoleOutput.Length > 0) request.OnConsole?.Invoke(outcome.Failure.ConsoleOutput + "\n");
                return outcome.Failure;
            }

            return await RunProgramAsync(request, outcome.Build!, workingFolder!, clock, ct).ConfigureAwait(false);
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

    private async Task<BuildOutcome> BuildAsync(CppCell cell, ToolchainInfo toolchain, string workingFolder, KernelExecutionRequest request, Stopwatch clock, CancellationToken ct)
    {
        var sourcePath = Path.Combine(_moduleDirectory, CppConsoleText.ProgramFileName);
        var isCl = CppCompilerCommand.IsCl(toolchain.ExecutablePath);
        var binaryPath = Path.Combine(_moduleDirectory, _host.IsWindows || isCl ? "cell.exe" : "cell");
        var includeDirectory = await CppDisplayRuntime.EnsureIncludeDirectoryAsync(_cppRoot, ct).ConfigureAwait(false);
        var includeDirectories = new List<string> { workingFolder, includeDirectory };
        if (CppBuildAndRunScriptRunner.FindVcpkgIncludeDirectory(workingFolder, _host) is { } vcpkg) includeDirectories.Add(vcpkg);
        var environment = await CppProcessEnvironment.ForAsync(_host, toolchain, ct).ConfigureAwait(false);

        var includes = new List<string>();
        lock (_stateLock)
        {
            foreach (var include in cell.Includes) if (!_includes.Contains(include)) _includes.Add(include);
            includes.AddRange(_includes);
        }

        var showLast = true;
        var dropped = new HashSet<string>(StringComparer.Ordinal);
        for (var attempt = 0; attempt < 4; attempt++)
        {
            CppItem[] items;
            CppVariable[] variables;
            lock (_stateLock)
            {
                items = [.. _items.Items];
                variables = _variables.Values.Where(v => !dropped.Contains(v.Name)).ToArray();
            }

            var program = CppCellProgramBuilder.Build(cell, items, includes, variables, showLast);
            if (program.Error != null) return new BuildOutcome(null, Failure(program.Error, program.Error, clock));

            await File.WriteAllTextAsync(sourcePath, program.Source, new UTF8Encoding(false), ct).ConfigureAwait(false);
            var output = new StringBuilder();
            int exitCode;
            using (var process = _processes.Start(new ProcessStartSpec
            {
                FileName = toolchain.ExecutablePath,
                Arguments = [.. CppCompilerCommand.Arguments(_host, toolchain, sourcePath, binaryPath, _moduleDirectory, includeDirectories)],
                WorkingDirectory = _moduleDirectory,
                Environment = environment,
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

                return new BuildOutcome(new Build(program, binaryPath, [.. dropped], environment), null);
            }

            var parsed = new ClangGccDiagnosticParser().Parse(buildText, sourcePath);
            var errors = parsed.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

            // The cell's last expression couldn't be shown (it has no value: a call that returns nothing): run it as it is.
            if (showLast && program.ShownGenerated is { } shown && errors.Any(d => d.Line >= shown.First && d.Line <= shown.Last))
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

    private KernelExecutionResult BuildFailure(CppCellProgram program, string output, DiagnosticParseResult parsed, Stopwatch clock)
    {
        var shown = CppConsoleText.Build(output, program) + UnkeptHint(output);
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

    // "use of undeclared identifier 'x'" about a variable an earlier cell made but the notebook couldn't keep says why it's gone.
    private string UnkeptHint(string output)
    {
        List<(string Name, string Why)> found;
        lock (_stateLock)
        {
            found = UndeclaredName().Matches(output)
                .Select(m => m.Groups["name"].Success ? m.Groups["name"].Value : m.Groups["name2"].Success ? m.Groups["name2"].Value : m.Groups["name3"].Value)
                .Distinct().Where(_unkept.ContainsKey).Select(n => (n, _unkept[n])).ToList();
        }

        return string.Concat(found.Select(f => $"\nnote: {f.Name} was made in an earlier cell, but the notebook couldn't keep it: {f.Why}. Make it again in this cell, or share it as data with fry::share(\"{f.Name}\", {f.Name})."));
    }

    // Puts a message on the line of the cell it is about; one from the scaffolding or an earlier cell's code has no line here.
    private static IReadOnlyList<DiagnosticItem> MapToCell(IReadOnlyList<DiagnosticItem> diagnostics, CppCellProgram program)
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

    private async Task<KernelExecutionResult> RunProgramAsync(KernelExecutionRequest request, Build build, string workingFolder, Stopwatch clock, CancellationToken ct)
    {
        var console = new StringBuilder();
        var started = new TaskCompletionSource<IManagedProcess>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var visuals = new ExternalVisualSession();
        var processor = new ExternalOutputProcessor(
            onConsoleText: text =>
            {
                lock (console) console.Append(text);
                request.OnConsole?.Invoke(text);
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

        var environment = new Dictionary<string, string?>(build.Environment, StringComparer.Ordinal)
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
                // std::cout and std::cerr in one cell read in the order they ran.
                MergeStandardError = true
            }), processor.ProcessChunk, processor.ProcessChunk);
            started.SetResult(running);

            // A plain read of std::cin can't ask anyone for a line, so a program that does only that sees the end of its input rather than
            // waiting for good; one that calls fry::input is asked for each line as it comes.
            if (!(request.OnInputRequest != null && AsksForInput().IsMatch(build.Program.Source))) running.CloseInput();

            var exitCode = await running.WaitForExitOrKillAsync(ct).ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();
            processor.Flush();
            string consoleText;
            lock (console) consoleText = console.ToString();

            if (exitCode == 0)
            {
                var kept = KeepVariables(varsOut);
                if (kept.Length > 0)
                {
                    request.OnConsole?.Invoke(kept);
                    consoleText += kept;
                }

                return new KernelExecutionResult { Success = true, ConsoleOutput = consoleText, Elapsed = clock.Elapsed };
            }

            return new KernelExecutionResult
            {
                Success = false,
                ConsoleOutput = consoleText,
                ErrorMessage = FailureMessage(exitCode, consoleText),
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

    // An exception nobody caught, a crash: C++ has no line to give, so the cause is named.
    private static string FailureMessage(int exitCode, string console)
    {
        if (UncaughtException().Match(console) is { Success: true } uncaught)
        {
            var type = uncaught.Groups["type"].Success ? uncaught.Groups["type"].Value : uncaught.Groups["type2"].Value;
            var what = (uncaught.Groups["what"].Success ? uncaught.Groups["what"].Value : uncaught.Groups["what2"].Value).Trim();
            return what.Length > 0 ? $"{type}: {what}" : type;
        }

        var signal = exitCode > 128 ? exitCode - 128 : exitCode < 0 ? -exitCode : 0;
        return signal switch
        {
            11 => "The program crashed (segmentation fault)",
            6 => "The program was aborted",
            8 => "The program crashed (arithmetic exception)",
            9 => "The program was killed",
            _ => $"The program exited with code {exitCode}"
        };
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
        CppVariable[] variables;
        lock (_stateLock) variables = _variables.Values.Where(v => !dropped.Contains(v.Name)).ToArray();
        var lines = variables.Select(v => JsonSerializer.Serialize(new { name = v.Name, json = JsonDocument.Parse(v.Json).RootElement }));
        File.WriteAllText(path, string.Join('\n', lines) + "\n", new UTF8Encoding(false));
    }

    // The cell ran to its end: the variables it handed on replace the ones of the same names from before. Returns a note about any that
    // couldn't be kept and that the cell might have expected to be, or an empty string.
    private string KeepVariables(string varsOut)
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
                        _variables[name] = new CppVariable(name, root.GetProperty("type").GetString() ?? "auto", root.GetProperty("json").GetRawText());
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
                list.Add(new NotebookVariableInfo
                {
                    Name = variable.Name,
                    TypeName = variable.Type,
                    ValueDisplay = variable.Json.Length > 120 ? variable.Json[..120] + "…" : variable.Json,
                    Kind = "Variable",
                    Kernel = "C++"
                });
            }

            foreach (var item in _items.Items.Where(i => i.Kind is "function" or "type" && i.Names.Count > 0))
            {
                foreach (var name in item.Names)
                {
                    list.Add(new NotebookVariableInfo
                    {
                        Name = name,
                        TypeName = item.Kind == "type" ? "type" : "function",
                        ValueDisplay = item.Text.Split('\n')[0].TrimEnd('{', ' '),
                        Kind = item.Kind == "type" ? "Type" : "Function",
                        Kernel = "C++"
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

        throw new KernelValueException($"C++ kernel has no variable named '{name}' that it can hand on. A variable of a type that can't be saved (a pointer, an array, a struct the notebook can't list the members of) can be shared as data with fry::share(\"{name}\", {name}).");
    }

    public Task SetValueFromJsonAsync(string name, string json, CancellationToken ct)
    {
        if (!Regex.IsMatch(name, @"^[A-Za-z_]\w*$") || CppKeywords.Contains(name))
        {
            throw new KernelValueException($"'{name}' can't be a C++ variable name.");
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
            _variables[name] = new CppVariable(name, CppValueTypes.Infer(json), json);
            _unkept.Remove(name);
        }

        return Task.CompletedTask;
    }

    private static readonly HashSet<string> CppKeywords = new(StringComparer.Ordinal)
    {
        "alignas", "alignof", "and", "asm", "auto", "bool", "break", "case", "catch", "char", "class", "const", "constexpr", "continue", "default", "delete", "do",
        "double", "else", "enum", "explicit", "export", "extern", "false", "float", "for", "friend", "goto", "if", "inline", "int", "long", "mutable", "namespace",
        "new", "noexcept", "not", "nullptr", "operator", "or", "private", "protected", "public", "register", "return", "short", "signed", "sizeof", "static",
        "struct", "switch", "template", "this", "throw", "true", "try", "typedef", "typeid", "typename", "union", "unsigned", "using", "virtual", "void", "volatile", "while", "xor"
    };

    public void HardReset()
    {
        lock (_stateLock)
        {
            _items.Clear();
            _includes.Clear();
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
