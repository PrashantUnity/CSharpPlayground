import * as vscode from 'vscode';
import { CellProblems } from './cellProblems';
import { cellOf, cellScope } from './cellFeatures';
import { placeDiagnostics } from '../notebook/cellDiagnostics';
import { isFryNotebook } from '../notebook/notebookController';
import { SidecarClient } from '../sidecar/sidecarClient';

const DELAY_MS = 700;
const MAX_CELLS = 40;

/**
 * Marks the problems of a notebook's cells as they are typed: what the C# compiler finds with the earlier cells in scope, and
 * a directive that is not one (`#!pyton`). It waits for a pause in typing, asks about the cell and the ones after it (they
 * depend on it), skips a cell whose text and context have not changed, and drops an answer that a later keystroke made old.
 */
export class LiveDiagnostics implements vscode.Disposable {
  private readonly disposables: vscode.Disposable[] = [];
  private readonly timers = new Map<string, NodeJS.Timeout>();
  private readonly running = new Map<string, vscode.CancellationTokenSource>();
  /** What each cell was last checked with, so it is not checked again for nothing. */
  private readonly checked = new Map<string, string>();

  constructor(
    private readonly client: SidecarClient,
    private readonly problems: CellProblems
  ) {
    this.disposables.push(
      vscode.workspace.onDidChangeTextDocument((event) => {
        const found = cellOf(event.document);
        if (found) this.schedule(found.notebook, found.cell.index);
      }),
      vscode.workspace.onDidChangeNotebookDocument((event) => {
        if (!isFryNotebook(event.notebook) || event.contentChanges.length === 0) return;
        this.schedule(event.notebook, Math.min(...event.contentChanges.map((change) => change.range.start)));
      }),
      vscode.workspace.onDidOpenNotebookDocument((notebook) => {
        if (isFryNotebook(notebook)) this.schedule(notebook, 0);
      }),
      vscode.workspace.onDidCloseNotebookDocument((notebook) => {
        for (const cell of notebook.getCells()) {
          this.problems.forget(cell.document.uri);
          this.checked.delete(cell.document.uri.toString());
        }
        this.cancel(notebook.uri.toString());
      }),
      client.onDidInitialize(() => vscode.workspace.notebookDocuments.filter(isFryNotebook).forEach((notebook) => this.schedule(notebook, 0)))
    );
    vscode.workspace.notebookDocuments.filter(isFryNotebook).forEach((notebook) => this.schedule(notebook, 0));
  }

  private enabled(): boolean {
    return vscode.workspace.isTrusted && vscode.workspace.getConfiguration('frysharp.notebook').get<boolean>('liveDiagnostics', true);
  }

  private schedule(notebook: vscode.NotebookDocument, from: number): void {
    if (!this.enabled()) return;
    const key = notebook.uri.toString();
    clearTimeout(this.timers.get(key));
    this.cancel(key);
    this.timers.set(key, setTimeout(() => void this.check(notebook, from), DELAY_MS));
  }

  private cancel(key: string): void {
    this.running.get(key)?.cancel();
    this.running.delete(key);
  }

  private async check(notebook: vscode.NotebookDocument, from: number): Promise<void> {
    if (notebook.isClosed || this.client.state === 'crashed') return;
    const key = notebook.uri.toString();
    const source = new vscode.CancellationTokenSource();
    this.running.set(key, source);

    try {
      const cells = notebook.getCells().filter((cell) => cell.index >= from && cell.kind === vscode.NotebookCellKind.Code).slice(0, MAX_CELLS);
      for (const cell of cells) {
        if (source.token.isCancellationRequested) return;
        const text = cell.document.getText();
        const scope = cellScope(this.client, notebook, cell);
        // C# is compiled; other languages only have their directives read
        if (scope.language !== 'csharp' && !/^\s*#!/m.test(text)) {
          this.problems.setLive(cell.document.uri, []);
          continue;
        }

        const stamp = JSON.stringify([cell.document.languageId, scope.context, text]);
        if (this.checked.get(cell.document.uri.toString()) === stamp) continue;

        const version = cell.document.version;
        const result = await this.client.diagnoseCell({ language: cell.document.languageId, source: text, context: scope.context }, source.token);
        if (source.token.isCancellationRequested || cell.document.version !== version) return; // the code changed while asking: the answer is for old code

        this.checked.set(cell.document.uri.toString(), stamp);
        const placed = placeDiagnostics(result.diagnostics, text);
        const severity = { error: vscode.DiagnosticSeverity.Error, warning: vscode.DiagnosticSeverity.Warning, information: vscode.DiagnosticSeverity.Information };
        this.problems.setLive(
          cell.document.uri,
          placed.map((p) => {
            const diagnostic = new vscode.Diagnostic(new vscode.Range(p.startLine, p.startCharacter, p.endLine, p.endCharacter), p.message, severity[p.severity]);
            diagnostic.code = p.code;
            diagnostic.source = 'FrySharp';
            return diagnostic;
          })
        );
      }
    } catch {
      // the engine is not there, or the question was withdrawn: nothing is marked, and nothing is said
    } finally {
      if (this.running.get(key) === source) this.running.delete(key);
    }
  }

  dispose(): void {
    this.timers.forEach((timer) => clearTimeout(timer));
    this.running.forEach((source) => source.cancel());
    this.disposables.forEach((d) => d.dispose());
  }
}
