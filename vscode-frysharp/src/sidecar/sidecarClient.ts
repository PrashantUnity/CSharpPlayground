import * as cp from 'child_process';
import * as rpc from 'vscode-jsonrpc/node';
import { Emitter } from './events';
import {
  CellDiagnosticsParams,
  CellDiagnosticsResult,
  CellPositionParams,
  CellStateNotification,
  CompletionResult,
  EngineInfo,
  ExecuteCellParams,
  ExecuteCellResult,
  InitializeParams,
  InitializeResult,
  HoverResult,
  InputRequest,
  LanguageInfo,
  NotebookOutputNotification,
  NotebookRef,
  RunDisplayNotification,
  RunExitedNotification,
  RunOutputNotification,
  RunStartParams,
  RunStartResult,
  VariableDto
} from './protocol';

/** This extension speaks version 1 of the session protocol (docs/session-protocol.md). */
export const PROTOCOL_VERSION = 1;

export type SidecarState = 'stopped' | 'starting' | 'ready' | 'crashed';

export interface SidecarProcess {
  command: string;
  args: string[];
  cwd: string;
  /** Which sidecar this is and where it came from, for people looking for why something does not work. */
  description?: string;
}

export interface SidecarClientOptions {
  /** What to run, worked out each time the engine starts (the .NET to use, the sidecar to run). */
  prepare(): Promise<SidecarProcess>;
  log(message: string): void;
  clientName?: string;
}

/** Something that can be told to stop waiting (a VS Code CancellationToken is one). */
export interface CancellationLike {
  isCancellationRequested: boolean;
  onCancellationRequested(listener: () => void): { dispose(): void };
}

export interface RequestOptions {
  /** When it is cancelled the engine is told to stop, and the request is rejected. */
  token?: CancellationLike;
  /** How long to wait for the answer; 0 waits as long as it takes (a cell). Default 30 s. */
  timeoutMs?: number;
}

const DEFAULT_TIMEOUT_MS = 30_000;

/**
 * The extension's connection to the engine: JSON-RPC over the sidecar's stdio. It starts the engine when first asked,
 * starts it again after it died (its notebooks' kernels are gone then, which `onDidLoseState` says), and gives every
 * request a deadline. It imports nothing of VS Code, so it is tested in plain Node against the real engine.
 */
export class SidecarClient {
  private child?: cp.ChildProcess;
  private connection?: rpc.MessageConnection;
  private starting?: Promise<InitializeResult>;
  private initialized?: InitializeResult;
  private disposed = false;
  private currentState: SidecarState = 'stopped';
  private everStarted = false;
  private launched?: SidecarProcess;

  private inputHandler?: (request: InputRequest) => Promise<string | null>;

  private readonly stateEmitter = new Emitter<SidecarState>();
  private readonly initializedEmitter = new Emitter<InitializeResult>();
  private readonly lostStateEmitter = new Emitter<{ reason: string }>();
  private readonly notebookOutputEmitter = new Emitter<NotebookOutputNotification>();
  private readonly cellStateEmitter = new Emitter<CellStateNotification>();
  private readonly runOutputEmitter = new Emitter<RunOutputNotification>();
  private readonly runExitedEmitter = new Emitter<RunExitedNotification>();
  private readonly runDisplayEmitter = new Emitter<RunDisplayNotification>();
  private readonly runInputRequestedEmitter = new Emitter<{ runId: string }>();

  readonly onDidChangeState = this.stateEmitter.event;
  /** The engine started and said what it can do (its languages): once for each start. */
  readonly onDidInitialize = this.initializedEmitter.event;
  /** The engine stopped on its own (once, when it did): every notebook's kernels, and their variables, are gone. A restart asked for does not say it. */
  readonly onDidLoseState = this.lostStateEmitter.event;
  readonly onNotebookOutput = this.notebookOutputEmitter.event;
  readonly onCellState = this.cellStateEmitter.event;
  readonly onRunOutput = this.runOutputEmitter.event;
  readonly onRunExited = this.runExitedEmitter.event;
  readonly onRunDisplay = this.runDisplayEmitter.event;
  /** A running program waits for a line of input. */
  readonly onRunInputRequested = this.runInputRequestedEmitter.event;

  constructor(private readonly options: SidecarClientOptions) {}

  /** What the engine was last started as (the command and which sidecar it is), when it has been. */
  get launch(): SidecarProcess | undefined {
    return this.launched;
  }

  get state(): SidecarState {
    return this.currentState;
  }

  /** Who answers when a running cell asks for a line of input: the text, or null for the end of input. Without one, a cell sees the end of input. */
  setInputHandler(handler: ((request: InputRequest) => Promise<string | null>) | undefined): void {
    this.inputHandler = handler;
  }

