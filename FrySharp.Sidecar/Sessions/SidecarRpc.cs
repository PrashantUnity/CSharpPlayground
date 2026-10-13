using FrySharp.Sidecar.Hosting;
using FrySharp.Sidecar.Protocol;
using FrySharp.Sidecar.Services;
using Microsoft.Extensions.Logging;
using PdfEditorApp.Plugins.CSharpEditor.Services.Execution;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using StreamJsonRpc;

namespace FrySharp.Sidecar.Sessions;

/// <summary>
/// JSON-RPC 2.0 dispatch for the VS Code extension sidecar over stdio.
/// </summary>
public sealed class SidecarRpc(
    JsonRpc connection,
    SidecarOptions options,
    WorkspaceService workspace,
    DocumentService documents,
    LanguageFeatures languageFeatures,
    LanguageRegistry<ILanguageEngine> languages,
    RunService runs,
    NotebookService notebooks,
    ILogger logger)
{
    private SidecarSession? _session;

    // ── Session ──────────────────────────────────────────────────────────────────────────────────────────────────

    [JsonRpcMethod("fry/initialize", UseSingleObjectParameterDeserialization = true)]
    public InitializeResult Initialize(InitializeParams request)
    {
        if (request.ProtocolVersion != ProtocolVersion.Current)
        {
            throw new LocalRpcException($"Extension speaks protocol {request.ProtocolVersion}, sidecar speaks {ProtocolVersion.Current}.")
            { ErrorCode = ErrorCodes.NotSupported };
        }

        _session = new SidecarSession(request.ClientId, connection, logger);
        return new InitializeResult(
            ProtocolVersion.Current,
            typeof(SidecarRpc).Assembly.GetName().Version?.ToString(3) ?? "1.0.0",
            workspace.Paths.Root,
            Path.GetFileName(workspace.Paths.Root),
            Languages(),
            Resumed: false,
            options.MaxOpenFileBytes);
    }

    private List<LanguageInfo> Languages()
    {
        return languages.All.Select(l => new LanguageInfo(
            l.Id, l.DisplayName, l.ShortName,
            l.Id == LanguageIds.CSharp ? [.. HeadlessLanguages.CSharpSourceExtensions] : l.FileExtensions,
            l.Aliases, l.LineCommentPrefix, l.IconKind, l.AccentHex,
            CanRun: FileRunner.CanRun(l),
            CanRunCells: NotebookDirectiveRunner.IsNotebookLanguage(l),
            HasCompletion: l.Id == LanguageIds.CSharp,
            l.NewFileTemplate)).ToList();
    }

    // ── Workspace ────────────────────────────────────────────────────────────────────────────────────────────────

    [JsonRpcMethod("fry/workspace/listFolder", UseSingleObjectParameterDeserialization = true)]
    public Task<FolderListing> ListFolderAsync(PathParams request, CancellationToken ct)
    {
        Require();
        return workspace.ListFolderAsync(request.Path, ct);
    }

    [JsonRpcMethod("fry/workspace/findFiles", UseSingleObjectParameterDeserialization = true)]
    public FindFilesResult FindFiles(FindFilesParams request)
    {
        Require();
        return workspace.FindFiles(request.Query, request.Limit);
    }

    [JsonRpcMethod("fry/workspace/search", UseSingleObjectParameterDeserialization = true)]
    public Task<SearchSummary> SearchAsync(SearchParams request, CancellationToken ct)
    {
        var session = Require();
        return workspace.SearchAsync(request, documents.OpenTexts(session),
            (path, hits) => session.Notify(ClientMethods.SearchHits, new SearchHitsNotification(request.SearchId, path, hits)), ct);
    }

    [JsonRpcMethod("fry/workspace/createFile", UseSingleObjectParameterDeserialization = true)]
    public async Task<PathParams> CreateFileAsync(CreateFileParams request, CancellationToken ct)
    {
        Require();
        var content = request.Content ?? languages.FindSourceFileLanguage(request.Path)?.NewFileTemplate ?? string.Empty;
        if (request.Content == null && LanguageFeatures.IsCSharp(request.Path))
        {
            content = languages.Get(LanguageIds.CSharp)?.NewFileTemplate ?? string.Empty;
        }
        return new PathParams(await workspace.CreateFileAsync(request.Path, content, ct));
    }

    [JsonRpcMethod("fry/workspace/createFolder", UseSingleObjectParameterDeserialization = true)]
    public PathParams CreateFolder(PathParams request)
    {
        Require();
        return new PathParams(workspace.CreateFolder(request.Path));
    }

    [JsonRpcMethod("fry/workspace/delete", UseSingleObjectParameterDeserialization = true)]
    public void Delete(PathParams request)
    {
        Require();
        workspace.Delete(request.Path);
    }

    [JsonRpcMethod("fry/workspace/rename", UseSingleObjectParameterDeserialization = true)]
    public PathParams Rename(RenameParams request)
    {
        Require();
        return new PathParams(workspace.Rename(request.From, request.To));
    }

    // ── Documents ────────────────────────────────────────────────────────────────────────────────────────────────

    [JsonRpcMethod("fry/document/open", UseSingleObjectParameterDeserialization = true)]
    public Task<DocumentContent> OpenDocumentAsync(PathParams request, CancellationToken ct)
    {
        Require();
        return documents.OpenAsync(request.Path, ct);
    }

    [JsonRpcMethod("fry/document/didOpen", UseSingleObjectParameterDeserialization = true)]
    public void DidOpen(DidOpenParams request) => documents.DidOpen(Require(), request);

    [JsonRpcMethod("fry/document/didChange", UseSingleObjectParameterDeserialization = true)]
    public void DidChange(DidChangeParams request) => documents.DidChange(Require(), request);

    [JsonRpcMethod("fry/document/didClose", UseSingleObjectParameterDeserialization = true)]
    public void DidClose(PathParams request) => documents.DidClose(Require(), request.Path);

    [JsonRpcMethod("fry/document/save", UseSingleObjectParameterDeserialization = true)]
    public Task<SaveResult> SaveAsync(SaveParams request, CancellationToken ct) => documents.SaveAsync(Require(), request, ct);

    // ── Language features ────────────────────────────────────────────────────────────────────────────────────────

    [JsonRpcMethod("fry/language/completion", UseSingleObjectParameterDeserialization = true)]
    public Task<CompletionResult> CompletionAsync(PositionParams request, CancellationToken ct) =>
        languageFeatures.CompleteAsync(Require(), request, ct);

    [JsonRpcMethod("fry/language/hover", UseSingleObjectParameterDeserialization = true)]
    public Task<HoverResult?> HoverAsync(PositionParams request, CancellationToken ct) =>
        languageFeatures.HoverAsync(Require(), request, ct);

    // ── Runs ─────────────────────────────────────────────────────────────────────────────────────────────────────

    [JsonRpcMethod("fry/run/start", UseSingleObjectParameterDeserialization = true)]
    public RunStartResult StartRun(RunStartParams request) => runs.Start(Require(), request);

    [JsonRpcMethod("fry/run/stop", UseSingleObjectParameterDeserialization = true)]
    public bool StopRun(RunIdParams request) => runs.Stop(Require(), request.RunId);

    [JsonRpcMethod("fry/run/input", UseSingleObjectParameterDeserialization = true)]
    public Task RunInputAsync(RunInputParams request) => runs.InputAsync(Require(), request);

    [JsonRpcMethod("fry/run/list", UseSingleObjectParameterDeserialization = true)]
    public IReadOnlyList<RunInfo> ListRuns(EmptyParams request) => runs.List(Require());

    // ── Notebooks ────────────────────────────────────────────────────────────────────────────────────────────────

    [JsonRpcMethod("fry/notebook/open", UseSingleObjectParameterDeserialization = true)]
    public Task<NotebookContent> OpenNotebookAsync(PathParams request, CancellationToken ct) => notebooks.OpenAsync(Require(), request.Path, ct);

    [JsonRpcMethod("fry/notebook/edit", UseSingleObjectParameterDeserialization = true)]
    public void EditNotebook(NotebookEditParams request) => notebooks.Edit(Require(), request);

    [JsonRpcMethod("fry/notebook/execute", UseSingleObjectParameterDeserialization = true)]
    public Task<ExecuteCellResult> ExecuteCellAsync(ExecuteCellParams request, CancellationToken ct) => notebooks.ExecuteAsync(Require(), request, ct);

    [JsonRpcMethod("fry/notebook/interrupt", UseSingleObjectParameterDeserialization = true)]
    public void InterruptNotebook(PathParams request) => notebooks.Interrupt(request.Path);

    [JsonRpcMethod("fry/notebook/restart", UseSingleObjectParameterDeserialization = true)]
    public void RestartNotebook(PathParams request) => notebooks.Restart(request.Path);

    [JsonRpcMethod("fry/notebook/variables", UseSingleObjectParameterDeserialization = true)]
    public Task<IReadOnlyList<VariableDto>> NotebookVariablesAsync(PathParams request, CancellationToken ct) =>
        notebooks.VariablesAsync(request.Path, ct);

    [JsonRpcMethod("fry/notebook/save", UseSingleObjectParameterDeserialization = true)]
    public Task<SaveResult> SaveNotebookAsync(NotebookSaveParams request, CancellationToken ct) => notebooks.SaveAsync(request, ct);

    [JsonRpcMethod("fry/notebook/close", UseSingleObjectParameterDeserialization = true)]
    public Task CloseNotebookAsync(PathParams request) => notebooks.CloseAsync(request.Path);

    private SidecarSession Require()
    {
        if (_session is not { } session)
        {
            throw new LocalRpcException("Call fry/initialize first.") { ErrorCode = ErrorCodes.Invalid };
        }
        return session;
    }
}
