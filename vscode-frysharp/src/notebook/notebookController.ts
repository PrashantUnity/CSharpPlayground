import * as path from 'path';
import * as vscode from 'vscode';
import { SidecarClient } from '../sidecar/sidecarClient';
import { NotebookOutputNotification } from '../sidecar/protocol';
import { FryNotebookSerializer } from './notebookSerializer';

export class FryNotebookController implements vscode.Disposable {
  private controller: vscode.NotebookController;
  private disposables: vscode.Disposable[] = [];
  private activeExecutions = new Map<string, { execution: vscode.NotebookCellExecution; cell: vscode.NotebookCell }>();
  private pendingOutputs = new Map<string, Promise<void>[]>();

  constructor(private sidecar: SidecarClient) {
    this.controller = vscode.notebooks.createNotebookController(
      'frysharp-kernel',
      'frysharp-notebook',
      'FrySharp Polyglot Kernel'
    );

    this.controller.supportedLanguages = ['csharp', 'python', 'javascript', 'java', 'sql', 'fsharp', 'rust', 'cpp'];
    this.controller.supportsExecutionOrder = true;
    this.controller.executeHandler = this.executeHandler.bind(this);
    this.controller.interruptHandler = this.interruptHandler.bind(this);

    // Listen to output streamed from sidecar
    this.disposables.push(
      this.sidecar.onNotebookOutput(this.handleOutput.bind(this))
    );
  }

  private normalizePath(p: string): string {
    return p.replace(/\\/g, '/').replace(/^\/+/, '');
  }

  private getRelativePath(notebook: vscode.NotebookDocument): string {
    const workspaceRoot = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
    let rel = workspaceRoot ? path.relative(workspaceRoot, notebook.uri.fsPath).replace(/\\/g, '/') : '';
    if (!rel || rel.startsWith('..') || path.isAbsolute(rel)) {
      rel = path.basename(notebook.uri.fsPath) || 'untitled.frynb';
    }
    return this.normalizePath(rel);
  }

  private async executeHandler(cells: vscode.NotebookCell[], notebook: vscode.NotebookDocument): Promise<void> {
    const relativePath = this.getRelativePath(notebook);

    for (const cell of cells) {
      await this.executeCell(cell, relativePath);
    }
  }

  private async executeCell(cell: vscode.NotebookCell, relativePath: string): Promise<void> {
    const execution = this.controller.createNotebookCellExecution(cell);
    execution.executionOrder = (cell.executionSummary?.executionOrder || 0) + 1;
    execution.start(Date.now());
    await execution.clearOutput();

    const cellId = cell.metadata?.id || `cell-${cell.index + 1}`;
    const key = `${relativePath}:${cellId}`;
    this.activeExecutions.set(key, { execution, cell });
    this.pendingOutputs.set(key, []);

    execution.token.onCancellationRequested(async () => {
      await this.sidecar.interruptNotebook(relativePath);
      execution.end(false, Date.now());
    });

    try {
      const source = cell.document.getText();
      const language = cell.kind === vscode.NotebookCellKind.Code ? cell.document.languageId : undefined;
      const result = await this.sidecar.executeCell(relativePath, cellId, source, language);

      // Wait for all in-flight output notifications to flush into the notebook cell
      const pending = this.pendingOutputs.get(key) || [];
      await Promise.all(pending);
      // Brief pause to allow trailing notifications in the pipe
      await new Promise((r) => setTimeout(r, 60));
      const trailing = this.pendingOutputs.get(key) || [];
      await Promise.all(trailing);

      execution.executionOrder = result.executionCount;
      execution.end(result.success, Date.now());
    } catch (err: any) {
      const errorOutput = new vscode.NotebookCellOutput([
        vscode.NotebookCellOutputItem.error({
          name: 'ExecutionError',
          message: err?.message || String(err)
        })
      ]);
      await execution.appendOutput(errorOutput);
      execution.end(false, Date.now());
    } finally {
      this.activeExecutions.delete(key);
      this.pendingOutputs.delete(key);
    }
  }

