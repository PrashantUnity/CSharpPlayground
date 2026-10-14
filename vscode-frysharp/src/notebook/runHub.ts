import * as path from 'path';
import * as vscode from 'vscode';
import { newId } from './format/model';
import { placeDiagnostics } from './cellDiagnostics';
import type { CellProblems } from '../language/cellProblems';
import { NotebookIdentity } from './identity';
import { LiveOutputs, OutputSink } from './liveOutputs';
import { outputTabsEnabled } from './outputSettings';
import { CellOutput, OutputItem } from './format/model';
import { toVsCodeOutput } from './notebookSerializer';
import { SequencedQueue } from './sequencedQueue';
import { DiagnosticDto, InputRequest, NotebookOutputNotification } from '../sidecar/protocol';
import { SidecarClient } from '../sidecar/sidecarClient';

type Heard = { kind: 'output'; notification: NotebookOutputNotification } | { kind: 'state' };

/** One run of one cell, from the start of its execution to the end of it. */
export interface Run {
  id: string;
  execution: vscode.NotebookCellExecution;
  cell: vscode.NotebookCell;
  identity: NotebookIdentity;
  /** What the cell has made, and how it is shown: as outputs one after another, or (when there are several kinds) as one output drawn as tabs. */
  live: LiveOutputs<vscode.NotebookCellOutput>;
  queue: SequencedQueue<Heard>;
}

/**
 * What the engine says about the cells that are running, and what comes back from the person using them: the output
 * (in order, however it arrives), the input a cell asks for, and the places in a cell's code where it went wrong. Shared
 * by the controllers of every notebook type, so a run is found by its id wherever it began.
 */
export class RunHub implements vscode.Disposable {
  private readonly runs = new Map<string, Run>();
  private readonly runningEmitter = new vscode.EventEmitter<number>();
  private readonly endedEmitter = new vscode.EventEmitter<NotebookIdentity>();
  private readonly disposables: vscode.Disposable[];

  /** The number of cells running (in all notebooks), when it changes. */
  readonly onDidChangeRunning = this.runningEmitter.event;

  /** A cell finished (or failed): the notebook it ran in, whose variables may have changed. */
  readonly onDidEndRun = this.endedEmitter.event;

  constructor(
    sidecar: SidecarClient,
    private readonly problems: CellProblems
  ) {
    this.disposables = [
      sidecar.onNotebookOutput((n) => this.heard(n.executionId, n.seq, { kind: 'output', notification: n })),
      sidecar.onCellState((n) => this.heard(n.executionId, n.seq, { kind: 'state' })),
      // an error marked when a cell ran is for the code as it was run: gone when the code changes (the live check marks what is wrong now)
      vscode.workspace.onDidChangeTextDocument((e) => this.problems.clearRun(e.document.uri)),
      this.runningEmitter,
      this.endedEmitter
    ];
    sidecar.setInputHandler((request) => this.askForInput(request));
  }

  get running(): number {
    return this.runs.size;
  }

  begin(execution: vscode.NotebookCellExecution, cell: vscode.NotebookCell, identity: NotebookIdentity): Run {
    this.problems.clearRun(cell.document.uri);
    const run: Run = {
      id: newId(),
      execution,
      cell,
      identity,
      live: new LiveOutputs(new ExecutionSink(execution), { tabs: outputTabsEnabled() }),
      queue: new SequencedQueue<Heard>((heard) => this.show(run, heard))
    };
    this.runs.set(run.id, run);
    this.runningEmitter.fire(this.runs.size);
    return run;
  }

  end(run: Run): void {
    this.runs.delete(run.id);
    this.runningEmitter.fire(this.runs.size);
    this.endedEmitter.fire(run.identity);
  }

  // ── What the engine says while a cell runs ─────────────────────────────────────────────────────────────────────

  private heard(executionId: string | undefined, seq: number | undefined, what: Heard): void {
    const run = executionId ? this.runs.get(executionId) : undefined;
    run?.queue.push(seq, what);
  }

  private async show(run: Run, heard: Heard): Promise<void> {
    if (heard.kind !== 'output') return;
    const output = heard.notification.output;

    if (output.kind === 'stream' && output.text) {
      await run.live.show({ kind: 'stream', text: output.text });
    } else if (output.kind === 'display' && output.data) {
      await run.live.show({ kind: 'display', data: output.data as Record<string, unknown>, metadata: output.metadata as Record<string, unknown> | undefined, displayId: output.displayId, update: output.update });
    }
  }

  /** The cell has ended: what was waiting to be shown is, and a console cut short while it ran is whole. */
  finish(run: Run): Promise<void> {
    return run.live.finish();
  }

  // ── Input ──────────────────────────────────────────────────────────────────────────────────────────────────────

  private async askForInput(request: InputRequest): Promise<string | null> {
    const run = request.executionId ? this.runs.get(request.executionId) : undefined;
    const where = run ? `${path.basename(run.identity.path)}, cell ${run.cell.index + 1}` : 'a notebook cell';
    const answer = await vscode.window.showInputBox(
      {
        title: `FrySharp: input for ${where}`,
        prompt: request.prompt || 'The cell is waiting for a line of input.',
        placeHolder: 'Enter sends the line; Escape is the end of input',
        password: request.password,
        ignoreFocusOut: true
      },
      run?.execution.token
    );
    return answer === undefined ? null : answer; // Escape, or Stop: the cell reads the end of input
  }

  // ── Where a cell went wrong ────────────────────────────────────────────────────────────────────────────────────

  markErrors(cell: vscode.NotebookCell, diagnostics: DiagnosticDto[] | undefined): void {
    const placed = placeDiagnostics(diagnostics, cell.document.getText());
    if (placed.length === 0) return;

    const severity = { error: vscode.DiagnosticSeverity.Error, warning: vscode.DiagnosticSeverity.Warning, information: vscode.DiagnosticSeverity.Information };
    this.problems.setRun(
      cell.document.uri,
      placed.map((p) => {
        const diagnostic = new vscode.Diagnostic(new vscode.Range(p.startLine, p.startCharacter, p.endLine, p.endCharacter), p.message, severity[p.severity]);
        diagnostic.code = p.code;
        diagnostic.source = 'FrySharp';
        return diagnostic;
      })
    );
  }

  dispose(): void {
    this.disposables.forEach((d) => d.dispose());
  }
}

/** Where a run's output goes: the cell's execution in VS Code. */
class ExecutionSink implements OutputSink<vscode.NotebookCellOutput> {
  constructor(private readonly execution: vscode.NotebookCellExecution) {}

  async append(output: CellOutput): Promise<vscode.NotebookCellOutput> {
    const made = toVsCodeOutput(output);
    await this.execution.appendOutput(made);
    return made;
  }

  async appendItems(handle: vscode.NotebookCellOutput, items: OutputItem[]): Promise<void> {
    await this.execution.appendOutputItems(items.map(toItem), handle);
  }

  async replaceItems(handle: vscode.NotebookCellOutput, items: OutputItem[]): Promise<void> {
    await this.execution.replaceOutputItems(items.map(toItem), handle);
  }

  async replaceAll(outputs: CellOutput[]): Promise<vscode.NotebookCellOutput[]> {
    const made = outputs.map(toVsCodeOutput);
    await this.execution.replaceOutput(made);
    return made;
  }
}

const toItem = (item: OutputItem): vscode.NotebookCellOutputItem => new vscode.NotebookCellOutputItem(item.data, item.mime);
