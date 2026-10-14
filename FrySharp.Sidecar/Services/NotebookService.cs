using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using FrySharp.Sidecar.Protocol;
using FrySharp.Sidecar.Sessions;
using Microsoft.Extensions.Logging;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Execution;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Output;
using StreamJsonRpc;

namespace FrySharp.Sidecar.Services;

/// <summary>
/// Manages open polyglot notebooks and cell evaluation pipelines.
/// </summary>
public sealed class NotebookService
{
    private readonly ConcurrentDictionary<string, OpenNotebook> _open = new(StringComparer.Ordinal);
    private readonly LanguageRegistry<ILanguageEngine> _languages;
    private readonly WorkspaceService _workspace;
    private readonly ILogger<NotebookService> _logger;

    public NotebookService(LanguageRegistry<ILanguageEngine> languages, WorkspaceService workspace, ILogger<NotebookService> logger)
    {
        _languages = languages;
        _workspace = workspace;
        _logger = logger;
    }

    public async Task<NotebookContent> OpenAsync(SidecarSession session, string path, CancellationToken ct)
    {
        var full = _workspace.Resolve(path);
        if (!NotebookFileFormat.IsNotebookPath(full)) throw Error(ErrorCodes.NotSupported, $"'{path}' is not a notebook.");
        if (!File.Exists(full)) throw Error(ErrorCodes.NotFound, $"'{path}' doesn't exist.");

        var relative = _workspace.Paths.ToRelative(full);
        if (!_open.TryGetValue(relative, out var notebook))
        {
            var text = await File.ReadAllTextAsync(full, ct).ConfigureAwait(false);
            NotebookDocumentItem document;
            try
            {
                document = NotebookFileFormat.Read(full, text, _languages);
            }
            catch (System.Text.Json.JsonException ex)
            {
                throw Error(ErrorCodes.Invalid, $"'{relative}' is not a valid notebook: {ex.Message}");
            }

            var created = new OpenNotebook(relative, full, document, NotebookFileFormat.KindOf(full),
                DocumentService.DiskVersionOf(new FileInfo(full)), _languages, _workspace.Paths.Root);
            notebook = _open.GetOrAdd(relative, created);
            if (!ReferenceEquals(notebook, created)) created.Dispose();
        }

        lock (notebook.Gate)
        {
            return new NotebookContent(notebook.Path, notebook.Kind == NotebookFileKind.Ipynb ? "ipynb" : "frynb",
                notebook.Document.Title, notebook.Document.Kernel, notebook.DiskVersion,
                notebook.Document.Cells.Select(ToDto).ToList());
        }
    }

    public void Edit(SidecarSession session, NotebookEditParams request)
    {
        var notebook = Get(request.Path);
        lock (notebook.Gate)
        {
            var cells = notebook.Document.Cells;
            foreach (var edit in request.Edits)
            {
                var index = cells.FindIndex(c => c.Id == edit.CellId);
                switch (edit.Op)
                {
                    case "insert":
                        if (index >= 0) throw Error(ErrorCodes.AlreadyExists, $"Cell '{edit.CellId}' already exists.");
                        cells.Insert(Math.Clamp(edit.Index ?? cells.Count, 0, cells.Count), new NotebookCellItem
                        {
                            Id = edit.CellId,
                            Type = edit.Type == "markdown" ? CellType.Markdown : CellType.Code,
                            Language = edit.Language,
                            Source = edit.Source ?? string.Empty,
                        });
                        break;
                    case "delete":
                        if (index >= 0) cells.RemoveAt(index);
                        break;
                    case "move":
                        if (index < 0) throw MissingCell(edit.CellId);
                        var moved = cells[index];
                        cells.RemoveAt(index);
                        cells.Insert(Math.Clamp(edit.Index ?? cells.Count, 0, cells.Count), moved);
                        break;
                    case "source":
                        if (index < 0) throw MissingCell(edit.CellId);
                        cells[index].Source = edit.Source ?? string.Empty;
                        break;
                    case "language":
                        if (index < 0) throw MissingCell(edit.CellId);
                        cells[index].Language = string.IsNullOrEmpty(edit.Language) || edit.Language == notebook.Document.Kernel ? null : edit.Language;
                        break;
                    case "type":
                        if (index < 0) throw MissingCell(edit.CellId);
                        cells[index].Type = edit.Type == "markdown" ? CellType.Markdown : CellType.Code;
                        break;
                    default:
                        throw Error(ErrorCodes.Invalid, $"'{edit.Op}' is not a notebook edit.");
                }
            }
            notebook.Dirty = true;
        }
    }

