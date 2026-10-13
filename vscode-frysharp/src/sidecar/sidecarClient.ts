import * as cp from 'child_process';
import * as fs from 'fs';
import * as path from 'path';
import * as vscode from 'vscode';
import * as rpc from 'vscode-jsonrpc/node';
import {
  ExecuteCellParams,
  ExecuteCellResult,
  InitializeParams,
  InitializeResult,
  NotebookContent,
  NotebookOutputNotification,
  CellStateNotification,
  RunOutputNotification,
  RunExitedNotification,
  RunStartParams,
  RunStartResult,
  VariableDto
} from './protocol';

export class SidecarClient implements vscode.Disposable {
  private process?: cp.ChildProcess;
  private connection?: rpc.MessageConnection;
  private outputChannel: vscode.OutputChannel;
  private onNotebookOutputEmitter = new vscode.EventEmitter<NotebookOutputNotification>();
  private onCellStateEmitter = new vscode.EventEmitter<CellStateNotification>();
  private onRunOutputEmitter = new vscode.EventEmitter<RunOutputNotification>();
  private onRunExitedEmitter = new vscode.EventEmitter<RunExitedNotification>();

  public readonly onNotebookOutput = this.onNotebookOutputEmitter.event;
  public readonly onCellState = this.onCellStateEmitter.event;
  public readonly onRunOutput = this.onRunOutputEmitter.event;
  public readonly onRunExited = this.onRunExitedEmitter.event;

  constructor(private context: vscode.ExtensionContext) {
    this.outputChannel = vscode.window.createOutputChannel('FrySharp Sidecar');
  }

  public async start(): Promise<void> {
    if (this.connection) {
      return;
    }

    const workspaceRoot = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath || process.cwd();
    this.outputChannel.appendLine(`Starting FrySharp sidecar in: ${workspaceRoot}`);

    // Resolve sidecar executable: custom setting -> bundled binary -> repo build -> dotnet run
    const config = vscode.workspace.getConfiguration('frysharp');
    const customSidecarPath = config.get<string>('sidecarPath');

    const bundledDll = path.join(this.context.extensionPath, 'bin', 'sidecar', 'frysharp-sidecar.dll');
    const repoRoot = path.resolve(this.context.extensionPath, '..');
    const repoDevDll = path.join(repoRoot, 'FrySharp.Sidecar', 'bin', 'Debug', 'net10.0', 'frysharp-sidecar.dll');
    const repoCsproj = path.join(repoRoot, 'FrySharp.Sidecar', 'FrySharp.Sidecar.csproj');

    let command = 'dotnet';
    let args: string[] = [];

    if (this.process) {
      try { this.process.kill(); } catch { }
      this.process = undefined;
    }

    if (customSidecarPath && fs.existsSync(customSidecarPath)) {
      this.outputChannel.appendLine(`Using custom sidecar: ${customSidecarPath}`);
      args = [customSidecarPath, '--workspace', workspaceRoot];
    } else if (fs.existsSync(repoDevDll) && (!fs.existsSync(bundledDll) || fs.statSync(repoDevDll).mtimeMs > fs.statSync(bundledDll).mtimeMs)) {
      this.outputChannel.appendLine(`Using latest local dev sidecar: ${repoDevDll}`);
      args = [repoDevDll, '--workspace', workspaceRoot];
    } else if (fs.existsSync(bundledDll)) {
      this.outputChannel.appendLine(`Using bundled sidecar: ${bundledDll}`);
      args = [bundledDll, '--workspace', workspaceRoot];
    } else if (fs.existsSync(repoDevDll)) {
      this.outputChannel.appendLine(`Using local dev sidecar: ${repoDevDll}`);
      args = [repoDevDll, '--workspace', workspaceRoot];
    } else if (fs.existsSync(repoCsproj)) {
      this.outputChannel.appendLine(`Running sidecar via dotnet run: ${repoCsproj}`);
      args = ['run', '--project', repoCsproj, '--', '--workspace', workspaceRoot];
    } else {
      throw new Error('FrySharp sidecar binary not found. Please ensure .NET 10 is installed.');
    }

    this.process = cp.spawn(command, args, {
      cwd: workspaceRoot,
      env: { ...process.env, DOTNET_NOLOGO: '1' }
    });

    this.process.stderr?.on('data', (data) => {
      this.outputChannel.append(`[Sidecar Log] ${data.toString()}`);
    });

    this.process.on('exit', (code, signal) => {
      this.outputChannel.appendLine(`Sidecar process exited with code ${code}, signal ${signal}`);
      this.connection = undefined;
    });

    this.connection = rpc.createMessageConnection(
      new rpc.StreamMessageReader(this.process.stdout!),
      new rpc.StreamMessageWriter(this.process.stdin!)
    );

    // Register notifications
    this.connection.onNotification('fry/notebook/output', (n: NotebookOutputNotification) => {
      this.onNotebookOutputEmitter.fire(n);
    });

    this.connection.onNotification('fry/notebook/cellState', (n: CellStateNotification) => {
      this.onCellStateEmitter.fire(n);
    });

    this.connection.onNotification('fry/run/output', (n: RunOutputNotification) => {
      this.onRunOutputEmitter.fire(n);
    });

    this.connection.onNotification('fry/run/exited', (n: RunExitedNotification) => {
      this.onRunExitedEmitter.fire(n);
    });

    this.connection.listen();

    const initResult = await this.connection.sendRequest<InitializeResult>('fry/initialize', {
      clientId: 'vscode-client',
      clientName: 'VS Code Extension',
      protocolVersion: 1
    } as InitializeParams);

    this.outputChannel.appendLine(`Connected to FrySharp Sidecar v${initResult.serverVersion}`);
  }

