import * as fs from 'fs';
import * as path from 'path';
import * as vscode from 'vscode';
import { FryNotebookController, isFryNotebook, NOTEBOOK_TYPES, notebookIdentity } from './notebook/notebookController';
import { engineInfoMarkdown } from './engineInfo';
import { CellStatusBar } from './notebook/cellStatusBar';
import { RunCodeLens } from './run/runCodeLens';
import { FryNotebookSerializer } from './notebook/notebookSerializer';
import { outputTabsEnabled } from './notebook/outputSettings';
import { CellFeatures } from './language/cellFeatures';
import { CellProblems } from './language/cellProblems';
import { LiveDiagnostics } from './language/liveDiagnostics';
import { RunHub } from './notebook/runHub';
import { Scratchpad } from './notebook/scratchpad';
import { EngineStatusBar } from './notebook/statusBar';
import { VariablesTree } from './notebook/variablesTree';
import { showVariablesWebview } from './notebook/variablesView';
import { ResultsView } from './run/resultsView';
import { ScriptRuns } from './run/scriptRuns';
import { resolveSidecarLaunch } from './sidecar/launcher';
import { DotnetMissingError, REQUIRED_MAJOR, resolveDotnet } from './sidecar/runtime';
import { SidecarClient, SidecarProcess } from './sidecar/sidecarClient';

const DOTNET_INSTALL_TOOL = 'ms-dotnettools.vscode-dotnet-runtime';

let sidecar: SidecarClient | undefined;

/** What the extension hands to the tests that run it in a real VS Code. */
/** What the output renderer says in a notebook's web view each time it has drawn an output (or could not). */
export interface RendererReport {
  type: 'rendered' | 'error';
  mime: string;
  id: string;
  /** For an engine page that is a whole chart: whether the chart really drew, as the page itself said. */
  chartDrawn?: boolean;
  /** For a cell's output drawn as tabs: the tabs it has (in order) and the one showing. */
  tabs?: string[];
  selected?: string;
  message?: string;
}

/** The languages whose files the run buttons are offered for before the engine has reported what it runs. */
export const DEFAULT_RUNNABLE_LANGUAGES = ['csharp', 'fsharp', 'java', 'rust', 'go', 'cpp', 'python', 'javascript', 'sql'];

export interface FrySharpApi {
  rendererReports: RendererReport[];
  variables: VariablesTree;
  scripts: ScriptRuns;
  results: ResultsView;
  statusText(): string;
}

