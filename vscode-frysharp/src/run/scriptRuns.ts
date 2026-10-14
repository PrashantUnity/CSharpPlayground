import * as fs from 'fs';
import * as path from 'path';
import * as vscode from 'vscode';
import { RunTerminal } from './runTerminal';
import { findLinks } from './runLinks';
import { itemFromDisplay } from './resultsPage';
import { ResultsView } from './resultsView';
import { SidecarClient } from '../sidecar/sidecarClient';

/**
 * Running a script is a terminal (the program's text, colours, typed input, Stop), problems in the Problems panel, and a
 * Results panel for what is not text, as the desktop's bottom deck has them. One terminal for each file, used again each
 * time it runs.
 */
export class ScriptRuns implements vscode.Disposable {
  private readonly terminals = new Map<string, { terminal: vscode.Terminal; run: RunTerminal }>();
  private readonly byRunId = new Map<string, RunTerminal>();
  private readonly problems = vscode.languages.createDiagnosticCollection('frysharp-run');
  private readonly disposables: vscode.Disposable[] = [];
  private counter = 0;
  private readonly changedEmitter = new vscode.EventEmitter<void>();

  /** A run started or ended. */
  readonly onDidChangeRunning = this.changedEmitter.event;

  constructor(
    private readonly sidecar: SidecarClient,
    readonly results: ResultsView
  ) {
    this.disposables.push(
      this.problems,
      this.changedEmitter,
      sidecar.onRunOutput((n) => this.byRunId.get(n.runId)?.write(n.text)),
      sidecar.onRunDisplay((n) => this.display(n.runId, n.data)),
      sidecar.onRunExited((n) => this.exited(n.runId, n.exitCode, n.durationMs, n.problems)),
      vscode.window.registerTerminalLinkProvider({
        provideTerminalLinks: (context) => this.links(context),
        handleTerminalLink: (link) => this.open(link as OutputTerminalLink)
      }),
      vscode.window.onDidCloseTerminal((closed) => {
        for (const [file, entry] of this.terminals) if (entry.terminal === closed) this.terminals.delete(file);
      })
    );
  }

  /** The terminal a file's runs are in, if it has run. */
  terminalFor(uri: vscode.Uri): RunTerminal | undefined {
    return this.terminals.get(uri.fsPath)?.run;
  }

  /** The number of programs running. */
  get running(): number {
    return [...this.terminals.values()].filter((entry) => entry.run.running).length;
  }

  // ── Starting ──────────────────────────────────────────────────────────────────────────────────────────────────

  /** Runs a file in its terminal (a new one the first time, the same one after), asking nothing. */
  async run(uri: vscode.Uri, extraArguments?: string[]): Promise<void> {
    if (!vscode.workspace.isTrusted) {
      void vscode.window.showWarningMessage('Running code is off until you trust this workspace.');
      return;
    }

    const config = vscode.workspace.getConfiguration('frysharp.run');
    if (config.get<boolean>('saveBeforeRun', true)) {
      const open = vscode.workspace.textDocuments.find((document) => document.uri.toString() === uri.toString());
      if (open?.isDirty) await open.save();
    }

    const arguments_ = extraArguments ?? config.get<string[]>('arguments', []);
    const existing = this.terminals.get(uri.fsPath);
    if (existing) {
      existing.terminal.show(true);
      await existing.run.restart(arguments_);
      return;
    }

    const run = new RunTerminal(this, uri, arguments_);
    const terminal = vscode.window.createTerminal({ name: `FrySharp: ${path.basename(uri.fsPath)}`, pty: run, iconPath: new vscode.ThemeIcon('play') });
    this.terminals.set(uri.fsPath, { terminal, run });
    terminal.show(true);
  }

  /** What `RunTerminal` calls to start its run: the engine's answer is the run's id. */
  async begin(run: RunTerminal, arguments_: string[]): Promise<string> {
    const init = await this.sidecar.ensureStarted();
    const extension = path.extname(run.file.fsPath).toLowerCase();
    const language = init.languages.find((l) => l.canRun && l.extensions.some((e) => e.toLowerCase() === extension));
    if (!language) {
      const runnable = init.languages.filter((l) => l.canRun).flatMap((l) => l.extensions).join(' ');
      throw new Error(`FrySharp does not run ${extension || 'this'} files (it runs ${runnable}).`);
    }

    this.clearProblems(run);
    const runId = `run-${++this.counter}-${Date.now().toString(36)}`;
    this.byRunId.set(runId, run);
    this.changed();

    const relative = path.relative(init.workspaceRoot, run.file.fsPath).replace(/\\/g, '/');
    const inside = relative !== '' && !relative.startsWith('..') && !path.isAbsolute(relative);
    try {
      if (inside) {
        await this.sidecar.startRun({ runId, path: relative, arguments: arguments_ });
      } else if (language.id === 'csharp') {
        // outside the folder the engine works in: C# can still run as text
        const document = await vscode.workspace.openTextDocument(run.file);
        await this.sidecar.startRun({ runId, code: document.getText(), languageId: 'csharp', arguments: arguments_ });
      } else {
        throw new Error(`FrySharp runs ${language.displayName} files inside the folder it works in (${init.workspaceRoot}); this one is outside it.`);
      }
    } catch (error) {
      this.byRunId.delete(runId);
      throw error;
    }
    return runId;
  }

