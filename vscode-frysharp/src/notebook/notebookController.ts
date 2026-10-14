import * as vscode from 'vscode';
import { ENGINE_ERROR } from './format/model';
import { identityOf, NotebookIdentity } from './identity';
import { RunHub } from './runHub';
import { SidecarClient } from '../sidecar/sidecarClient';

/** The notebook types of the extension: its own file, and a Jupyter file opened with it. */
export const NOTEBOOK_TYPES = ['frysharp-notebook', 'frysharp-jupyter'] as const;
export const isFryNotebook = (notebook: vscode.NotebookDocument): boolean => (NOTEBOOK_TYPES as readonly string[]).includes(notebook.notebookType);

export function notebookIdentity(notebook: vscode.NotebookDocument): NotebookIdentity {
  return identityOf(notebook.uri, (uri) => vscode.workspace.getWorkspaceFolder(vscode.Uri.parse(uri.toString()))?.uri.fsPath ?? vscode.workspace.workspaceFolders?.[0]?.uri.fsPath);
}

/** What the engine runs, as VS Code's controller for one notebook type: starts a cell's execution and ends it; the output is the hub's. */
export class FryNotebookController implements vscode.Disposable {
  private readonly controller: vscode.NotebookController;
  private readonly disposables: vscode.Disposable[] = [];

  constructor(
    private readonly sidecar: SidecarClient,
    private readonly hub: RunHub,
    private readonly notebookType: (typeof NOTEBOOK_TYPES)[number]
  ) {
    this.controller = vscode.notebooks.createNotebookController(`frysharp-kernel-${notebookType}`, notebookType, 'FrySharp Polyglot Kernel');
    this.controller.description = '.NET 10 · Python · JavaScript';
    this.controller.supportsExecutionOrder = true;
    this.controller.executeHandler = this.executeHandler.bind(this);
    this.controller.interruptHandler = this.interruptHandler.bind(this);

    this.disposables.push(
      // The languages are the engine's to say: a cell in one it cannot run is not offered a Run button.
      this.sidecar.onDidInitialize((init) => {
        const runnable = init.languages.filter((language) => language.canRunCells);
        this.controller.supportedLanguages = runnable.map((language) => language.id);
        this.controller.description = runnable.map((language) => language.displayName).join(' · ');
      }),
      // It is the kernel to use for these notebooks without asking.
      vscode.workspace.onDidOpenNotebookDocument((notebook) => this.prefer(notebook))
    );
    vscode.workspace.notebookDocuments.forEach((notebook) => this.prefer(notebook));
  }

  private prefer(notebook: vscode.NotebookDocument): void {
    if (notebook.notebookType === this.notebookType) {
      this.controller.updateNotebookAffinity(notebook, vscode.NotebookControllerAffinity.Preferred);
    }
  }

  private async executeHandler(cells: vscode.NotebookCell[], notebook: vscode.NotebookDocument): Promise<void> {
    if (!vscode.workspace.isTrusted) {
      void vscode.window.showWarningMessage('Running code is off until you trust this workspace.');
      return;
    }

    const stopOnError = vscode.workspace.getConfiguration('frysharp.notebook').get<boolean>('stopOnError', true);
    const identity = notebookIdentity(notebook);
    for (const cell of cells) {
      const succeeded = await this.executeCell(cell, identity);
      // Run All stops at the cell that failed, as running them one by one would: what follows depends on it.
      if (!succeeded && stopOnError) break;
    }
  }

  private async executeCell(cell: vscode.NotebookCell, identity: NotebookIdentity): Promise<boolean> {
    const execution = this.controller.createNotebookCellExecution(cell);
    execution.executionOrder = (cell.executionSummary?.executionOrder || 0) + 1;
    execution.start(Date.now());
    await execution.clearOutput();
    const run = this.hub.begin(execution, cell, identity);

    // Stop asks the engine to stop the cell; the cell ends when the engine says it has, with what it printed before.
    const stop = execution.token.onCancellationRequested(() => void this.sidecar.interruptNotebook({ notebookId: identity.notebookId }));

    try {
      const cellId = cell.metadata?.id || `cell-${cell.index + 1}`;
      const result = await this.sidecar.executeCell({
        path: identity.path,
        cellId,
        source: cell.document.getText(),
        language: cell.kind === vscode.NotebookCellKind.Code ? cell.document.languageId : undefined,
        notebookId: identity.notebookId,
        workingDirectory: identity.workingDirectory,
        executionId: run.id
      });

      // The reply can arrive before the last of the output: wait until everything the run sent is shown.
      if (!(await run.queue.processed(result.lastSeq ?? 0))) {
        console.warn(`[FrySharp] some output of ${identity.path} (${cellId}) did not arrive`);
      }
      await this.hub.finish(run);
      // why it failed, as VS Code shows an error (what the cell printed is above it, whole)
      if (!result.success && result.error) {
        await execution.appendOutput(
          new vscode.NotebookCellOutput([vscode.NotebookCellOutputItem.error({ name: result.error.name, message: result.error.message })], { [ENGINE_ERROR]: true })
        );
      }

      this.hub.markErrors(cell, result.diagnostics);
      execution.executionOrder = result.executionCount;
      execution.end(result.success, Date.now());
      return result.success;
    } catch (error) {
      await this.hub.finish(run).catch(() => undefined);
      await execution.appendOutput(
        new vscode.NotebookCellOutput([vscode.NotebookCellOutputItem.error({ name: 'FrySharp engine', message: error instanceof Error ? error.message : String(error) })])
      );
      execution.end(false, Date.now());
      return false;
    } finally {
      stop.dispose();
      this.hub.end(run);
    }
  }

  private async interruptHandler(notebook: vscode.NotebookDocument): Promise<void> {
    await this.sidecar.interruptNotebook({ notebookId: notebookIdentity(notebook).notebookId });
  }

  dispose(): void {
    this.controller.dispose();
    this.disposables.forEach((d) => d.dispose());
  }
}