    /// <summary>The key a notebook is held under: its id when the client holds it, else its path.</summary>
    public static string KeyOf(NotebookRef request) => KeyOf(request.NotebookId, request.Path);

    public static string KeyOf(string? notebookId, string? path) =>
        notebookId is { Length: > 0 } ? notebookId
        : path is { Length: > 0 } ? path
        : throw Error(ErrorCodes.Invalid, "Name the notebook with notebookId or path.");

    public async Task<ExecuteCellResult> ExecuteAsync(SidecarSession session, ExecuteCellParams request, CancellationToken ct)
    {
        var key = KeyOf(request.NotebookId, request.Path);
        var notebook = request.NotebookId is { Length: > 0 }
            ? GetOrCreateHeld(key, request.WorkingDirectory)
            : await GetOrOpenAsync(session, key, ct).ConfigureAwait(false);
        await notebook.ExecutionGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            NotebookCellItem? cell;
            int executionCount;
            lock (notebook.Gate)
            {
                cell = notebook.Document.Cells.FirstOrDefault(c => c.Id == request.CellId);
                if (cell == null)
                {
                    cell = new NotebookCellItem
                    {
                        Id = request.CellId,
                        Type = CellType.Code,
                        Language = request.Language,
                        Source = request.Source,
                    };
                    notebook.Document.Cells.Add(cell);
                }

                if (cell.Type != CellType.Code) throw Error(ErrorCodes.Invalid, "Only code cells run.");
                cell.Source = request.Source;
                if (!string.IsNullOrWhiteSpace(request.Language))
                {
                    cell.Language = request.Language;
                }
                ClearOutputs(cell);
                executionCount = ++notebook.Document.ExecutionCount;
                cell.ExecutionCount = executionCount;
                cell.IsExecuting = true;
                notebook.Dirty = true;
            }

            // Everything this run tells the client is numbered, in the order it was made: the reply can overtake the last
            // of it on the way, and the client waits for LastSeq before it calls the cell finished.
            var sequence = new Sequence(session, notebook.Path, cell.Id, request.ExecutionId);
            sequence.State(running: true, executionCount, success: null, durationMs: null);

            using var execution = new CancellationTokenSource();
            notebook.BeginExecution(execution);
            var clock = Stopwatch.StartNew();
            var outcome = CellOutcome.Failed;

            using var output = new RunOutput(text => sequence.Output(new CellOutput("stream", text)));
            void Write(string text)
            {
                if (notebook.KeepsOutputs) lock (notebook.Gate) cell.OutputText += text;
                output.Append(text);
            }
            void Show(RichCellOutput rich)
            {
                output.Flush();
                if (notebook.KeepsOutputs) lock (notebook.Gate) Keep(cell, rich);
                if (RichOutputMime.ToBundle(rich, out var metadata) is { } bundle)
                {
                    sequence.Output(Display(bundle, metadata, rich.Visual?.DisplayId, update: false));
                    if (rich.Visual is { } visual) Redraw(sequence, visual);
                }
            }

            try
            {
                outcome = await RunCellAsync(notebook, cell, request, sequence, Write, Show, execution.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Write("\n⚠️ Stopped.\n");
            }
            catch (Exception ex)
            {
                try
                {
                    _logger.LogWarning(ex, "Running a cell of {Path} failed.", notebook.Path);
                }
                catch
                {
                    // Logging failures must not crash the cell execution RPC
                }
                Write($"\n❌ {ex.Message}\n");
            }
            finally
            {
                output.Flush();
                notebook.EndExecution();
                lock (notebook.Gate)
                {
                    cell.IsExecuting = false;
                    cell.HasError = !outcome.Success;
                    cell.ExecutionTimeText = $"{clock.Elapsed.TotalMilliseconds:N0} ms";
                }
                sequence.State(running: false, executionCount, outcome.Success, clock.ElapsedMilliseconds);
            }

            // Send what was queued before the reply does.
            await session.DrainAsync().ConfigureAwait(false);
            return new ExecuteCellResult(executionCount, outcome.Success, clock.ElapsedMilliseconds, sequence.Last, outcome.Diagnostics, outcome.Error);
        }
        finally
        {
            notebook.ExecutionGate.Release();
        }
    }

