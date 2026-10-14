// Types matching FrySharp.Sidecar.Protocol

export interface InitializeParams {
  clientId: string;
  clientName?: string;
  protocolVersion: number;
}

export interface LanguageInfo {
  id: string;
  displayName: string;
  shortName: string;
  extensions: string[];
  aliases: string[];
  lineComment: string;
  icon: string;
  accent: string;
  canRun: boolean;
  canRunCells: boolean;
  hasCompletion: boolean;
  newFileTemplate: string;
}

export interface InitializeResult {
  protocolVersion: number;
  serverVersion: string;
  workspaceRoot: string;
  workspaceName: string;
  languages: LanguageInfo[];
  resumed: boolean;
  maxOpenFileBytes: number;
}

export interface CellOutput {
  kind: 'stream' | 'display' | 'error';
  text?: string;
  data?: Record<string, any>;
  metadata?: Record<string, any>;
  /** Names a display a program may update later (a chart it redraws). */
  displayId?: string;
  /** This redraws the display `displayId` already shown, rather than showing a new one. */
  update?: boolean;
}

/** Where a cell went wrong, in its own lines and columns (1-based). */
export interface DiagnosticDto {
  severity: string;
  code: string;
  message: string;
  startLine: number;
  startColumn: number;
  endLine: number;
  endColumn: number;
}

export interface NotebookCell {
  id: string;
  type: 'code' | 'markdown';
  language?: string;
  source: string;
  executionCount?: number;
  hasError: boolean;
  outputs: CellOutput[];
}

export interface NotebookContent {
  path: string;
  format: 'frynb' | 'ipynb';
  title: string;
  defaultLanguage: string;
  diskVersion: string;
  cells: NotebookCell[];
}

/**
 * Which notebook a request is about. A notebook open in an editor is named by `notebookId`, any text that stays the same
 * for that notebook (a file's URI, an untitled notebook's); the engine then keeps only its kernels. One named by `path`
 * (relative to the workspace) is a file the engine opens, edits and saves itself.
 */
export interface NotebookRef {
  path?: string;
  notebookId?: string;
}

export interface ExecuteCellParams {
  path: string;
  cellId: string;
  source: string;
  language?: string;
  notebookId?: string;
  /** The folder the notebook's kernels run in (the notebook's own folder, which need not be in the workspace). */
  workingDirectory?: string;
  /** Names this run: every notification about it carries it. */
  executionId?: string;
}

export interface ExecuteCellResult {
  executionCount: number;
  success: boolean;
  durationMs: number;
  /** The number of the last notification the run sent: the reply can arrive before some of them. */
  lastSeq?: number;
  diagnostics?: DiagnosticDto[];
  /** Why the cell failed, in a line (not set when it was stopped). */
  error?: CellError;
}

export interface CellError {
  /** The kind of error: an exception's class, a compiler's code, "panic". */
  name: string;
  message: string;
}

/** A running cell asks for a line of input (`input()`, `Console.ReadLine()`). */
export interface InputRequest {
  prompt: string;
  password: boolean;
  path?: string;
  cellId?: string;
  executionId?: string;
}

export interface NotebookOutputNotification {
  path: string;
  cellId: string;
  output: CellOutput;
  executionId?: string;
  /** 1, 2, 3... for the notifications of one run, in the order they were made. */
  seq?: number;
}

export interface CellStateNotification {
  path: string;
  cellId: string;
  running: boolean;
  executionCount?: number;
  success?: boolean;
  durationMs?: number;
  executionId?: string;
  seq?: number;
}

export interface RunStartParams {
  runId: string;
  path?: string;
  code?: string;
  languageId?: string;
  arguments?: string[];
}

export interface RunStartResult {
  runId: string;
  title: string;
}

export interface RunOutputNotification {
  runId: string;
  text: string;
}

/** A program a run started shows something that is not text: a table, a chart, an image (the same MIME bundle as a cell's). */
export interface RunDisplayNotification {
  runId: string;
  data: Record<string, any>;
  metadata?: Record<string, any>;
}

export interface RunInputParams {
  runId: string;
  text: string;
}

export interface RunExitedNotification {
  runId: string;
  exitCode: number;
  durationMs: number;
  problems: Array<{
    path?: string;
    severity: string;
    code: string;
    message: string;
    line: number;
    column: number;
  }>;
}

export interface VariableDto {
  name: string;
  type: string;
  value: string;
  kernel: string;
}

// ── Language features in a notebook cell ───────────────────────────────────────────────────────────────────────────

/** Where in a cell, and what the cells before it define (their text, in order, those in the same language). */
export interface CellPositionParams {
  language: string;
  source: string;
  offset: number;
  context?: string[];
}

export interface CellDiagnosticsParams {
  language: string;
  source: string;
  context?: string[];
}

export interface CellDiagnosticsResult {
  diagnostics: DiagnosticDto[];
}

export interface CompletionItem {
  label: string;
  insertText: string;
  kind: string;
  detail?: string;
  documentation?: string;
  sortText: string;
}

export interface CompletionResult {
  items: CompletionItem[];
}

export interface HoverResult {
  markdown: string[];
}

/** What the engine runs on and which tool each language found (`fry/engine/info`). */
export interface EngineInfo {
  serverVersion: string;
  protocolVersion: number;
  runtime: string;
  operatingSystem: string;
  architecture: string;
  workspaceRoot: string;
  toolchains: ToolchainReport[];
}

export interface ToolchainReport {
  languageId: string;
  displayName: string;
  found: boolean;
  name?: string;
  version?: string;
  path?: string;
  source?: string;
  /** When not found: what is missing and how to get it. */
  missing?: string;
}