export async function activate(context: vscode.ExtensionContext): Promise<FrySharpApi> {
  const log = vscode.window.createOutputChannel('FrySharp', { log: true });
  context.subscriptions.push(log);

  const client = new SidecarClient({
    log: (message) => log.info(message),
    prepare: () => prepareSidecar(context, log)
  });
  sidecar = client;
  context.subscriptions.push({ dispose: () => client.dispose() });

  // When the engine stops on its own, what the notebooks had in memory is gone: say so, rather than let a variable
  // vanish for no reason a user can see.
  context.subscriptions.push(
    client.onDidLoseState(({ reason }) => {
      void vscode.window.showWarningMessage(`${reason} Variables and state from earlier cells are gone: run the cells again.`, 'Show Log').then((choice) => {
        if (choice) log.show();
      });
    })
  );

  // The output renderer reports what it drew: an error in it goes to the log, and a test can see that VS Code really ran it.
  const rendererReports: RendererReport[] = [];
  const messaging = vscode.notebooks.createRendererMessaging('frysharp-output');
  context.subscriptions.push(
    messaging.onDidReceiveMessage((event) => {
      const report = event.message as RendererReport;
      rendererReports.push(report);
      if (rendererReports.length > 500) rendererReports.shift();
      if (report.type === 'error') log.error(`The output renderer failed on ${report.mime}: ${report.message}`);
    })
  );

  // The run buttons and keys are for the files of the languages the engine runs. Until it has said (it starts when first
  // needed) they are offered for the languages it has always run.
  const setRunnable = (ids: string[]) => void vscode.commands.executeCommand('setContext', 'frysharp.runnableLanguages', ids);
  setRunnable(DEFAULT_RUNNABLE_LANGUAGES);
  context.subscriptions.push(client.onDidInitialize(() => setRunnable(client.languages.filter((language) => language.canRun).map((language) => language.id))));

  const problems = new CellProblems();
  const hub = new RunHub(client, problems);
  context.subscriptions.push(problems, hub, new CellFeatures(client), new LiveDiagnostics(client, problems));
  const cellStatus = new CellStatusBar(client);
  context.subscriptions.push(cellStatus);
  for (const type of NOTEBOOK_TYPES) {
    context.subscriptions.push(vscode.notebooks.registerNotebookCellStatusBarItemProvider(type, cellStatus));
    context.subscriptions.push(vscode.workspace.registerNotebookSerializer(type, new FryNotebookSerializer(type === 'frysharp-jupyter' ? 'ipynb' : 'frynb', () => outputTabsEnabled()), { transientOutputs: false }));
    context.subscriptions.push(new FryNotebookController(client, hub, type));
  }

  const variables = new VariablesTree(client, hub);
  const statusBar = new EngineStatusBar(client, hub);
  context.subscriptions.push(variables, statusBar, vscode.window.registerTreeDataProvider('frysharp.variables', variables));

  // The engine starts when a notebook opens (or the first cell runs), not when VS Code does, and not in a folder that is not trusted.
  const warm = (notebook?: vscode.NotebookDocument) => {
    if (vscode.workspace.isTrusted && (notebook === undefined || isFryNotebook(notebook))) {
      client.ensureStarted().catch((error) => log.error(String(error?.message ?? error)));
    }
  };
  context.subscriptions.push(
    vscode.workspace.onDidOpenNotebookDocument(warm),
    vscode.workspace.onDidGrantWorkspaceTrust(() => {
      if (vscode.workspace.notebookDocuments.some(isFryNotebook)) warm();
    }),
    // A closed notebook's kernels (and the Python and Node processes behind them) are stopped.
    vscode.workspace.onDidCloseNotebookDocument((notebook) => {
      if (isFryNotebook(notebook)) void client.closeNotebook({ notebookId: notebookIdentity(notebook).notebookId });
    })
  );
  vscode.workspace.notebookDocuments.forEach(warm);

  const register = (command: string, callback: (...args: any[]) => unknown) => context.subscriptions.push(vscode.commands.registerCommand(command, callback));

  register('frysharp.newNotebook', async () => {
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
    initialData.metadata = { format: 'frynb', title: 'New Notebook', kernel: 'csharp' };

    const doc = await vscode.workspace.openNotebookDocument(NOTEBOOK_TYPES[0], initialData);
    await vscode.window.showNotebookDocument(doc);
  });

  // Scripts: a terminal for each, problems in the Problems panel, and a Results panel for what is not text.
  const results = new ResultsView(context.extensionUri);
  const scripts = new ScriptRuns(client, results);
  const runnable = () => (client.languages.length > 0 ? client.languages.filter((language) => language.canRun).map((language) => language.id) : DEFAULT_RUNNABLE_LANGUAGES);
  const lenses = new RunCodeLens(scripts, runnable);
  context.subscriptions.push(lenses, vscode.languages.registerCodeLensProvider({ scheme: 'file' }, lenses), vscode.languages.registerCodeLensProvider({ scheme: 'untitled' }, lenses), client.onDidInitialize(() => lenses.refresh()));
  context.subscriptions.push(results, scripts, vscode.window.registerWebviewViewProvider(ResultsView.viewType, results, { webviewOptions: { retainContextWhenHidden: true } }));

  // The file a command is about: the one it is given (the Explorer's menu), else the one being edited.
  const scriptOf = (arg?: unknown): vscode.Uri | undefined => {
    if (arg instanceof vscode.Uri) return arg;
    const editor = vscode.window.activeTextEditor;
    if (!editor || editor.document.isUntitled) {
      void vscode.window.showWarningMessage('Open a saved script to run it with FrySharp.');
      return undefined;
    }
    return editor.document.uri;
  };
  register('frysharp.runScript', async (arg?: unknown) => {
    const file = scriptOf(arg);
    if (file) await scripts.run(file);
  });
  register('frysharp.runScriptWithArguments', async (arg?: unknown) => {
    const file = scriptOf(arg);
    if (!file) return;
    const typed = await vscode.window.showInputBox({
      title: `Run ${path.basename(file.fsPath)} with arguments`,
      prompt: 'Arguments, separated by spaces (use quotes around one that has a space)',
      value: context.workspaceState.get<string>(`frysharp.args:${file.fsPath}`, '')
    });
    if (typed === undefined) return;
    await context.workspaceState.update(`frysharp.args:${file.fsPath}`, typed);
    await scripts.run(file, splitArguments(typed));
  });
  const scratchpad = new Scratchpad();
  context.subscriptions.push(scratchpad);
  register('frysharp.newScratchpad', () => scratchpad.open());
  register('frysharp.sendToScratchpad', async () => {
    const editor = vscode.window.activeTextEditor;
    if (editor) await scratchpad.send(editor);
  });
  register('frysharp.stopRun', () => scripts.stopActive());
  register('frysharp.rerun', () => scripts.rerunActive());
  register('frysharp.clearResults', () => results.clear());

  register('frysharp.restartSidecar', async () => {
    await client.restart();
    vscode.window.showInformationMessage('FrySharp engine restarted: the variables of every notebook are reset.');
  });

  register('frysharp.showLog', () => log.show());

  const targetNotebook = (arg?: any): vscode.NotebookDocument | undefined => {
    if (arg?.notebook) return arg.notebook;
    if (arg?.document) return arg.document;
    if (arg?.uri) return vscode.workspace.notebookDocuments.find((d) => d.uri.toString() === arg.uri.toString());
    return vscode.window.activeNotebookEditor?.notebook;
  };

  register('frysharp.restartNotebook', async (arg?: any) => {
    const notebook = targetNotebook(arg);
    if (!notebook) {
      vscode.window.showWarningMessage('No active FrySharp notebook found to restart.');
      return;
    }
    try {
      await client.restartNotebook({ notebookId: notebookIdentity(notebook).notebookId });
      vscode.window.showInformationMessage(`✓ FrySharp Kernel Restarted: session state and variables reset for ${path.basename(notebook.uri.path)}.`);
    } catch (err: any) {
      vscode.window.showErrorMessage(`Restart failed: ${err.message}`);
    }
  });

  register('frysharp.interruptNotebook', async (arg?: any) => {
    const notebook = targetNotebook(arg);
    if (!notebook) {
      vscode.window.showWarningMessage('No active FrySharp notebook found to interrupt.');
      return;
    }
    try {
      await client.interruptNotebook({ notebookId: notebookIdentity(notebook).notebookId });
      vscode.window.showInformationMessage(`⚠️ FrySharp Kernel Interrupted for ${path.basename(notebook.uri.path)}.`);
    } catch (err: any) {
      vscode.window.showErrorMessage(`Interrupt failed: ${err.message}`);
    }
  });

  register('frysharp.showVariables', async (arg?: any) => {
    const notebook = targetNotebook(arg);
    if (!notebook) {
      vscode.window.showWarningMessage('No active FrySharp notebook found to inspect variables.');
      return;
    }
    const identity = notebookIdentity(notebook);
    try {
      const fetch = () => client.getVariables({ notebookId: identity.notebookId });
      showVariablesWebview(context, identity.path, await fetch(), fetch);
    } catch (err: any) {
      vscode.window.showErrorMessage(`Failed to fetch variables: ${err.message}`);
    }
  });

  register('frysharp.refreshVariables', () => variables.refresh());
  register('frysharp.copyVariableValue', async (node?: unknown) => {
    const variable = VariablesTree.variableOf(node);
    if (variable) await vscode.env.clipboard.writeText(variable.value);
  });

  register('frysharp.engineMenu', async () => {
    const notebook = vscode.window.activeNotebookEditor?.notebook;
    const items: Array<vscode.QuickPickItem & { command: string }> = [
      { label: '$(info) Show Engine Info', description: 'versions, and the tool each language found', command: 'frysharp.showEngineInfo' },
      { label: '$(output) Show Engine Log', command: 'frysharp.showLog' },
      { label: '$(debug-restart) Restart Kernel of This Notebook', description: notebook ? path.basename(notebook.uri.path) : undefined, command: 'frysharp.restartNotebook' },
      { label: '$(primitive-square) Interrupt This Notebook', command: 'frysharp.interruptNotebook' },
      { label: '$(symbol-variable) Show Variables', command: 'frysharp.showVariables' },
      { label: '$(refresh) Restart the Engine', description: 'every notebook loses its variables', command: 'frysharp.restartSidecar' }
    ];
    const picked = await vscode.window.showQuickPick(items, { title: 'FrySharp' });
    if (picked) await vscode.commands.executeCommand(picked.command);
  });

  register('frysharp.showEngineInfo', async () => {
    try {
      const info = await vscode.window.withProgress({ location: vscode.ProgressLocation.Notification, title: 'FrySharp: asking the engine what it found…' }, async () => {
        await client.ensureStarted();
        return client.engineInfo();
      });
      const config = vscode.workspace.getConfiguration('frysharp');
      const setting = (name: string) => String(config.get<unknown>(name) ?? '');
      const content = engineInfoMarkdown(info, {
        extensionVersion: String(context.extension.packageJSON.version),
        vscodeVersion: vscode.version,
        launch: client.launch && { command: client.launch.command, description: client.launch.description },
        logFolder: context.logUri.fsPath,
        settings: [
          ['frysharp.dotnetPath', setting('dotnetPath')],
          ['frysharp.sidecarPath', setting('sidecarPath')],
          ['frysharp.notebook.outputTabs', String(vscode.workspace.getConfiguration('frysharp.notebook').get<boolean>('outputTabs', true))]
        ]
      });
      const document = await vscode.workspace.openTextDocument({ language: 'markdown', content });
      await vscode.window.showTextDocument(document, { preview: true });
      await vscode.commands.executeCommand('markdown.showPreviewToSide', document.uri);
    } catch (error) {
      void vscode.window.showErrorMessage(`FrySharp could not get the engine's info: ${error instanceof Error ? error.message : String(error)}`, 'Show Log').then((choice) => {
        if (choice) log.show();
      });
    }
  });

  register('frysharp.clearNotebookOutputs', () => vscode.commands.executeCommand('notebook.clearAllCellsOutputs'));

  return { rendererReports, variables, scripts, results, statusText: () => statusBar.text };
}