  public async openNotebook(relativePath: string): Promise<NotebookContent> {
    if (!this.connection) await this.start();
    return this.connection!.sendRequest<NotebookContent>('fry/notebook/open', { path: relativePath });
  }

  public async executeCell(relativePath: string, cellId: string, source: string, language?: string): Promise<ExecuteCellResult> {
    if (!this.connection) await this.start();
    return this.connection!.sendRequest<ExecuteCellResult>('fry/notebook/execute', {
      path: relativePath,
      cellId,
      source,
      language
    } as ExecuteCellParams);
  }

  public async interruptNotebook(relativePath: string): Promise<void> {
    if (!this.connection) return;
    await this.connection.sendRequest('fry/notebook/interrupt', { path: relativePath });
  }

  public async restartNotebook(relativePath: string): Promise<void> {
    if (!this.connection) return;
    await this.connection.sendRequest('fry/notebook/restart', { path: relativePath });
  }

  public async getVariables(relativePath: string): Promise<VariableDto[]> {
    if (!this.connection) await this.start();
    return this.connection!.sendRequest<VariableDto[]>('fry/notebook/variables', { path: relativePath });
  }

  public async saveNotebook(relativePath: string): Promise<void> {
    if (!this.connection) return;
    await this.connection.sendRequest('fry/notebook/save', { path: relativePath });
  }

  public async startRun(params: RunStartParams): Promise<RunStartResult> {
    if (!this.connection) await this.start();
    return this.connection!.sendRequest<RunStartResult>('fry/run/start', params);
  }

  public async stopRun(runId: string): Promise<boolean> {
    if (!this.connection) return false;
    return this.connection.sendRequest<boolean>('fry/run/stop', { runId });
  }

  public async restart(): Promise<void> {
    this.dispose();
    await this.start();
  }

  public dispose(): void {
    try {
      this.connection?.dispose();
    } catch { }
    this.connection = undefined;
    if (this.process) {
      try {
        this.process.kill();
      } catch { }
      this.process = undefined;
    }
  }
}