    // A display as the client gets it: the engine's MIME bundle, and the id a program can update it by.
    private static CellOutput Display(System.Text.Json.Nodes.JsonObject bundle, System.Text.Json.Nodes.JsonObject metadata, string? displayId, bool update)
    {
        var data = Newtonsoft.Json.Linq.JObject.Parse(bundle.ToJsonString());
        var meta = metadata.Count > 0 ? Newtonsoft.Json.Linq.JObject.Parse(metadata.ToJsonString()) : null;
        return new CellOutput("display", Data: data, Metadata: meta, DisplayId: displayId, Update: update);
    }

    // A chart a program redraws (handle.update(...)) changes the visual itself: send each new picture as an update of the display.
    private static void Redraw(Sequence sequence, VisualOutput visual)
    {
        void OnChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(VisualOutput.Spec)) return;
            var redrawn = new RichCellOutput { Visual = visual };
            if (RichOutputMime.ToBundle(redrawn, out var metadata) is { } bundle)
            {
                sequence.Output(Display(bundle, metadata, visual.DisplayId, update: true));
            }
        }
        visual.PropertyChanged += OnChanged;
    }

    private async Task<CellOutcome> RunCellAsync(OpenNotebook notebook, NotebookCellItem cell, ExecuteCellParams request, Sequence sequence,
        Action<string> write, Action<RichCellOutput> show, CancellationToken ct)
    {
        var own = cell.Language ?? notebook.Document.Kernel;
        var directives = NotebookCellDirectives.Parse(cell.Source, _languages, own);
        var language = _languages.Get(directives.LanguageId ?? own);
        var kernel = language == null ? null : notebook.Router.GetOrCreate(language.Id);

        if (language == null || kernel == null)
        {
            write(language == null
                ? $"❌ This cell is written in \"{own}\", which cannot run here.\n"
                : $"❌ {language.DisplayName} code cannot run in notebook cells.\n");
            return CellOutcome.Failed;
        }

        if (directives.Errors.Count > 0)
        {
            foreach (var error in directives.Errors) write($"❌ {error}\n");
            return CellOutcome.Failed;
        }

        var query = new ToolchainQuery(Path.GetDirectoryName(notebook.FullPath), _workspace.Paths.Root);
        if (!await notebook.Directives.RunAsync(directives, language, kernel, query, write, ct).ConfigureAwait(false)) return CellOutcome.Failed;

        var kernelRequest = new KernelExecutionRequest
        {
            Code = directives.Code,
            SourceId = cell.Id,
            Label = $"[{cell.ExecutionCount}]",
            OnConsole = write,
            OnRichOutput = show,
            OnInputRequest = (prompt, password, token) => sequence.Session.RequestInputAsync(new InputRequest(prompt, password, notebook.Path, cell.Id, request.ExecutionId), token),
        };

        var execution = kernel.CanForceStop
            ? kernel.ExecuteAsync(kernelRequest, ct)
            : Task.Run(() => kernel.ExecuteAsync(kernelRequest, ct), CancellationToken.None);
        if (!kernel.CanForceStop && !await ExecutionAbandonment.WaitWithGraceAsync(execution, ct).ConfigureAwait(false))
        {
            write("\n⚠️ The cell did not respond to Stop and was abandoned; its kernel keeps running in the background.\n");
            return CellOutcome.Failed;
        }

        var result = await execution.ConfigureAwait(false);
        var diagnostics = new List<DiagnosticDto>();
        if (!result.Success && result.Diagnostics.Count > 0)
        {
            foreach (var diagnostic in result.Diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error))
            {
                write($"{diagnostic.LocationString}: {diagnostic.Id} {diagnostic.Message}\n");
                diagnostics.Add(LanguageFeatures.ToDto(diagnostic));
            }
        }
        else if (!result.Success && !result.WasCancelled && result.ErrorLine is { } line)
        {
            // A traceback in Python or JavaScript names the line: whole line, as far as the editor goes.
            diagnostics.Add(new DiagnosticDto("error", result.ErrorName ?? "Error", result.ErrorMessage, line, 1, line, int.MaxValue));
        }
        return new CellOutcome(result.Success, diagnostics, !result.Success && !result.WasCancelled ? ErrorOf(result) : null);
    }

    // Why a failed cell failed, in a line: the kind of error and its message (the text printed above has all of it).
    private static CellError ErrorOf(KernelExecutionResult result)
    {
        var errors = result.Diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ToList();
        var name = !string.IsNullOrWhiteSpace(result.ErrorName) ? result.ErrorName!.Trim()
            : errors.FirstOrDefault() is { Id.Length: > 0 } first ? first.Id
            : "Error";
        var message = !string.IsNullOrWhiteSpace(result.ErrorMessage)
            ? result.ErrorMessage!.Trim().Split('\n')[0].Trim()
            : errors.FirstOrDefault()?.Message.Split('\n')[0].Trim() ?? "The cell failed.";
        if (message.Length > 300) message = message[..300] + "…";
        if (errors.Count > 1) message += $" (and {errors.Count - 1} more)";
        return new CellError(name, message);
    }

    private sealed record CellOutcome(bool Success, IReadOnlyList<DiagnosticDto> Diagnostics, CellError? Error = null)
    {
        public static CellOutcome Failed { get; } = new(false, []);
    }

    /// <summary>Numbers the notifications of one run, and sends them in that order.</summary>
    private sealed class Sequence(SidecarSession session, string path, string cellId, string? executionId)
    {
        private readonly Lock _gate = new();
        private int _last;

        public SidecarSession Session { get; } = session;

        /// <summary>The number of the last notification made.</summary>
        public int Last
        {
            get
            {
                lock (_gate) return _last;
            }
        }

        public void Output(CellOutput output)
        {
            lock (_gate) Session.Notify(ClientMethods.NotebookOutput, new NotebookOutputNotification(path, cellId, output, executionId, ++_last));
        }

        public void State(bool running, int? executionCount, bool? success, long? durationMs)
        {
            lock (_gate) Session.Notify(ClientMethods.NotebookCellState, new CellStateNotification(path, cellId, running, executionCount, success, durationMs, executionId, ++_last));
        }
    }

    public void Interrupt(string path) => Get(path).CancelExecution();

    public void Restart(string path)
    {
        var notebook = Get(path);
        notebook.CancelExecution();
        notebook.Router.ResetAll();
    }

    public async Task<IReadOnlyList<VariableDto>> VariablesAsync(string path, CancellationToken ct)
    {
        var variables = await Get(path).Router.GetVariablesAsync(ct).ConfigureAwait(false);
        return variables.Select(v => new VariableDto(v.Name, v.TypeName, v.ValueDisplay, v.Kernel)).ToList();
    }

    public async Task<SaveResult> SaveAsync(NotebookSaveParams request, CancellationToken ct)
    {
        var notebook = Get(request.Path);
        string text;
        lock (notebook.Gate)
        {
            if (!notebook.Dirty) return new SaveResult(notebook.DiskVersion);

            var onDisk = File.Exists(notebook.FullPath) ? DocumentService.DiskVersionOf(new FileInfo(notebook.FullPath)) : null;
            if (!request.Force && onDisk != null && onDisk != notebook.DiskVersion)
            {
                throw Error(ErrorCodes.Conflict, $"'{notebook.Path}' changed on disk since it was opened.");
            }

            notebook.Document.LastModified = DateTime.UtcNow;
            text = NotebookFileFormat.Write(notebook.Document, notebook.Kind, _languages);
        }

        await AtomicFile.WriteAllTextAsync(notebook.FullPath, text, new UTF8Encoding(false), ct).ConfigureAwait(false);
        lock (notebook.Gate)
        {
            notebook.DiskVersion = DocumentService.DiskVersionOf(new FileInfo(notebook.FullPath));
            notebook.Dirty = false;
            return new SaveResult(notebook.DiskVersion);
        }
    }

    public Task CloseAsync(string path)
    {
        if (_open.TryRemove(path, out var notebook))
        {
            notebook.Dispose();
        }
        else if (_workspace.Paths.TryResolve(path, out var full))
        {
            var relative = _workspace.Paths.ToRelative(full);
            if (_open.TryRemove(relative, out notebook))
            {
                notebook.Dispose();
            }
        }
        return Task.CompletedTask;
    }

    private async Task<OpenNotebook> GetOrOpenAsync(SidecarSession session, string path, CancellationToken ct)
    {
        if (_open.TryGetValue(path, out var open)) return open;

        var full = _workspace.Resolve(path);
        var relative = _workspace.Paths.ToRelative(full);
        if (_open.TryGetValue(relative, out open)) return open;

        if (File.Exists(full) && NotebookFileFormat.IsNotebookPath(full))
        {
            await OpenAsync(session, relative, ct).ConfigureAwait(false);
            if (_open.TryGetValue(relative, out open)) return open;
        }

        var doc = new NotebookDocumentItem
        {
            Title = Path.GetFileNameWithoutExtension(full),
            Kernel = LanguageIds.CSharp
        };
        var kind = NotebookFileFormat.IsNotebookPath(full)
            ? NotebookFileFormat.KindOf(full)
            : NotebookFileKind.Frynb;

        var created = new OpenNotebook(relative, full, doc, kind, "0", _languages, _workspace.Paths.Root);
        return _open.GetOrAdd(relative, created);
    }

    // A notebook the client holds (VS Code has its cells and its outputs): the server keeps only its kernels, and the folder
    // they run in, which need not be inside the workspace.
    private OpenNotebook GetOrCreateHeld(string id, string? workingDirectory)
    {
        if (_open.TryGetValue(id, out var existing)) return existing;

        var directory = workingDirectory is { Length: > 0 } && Directory.Exists(workingDirectory) ? workingDirectory : _workspace.Paths.Root;
        var created = new OpenNotebook(id, System.IO.Path.Combine(directory, "notebook.frynb"), new NotebookDocumentItem { Title = id, Kernel = LanguageIds.CSharp },
            NotebookFileKind.Frynb, "0", _languages, _workspace.Paths.Root) { KeepsOutputs = false };
        var winner = _open.GetOrAdd(id, created);
        if (!ReferenceEquals(winner, created)) created.Dispose();
        return winner;
    }

    private OpenNotebook Get(string path)
    {
        if (_open.TryGetValue(path, out var notebook)) return notebook;
        if (_workspace.Paths.TryResolve(path, out var full))
        {
            var relative = _workspace.Paths.ToRelative(full);
            if (_open.TryGetValue(relative, out notebook)) return notebook;
        }
        throw Error(ErrorCodes.NotFound, $"'{path}' is not open.");
    }

    private static void Keep(NotebookCellItem cell, RichCellOutput rich)
    {
        if (rich.Visual is { } visual)
        {
            (cell.Visuals ??= []).Add(VisualOutputSnapshot.From(visual));
            if (VisualSvgRenderer.TryRender(visual.Spec, out var html))
            {
                cell.HtmlContent = html;
            }
            return;
        }

        switch (rich.Kind)
        {
            case CellOutputKind.Image when rich.ImageBytes is { Length: > 0 }:
                cell.ImageBytes = rich.ImageBytes;
                cell.ImageFormat = rich.ImageFormat ?? "PNG";
                cell.ImageWidth = rich.ImageWidth;
                cell.ImageHeight = rich.ImageHeight;
                var mime = (rich.ImageFormat ?? "PNG").ToUpperInvariant() switch
                {
                    "JPEG" or "JPG" => "image/jpeg",
                    "GIF" => "image/gif",
                    "SVG" => "image/svg+xml",
                    _ => "image/png"
                };
                var base64 = Convert.ToBase64String(rich.ImageBytes);
                cell.HtmlContent = $$"""
                <div class="fry-img-card" style="margin:8px 0;max-width:840px;">
                  <img src="data:{{mime}};base64,{{base64}}" style="max-width:100%;height:auto;border-radius:6px;border:1px solid #30363d;box-shadow:0 4px 16px rgba(0,0,0,0.35);display:block;" alt="Synthesized Image" />
                </div>
                """;
                break;
            case CellOutputKind.Html:
                cell.HtmlContent = rich.HtmlContent;
                cell.MarkdownContent = rich.MarkdownSource;
                break;
            case CellOutputKind.Table when rich.TableResult is { } table:
                cell.TableSnapshot = table.ToSnapshot();
                cell.HtmlContent = InteractiveTableHtmlRenderer.Render(table);
                break;
            case CellOutputKind.ObjectInspector when rich.InspectorNode is { } node:
                cell.InspectorSnapshot = node.ToSnapshot();
                break;
            case CellOutputKind.Control:
                cell.HadInteractiveControl = true;
                break;
        }
    }

    private static void ClearOutputs(NotebookCellItem cell)
    {
        cell.OutputText = string.Empty;
        cell.HasError = false;
        cell.ImageBytes = null;
        cell.ImageFormat = null;
        cell.ImageWidth = null;
        cell.ImageHeight = null;
        cell.HtmlContent = null;
        cell.MarkdownContent = null;
        cell.TableSnapshot = null;
        cell.InspectorSnapshot = null;
        cell.Visuals = null;
        cell.HadInteractiveControl = false;
    }

    private static NotebookCell ToDto(NotebookCellItem cell)
    {
        var outputs = new List<CellOutput>();
        if (!string.IsNullOrEmpty(cell.OutputText)) outputs.Add(new CellOutput(cell.HasError ? "error" : "stream", cell.OutputText));

        foreach (var rich in SavedOutputs(cell))
        {
            if (RichOutputMime.ToBundle(rich, out var metadata) is { } bundle)
            {
                var data = Newtonsoft.Json.Linq.JObject.Parse(bundle.ToJsonString());
                var meta = metadata.Count > 0 ? Newtonsoft.Json.Linq.JObject.Parse(metadata.ToJsonString()) : null;
                outputs.Add(new CellOutput("display", Data: data, Metadata: meta));
            }
        }

        return new NotebookCell(cell.Id, cell.Type == CellType.Markdown ? "markdown" : "code", cell.Language, cell.Source,
            cell.ExecutionCount, cell.HasError, outputs);
    }

    private static IEnumerable<RichCellOutput> SavedOutputs(NotebookCellItem cell)
    {
        if (cell.ImageBytes is { Length: > 0 } image)
        {
            yield return new RichCellOutput { Kind = CellOutputKind.Image, ImageBytes = image, ImageFormat = cell.ImageFormat, ImageWidth = cell.ImageWidth, ImageHeight = cell.ImageHeight };
        }
        if (!string.IsNullOrEmpty(cell.HtmlContent))
        {
            yield return new RichCellOutput { Kind = CellOutputKind.Html, HtmlContent = cell.HtmlContent, MarkdownSource = cell.MarkdownContent };
        }
        if (cell.TableSnapshot is { } table) yield return new RichCellOutput { Kind = CellOutputKind.Table, TableResult = table.ToLive() };
        if (cell.InspectorSnapshot is { } inspector) yield return new RichCellOutput { Kind = CellOutputKind.ObjectInspector, InspectorNode = inspector.ToLive() };
        foreach (var visual in cell.Visuals ?? [])
        {
            if (visual.ToOutput() is { } live) yield return new RichCellOutput { Visual = live };
        }
    }

    private static LocalRpcException MissingCell(string id) => Error(ErrorCodes.NotFound, $"There is no cell '{id}'.");

    private static LocalRpcException Error(int code, string message) => new(message) { ErrorCode = code };

    private sealed class OpenNotebook : IDisposable
    {
        public OpenNotebook(string path, string fullPath, NotebookDocumentItem document, NotebookFileKind kind, string diskVersion,
            ILanguageCatalog languages, string workspaceRoot)
        {
            Path = path;
            FullPath = fullPath;
            Document = document;
            Kind = kind;
            DiskVersion = diskVersion;
            Router = new NotebookKernelRouter(languages, new KernelCreationContext(() => System.IO.Path.GetDirectoryName(fullPath), () => workspaceRoot));
            Directives = new NotebookDirectiveRunner(languages, Router);
        }

        private CancellationTokenSource? _execution;

        public Lock Gate { get; } = new();

        /// <summary>False for a notebook the client holds: its cells and outputs are the client's, so the server does not keep them.</summary>
        public bool KeepsOutputs { get; init; } = true;
        public string Path { get; }
        public string FullPath { get; }
        public NotebookDocumentItem Document { get; }
        public NotebookFileKind Kind { get; }
        public string DiskVersion { get; set; }
        public bool Dirty { get; set; }
        public NotebookKernelRouter Router { get; }
        public NotebookDirectiveRunner Directives { get; }
        public SemaphoreSlim ExecutionGate { get; } = new(1, 1);

        public void BeginExecution(CancellationTokenSource execution)
        {
            lock (Gate) _execution = execution;
        }

        public void EndExecution()
        {
            lock (Gate) _execution = null;
        }

        public void CancelExecution()
        {
            lock (Gate) _execution?.Cancel();
        }

        public void Dispose()
        {
            CancelExecution();
            Router.Dispose();
            ExecutionGate.Dispose();
        }
    }
}
