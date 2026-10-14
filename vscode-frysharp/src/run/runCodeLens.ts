import * as vscode from 'vscode';
import { offersLens } from './lensRules';
import type { ScriptRuns } from './scriptRuns';

/** "Run with FrySharp" (and "Stop" while it runs) above the first line of a script, when the setting `frysharp.codeLens.enabled` is on. */
export class RunCodeLens implements vscode.CodeLensProvider, vscode.Disposable {
  private readonly changed = new vscode.EventEmitter<void>();
  readonly onDidChangeCodeLenses = this.changed.event;
  private readonly disposables: vscode.Disposable[];

  constructor(
    private readonly scripts: ScriptRuns,
    private readonly runnable: () => readonly string[]
  ) {
    this.disposables = [
      this.changed,
      this.scripts.onDidChangeRunning(() => this.changed.fire()),
      vscode.workspace.onDidChangeConfiguration((e) => {
        if (e.affectsConfiguration('frysharp.codeLens')) this.changed.fire();
      })
    ];
  }

  /** Tells the lenses the languages the engine runs have been listed. */
  refresh(): void {
    this.changed.fire();
  }

  provideCodeLenses(document: vscode.TextDocument): vscode.CodeLens[] {
    if (!vscode.workspace.getConfiguration('frysharp.codeLens').get<boolean>('enabled', false)) return [];
    if (!offersLens(document.languageId, document.fileName, document.uri.scheme, this.runnable())) return [];

    const top = new vscode.Range(0, 0, 0, 0);
    const running = this.scripts.terminalFor(document.uri)?.running === true;
    const lenses = [new vscode.CodeLens(top, { title: running ? '$(sync~spin) Running…' : '$(play) Run with FrySharp', command: running ? '' : 'frysharp.runScript', arguments: [document.uri] })];
    if (running) lenses.push(new vscode.CodeLens(top, { title: '$(debug-stop) Stop', command: 'frysharp.stopRun' }));
    return lenses;
  }

  dispose(): void {
    this.disposables.forEach((d) => d.dispose());
  }
}
