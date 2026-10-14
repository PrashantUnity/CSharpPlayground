import * as vscode from 'vscode';

/**
 * The problems marked in a notebook cell's code, from two places: what the engine finds as the code is typed (live), and what
 * it found when the cell last ran (run). One list is shown, with a problem both found shown once.
 */
export class CellProblems implements vscode.Disposable {
  private readonly collection = vscode.languages.createDiagnosticCollection('frysharp');
  private readonly live = new Map<string, vscode.Diagnostic[]>();
  private readonly run = new Map<string, vscode.Diagnostic[]>();

  setLive(uri: vscode.Uri, diagnostics: vscode.Diagnostic[]): void {
    this.live.set(uri.toString(), diagnostics);
    this.publish(uri);
  }

  setRun(uri: vscode.Uri, diagnostics: vscode.Diagnostic[]): void {
    this.run.set(uri.toString(), diagnostics);
    this.publish(uri);
  }

  clearRun(uri: vscode.Uri): void {
    if (this.run.delete(uri.toString())) this.publish(uri);
  }

  /** Forgets a cell altogether (it was closed). */
  forget(uri: vscode.Uri): void {
    this.live.delete(uri.toString());
    this.run.delete(uri.toString());
    this.collection.delete(uri);
  }

  private publish(uri: vscode.Uri): void {
    const key = uri.toString();
    const merged = new Map<string, vscode.Diagnostic>();
    for (const diagnostic of [...(this.live.get(key) ?? []), ...(this.run.get(key) ?? [])]) {
      const where = `${diagnostic.range.start.line}:${diagnostic.range.start.character}:${String(diagnostic.code)}`;
      if (!merged.has(where)) merged.set(where, diagnostic);
    }
    this.collection.set(uri, [...merged.values()]);
  }

  dispose(): void {
    this.collection.dispose();
  }
}
