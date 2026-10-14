import * as vscode from 'vscode';
import { isFryNotebook, notebookIdentity } from './notebookController';
import { RunHub } from './runHub';
import { VariableDto } from '../sidecar/protocol';
import { SidecarClient } from '../sidecar/sidecarClient';

type Node = { kind: 'kernel'; kernel: string; variables: VariableDto[] } | { kind: 'variable'; variable: VariableDto } | { kind: 'message'; text: string };

/** What a variable looks like in a line: its value on one line, cut where a line has no more room. */
export function preview(value: string, max = 80): string {
  const line = value.replace(/\s+/g, ' ').trim();
  return line.length > max ? `${line.slice(0, max - 1)}…` : line;
}

/**
 * The variables of the notebook in front, by the language each lives in. It is asked again whenever a cell ends in that
 * notebook, or another notebook comes to the front: never by a timer, never for a notebook nobody is looking at.
 */
export class VariablesTree implements vscode.TreeDataProvider<Node>, vscode.Disposable {
  private readonly changed = new vscode.EventEmitter<Node | undefined>();
  private readonly disposables: vscode.Disposable[] = [];
  private pending?: NodeJS.Timeout;

  readonly onDidChangeTreeData = this.changed.event;

  constructor(
    private readonly sidecar: SidecarClient,
    hub: RunHub
  ) {
    this.disposables.push(
      this.changed,
      hub.onDidEndRun((identity) => {
        if (identity.notebookId === this.active()?.notebookId) this.refresh();
      }),
      vscode.window.onDidChangeActiveNotebookEditor(() => {
        void vscode.commands.executeCommand('setContext', 'frysharp.notebookActive', this.active() !== undefined);
        this.refresh();
      }),
      this.sidecar.onDidChangeState(() => this.refresh())
    );
    void vscode.commands.executeCommand('setContext', 'frysharp.notebookActive', this.active() !== undefined);
  }

  private active(): { notebookId: string; name: string } | undefined {
    const notebook = vscode.window.activeNotebookEditor?.notebook;
    if (!notebook || !isFryNotebook(notebook)) return undefined;
    const identity = notebookIdentity(notebook);
    return { notebookId: identity.notebookId, name: identity.path };
  }

  /** Asks again (once, however many reasons come at the same time). */
  refresh(): void {
    clearTimeout(this.pending);
    this.pending = setTimeout(() => this.changed.fire(undefined), 150);
  }

  async getChildren(node?: Node): Promise<Node[]> {
    if (node?.kind === 'kernel') return node.variables.map((variable) => ({ kind: 'variable', variable }));
    if (node) return [];

    const notebook = this.active();
    if (!notebook) return [{ kind: 'message', text: 'Open a FrySharp notebook to see its variables.' }];
    if (this.sidecar.state !== 'ready') return [{ kind: 'message', text: 'The engine has not started: run a cell.' }];

    let variables: VariableDto[];
    try {
      variables = await this.sidecar.getVariables({ notebookId: notebook.notebookId });
    } catch {
      return [{ kind: 'message', text: 'The notebook has no kernel yet: run a cell.' }];
    }
    if (variables.length === 0) return [{ kind: 'message', text: 'No variables yet: run a cell that defines one.' }];

    const byKernel = new Map<string, VariableDto[]>();
    for (const variable of variables) {
      byKernel.set(variable.kernel, [...(byKernel.get(variable.kernel) ?? []), variable]);
    }
    return [...byKernel.entries()].map(([kernel, list]) => ({ kind: 'kernel', kernel, variables: list }));
  }

  getTreeItem(node: Node): vscode.TreeItem {
    if (node.kind === 'message') {
      const item = new vscode.TreeItem(node.text);
      item.iconPath = new vscode.ThemeIcon('info');
      return item;
    }
    if (node.kind === 'kernel') {
      const item = new vscode.TreeItem(node.kernel, vscode.TreeItemCollapsibleState.Expanded);
      item.description = `${node.variables.length}`;
      item.iconPath = new vscode.ThemeIcon('symbol-namespace');
      return item;
    }

    const { variable } = node;
    const item = new vscode.TreeItem(variable.name);
    item.description = `${variable.type}  ${preview(variable.value)}`;
    item.tooltip = new vscode.MarkdownString().appendMarkdown(`**${variable.name}** \`${variable.type}\` (${variable.kernel})\n\n`).appendCodeblock(variable.value, 'text');
    item.iconPath = new vscode.ThemeIcon('symbol-variable');
    item.contextValue = 'frysharp.variable';
    return item;
  }

  /** The variable a tree item stands for, for the commands on it. */
  static variableOf(node: unknown): VariableDto | undefined {
    const candidate = node as Node | undefined;
    return candidate?.kind === 'variable' ? candidate.variable : undefined;
  }

  dispose(): void {
    clearTimeout(this.pending);
    this.disposables.forEach((d) => d.dispose());
  }
}