  private async interruptHandler(notebook: vscode.NotebookDocument): Promise<void> {
    const relativePath = this.getRelativePath(notebook);
    await this.sidecar.interruptNotebook(relativePath);
  }

  private findExecution(notifPath: string, cellId: string): { execution?: vscode.NotebookCellExecution; cell?: vscode.NotebookCell; key: string } {
    const norm = this.normalizePath(notifPath);
    const exactKey = `${norm}:${cellId}`;

    if (this.activeExecutions.has(exactKey)) {
      return { ...this.activeExecutions.get(exactKey)!, key: exactKey };
    }

    // Match by base name or cellId if single active
    for (const [k, val] of this.activeExecutions.entries()) {
      if (k.endsWith(`:${cellId}`) || k.includes(path.basename(notifPath))) {
        return { ...val, key: k };
      }
    }

    return { key: exactKey };
  }

  private async handleOutput(notification: NotebookOutputNotification): Promise<void> {
    const found = this.findExecution(notification.path, notification.cellId);
    if (!found.execution) return;

    const execution = found.execution;
    const key = found.key;

    const promise = (async () => {
      try {
        const out = notification.output;
        if (out.kind === 'stream' && out.text) {
          await execution.appendOutput(new vscode.NotebookCellOutput([
            vscode.NotebookCellOutputItem.text(out.text, 'text/plain')
          ]));
        } else if (out.kind === 'display' && out.data) {
          if (!out.data['text/html']) {
            if (out.data['application/vnd.fry.table+json']) {
              out.data['text/html'] = FryNotebookSerializer.formatTableJsonToHtml(out.data['application/vnd.fry.table+json']);
            } else {
              const imgKey = Object.keys(out.data).find(k => k.startsWith('image/') && k !== 'image/svg+xml');
              if (imgKey && typeof out.data[imgKey] === 'string') {
                const b64 = out.data[imgKey];
                out.data['text/html'] = `<div class="fry-img-card" style="margin:8px 0;max-width:840px;"><img src="data:${imgKey};base64,${b64}" style="max-width:100%;height:auto;border-radius:6px;border:1px solid #30363d;box-shadow:0 4px 16px rgba(0,0,0,0.35);display:block;" alt="Image Output" /></div>`;
              }
            }
          }
          // Prioritize standard web types so VS Code renders text/html, image, and markdown first
          const preferredOrder = ['text/html', 'image/svg+xml', 'image/png', 'image/jpeg', 'image/gif', 'text/markdown', 'text/plain'];
          const entries = Object.entries(out.data).sort(([mimeA], [mimeB]) => {
            const idxA = preferredOrder.indexOf(mimeA);
            const idxB = preferredOrder.indexOf(mimeB);
            const rankA = idxA >= 0 ? idxA : 100;
            const rankB = idxB >= 0 ? idxB : 100;
            return rankA - rankB;
          });

          const items = entries.map(([mime, val]) => {
            let bytes: Uint8Array;
            if (typeof val === 'string' && mime.startsWith('image/') && mime !== 'image/svg+xml') {
              bytes = Buffer.from(val, 'base64');
            } else {
              const text = typeof val === 'string' ? val : JSON.stringify(val);
              bytes = new TextEncoder().encode(text);
            }
            return new vscode.NotebookCellOutputItem(bytes, mime);
          });
          await execution.appendOutput(new vscode.NotebookCellOutput(items, out.metadata || undefined));
        }
      } catch (err) {
        console.error('[NotebookController] Error appending output:', err);
      }
    })();

    const list = this.pendingOutputs.get(key) || [];
    list.push(promise);
    this.pendingOutputs.set(key, list);
    await promise;
  }

  public dispose(): void {
    this.controller.dispose();
    this.disposables.forEach((d) => d.dispose());
  }
}
