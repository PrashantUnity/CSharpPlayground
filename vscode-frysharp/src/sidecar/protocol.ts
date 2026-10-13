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

export interface ExecuteCellParams {
  path: string;
  cellId: string;
  source: string;
  language?: string;
}

export interface ExecuteCellResult {
  executionCount: number;
  success: boolean;
  durationMs: number;
}

export interface NotebookOutputNotification {
  path: string;
  cellId: string;
  output: CellOutput;
}

export interface CellStateNotification {
  path: string;
  cellId: string;
  running: boolean;
  executionCount?: number;
  success?: boolean;
  durationMs?: number;
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
