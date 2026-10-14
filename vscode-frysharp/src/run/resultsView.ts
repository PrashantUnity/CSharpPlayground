import * as vscode from 'vscode';
import { ResultItem, resultsHtml } from './resultsPage';

const KEPT = 50;

/** What the Results panel's page says each time it has drawn an item (or could not). */
export interface ResultsReport {
  type: 'rendered' | 'error';
  id: number;
  mime?: string;
  message?: string;
}

/** The Results panel (the desktop's .Dump deck): what programs show that is not text, newest last, until cleared. */
export class ResultsView implements vscode.WebviewViewProvider, vscode.Disposable {
  static readonly viewType = 'frysharp.results';
  private view?: vscode.WebviewView;
  private items: ResultItem[] = [];

  /** What the page reported drawing, for tests. */
  readonly reports: ResultsReport[] = [];

  constructor(private readonly extensionUri: vscode.Uri) {}

  resolveWebviewView(view: vscode.WebviewView): void {
    this.view = view;
    const dist = vscode.Uri.joinPath(this.extensionUri, 'dist');
    view.webview.options = { enableScripts: true, localResourceRoots: [dist] };
    view.webview.html = resultsHtml(this.items, {
      rendererUri: view.webview.asWebviewUri(vscode.Uri.joinPath(dist, 'renderer.js')).toString(),
      cspSource: view.webview.cspSource
    });
    view.webview.onDidReceiveMessage((message: ResultsReport) => this.reports.push(message));
    view.onDidDispose(() => (this.view = undefined));
  }

  /** Shows an item, opening the panel if it is not open. */
  async add(item: ResultItem): Promise<void> {
    this.items.push(item);
    if (this.items.length > KEPT) this.items.splice(0, this.items.length - KEPT);

    if (!this.view) {
      await vscode.commands.executeCommand(`${ResultsView.viewType}.focus`, { preserveFocus: true });
      return; // the view draws what has been added when it opens
    }
    this.view.show?.(true);
    await this.view.webview.postMessage({ type: 'add', item });
  }

  clear(): void {
    this.items = [];
    void this.view?.webview.postMessage({ type: 'clear' });
  }

  get count(): number {
    return this.items.length;
  }

  dispose(): void {
    this.view = undefined;
  }
}