  sendInput(runId: string, line: string): Promise<void> {
    return this.sidecar.runInput(runId, line);
  }

  async stop(runId: string): Promise<void> {
    await this.sidecar.stopRun(runId).catch(() => false);
  }

  /** Stops the program of the terminal in front, or every one that runs when none is. */
  async stopActive(): Promise<void> {
    const active = vscode.window.activeTerminal;
    const entries = [...this.terminals.values()];
    const targets = entries.filter((entry) => entry.run.running && entry.terminal === active);
    for (const entry of targets.length > 0 ? targets : entries.filter((e) => e.run.running)) {
      if (entry.run.runId) await this.stop(entry.run.runId);
    }
  }

  /** Runs the program of the terminal in front again; or the last one that ran. */
  async rerunActive(): Promise<void> {
    const active = vscode.window.activeTerminal;
    const entry = [...this.terminals.values()].find((e) => e.terminal === active) ?? [...this.terminals.values()].pop();
    if (entry) await entry.run.restart();
  }

  closed(run: RunTerminal): void {
    this.terminals.delete(run.file.fsPath);
    this.changed();
  }

  /** Tells the rest of VS Code whether something is running (the Stop key only works while it is). */
  changed(): void {
    void vscode.commands.executeCommand('setContext', 'frysharp.runActive', this.running > 0);
    this.changedEmitter.fire();
  }

  // ── What the engine says ──────────────────────────────────────────────────────────────────────────────────────

  private display(runId: string, data: Record<string, unknown>): void {
    const run = this.byRunId.get(runId);
    if (!run) return;
    const title = `${path.basename(run.file.fsPath)}, ${new Date().toLocaleTimeString()}`;
    const item = itemFromDisplay(this.counter * 1000 + this.results.count, title, data);
    void this.results.add(item);
    run.shown(typeof data['text/plain'] === 'string' ? data['text/plain'].split('\n')[0] : 'a display');
  }

  private exited(runId: string, exitCode: number, durationMs: number, problems: Array<{ path?: string; severity: string; code: string; message: string; line: number; column: number }>): void {
    const run = this.byRunId.get(runId);
    if (!run) return;
    this.byRunId.delete(runId);

    const root = this.sidecar.workspaceRoot;
    const byFile = new Map<string, vscode.Diagnostic[]>();
    for (const problem of problems) {
      const file = problem.path && root ? path.resolve(root, problem.path) : run.file.fsPath;
      const line = Math.max(0, problem.line - 1);
      const column = Math.max(0, problem.column - 1);
      const severity = problem.severity === 'warning' ? vscode.DiagnosticSeverity.Warning : problem.severity === 'info' ? vscode.DiagnosticSeverity.Information : vscode.DiagnosticSeverity.Error;
      const diagnostic = new vscode.Diagnostic(new vscode.Range(line, column, line, column + 1), problem.message, severity);
      diagnostic.code = problem.code;
      diagnostic.source = 'FrySharp';
      byFile.set(file, [...(byFile.get(file) ?? []), diagnostic]);
    }
    for (const [file, diagnostics] of byFile) {
      const uri = vscode.Uri.file(file);
      this.problems.set(uri, diagnostics);
      run.marked.push(uri);
    }
    run.exited(exitCode, durationMs);
  }

  private clearProblems(run: RunTerminal): void {
    for (const uri of run.marked) this.problems.delete(uri);
    run.marked = [];
  }

  // ── Places named in the output ────────────────────────────────────────────────────────────────────────────────

  private links(context: vscode.TerminalLinkContext): OutputTerminalLink[] {
    const owner = [...this.terminals.values()].find((entry) => entry.terminal === context.terminal);
    if (!owner) return [];

    const folders = [path.dirname(owner.run.file.fsPath), this.sidecar.workspaceRoot, ...(vscode.workspace.workspaceFolders ?? []).map((f) => f.uri.fsPath)];
    const links: OutputTerminalLink[] = [];
    for (const place of findLinks(context.line)) {
      const candidates = path.isAbsolute(place.file) ? [place.file] : folders.filter((f): f is string => !!f).map((folder) => path.resolve(folder, place.file));
      const found = candidates.find((candidate) => fs.existsSync(candidate));
      if (found) links.push({ startIndex: place.start, length: place.length, tooltip: 'Open in the editor', file: found, line: place.line, column: place.column });
    }
    return links;
  }

  private async open(link: OutputTerminalLink): Promise<void> {
    const position = new vscode.Position(link.line - 1, link.column - 1);
    await vscode.window.showTextDocument(vscode.Uri.file(link.file), { selection: new vscode.Range(position, position) });
  }

  dispose(): void {
    for (const { terminal } of this.terminals.values()) terminal.dispose();
    this.terminals.clear();
    this.disposables.forEach((d) => d.dispose());
  }
}

interface OutputTerminalLink extends vscode.TerminalLink {
  file: string;
  line: number;
  column: number;
}
