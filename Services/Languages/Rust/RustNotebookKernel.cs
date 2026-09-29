using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;

/// <summary>
/// Notebook kernel for Rust. Each cell is built with Cargo as a small program and run: the functions, types and imports earlier
/// cells defined are part of it, the cell's statements are its <c>main</c>, and the value of its last expression is shown.
/// A program can't keep memory between runs, so what a <c>let</c> made is kept as source: when a cell ends, each variable it made
/// whose type is plain data (numbers, text, lists, options, tuples, maps and sets of those) is written out as its type and an
/// expression that builds it again, and the next cell starts by declaring them. A variable of a type of your own, a closure or a
/// reference isn't kept. <c>fry::share!(x)</c> keeps a value as data for cells in other languages (<c>#!share --from rust x</c>),
/// and <c>#!share</c> brings values from other kernels in as typed variables. Crates come from <c>%cargo add</c> and <c>// #crate:</c> comments and are compiled once
/// for every notebook and script.
/// </summary>
public sealed class RustNotebookKernel : INotebookKernel
{
    // Progress lines cargo prints that say nothing about why a build failed.
    private static readonly Regex CargoProgress = new(@"^\s*(Compiling|Downloading|Downloaded|Updating|Locking|Adding|Blocking|Fresh|Finished|Running|Fetching|Packaging|Checking|Documenting)\b", RegexOptions.Compiled);

    private readonly RustToolchainProvider _toolchain;
    private readonly IProcessLauncher _processes;
    private readonly IHostEnvironment _host;
    private readonly KernelCreationContext _context;
    private readonly string _rustRoot;
    private readonly RustNotebookDependencies _dependencies;
    private readonly SemaphoreSlim _running = new(1, 1);
    private readonly string _sessionId = Guid.NewGuid().ToString("N")[..8];
    private readonly object _stateLock = new();

    private readonly RustItemStore _items = new();
    private readonly Dictionary<string, RustCrate> _crates = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _shared = new(StringComparer.Ordinal);

    // Variables earlier cells made with let and the next cell starts with: name -> (type, source that builds the value).
    private readonly Dictionary<string, (string Type, string Source)> _variables = new(StringComparer.Ordinal);

    // Names a cell made with let whose type can't be kept, so a later "cannot find value" can say why.
    private readonly HashSet<string> _unkept = new(StringComparer.Ordinal);
    private string _edition = RustDirectives.DefaultEdition;
    private bool _release;
    private int _executionCount;
    private bool _isDisposed;

