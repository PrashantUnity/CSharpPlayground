// The output of a cell while it runs. A cell that only prints (or only shows one kind of thing) has its output as VS Code
// shows it natively: text added to one stdout output, a table as an output of its own. The moment it makes a second kind (printed
// text and a table, a chart and text), everything it has made becomes the one output the tabs are drawn from (./format/tabs), and
// stays one; from then on it is rewritten as more arrives, at most a few times a second, with the end of a long console only until
// the cell has finished (then all of it). No VS Code in here: the places the output goes are an OutputSink.

import { CellOutput, OutputItem, STDOUT, textItem } from './format/model';
import { outputFromDisplay } from './format/outputs';
import { canPack, pack } from './format/tabs';

/** Where the output goes: VS Code's execution for a cell, or a recording of what was asked in a test. A handle names one output. */
export interface OutputSink<Handle> {
  append(output: CellOutput): Promise<Handle>;
  appendItems(handle: Handle, items: OutputItem[]): Promise<void>;
  replaceItems(handle: Handle, items: OutputItem[]): Promise<void>;
  /** Replaces every output of the cell with these. */
  replaceAll(outputs: CellOutput[]): Promise<Handle[]>;
}

/** What the engine said while the cell ran. */
export type Said =
  | { kind: 'stream'; text: string }
  | { kind: 'display'; data: Record<string, unknown>; metadata?: Record<string, unknown>; displayId?: string; update?: boolean };

export interface LiveOptions {
  /** Whether a cell with several kinds of output shows them as tabs. */
  tabs: boolean;
  /** The fewest milliseconds between two rewrites of the tabbed output while the cell runs. */
  throttleMs?: number;
  /** How many lines at the end of a console the tabbed output carries while the cell runs (all of it at the end). */
  liveConsoleLines?: number;
  /** For a test: runs a function after a delay and says how to cancel it. */
  schedule?: (run: () => void, ms: number) => () => void;
}

type Entry = { kind: 'stream'; chunks: string[] } | { kind: 'output'; output: CellOutput };

const stdoutOutput = (text: string): CellOutput => ({ items: [textItem(STDOUT, text)] });

export class LiveOutputs<Handle> {
  private readonly entries: Entry[] = [];
  private readonly handles: Array<Handle | undefined> = [];
  private readonly displays = new Map<string, number>();
  private packed: Handle | undefined;
  private timer: (() => void) | undefined;
  private chain: Promise<unknown> = Promise.resolve();
  private dirty = false;

  constructor(
    private readonly sink: OutputSink<Handle>,
    private readonly options: LiveOptions
  ) {}

  /** Whether everything is one output now. */
  get isTabbed(): boolean {
    return this.packed !== undefined;
  }

  /** Everything the cell has made so far, as outputs. */
  outputs(): CellOutput[] {
    return this.entries.map((entry) => (entry.kind === 'stream' ? stdoutOutput(entry.chunks.join('')) : entry.output));
  }

  /** Takes in what the engine said, in order: one at a time (the next waits for the last to be shown). */
  show(said: Said): Promise<void> {
    return this.serial(() => this.take(said));
  }

  /** The cell has ended: whatever was waiting to be shown is, and a console cut short for the run is whole. */
  finish(): Promise<void> {
    return this.serial(async () => {
      this.cancelTimer();
      if (this.packed !== undefined) await this.rewrite(false);
    });
  }

  // ── one thing at a time ─────────────────────────────────────────────────────────────────────────────────────────

  private serial(task: () => Promise<void>): Promise<void> {
    const next = this.chain.then(task, task);
    this.chain = next.catch(() => undefined);
    return next;
  }

  private async take(said: Said): Promise<void> {
    if (said.kind === 'stream' && !said.text) return;
    // what the cell has made now, and what it takes to show that natively (not done if it is about to become tabs: nothing flickers)
    const native = said.kind === 'stream' ? this.applyText(said.text) : this.applyDisplay(said);

    if (this.packed === undefined && this.options.tabs && canPack(this.outputs())) {
      // the second kind of thing: all of it is one output from here on
      this.handles.length = 0;
      this.packed = (await this.sink.replaceAll([pack(this.outputs(), { maxConsoleLines: this.liveLines })]))[0];
    } else if (this.packed === undefined) {
      await native();
    } else {
      this.dirty = true;
      this.schedule();
    }
  }

  private get liveLines(): number {
    return this.options.liveConsoleLines ?? 2000;
  }

  private applyText(text: string): () => Promise<void> {
    const last = this.entries[this.entries.length - 1];
    if (last?.kind === 'stream') {
      last.chunks.push(text);
      const index = this.entries.length - 1;
      return async () => this.sink.appendItems(this.handles[index] as Handle, [textItem(STDOUT, text)]);
    }

    this.entries.push({ kind: 'stream', chunks: [text] });
    const index = this.entries.length - 1;
    return async () => {
      this.handles[index] = await this.sink.append(stdoutOutput(text));
    };
  }

  private applyDisplay(said: Extract<Said, { kind: 'display' }>): () => Promise<void> {
    const output = outputFromDisplay(said.data, said.metadata);
    const earlier = said.update && said.displayId ? this.displays.get(said.displayId) : undefined;
    if (earlier !== undefined) {
      // a chart the program redraws is redrawn where it is
      this.entries[earlier] = { kind: 'output', output };
      return async () => this.sink.replaceItems(this.handles[earlier] as Handle, output.items);
    }

    this.entries.push({ kind: 'output', output });
    const index = this.entries.length - 1;
    if (said.displayId) this.displays.set(said.displayId, index);
    return async () => {
      this.handles[index] = await this.sink.append(output);
    };
  }

  // ── the tabbed output, rewritten as it grows ────────────────────────────────────────────────────────────────────

  private schedule(): void {
    if (this.timer) return;
    const run = () => {
      this.timer = undefined;
      void this.serial(() => this.rewrite(true));
    };
    const ms = this.options.throttleMs ?? 200;
    this.timer = this.options.schedule ? this.options.schedule(run, ms) : ((handle) => () => clearTimeout(handle))(setTimeout(run, ms));
  }

  private cancelTimer(): void {
    this.timer?.();
    this.timer = undefined;
  }

  private async rewrite(live: boolean): Promise<void> {
    if (this.packed === undefined || (!this.dirty && live)) return;
    this.dirty = false;
    const outputs = this.outputs();
    await this.sink.replaceItems(this.packed, pack(outputs, live ? { maxConsoleLines: this.liveLines } : {}).items);
  }
}
