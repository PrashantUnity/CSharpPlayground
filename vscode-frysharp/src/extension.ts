import * as path from 'path';
import * as vscode from 'vscode';
import { FryNotebookController } from './notebook/notebookController';
import { FryNotebookSerializer } from './notebook/notebookSerializer';
import { showVariablesWebview } from './notebook/variablesView';
import { SidecarClient } from './sidecar/sidecarClient';

let sidecar: SidecarClient | undefined;

export async function activate(context: vscode.ExtensionContext) {
  const outputChannel = vscode.window.createOutputChannel('FrySharp Script Runner');
  sidecar = new SidecarClient(context);

  // Auto-start sidecar
  sidecar.start().catch((err) => {
    vscode.window.showErrorMessage(`Failed to start FrySharp Sidecar: ${err.message}`);
  });

  // Register Notebook Serializer
  context.subscriptions.push(
    vscode.workspace.registerNotebookSerializer(
      'frysharp-notebook',
      new FryNotebookSerializer(),
      { transientOutputs: false }
    )
  );

  // Register Notebook Controller
  const controller = new FryNotebookController(sidecar);
  context.subscriptions.push(controller);

  // Register Output stream for Run Script
  context.subscriptions.push(
    sidecar.onRunOutput((n) => {
      outputChannel.append(n.text);
    }),
    sidecar.onRunExited((n) => {
      outputChannel.appendLine(`\n[Process exited with code ${n.exitCode} in ${n.durationMs}ms]`);
      if (n.problems.length > 0) {
        outputChannel.appendLine(`Problems (${n.problems.length}):`);
        for (const p of n.problems) {
          outputChannel.appendLine(`  ${p.path || ''}(${p.line},${p.column}): ${p.severity} ${p.code}: ${p.message}`);
        }
      }
    })
  );

  // Command: New Polyglot Notebook
  context.subscriptions.push(
    vscode.commands.registerCommand('frysharp.newNotebook', async () => {
      const initialData = new vscode.NotebookData([
        new vscode.NotebookCellData(
          vscode.NotebookCellKind.Markup,
          '# Welcome to FrySharp Polyglot Notebook\nRun interactive C#, Python, JavaScript, and share variables across cells with `#!share`.',
          'markdown'
        ),
        new vscode.NotebookCellData(
          vscode.NotebookCellKind.Code,
          '// C# .NET 10 Roslyn cell\nint a = 42;\nConsole.WriteLine($"Hello from FrySharp! a = {a}");',
          'csharp'
        )
      ]);
      initialData.metadata = { title: 'New Notebook', kernel: 'csharp' };

      const doc = await vscode.workspace.openNotebookDocument('frysharp-notebook', initialData);
      await vscode.window.showNotebookDocument(doc);
    })
  );

  // Command: Run Script with FrySharp
  context.subscriptions.push(
    vscode.commands.registerCommand('frysharp.runScript', async () => {
      const editor = vscode.window.activeTextEditor;
      if (!editor) {
        vscode.window.showWarningMessage('No active script open to run.');
        return;
      }

      const filePath = editor.document.uri.fsPath;
      const workspaceRoot = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath || '';
      const relativePath = path.relative(workspaceRoot, filePath).replace(/\\/g, '/');

      outputChannel.clear();
      outputChannel.show(true);
      outputChannel.appendLine(`▶ Running ${path.basename(filePath)} with FrySharp...\n`);

      const runId = `run-${Date.now()}`;
      try {
        await sidecar!.startRun({
          runId,
          path: relativePath
        });
      } catch (err: any) {
        vscode.window.showErrorMessage(`Run failed: ${err.message}`);
      }
    })
  );

  // Command: Restart Sidecar
  context.subscriptions.push(
    vscode.commands.registerCommand('frysharp.restartSidecar', async () => {
      await sidecar?.restart();
      vscode.window.showInformationMessage('FrySharp engine sidecar restarted successfully.');
    })
  );

  function getTargetNotebook(arg?: any): { notebook: vscode.NotebookDocument; relativePath: string } | undefined {
    let doc: vscode.NotebookDocument | undefined;
    if (arg?.notebook) doc = arg.notebook;
    else if (arg?.document) doc = arg.document;
    else if (arg?.uri) {
      doc = vscode.workspace.notebookDocuments.find(d => d.uri.toString() === arg.uri.toString());
    } else {
      doc = vscode.window.activeNotebookEditor?.notebook;
    }

    if (!doc) return undefined;
    const filePath = doc.uri.fsPath;
    const workspaceRoot = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath || '';
    const relativePath = path.relative(workspaceRoot, filePath).replace(/\\/g, '/');
    return { notebook: doc, relativePath };
  }

  // Command: Restart Notebook Kernel
  context.subscriptions.push(
    vscode.commands.registerCommand('frysharp.restartNotebook', async (arg?: any) => {
      const target = getTargetNotebook(arg);
      if (!target) {
        vscode.window.showWarningMessage('No active FrySharp notebook found to restart.');
        return;
      }
      try {
        await sidecar?.restartNotebook(target.relativePath);
        vscode.window.showInformationMessage(`✓ FrySharp Kernel Restarted: session state and variables reset for ${path.basename(target.relativePath)}.`);
      } catch (err: any) {
        vscode.window.showErrorMessage(`Restart failed: ${err.message}`);
      }
    })
  );

  // Command: Interrupt Notebook Kernel
  context.subscriptions.push(
    vscode.commands.registerCommand('frysharp.interruptNotebook', async (arg?: any) => {
      const target = getTargetNotebook(arg);
      if (!target) {
        vscode.window.showWarningMessage('No active FrySharp notebook found to interrupt.');
        return;
      }
      try {
        await sidecar?.interruptNotebook(target.relativePath);
        vscode.window.showInformationMessage(`⚠️ FrySharp Kernel Interrupted for ${path.basename(target.relativePath)}.`);
      } catch (err: any) {
        vscode.window.showErrorMessage(`Interrupt failed: ${err.message}`);
      }
    })
  );

  // Command: Show Polyglot Variables
  context.subscriptions.push(
    vscode.commands.registerCommand('frysharp.showVariables', async (arg?: any) => {
      const target = getTargetNotebook(arg);
      if (!target) {
        vscode.window.showWarningMessage('No active FrySharp notebook found to inspect variables.');
        return;
      }
      try {
        const vars = (await sidecar?.getVariables(target.relativePath)) || [];
        showVariablesWebview(context, target.relativePath, vars, async () => {
          return (await sidecar?.getVariables(target.relativePath)) || [];
        });
      } catch (err: any) {
        vscode.window.showErrorMessage(`Failed to fetch variables: ${err.message}`);
      }
    })
  );

  // Command: Clear All Outputs
  context.subscriptions.push(
    vscode.commands.registerCommand('frysharp.clearNotebookOutputs', async () => {
      await vscode.commands.executeCommand('notebook.clearAllCellsOutputs');
    })
  );

  context.subscriptions.push(sidecar);
}

export function deactivate() {
  sidecar?.dispose();
}