    public RustNotebookKernel(
        RustToolchainProvider toolchain,
        IProcessLauncher processes,
        IHostEnvironment host,
        string rustRoot,
        RustNotebookDependencies dependencies,
        KernelCreationContext context)
    {
        _toolchain = toolchain ?? throw new ArgumentNullException(nameof(toolchain));
        _processes = processes ?? throw new ArgumentNullException(nameof(processes));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _rustRoot = rustRoot ?? throw new ArgumentNullException(nameof(rustRoot));
        _dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public string LanguageId => LanguageIds.Rust;
    public string DisplayName => "Rust (rustc / Cargo)";
    public bool CanForceStop => true;

    public bool IsSessionActive
    {
        get
        {
            lock (_stateLock) return _executionCount > 0 || _shared.Count > 0 || _variables.Count > 0 || _items.Items.Count > 0;
        }
    }

    /// <summary>The file a cell is built from: one per kernel, so Cargo's work on it carries over from cell to cell.</summary>
    private string CellSourcePath => Path.Combine(_rustRoot, "notebook", "cells", _sessionId, "cell.rs");

    public async Task<KernelExecutionResult> ExecuteAsync(KernelExecutionRequest request, CancellationToken ct)
    {
        var clock = Stopwatch.StartNew();
        try
        {
            await _running.WaitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return Cancelled(clock);
        }

        try
        {
            return await RunCellAsync(request, clock, ct).ConfigureAwait(false);
        }
        finally
        {
            _running.Release();
        }
    }

    private async Task<KernelExecutionResult> RunCellAsync(KernelExecutionRequest request, Stopwatch clock, CancellationToken ct)
    {
        lock (_stateLock) _executionCount++;

        var workingFolder = _context.WorkingDirectory();
        if (string.IsNullOrWhiteSpace(workingFolder) || !Directory.Exists(workingFolder)) workingFolder = _host.HomeDirectory;

        var resolution = await _toolchain.ResolveAsync(new ToolchainQuery(workingFolder, _context.WorkspaceRoot?.Invoke()), ct).ConfigureAwait(false);
        if (!resolution.IsFound || resolution.Toolchain == null)
        {
            var guidance = resolution.Missing ?? RustGuidance.NotInstalled(_host);
            var text = guidance.ToText();
            request.OnConsole?.Invoke(text + "\n");
            return Failure(guidance.Summary, text, clock);
        }

        // A macro call at the top of a cell might make items (an impl for each of a list of types, a static) that later cells need,
        // or might be code to run. It is tried as items first, and as a statement if the cell doesn't build that way.
        var cell = RustCellSplitter.Split(request.Code, MacroPlacement.ItemsWhenUnknown);
        var built = await BuildAsync(request, cell, resolution.Toolchain, workingFolder, clock, ct).ConfigureAwait(false);
        if (built.Failure is { WasCancelled: false } && cell.HasUncertainMacros)
        {
            built = await BuildAsync(request, RustCellSplitter.Split(request.Code, MacroPlacement.Statements), resolution.Toolchain, workingFolder, clock, ct).ConfigureAwait(false);
        }

        if (built.Failure != null)
        {
            if (!built.Failure.WasCancelled && built.Failure.ConsoleOutput.Length > 0) request.OnConsole?.Invoke(built.Failure.ConsoleOutput + "\n");
            return built.Failure;
        }

        return await RunProgramAsync(request, built.Build!, clock, ct).ConfigureAwait(false);
    }

    // What a successful build leaves for the run: where the program and its source are, how to start it, and the program's lines.
    private sealed record CellBuild(
        RustCellProgram Program, RustStage Stage, string SourcePath, IReadOnlyDictionary<string, string?> Environment, string WorkingFolder,
        IReadOnlyList<string> Tracked, IReadOnlySet<string> NotKept);

    private sealed record BuildOutcome(KernelExecutionResult? Failure, CellBuild? Build);

    // notKept: variables this cell makes that the build can't hand on (moved away, never set, …): the program is built without them.
    private async Task<BuildOutcome> BuildAsync(
        KernelExecutionRequest request, RustCell cell, ToolchainInfo toolchain, string workingFolder, Stopwatch clock, CancellationToken ct,
        IReadOnlySet<string>? notKept = null)
    {
        notKept ??= new HashSet<string>(StringComparer.Ordinal);
        // 1. What this cell would leave behind if it builds: items, crates, and the settings its comments ask for.
        var directives = RustDirectives.Parse(request.Code);
        RustItemStore candidateItems;
        string[] declarations;
        List<string> inherited;
        Dictionary<string, RustCrate> candidateCrates;
        lock (_stateLock)
        {
            candidateItems = _items.Clone();
            candidateItems.Apply(cell.Items);
            // A value that was shared on purpose comes after the variables, so it keeps the type the JSON gives it.
            declarations = _variables.Select(pair => $"let mut {pair.Key}: {pair.Value.Type} = {pair.Value.Source};")
                .Concat(_shared.Select(pair => RustValueLiterals.Declare(pair.Key, pair.Value)))
                .ToArray();
            inherited = _variables.Keys.ToList();
            candidateCrates = new Dictionary<string, RustCrate>(_crates, StringComparer.Ordinal);
        }

        foreach (var crate in _dependencies.Load()) candidateCrates.TryAdd(crate.Name, crate);
        foreach (var crate in directives.Crates) candidateCrates[crate.Name] = crate;
        var edition = directives.Edition != RustDirectives.DefaultEdition ? directives.Edition : _edition;
        var release = directives.Release || _release;

        // 2. The program: earlier items, this cell's, and a main with its statements.
        var cellItemKeys = cell.Items.Select(i => i.Key).ToHashSet(StringComparer.Ordinal);
        var earlier = candidateItems.Items.Where(i => !cellItemKeys.Contains(i.Key)).ToList();
        var letNames = cell.DefinesMain
            ? []
            : cell.Statements.Where(statement => statement.StartsWithLet).SelectMany(statement => RustLetNames.Of(statement.Text)).Distinct().ToList();

        // Every variable in reach when the cell ends is handed on again: the ones this cell made, and the ones it started with (it may have changed them).
        var tracked = letNames.Concat(inherited).Distinct().ToList();
        var program = RustCellProgramBuilder.Build(cell, earlier, declarations, tracked.Where(name => !notKept.Contains(name)).ToList());
        if (program.Error != null) return new BuildOutcome(Failure(program.Error, program.Error, clock), null);

        var sourcePath = CellSourcePath;
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        await File.WriteAllTextAsync(sourcePath, program.Source, new UTF8Encoding(false), ct).ConfigureAwait(false);

        var displayCrate = await RustDisplayRuntime.EnsureCrateAsync(_rustRoot, ct).ConfigureAwait(false);
        var stageDirectives = new RustDirectives { Crates = candidateCrates.Values.ToList(), Edition = edition, Release = release };
        var stage = RustProjectStager.Prepare(_rustRoot, sourcePath, stageDirectives, displayCrate, _host.IsWindows);
        var environment = await RustProcessEnvironment.ForAsync(_host, toolchain, stage.TargetDir, ct).ConfigureAwait(false);

        // 3. Build
        var buildArgs = new List<string> { "build", "--manifest-path", stage.ManifestPath, "--color", "never" };
        if (release) buildArgs.Add("--release");

        var buildOutput = new StringBuilder();
        var buildLock = new object();
        try
        {
            using var build = _processes.Start(new ProcessStartSpec
            {
                FileName = toolchain.ExecutablePath,
                Arguments = buildArgs,
                WorkingDirectory = workingFolder,
                Environment = environment
            }, text => { lock (buildLock) buildOutput.Append(text); }, text => { lock (buildLock) buildOutput.Append(text); });

            var buildExit = await build.WaitForExitOrKillAsync(ct).ConfigureAwait(false);

            // A process that ended just as the cell was stopped isn't a failed build.
            ct.ThrowIfCancellationRequested();
            if (buildExit != 0)
            {
                string output;
                lock (buildLock) output = buildOutput.ToString();

                // A variable the cell moved away, or never set, can't be kept: that's the notebook's line failing, not the cell's.
                // Build again without it, so the cell runs and only its own errors, if it has any, are reported.
                var rejected = new RustDiagnosticParser().Parse(output, sourcePath).Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error && program.PersistLines.ContainsKey(d.Line))
                    .Select(d => program.PersistLines[d.Line])
                    .Except(notKept)
                    .ToList();
                if (rejected.Count > 0)
                {
                    return await BuildAsync(request, cell, toolchain, workingFolder, clock, ct, new HashSet<string>(notKept.Concat(rejected), StringComparer.Ordinal)).ConfigureAwait(false);
                }

                return new BuildOutcome(BuildFailure(program, sourcePath, output, clock), null);
            }
        }
        catch (OperationCanceledException)
        {
            return new BuildOutcome(Cancelled(clock), null);
        }
        catch (ProcessStartException ex)
        {
            return new BuildOutcome(Failure(ex.Message, ex.Message, clock), null);
        }

        // 4. It builds, so what it defines is kept.
        lock (_stateLock)
        {
            _items.Apply(cell.Items);
            _crates.Clear();
            foreach (var pair in candidateCrates) _crates[pair.Key] = pair.Value;
            _edition = edition;
            _release = release;
        }

        return new BuildOutcome(null, new CellBuild(program, stage, sourcePath, environment, workingFolder, cell.DefinesMain ? [] : tracked, notKept));
    }