  /** The languages of the engine, once it has started. */
  get languages(): LanguageInfo[] {
    return this.initialized?.languages ?? [];
  }

  /** The folder the engine works in (relative paths are from here), once it has started. */
  get workspaceRoot(): string | undefined {
    return this.initialized?.workspaceRoot;
  }

  get serverVersion(): string | undefined {
    return this.initialized?.serverVersion;
  }

  /** Starts the engine if it is not running. Everyone who asks while it starts gets the same start. */
  ensureStarted(): Promise<InitializeResult> {
    if (this.disposed) return Promise.reject(new Error('The FrySharp engine client was disposed.'));
    if (this.currentState === 'ready' && this.initialized) return Promise.resolve(this.initialized);
    this.starting ??= this.start().finally(() => (this.starting = undefined));
    return this.starting;
  }

  async request<T>(method: string, params: unknown, options: RequestOptions = {}): Promise<T> {
    await this.ensureStarted();
    const connection = this.connection;
    if (!connection) throw new Error('The FrySharp engine is not running.');

    const timeoutMs = options.timeoutMs ?? DEFAULT_TIMEOUT_MS;
    const source = options.token ? new rpc.CancellationTokenSource() : undefined;
    const link = options.token?.onCancellationRequested(() => source?.cancel());
    if (options.token?.isCancellationRequested) source?.cancel();
    const call = source ? connection.sendRequest<T>(method, params, source.token) : connection.sendRequest<T>(method, params);
    if (timeoutMs === 0) return call.finally(() => link?.dispose());

    let timer: NodeJS.Timeout | undefined;
    const deadline = new Promise<never>((_, reject) => {
      timer = setTimeout(() => reject(new Error(`The FrySharp engine did not answer ${method} within ${Math.round(timeoutMs / 1000)} seconds.`)), timeoutMs);
    });
    try {
      return await Promise.race([call, deadline]);
    } finally {
      clearTimeout(timer);
      link?.dispose();
    }
  }

  // ── The requests the extension makes ──────────────────────────────────────────────────────────────────────────

  executeCell(params: ExecuteCellParams): Promise<ExecuteCellResult> {
    return this.request<ExecuteCellResult>('fry/notebook/execute', params, { timeoutMs: 0 });
  }

  async interruptNotebook(notebook: NotebookRef): Promise<void> {
    if (this.currentState === 'ready') await this.request('fry/notebook/interrupt', notebook);
  }

  async restartNotebook(notebook: NotebookRef): Promise<void> {
    if (this.currentState === 'ready') await this.request('fry/notebook/restart', notebook);
  }

  async closeNotebook(notebook: NotebookRef): Promise<void> {
    if (this.currentState === 'ready') await this.request('fry/notebook/close', notebook).catch(() => undefined);
  }

  getVariables(notebook: NotebookRef): Promise<VariableDto[]> {
    return this.request<VariableDto[]>('fry/notebook/variables', notebook);
  }

  // ── Language features of a notebook cell: quick, and not worth waiting long for ─────────────────────────────────────

  completeCell(params: CellPositionParams, token?: CancellationLike): Promise<CompletionResult> {
    return this.request<CompletionResult>('fry/language/completeCell', params, { timeoutMs: 8_000, token });
  }

  hoverCell(params: CellPositionParams, token?: CancellationLike): Promise<HoverResult | null> {
    return this.request<HoverResult | null>('fry/language/hoverCell', params, { timeoutMs: 8_000, token });
  }

  /** What the engine runs on and which tool each language found (the probes can take a few seconds the first time). */
  engineInfo(): Promise<EngineInfo> {
    return this.request<EngineInfo>('fry/engine/info', {}, { timeoutMs: 45_000 });
  }

  diagnoseCell(params: CellDiagnosticsParams, token?: CancellationLike): Promise<CellDiagnosticsResult> {
    return this.request<CellDiagnosticsResult>('fry/language/diagnoseCell', params, { timeoutMs: 15_000, token });
  }

  startRun(params: RunStartParams): Promise<RunStartResult> {
    return this.request<RunStartResult>('fry/run/start', params);
  }

  /** A line for the program to read (the engine writes it into the output too, as a terminal would show it). */
  runInput(runId: string, text: string): Promise<void> {
    return this.request('fry/run/input', { runId, text });
  }

  async stopRun(runId: string): Promise<boolean> {
    return this.currentState === 'ready' ? this.request<boolean>('fry/run/stop', { runId }) : false;
  }

  /** Stops the engine and starts it again. */
  async restart(): Promise<void> {
    this.stopProcess();
    this.setState('stopped');
    await this.ensureStarted();
  }