export function deactivate() {
  sidecar?.dispose();
}

// ─── Starting the engine: which .NET, which sidecar, in which folder ─────────────────────────────────────────────────

async function prepareSidecar(context: vscode.ExtensionContext, log: vscode.LogOutputChannel): Promise<SidecarProcess> {
  const config = vscode.workspace.getConfiguration('frysharp');

  let dotnet: string;
  try {
    dotnet = await resolveDotnet({
      setting: config.get<string>('dotnetPath') || undefined,
      acquire: () => acquireWithInstallTool(context),
      log: (message) => log.info(message)
    });
  } catch (error) {
    if (error instanceof DotnetMissingError) offerDotnet(error.message);
    throw error;
  }

  const workspaceRoot = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath ?? (await ensureFolder(context.globalStorageUri.fsPath));
  const launch = resolveSidecarLaunch(
    {
      setting: config.get<string>('sidecarPath') || undefined,
      extensionPath: context.extensionPath,
      development: context.extensionMode !== vscode.ExtensionMode.Production, // running from the repository: Development, or the Extension Host tests
      exists: fs.existsSync
    },
    workspaceRoot
  );
  log.info(`Using ${launch.description}.`);
  return { command: dotnet, args: launch.args, cwd: workspaceRoot, description: launch.description };
}

