import * as vscode from 'vscode';
import { isFryNotebook } from './notebookController';
import { RunHub } from './runHub';
import { SidecarClient } from '../sidecar/sidecarClient';

/** What the status bar says about the engine, for the state it is in. */
export function describeEngine(state: 'stopped' | 'starting' | 'ready' | 'crashed', running: number): { text: string; tooltip: string } {
  switch (state) {
    case 'starting':
      return { text: '$(sync~spin) FrySharp: starting', tooltip: 'The FrySharp engine is starting.' };
    case 'crashed':
      return { text: '$(error) FrySharp: stopped', tooltip: 'The FrySharp engine stopped. It starts again when you run a cell; variables are gone.' };
    case 'ready':
      return running > 0
        ? { text: `$(loading~spin) FrySharp: running${running > 1 ? ` (${running})` : ''}`, tooltip: 'A cell is running.' }
        : { text: '$(check) FrySharp: ready', tooltip: 'The FrySharp engine is ready.' };
    default:
      return { text: '$(circle-large-outline) FrySharp', tooltip: 'The FrySharp engine starts when a notebook opens or a cell runs.' };
  }
}

/** One item that says whether the engine is ready, starting, busy or gone, while a FrySharp notebook is in front; a click opens its menu. */
export class EngineStatusBar implements vscode.Disposable {
  private readonly item = vscode.window.createStatusBarItem('frysharp.engine', vscode.StatusBarAlignment.Left, 100);
  private readonly disposables: vscode.Disposable[] = [];

  constructor(
    private readonly sidecar: SidecarClient,
    private readonly hub: RunHub
  ) {
    this.item.name = 'FrySharp Engine';
    this.item.command = 'frysharp.engineMenu';
    this.disposables.push(
      this.item,
      sidecar.onDidChangeState(() => this.update()),
      sidecar.onDidInitialize(() => this.update()),
      hub.onDidChangeRunning(() => this.update()),
      vscode.window.onDidChangeActiveNotebookEditor(() => this.update())
    );
    this.update();
  }

  /** The text on show: for tests, and nothing else. */
  get text(): string {
    return this.item.text;
  }

  private update(): void {
    const notebook = vscode.window.activeNotebookEditor?.notebook;
    if (!notebook || !isFryNotebook(notebook)) {
      this.item.hide();
      return;
    }
    const { text, tooltip } = describeEngine(this.sidecar.state, this.hub.running);
    this.item.text = text;
    this.item.tooltip = this.sidecar.serverVersion ? `${tooltip}\nEngine ${this.sidecar.serverVersion}: ${this.sidecar.languages.filter((l) => l.canRunCells).map((l) => l.displayName).join(', ')}` : tooltip;
    this.item.backgroundColor = this.sidecar.state === 'crashed' ? new vscode.ThemeColor('statusBarItem.errorBackground') : undefined;
    this.item.show();
  }

  dispose(): void {
    this.disposables.forEach((d) => d.dispose());
  }
}
