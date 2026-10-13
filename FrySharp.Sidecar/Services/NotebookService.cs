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

    public async Task<ExecuteCellResult> ExecuteAsync(SidecarSession session, ExecuteCellParams request, CancellationToken ct)
    {
        var notebook = await GetOrOpenAsync(session, request.Path, ct).ConfigureAwait(false);
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

            session.Notify(ClientMethods.NotebookCellState, new CellStateNotification(notebook.Path, cell.Id, true, executionCount, null, null));
            using var execution = new CancellationTokenSource();
            notebook.BeginExecution(execution);
            var clock = Stopwatch.StartNew();
            var success = false;

            using var output = new RunOutput(text => session.Notify(ClientMethods.NotebookOutput,
                new NotebookOutputNotification(notebook.Path, cell.Id, new CellOutput("stream", text))));
            void Write(string text)
            {
                lock (notebook.Gate) cell.OutputText += text;
                output.Append(text);
            }
            void Show(RichCellOutput rich)
            {
                output.Flush();
                lock (notebook.Gate) Keep(cell, rich);
                if (RichOutputMime.ToBundle(rich, out var metadata) is { } bundle)
                {
                    var data = Newtonsoft.Json.Linq.JObject.Parse(bundle.ToJsonString());
                    var meta = metadata.Count > 0 ? Newtonsoft.Json.Linq.JObject.Parse(metadata.ToJsonString()) : null;
                    session.Notify(ClientMethods.NotebookOutput,
                        new NotebookOutputNotification(notebook.Path, cell.Id, new CellOutput("display", Data: data, Metadata: meta)));
                }
            }

            try
            {
                success = await RunCellAsync(notebook, cell, session, Write, Show, execution.Token).ConfigureAwait(false);
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
                    cell.HasError = !success;
                    cell.ExecutionTimeText = $"{clock.Elapsed.TotalMilliseconds:N0} ms";
                }
                session.Notify(ClientMethods.NotebookCellState,
                    new CellStateNotification(notebook.Path, cell.Id, false, executionCount, success, clock.ElapsedMilliseconds));
            }

            return new ExecuteCellResult(executionCount, success, clock.ElapsedMilliseconds);
        }
        finally
        {
            notebook.ExecutionGate.Release();
        }
    }

    private async Task<bool> RunCellAsync(OpenNotebook notebook, NotebookCellItem cell, SidecarSession session,
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
            return false;
        }

        if (directives.Errors.Count > 0)
        {
            foreach (var error in directives.Errors) write($"❌ {error}\n");
            return false;
        }

        var query = new ToolchainQuery(Path.GetDirectoryName(notebook.FullPath), _workspace.Paths.Root);
        if (!await notebook.Directives.RunAsync(directives, language, kernel, query, write, ct).ConfigureAwait(false)) return false;

        var request = new KernelExecutionRequest
        {
            Code = directives.Code,
            SourceId = cell.Id,
            Label = $"[{cell.ExecutionCount}]",
            OnConsole = write,
            OnRichOutput = show,
            OnInputRequest = (prompt, password, token) => session.RequestInputAsync(new InputRequest(prompt, password), token),
        };

        var execution = kernel.CanForceStop
            ? kernel.ExecuteAsync(request, ct)
            : Task.Run(() => kernel.ExecuteAsync(request, ct), CancellationToken.None);
        if (!kernel.CanForceStop && !await ExecutionAbandonment.WaitWithGraceAsync(execution, ct).ConfigureAwait(false))
        {
            write("\n⚠️ The cell did not respond to Stop and was abandoned; its kernel keeps running in the background.\n");
            return false;
        }

        var result = await execution.ConfigureAwait(false);
        if (!result.Success && result.Diagnostics.Count > 0)
        {
            foreach (var diagnostic in result.Diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error))
            {
                write($"{diagnostic.LocationString}: {diagnostic.Id} {diagnostic.Message}\n");
            }
        }
        return result.Success;
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
