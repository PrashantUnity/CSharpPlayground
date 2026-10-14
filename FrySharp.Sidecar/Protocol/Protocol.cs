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

// ── Engine info ──────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>What the engine runs on and which tool each language found: the answer to "why doesn't my Java cell run?".</summary>
public sealed record EngineInfo(
    string ServerVersion,
    int ProtocolVersion,
    string Runtime,
    string OperatingSystem,
    string Architecture,
    string WorkspaceRoot,
    IReadOnlyList<ToolchainReport> Toolchains);

/// <param name="Name">The toolchain's name and version as it calls itself ("Python 3.12.4"), when found.</param>
/// <param name="Source">Where it came from ("Homebrew", "PATH", the studio's own).</param>
/// <param name="Missing">When not found: what is missing and how to get it.</param>
public sealed record ToolchainReport(string LanguageId, string DisplayName, bool Found, string? Name, string? Version, string? Path, string? Source, string? Missing);

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

// ── Notebook cells: language features that need no document the server holds ────────────────────────────────────────────

/// <param name="Language">The cell's language id: C# and SQL have completion and hover.</param>
/// <param name="Source">The cell's text.</param>
/// <param name="Offset">The caret, in characters from the start of <paramref name="Source"/>.</param>
/// <param name="Context">The text of the cells before this one that are in the same language, in order: what they define is in scope.</param>
public sealed record CellPositionParams(string Language, string Source, int Offset, IReadOnlyList<string>? Context = null);

public sealed record CellDiagnosticsParams(string Language, string Source, IReadOnlyList<string>? Context = null);

public sealed record CellDiagnosticsResult(IReadOnlyList<DiagnosticDto> Diagnostics);

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

/// <param name="DisplayId">Names a display a program may update later (a chart it redraws); only visuals have one.</param>
/// <param name="Update">True when this redraws the display <paramref name="DisplayId"/> already shown, rather than showing a new one.</param>
public sealed record CellOutput(string Kind, string? Text = null, object? Data = null, object? Metadata = null, string? DisplayId = null, bool Update = false);

public sealed record NotebookCell(string Id, string Type, string? Language, string Source, int? ExecutionCount, bool HasError, IReadOnlyList<CellOutput> Outputs);

public sealed record NotebookContent(string Path, string Format, string Title, string DefaultLanguage, string DiskVersion, IReadOnlyList<NotebookCell> Cells);

public sealed record NotebookEdit(string Op, string CellId, int? Index = null, string? Type = null, string? Language = null, string? Source = null);

public sealed record NotebookEditParams(string Path, IReadOnlyList<NotebookEdit> Edits);

/// <summary>
/// Which notebook a request is about. A notebook open in an editor is named by <c>NotebookId</c>, any text the client
/// keeps stable for that notebook (its file, an untitled one, one outside the workspace folder); the server then holds only
/// its kernels. One named by <c>Path</c>, relative to the workspace, is a file the server opens, edits and saves.
/// </summary>
public sealed record NotebookRef(string? Path = null, string? NotebookId = null);

/// <param name="Path">The notebook file, relative to the workspace, or any name when <paramref name="NotebookId"/> is given.</param>
/// <param name="NotebookId">Names the notebook for a client that holds it itself: the server keeps its kernels, not its cells.</param>
/// <param name="WorkingDirectory">The folder the notebook's kernels run in (its own folder); the workspace root without it.</param>
/// <param name="ExecutionId">Names this run. Every notification about it carries it, so a client with several cells running (or the same cell run twice) cannot mix them up.</param>
public sealed record ExecuteCellParams(string Path, string CellId, string Source, string? Language = null, string? NotebookId = null, string? WorkingDirectory = null, string? ExecutionId = null);

/// <param name="LastSeq">The number of the last notification this run sent: the reply may arrive before some of them, so a client waits until it has handled this many.</param>
/// <param name="Diagnostics">Where the cell went wrong, in the cell's own lines and columns (1-based): compile errors, or the line a traceback names.</param>
public sealed record ExecuteCellResult(int ExecutionCount, bool Success, long DurationMs, int LastSeq = 0, IReadOnlyList<DiagnosticDto>? Diagnostics = null, CellError? Error = null);

/// <summary>Why a cell failed, in a line: what a client shows as the cell's error (not when the cell was stopped).</summary>
/// <param name="Name">The kind of error: an exception's class, a compiler's code (CS0103, E0308), "panic".</param>
public sealed record CellError(string Name, string Message);

public sealed record NotebookSaveParams(string Path, bool Force = false);

public sealed record VariableDto(string Name, string Type, string Value, string Kernel);

/// <param name="Seq">1, 2, 3... for the notifications of one run (<paramref name="ExecutionId"/>), in the order they were produced.</param>
public sealed record NotebookOutputNotification(string Path, string CellId, CellOutput Output, string? ExecutionId = null, int Seq = 0);

public sealed record CellStateNotification(string Path, string CellId, bool Running, int? ExecutionCount, bool? Success, long? DurationMs, string? ExecutionId = null, int Seq = 0);

/// <summary>A running cell asks for a line of input (<c>input()</c>, <c>Console.ReadLine()</c>): the client shows a box and answers with the text, or null for the end of input.</summary>
public sealed record InputRequest(string Prompt, bool Password, string? Path = null, string? CellId = null, string? ExecutionId = null);
