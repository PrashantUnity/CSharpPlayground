using System.Text.Json.Nodes;

namespace FrySharp.Sidecar.Protocol;

// The FrySharp session protocol: JSON-RPC 2.0 over stdio for the VS Code extension.

/// <summary>The protocol's version. A client sends the one it speaks; the server answers with its own.</summary>
public static class ProtocolVersion
{
    public const int Current = 1;
}

/// <summary>JSON-RPC error codes the server returns, besides the standard ones.</summary>
public static class ErrorCodes
{
    public const int NotFound = -32001;
    public const int AccessDenied = -32002;
    public const int Conflict = -32003;
    public const int Invalid = -32004;
    public const int TooLarge = -32005;
    public const int OutOfSync = -32006;
    public const int NotSupported = -32007;
    public const int AlreadyExists = -32008;
}

/// <summary>The names of the server's notifications and of the requests it makes of the client.</summary>
public static class ClientMethods
{
    public const string WorkspaceChanged = "fry/workspace/changed";
    public const string SearchHits = "fry/workspace/searchHits";
    public const string DocumentChangedOnDisk = "fry/document/changedOnDisk";
    public const string DocumentResync = "fry/document/resync";
    public const string Diagnostics = "fry/language/diagnostics";
    public const string RunOutput = "fry/run/output";
    public const string RunDisplay = "fry/run/display";
    public const string RunInputRequested = "fry/run/inputRequested";
    public const string RunExited = "fry/run/exited";
    public const string NotebookOutput = "fry/notebook/output";
    public const string NotebookCellState = "fry/notebook/cellState";
    public const string InputRequest = "fry/input/request";
    public const string SessionReplaced = "fry/session/replaced";
}

// ── Session ──────────────────────────────────────────────────────────────────────────────────────────────────────

public sealed record InitializeParams(string ClientId, string? ClientName = null, int ProtocolVersion = ProtocolVersion.Current);

public sealed record InitializeResult(
    int ProtocolVersion,
    string ServerVersion,
    string WorkspaceRoot,
    string WorkspaceName,
    IReadOnlyList<LanguageInfo> Languages,
    bool Resumed,
    long MaxOpenFileBytes);

public sealed record LanguageInfo(
    string Id,
    string DisplayName,
    string ShortName,
    IReadOnlyList<string> Extensions,
    IReadOnlyList<string> Aliases,
    string LineComment,
    string Icon,
    string Accent,
    bool CanRun,
    bool CanRunCells,
    bool HasCompletion,
    string NewFileTemplate);

public sealed record EmptyParams;

// ── Workspace ────────────────────────────────────────────────────────────────────────────────────────────────────

public sealed record PathParams(string Path);

public sealed record FolderEntry(string Name, string Path, bool IsFolder, long? Size, DateTimeOffset? Modified);

public sealed record FolderListing(string Path, IReadOnlyList<FolderEntry> Entries, bool Truncated);

public sealed record FindFilesParams(string Query, int Limit = 50);

public sealed record FindFilesResult(IReadOnlyList<FileMatch> Matches, bool Indexing);

public sealed record FileMatch(string Path, string Name);

public sealed record SearchParams(string SearchId, string Query, bool IsRegex = false, bool MatchCase = false, bool WholeWord = false);

public sealed record SearchHit(int Line, int Column, int Length, string Text);

public sealed record SearchHitsNotification(string SearchId, string Path, IReadOnlyList<SearchHit> Hits);

public sealed record SearchSummary(int FilesSearched, int FilesWithMatches, int Matches, bool HitLimit);

public sealed record CreateFileParams(string Path, string? Content = null);

public sealed record RenameParams(string From, string To);

public sealed record WorkspaceChangedNotification(bool Structure);

// ── Documents ────────────────────────────────────────────────────────────────────────────────────────────────────

public sealed record DocumentContent(string Path, string Text, string LanguageId, string DiskVersion, bool IsReadOnly);

public sealed record DidOpenParams(string Path, int Version, string Text, string DiskVersion);

public sealed record TextChange(int Offset, int Length, string Text);

public sealed record DidChangeParams(string Path, int Version, IReadOnlyList<TextChange> Changes);

public sealed record SaveParams(string Path, int Version, bool Force = false);

public sealed record SaveResult(string DiskVersion);

public sealed record DocumentNotification(string Path);

// ── Language features ────────────────────────────────────────────────────────────────────────────────────────────

public sealed record PositionParams(string Path, int Version, int Offset);

public sealed record CompletionItem(string Label, string InsertText, string Kind, string? Detail, string? Documentation, string SortText);

public sealed record CompletionResult(IReadOnlyList<CompletionItem> Items);

public sealed record HoverResult(IReadOnlyList<string> Markdown);

public sealed record DiagnosticDto(string Severity, string Code, string Message, int StartLine, int StartColumn, int EndLine, int EndColumn);

public sealed record DiagnosticsNotification(string Path, int Version, IReadOnlyList<DiagnosticDto> Diagnostics);

// ── Runs ─────────────────────────────────────────────────────────────────────────────────────────────────────────

public sealed record RunStartParams(string RunId, string? Path = null, string? Code = null, string? LanguageId = null, IReadOnlyList<string>? Arguments = null);

public sealed record RunStartResult(string RunId, string Title);

public sealed record RunIdParams(string RunId);

public sealed record RunInputParams(string RunId, string Text);

public sealed record RunInfo(string RunId, string Title, string? Path, string Output, bool Running, int? ExitCode);

public sealed record RunOutputNotification(string RunId, string Text);

public sealed record RunDisplayNotification(string RunId, object Data, object? Metadata = null);

public sealed record RunExitedNotification(string RunId, int ExitCode, long DurationMs, IReadOnlyList<RunProblem> Problems);

public sealed record RunProblem(string? Path, string Severity, string Code, string Message, int Line, int Column);

// ── Notebooks ────────────────────────────────────────────────────────────────────────────────────────────────────

public sealed record CellOutput(string Kind, string? Text = null, object? Data = null, object? Metadata = null);

public sealed record NotebookCell(string Id, string Type, string? Language, string Source, int? ExecutionCount, bool HasError, IReadOnlyList<CellOutput> Outputs);

public sealed record NotebookContent(string Path, string Format, string Title, string DefaultLanguage, string DiskVersion, IReadOnlyList<NotebookCell> Cells);

public sealed record NotebookEdit(string Op, string CellId, int? Index = null, string? Type = null, string? Language = null, string? Source = null);

public sealed record NotebookEditParams(string Path, IReadOnlyList<NotebookEdit> Edits);

public sealed record ExecuteCellParams(string Path, string CellId, string Source, string? Language = null);

public sealed record ExecuteCellResult(int ExecutionCount, bool Success, long DurationMs);

public sealed record NotebookSaveParams(string Path, bool Force = false);

public sealed record VariableDto(string Name, string Type, string Value, string Kernel);

public sealed record NotebookOutputNotification(string Path, string CellId, CellOutput Output);

public sealed record CellStateNotification(string Path, string CellId, bool Running, int? ExecutionCount, bool? Success, long? DurationMs);

public sealed record InputRequest(string Prompt, bool Password);
