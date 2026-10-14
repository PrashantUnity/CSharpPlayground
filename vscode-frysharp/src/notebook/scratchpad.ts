import * as vscode from 'vscode';
import { NOTEBOOK_TYPES } from './notebookController';

/** The language a cell gets for an editor's language: only the ones the engine runs in a notebook. */
const CELL_LANGUAGES = new Set(['csharp', 'python', 'javascript']);

/**
 * The scratchpad: one untitled notebook that keeps its kernels, to try code in without a file (the desktop's Scratchpad
 * and REPL). "Send selection" puts the selected code in a new cell at its end and runs it.
 */
export class Scratchpad implements vscode.Disposable {
  private notebook?: vscode.NotebookDocument;
  private readonly disposables: vscode.Disposable[] = [];

  constructor() {
    this.disposables.push(
      vscode.workspace.onDidCloseNotebookDocument((closed) => {
        if (closed === this.notebook) this.notebook = undefined;
      })
    );
  }

  /** The scratchpad, shown: made the first time, and when it was closed. */
  async open(): Promise<vscode.NotebookDocument> {
    if (!this.notebook || this.notebook.isClosed) {
      const first = new vscode.NotebookCellData(vscode.NotebookCellKind.Code, '', 'csharp');
      this.notebook = await vscode.workspace.openNotebookDocument(NOTEBOOK_TYPES[0], new vscode.NotebookData([first]));
    }
    await vscode.window.showNotebookDocument(this.notebook, { preview: false, preserveFocus: false });
    return this.notebook;
  }

  /** Runs the selected code (the whole line when nothing is selected) in the scratchpad. */
  async send(editor: vscode.TextEditor): Promise<void> {
    const language = CELL_LANGUAGES.has(editor.document.languageId) ? editor.document.languageId : undefined;
    if (!language) {
      void vscode.window.showWarningMessage('The scratchpad runs C#, Python and JavaScript.');
      return;
    }
    const selection = editor.selection;
    const code = selection.isEmpty ? editor.document.lineAt(selection.active.line).text : editor.document.getText(selection);
    if (!code.trim()) return;

    const notebook = await this.open();
    // an empty last cell is where the code goes; else a new one after it
    const last = notebook.cellCount > 0 ? notebook.cellAt(notebook.cellCount - 1) : undefined;
    const reuse = last !== undefined && last.kind === vscode.NotebookCellKind.Code && last.document.getText().trim() === '' && last.outputs.length === 0;
    const index = reuse ? notebook.cellCount - 1 : notebook.cellCount;

    const edit = new vscode.WorkspaceEdit();
    const cell = new vscode.NotebookCellData(vscode.NotebookCellKind.Code, code, language);
    edit.set(notebook.uri, [vscode.NotebookEdit.replaceCells(new vscode.NotebookRange(index, reuse ? index + 1 : index), [cell])]);
    await vscode.workspace.applyEdit(edit);
    void vscode.commands.executeCommand('notebook.cell.execute', { ranges: [{ start: index, end: index + 1 }], document: notebook.uri });
  }

  dispose(): void {
    this.disposables.forEach((d) => d.dispose());
  }
}