async function ensureFolder(folder: string): Promise<string> {
  await fs.promises.mkdir(folder, { recursive: true });
  return folder;
}

// The .NET Install Tool, when it is installed, downloads a runtime for us; it is not a dependency, so a machine without it still works.
async function acquireWithInstallTool(context: vscode.ExtensionContext): Promise<string | undefined> {
  if (!vscode.extensions.getExtension(DOTNET_INSTALL_TOOL)) return undefined;
  const result = await vscode.commands.executeCommand<{ dotnetPath?: string }>('dotnet.acquire', {
    version: `${REQUIRED_MAJOR}.0`,
    requestingExtensionId: context.extension.id
  });
  return result?.dotnetPath;
}

function offerDotnet(message: string): void {
  const installTool = 'Install .NET Install Tool';
  const download = 'Download .NET';
  const settings = 'Open Settings';
  const actions = vscode.extensions.getExtension(DOTNET_INSTALL_TOOL) ? [download, settings] : [installTool, download, settings];
  void vscode.window.showErrorMessage(message, ...actions).then((choice) => {
    if (choice === installTool) void vscode.commands.executeCommand('workbench.extensions.installExtension', DOTNET_INSTALL_TOOL);
    else if (choice === download) void vscode.env.openExternal(vscode.Uri.parse(`https://dotnet.microsoft.com/download/dotnet/${REQUIRED_MAJOR}.0`));
    else if (choice === settings) void vscode.commands.executeCommand('workbench.action.openSettings', 'frysharp.dotnetPath');
  });
}

/** `a "b c" d` is three arguments: a, b c, d. */
export function splitArguments(text: string): string[] {
  const found: string[] = [];
  for (const match of text.matchAll(/"([^"]*)"|'([^']*)'|(\S+)/g)) found.push(match[1] ?? match[2] ?? match[3]);
  return found;
}