    private async Task<KernelExecutionResult> RunProgramAsync(KernelExecutionRequest request, CellBuild build, Stopwatch clock, CancellationToken ct)
    {
        var console = new StringBuilder();
        var sharedNow = new HashSet<string>(StringComparer.Ordinal);
        var processor = new ExternalOutputProcessor(
            onConsoleText: text =>
            {
                lock (console) console.Append(text);
                request.OnConsole?.Invoke(text);
            },
            onRichOutput: bundle => request.OnRichOutput?.Invoke(bundle),
            onShare: (name, json) =>
            {
                try
                {
                    // A name or value the next cell couldn't declare is refused here, where the cell that shared it can say so.
                    _ = RustValueLiterals.Declare(name, json);
                    lock (_stateLock) _shared[name] = json;
                    lock (sharedNow) sharedNow.Add(name);
                }
                catch (KernelValueException ex)
                {
                    var warning = "⚠️ " + ex.Message + "\n";
                    lock (console) console.Append(warning);
                    request.OnConsole?.Invoke(warning);
                }
            });

        // The program says what it made with let by appending to this file as it ends.
        var varsFile = Path.Combine(Path.GetDirectoryName(build.SourcePath)!, "variables.jsonl");
        try
        {
            File.Delete(varsFile);
        }
        catch (IOException)
        {
        }

        var environment = new Dictionary<string, string?>(build.Environment, StringComparer.Ordinal) { ["FRY_VARS_FILE"] = varsFile };

        IManagedProcess? running = null;
        try
        {
            running = _processes.Start(new ProcessStartSpec
            {
                FileName = build.Stage.BinPath,
                Arguments = Array.Empty<string>(),
                WorkingDirectory = build.WorkingFolder,
                Environment = environment
            }, processor.ProcessChunk, processor.ProcessChunk);

            // A cell can't be typed into, so a program that reads its input sees the end of it rather than waiting for good.
            running.CloseInput();

            var exitCode = await running.WaitForExitOrKillAsync(ct).ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();
            processor.Flush();
            string consoleText;
            lock (console) consoleText = console.ToString();

            if (exitCode == 0)
            {
                var notes = KeepVariables(build, varsFile, sharedNow);
                if (notes.Length > 0)
                {
                    request.OnConsole?.Invoke(notes);
                    consoleText += notes;
                }

                return new KernelExecutionResult { Success = true, ConsoleOutput = consoleText, Elapsed = clock.Elapsed };
            }

            var parsed = new RustDiagnosticParser().Parse(consoleText, build.SourcePath);
            return new KernelExecutionResult
            {
                Success = false,
                ConsoleOutput = consoleText,
                ErrorMessage = $"The program exited with code {exitCode}",
                Diagnostics = MapToCell(parsed.Diagnostics, build.Program),
                Elapsed = clock.Elapsed
            };
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
        finally
        {
            running?.Dispose();
        }
    }

    // The cell ran to its end: the variables it made replace the ones of the same names from before. Returns a note about any that
    // couldn't be kept and that the cell might have expected to be (too large, or moved away), or an empty string.
    private string KeepVariables(CellBuild build, string varsFile, IReadOnlySet<string> sharedNow)
    {
        var kept = new Dictionary<string, (string Type, string Source)>(StringComparer.Ordinal);
        var tooLarge = new List<string>();
        try
        {
            foreach (var line in File.Exists(varsFile) ? File.ReadAllLines(varsFile) : [])
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                using var entry = System.Text.Json.JsonDocument.Parse(line);
                var root = entry.RootElement;
                var name = root.GetProperty("name").GetString() ?? string.Empty;
                if (root.TryGetProperty("skipped", out _)) tooLarge.Add(name);
                else kept[name] = (root.GetProperty("type").GetString() ?? string.Empty, root.GetProperty("source").GetString() ?? string.Empty);
            }
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or KeyNotFoundException)
        {
            // A file that can't be read leaves the variables as they were before this cell.
            return string.Empty;
        }

        var notes = new StringBuilder();
        lock (_stateLock)
        {
            foreach (var name in build.Tracked)
            {
                _variables.Remove(name);
                _unkept.Remove(name);

                // A variable made again here is newer than a value shared under its name in an earlier cell (unless this cell shared it).
                if (!sharedNow.Contains(name)) _shared.Remove(name);
            }

            foreach (var (name, value) in kept) _variables[name] = value;
            foreach (var name in build.Tracked.Where(name => !kept.ContainsKey(name))) _unkept.Add(name);
        }

        foreach (var name in tooLarge) notes.Append($"note: {name} is too large to keep for later cells.\n");
        foreach (var name in build.NotKept) notes.Append($"note: {name} isn't kept for later cells: it was moved, or isn't set when the cell ends.\n");
        return notes.ToString();
    }

