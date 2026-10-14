import * as path from 'path';
import * as vscode from 'vscode';
import { LineEditor } from './lineEditor';
import type { ScriptRuns } from './scriptRuns';

const DIM = '\x1b[90m';
const GREEN = '\x1b[32m';
const RED = '\x1b[31m';
const CYAN = '\x1b[36m';
const RESET = '\x1b[0m';

/** The terminal of one script: its output as it comes, with the program's own colours; a line typed goes to the program. */
export class RunTerminal implements vscode.Pseudoterminal {
  private readonly writeEmitter = new vscode.EventEmitter<string>();
  private readonly closeEmitter = new vscode.EventEmitter<number | void>();
  private readonly editor = new LineEditor();
  private current?: { runId: string; started: number };
  private finished = true;
  private everRan = false;

  readonly onDidWrite = this.writeEmitter.event;
  readonly onDidClose = this.closeEmitter.event;

  /** The URIs the last run marked in the Problems panel. */
  marked: vscode.Uri[] = [];

  /** Everything written to the terminal (the last megabyte): what a test, or a bug report, can read. */
  transcript = '';

  constructor(
    private readonly runs: ScriptRuns,
    readonly file: vscode.Uri,
    private arguments_: string[]
  ) {}

  get running(): boolean {
    return !this.finished;
  }

  get runId(): string | undefined {
    return this.current?.runId;
  }

  // ── Pseudoterminal ────────────────────────────────────────────────────────────────────────────────────────────

  open(): void {
    void this.start();
  }

  close(): void {
    this.runs.closed(this);
    if (this.current && !this.finished) void this.runs.stop(this.current.runId);
  }

  handleInput(data: string): void {
    if (this.finished) {
      // a finished program: any key runs it again
      if (this.everRan) void this.restart();
      return;
    }
    const typed = this.editor.feed(data);
    if (typed.echo) this.write(typed.echo);
    for (const line of typed.lines) void this.send(line);
    if (typed.interrupt && this.current) {
      this.write(`${DIM}^C${RESET}\r\n`);
      void this.runs.stop(this.current.runId);
    }
  }

  // ── Runs ──────────────────────────────────────────────────────────────────────────────────────────────────────

  /** Runs the script again, in this terminal, from a clean screen. */
  async restart(arguments_?: string[]): Promise<void> {
    if (arguments_) this.arguments_ = arguments_;
    if (this.current && !this.finished) {
      await this.runs.stop(this.current.runId);
      await this.waitUntilFinished();
    }
    this.write('\x1b[2J\x1b[3J\x1b[H'); // clear the screen and what scrolled off it
    await this.start();
  }

  private async start(): Promise<void> {
    this.editor.reset();
    this.everRan = true;
    this.finished = false;
    const label = [path.basename(this.file.fsPath), ...this.arguments_].join(' ');
    this.write(`${DIM}▶ ${label}${RESET}\r\n`);
    try {
      const runId = await this.runs.begin(this, this.arguments_);
      this.current = { runId, started: Date.now() };
    } catch (error) {
      this.write(`${RED}${error instanceof Error ? error.message : String(error)}${RESET}\r\n`);
      this.end();
    }
  }

  private async send(line: string): Promise<void> {
    if (!this.current) return;
    try {
      await this.runs.sendInput(this.current.runId, line);
    } catch (error) {
      this.write(`${RED}${error instanceof Error ? error.message : String(error)}${RESET}\r\n`);
    }
  }

  private waitUntilFinished(): Promise<void> {
    return new Promise((resolve) => {
      const started = Date.now();
      const check = () => (this.finished || Date.now() - started > 10_000 ? resolve() : setTimeout(check, 50));
      check();
    });
  }

  // ── What the engine says about the run ────────────────────────────────────────────────────────────────────────

  write(text: string): void {
    this.transcript = (this.transcript + text).slice(-1_000_000);
    this.writeEmitter.fire(text);
  }

  shown(summary: string): void {
    this.write(`${CYAN}▸ Shown in FrySharp Results: ${summary}${RESET}\r\n`);
  }

  exited(exitCode: number, durationMs: number): void {
    const seconds = durationMs < 1000 ? `${durationMs} ms` : `${(durationMs / 1000).toFixed(1)} s`;
    const colour = exitCode === 0 ? GREEN : RED;
    this.write(`\r\n${colour}${exitCode === 0 ? '✓' : '✗'} exited with code ${exitCode}${RESET}${DIM} in ${seconds} · press any key to run again${RESET}\r\n`);
    this.end();
  }

  private end(): void {
    this.finished = true;
    this.current = undefined;
    this.runs.changed();
  }
}