  dispose(): void {
    this.disposed = true;
    this.stopProcess();
    this.setState('stopped');
    for (const emitter of [this.stateEmitter, this.initializedEmitter, this.lostStateEmitter, this.notebookOutputEmitter, this.cellStateEmitter, this.runOutputEmitter, this.runExitedEmitter, this.runDisplayEmitter, this.runInputRequestedEmitter]) {
      emitter.dispose();
    }
  }

  // ── Starting, and noticing that it stopped ────────────────────────────────────────────────────────────────────

  private async start(): Promise<InitializeResult> {
    this.setState('starting');
    try {
      const spec = await this.options.prepare();
      this.launched = spec;
      this.options.log(`Starting the FrySharp engine: ${spec.command} ${spec.args.join(' ')}`);

      const child = cp.spawn(spec.command, spec.args, { cwd: spec.cwd, env: { ...process.env, DOTNET_NOLOGO: '1' } });
      this.child = child;
      child.stderr?.on('data', (data: Buffer) => this.options.log(data.toString().trimEnd()));
      child.on('error', (error) => this.options.log(`The engine could not be started: ${error.message}`));
      child.on('exit', (code, signal) => this.onExit(child, code, signal));

      const connection = rpc.createMessageConnection(new rpc.StreamMessageReader(child.stdout!), new rpc.StreamMessageWriter(child.stdin!));
      connection.onNotification('fry/notebook/output', (n: NotebookOutputNotification) => this.notebookOutputEmitter.fire(n));
      connection.onNotification('fry/notebook/cellState', (n: CellStateNotification) => this.cellStateEmitter.fire(n));
      connection.onNotification('fry/run/output', (n: RunOutputNotification) => this.runOutputEmitter.fire(n));
      connection.onNotification('fry/run/exited', (n: RunExitedNotification) => this.runExitedEmitter.fire(n));
      connection.onNotification('fry/run/display', (n: RunDisplayNotification) => this.runDisplayEmitter.fire(n));
      connection.onNotification('fry/run/inputRequested', (n: { runId: string }) => this.runInputRequestedEmitter.fire(n));
      connection.onRequest('fry/input/request', (request: InputRequest) => (this.inputHandler ? this.inputHandler(request) : null));
      connection.onError(([error]) => this.options.log(`Connection error: ${error.message}`));
      connection.listen();
      this.connection = connection;

      const params: InitializeParams = { clientId: 'vscode-client', clientName: this.options.clientName ?? 'VS Code Extension', protocolVersion: PROTOCOL_VERSION };
      const init = await this.handshake(connection, params);
      this.initialized = init;
      this.everStarted = true;
      this.setState('ready');
      this.options.log(`Connected to the FrySharp engine ${init.serverVersion}.`);
      this.initializedEmitter.fire(init);
      return init;
    } catch (error) {
      this.stopProcess();
      this.setState('crashed');
      throw error;
    }
  }

  private async handshake(connection: rpc.MessageConnection, params: InitializeParams): Promise<InitializeResult> {
    try {
      return await Promise.race([
        connection.sendRequest<InitializeResult>('fry/initialize', params),
        new Promise<never>((_, reject) => setTimeout(() => reject(new Error('The FrySharp engine did not answer when it started.')), 60_000).unref())
      ]);
    } catch (error) {
      const code = (error as { code?: number }).code;
      if (code === -32007) {
        throw new Error(`This extension speaks protocol ${PROTOCOL_VERSION}, and the engine does not (${(error as Error).message}). Update or reinstall FrySharp.`, { cause: error });
      }
      throw error;
    }
  }

  private onExit(child: cp.ChildProcess, code: number | null, signal: NodeJS.Signals | null): void {
    if (child !== this.child) return; // an engine this client already let go of
    this.options.log(`The FrySharp engine exited (code ${code}, signal ${signal}).`);
    this.child = undefined;
    this.connection?.dispose();
    this.connection = undefined;
    this.initialized = undefined;
    if (this.disposed) return;
    if (this.currentState === 'ready' || this.currentState === 'starting') {
      this.setState('crashed');
      if (this.everStarted) this.lostStateEmitter.fire({ reason: 'The FrySharp engine stopped unexpectedly.' });
    }
  }

  private stopProcess(): void {
    const child = this.child;
    this.child = undefined; // first: its exit is expected, not a crash
    this.initialized = undefined;
    try {
      this.connection?.dispose();
    } catch {
      // already closed
    }
    this.connection = undefined;
    try {
      child?.kill();
    } catch {
      // already gone
    }
  }

  private setState(state: SidecarState): void {
    if (state === this.currentState) return;
    this.currentState = state;
    this.stateEmitter.fire(state);
  }
}