    // "cannot find value `x`" about a variable an earlier cell made but the notebook couldn't keep says why it's gone.
    private string UnkeptHint(string output)
    {
        List<string> names;
        lock (_stateLock)
        {
            names = Regex.Matches(output, "cannot find value `([A-Za-z_][A-Za-z0-9_]*)`").Select(m => m.Groups[1].Value).Where(_unkept.Contains).Distinct().ToList();
        }

        return string.Concat(names.Select(name =>
            $"\nnote: `{name}` was made with let in an earlier cell, but the notebook couldn't keep it: it was moved away, or its type isn't one of the plain ones kept from cell to cell (numbers, text, lists, options, tuples, maps and sets). Make it again in this cell, or keep it as data with fry::share!({name})."));
    }

    private KernelExecutionResult BuildFailure(RustCellProgram program, string sourcePath, string output, Stopwatch clock)
    {
        var parsed = new RustDiagnosticParser().Parse(output, sourcePath);

        // What the user needs is rustc's own messages, not Cargo's progress.
        var shown = string.Join('\n', output.Replace("\r\n", "\n").Split('\n').Where(line => !CargoProgress.IsMatch(line))).Trim() + UnkeptHint(output);

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

    // Puts a message on the line of the cell it is about; one from the scaffolding or an earlier cell's code has no line here.
    private static IReadOnlyList<DiagnosticItem> MapToCell(IReadOnlyList<DiagnosticItem> diagnostics, RustCellProgram program)
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

    private static KernelExecutionResult Failure(string message, string console, Stopwatch clock) =>
        new() { Success = false, ErrorMessage = message, ConsoleOutput = console, Elapsed = clock.Elapsed };

    private static KernelExecutionResult Cancelled(Stopwatch clock) =>
        new() { WasCancelled = true, ErrorMessage = "Cell execution was cancelled.", Elapsed = clock.Elapsed };

    public Task<IReadOnlyList<NotebookVariableInfo>> GetVariablesAsync(CancellationToken ct)
    {
        var list = new List<NotebookVariableInfo>();
        lock (_stateLock)
        {
            foreach (var (name, json) in _shared)
            {
                list.Add(new NotebookVariableInfo
                {
                    Name = name,
                    TypeName = SafeTypeOf(json),
                    ValueDisplay = json.Length > 200 ? json[..200] + "…" : json,
                    Kind = "Variable",
                    Kernel = "Rust"
                });
            }

            foreach (var (name, value) in _variables)
            {
                list.Add(new NotebookVariableInfo
                {
                    Name = name,
                    TypeName = value.Type,
                    ValueDisplay = value.Source.Length > 200 ? value.Source[..200] + "…" : value.Source,
                    Kind = "Variable",
                    Kernel = "Rust"
                });
            }

            foreach (var item in _items.Items.Where(i => i.Name != null))
            {
                list.Add(new NotebookVariableInfo
                {
                    Name = item.Name!,
                    TypeName = item.Kind,
                    ValueDisplay = Signature(item),
                    Kind = KindLabel(item.Kind),
                    Kernel = "Rust"
                });
            }
        }

        return Task.FromResult<IReadOnlyList<NotebookVariableInfo>>(list);
    }

    // The item's first line without its attributes and docs, or its signature up to the body.
    private static string Signature(RustStoredItem item)
    {
        var lines = item.Text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("//", StringComparison.Ordinal) && !l.StartsWith("#[", StringComparison.Ordinal));
        var first = lines.FirstOrDefault() ?? item.Text.Trim();
        var brace = first.IndexOf('{');
        return (brace > 0 ? first[..brace] : first).Trim();
    }

