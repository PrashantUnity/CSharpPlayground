import * as vscode from 'vscode';
import type { SidecarClient } from '../sidecar/sidecarClient';
import { languageNote } from './cellStatus';

/** The strip under a code cell: the language it runs as, when a `#!directive` makes that differ from the editor's. */
export class CellStatusBar implements vscode.NotebookCellStatusBarItemProvider, vscode.Disposable {
  private readonly changed = new vscode.EventEmitter<void>();
  readonly onDidChangeCellStatusBarItems = this.changed.event;
  private readonly disposables: vscode.Disposable[];

  constructor(private readonly sidecar: SidecarClient) {
    this.disposables = [this.changed, this.sidecar.onDidInitialize(() => this.changed.fire())];
  }

  provideCellStatusBarItems(cell: vscode.NotebookCell): vscode.NotebookCellStatusBarItem | undefined {
    if (cell.kind !== vscode.NotebookCellKind.Code) return undefined;
    const note = languageNote(cell.document.getText(), cell.document.languageId, this.sidecar.languages);
    if (!note) return undefined;
    const item = new vscode.NotebookCellStatusBarItem(note.text, vscode.NotebookCellStatusBarAlignment.Right);
    item.tooltip = note.tooltip;
    return item;
  }

  dispose(): void {
    this.disposables.forEach((d) => d.dispose());
  }
}