    private static string KindLabel(string kind) => kind switch
    {
        "fn" => "Function",
        "struct" or "enum" or "union" or "trait" or "type" => "Type",
        "const" or "static" => "Constant",
        _ => "Item"
    };

    private static string SafeTypeOf(string json)
    {
        try
        {
            return RustValueLiterals.TypeOf(json);
        }
        catch (KernelValueException)
        {
            return "fry::Json";
        }
    }

    public Task<string> GetValueJsonAsync(string name, CancellationToken ct)
    {
        lock (_stateLock)
        {
            if (_shared.TryGetValue(name, out var json)) return Task.FromResult(json);
        }

        throw new KernelValueException($"The Rust kernel has no shared value named '{name}'. A Rust variable is shared by calling fry::share!({name}); in a cell that runs before this one.");
    }

    public Task SetValueFromJsonAsync(string name, string json, CancellationToken ct)
    {
        // Checked now, so a value that can't become a Rust variable is reported for the #!share line, not for a later build.
        _ = RustValueLiterals.Declare(name, json);
        lock (_stateLock) _shared[name] = json;
        return Task.CompletedTask;
    }

    public void HardReset()
    {
        lock (_stateLock)
        {
            _items.Clear();
            _crates.Clear();
            _shared.Clear();
            _variables.Clear();
            _unkept.Clear();
            _edition = RustDirectives.DefaultEdition;
            _release = false;
            _executionCount = 0;
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        HardReset();

        // The cell's file and the package built from it are this notebook's; the shared build folder is kept for the next one.
        try
        {
            var cellDir = Path.Combine(_rustRoot, "notebook", "cells", _sessionId);
            if (Directory.Exists(cellDir)) Directory.Delete(cellDir, recursive: true);
            var projectDir = Path.Combine(_rustRoot, "projects", RustProjectStager.StableId(CellSourcePath));
            if (Directory.Exists(projectDir)) Directory.Delete(projectDir, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Left behind, and small.
        }
    }
}
