import * as vscode from 'vscode';
import { contextFor, effectiveLanguage, resolverFor } from './cellContext';
import { directiveCompletions, DirectiveItem } from './directives';
import { isFryNotebook, NOTEBOOK_TYPES, notebookIdentity } from '../notebook/notebookController';
import { CompletionItem as EngineCompletion } from '../sidecar/protocol';
import { SidecarClient } from '../sidecar/sidecarClient';

/** The notebook a cell's document belongs to, and the cell. */
export function cellOf(document: vscode.TextDocument): { notebook: vscode.NotebookDocument; cell: vscode.NotebookCell } | undefined {
  if (document.uri.scheme !== 'vscode-notebook-cell') return undefined;
  for (const notebook of vscode.workspace.notebookDocuments) {
    if (!isFryNotebook(notebook)) continue;
    const cell = notebook.getCells().find((candidate) => candidate.document.uri.toString() === document.uri.toString());
    if (cell) return { notebook, cell };
  }
  return undefined;
}

/** What the engine knows about the cell: the language it runs in and the text of the earlier cells in that language. */
export function cellScope(client: SidecarClient, notebook: vscode.NotebookDocument, cell: vscode.NotebookCell): { language: string; context: string[] } {
  const resolve = resolverFor(client.languages);
  const cells = notebook.getCells().map((c) => ({ kind: c.kind === vscode.NotebookCellKind.Code ? ('code' as const) : ('markdown' as const), language: c.document.languageId, text: c.document.getText() }));
  const language = effectiveLanguage(cell.document.getText(), cell.document.languageId, resolve);
  return { language, context: contextFor(cells, cell.index, language, resolve) };
}

const KINDS: Record<string, vscode.CompletionItemKind> = {
  Method: vscode.CompletionItemKind.Method,
  ExtensionMethod: vscode.CompletionItemKind.Method,
  Property: vscode.CompletionItemKind.Property,
  Field: vscode.CompletionItemKind.Field,
  Class: vscode.CompletionItemKind.Class,
  Struct: vscode.CompletionItemKind.Struct,
  Record: vscode.CompletionItemKind.Struct,
  Interface: vscode.CompletionItemKind.Interface,
  Enum: vscode.CompletionItemKind.Enum,
  Keyword: vscode.CompletionItemKind.Keyword,
  Snippet: vscode.CompletionItemKind.Snippet,
  Variable: vscode.CompletionItemKind.Variable,
  Namespace: vscode.CompletionItemKind.Module
};

/** An engine completion as an editor one: a snippet where the engine marked a caret place, the rest as plain text. */
export function toCompletionItem(item: EngineCompletion): vscode.CompletionItem {
  const result = new vscode.CompletionItem({ label: item.label, detail: item.detail ? ` ${item.detail}` : undefined }, KINDS[item.kind] ?? vscode.CompletionItemKind.Text);
  result.insertText = item.kind === 'Snippet' ? new vscode.SnippetString(item.insertText) : item.insertText;
  if (item.documentation) result.documentation = new vscode.MarkdownString(item.documentation);
  result.sortText = item.sortText;
  return result;
}

function directiveKind(kind: DirectiveItem['kind']): vscode.CompletionItemKind {
  return { directive: vscode.CompletionItemKind.Keyword, language: vscode.CompletionItemKind.EnumMember, variable: vscode.CompletionItemKind.Variable, option: vscode.CompletionItemKind.Property, snippet: vscode.CompletionItemKind.Snippet }[kind];
}

/**
 * Completion and hover in the cells of a notebook. A language whose engine has them (C#, SQL) gets the engine's own, with
 * what the earlier cells define in scope; any cell gets the notebook's directives (`#!python`, `#!share --from csharp name`,
 * `%pip install`). Other languages are the business of the language extensions installed for them.
 */
export class CellFeatures implements vscode.CompletionItemProvider, vscode.HoverProvider, vscode.Disposable {
  private readonly disposables: vscode.Disposable[] = [];

  constructor(private readonly client: SidecarClient) {
    // Any language of our notebooks: the ones whose engine has intelligence answer, the rest are left to the editor's own
    // (and to the language extensions installed for them).
    const any = NOTEBOOK_TYPES.map((type) => ({ notebookType: type }));
    this.disposables.push(
      vscode.languages.registerCompletionItemProvider(any, this, '.'),
      vscode.languages.registerCompletionItemProvider(any, new DirectiveCompletion(client), '!', ' ', '-', '%'),
      vscode.languages.registerHoverProvider(any, this)
    );
  }

  async provideCompletionItems(document: vscode.TextDocument, position: vscode.Position, token: vscode.CancellationToken): Promise<vscode.CompletionItem[] | undefined> {
    const found = cellOf(document);
    if (!found || !vscode.workspace.isTrusted) return undefined;
    const scope = cellScope(this.client, found.notebook, found.cell);
    if (!this.hasIntelligence(scope.language)) return undefined;

    try {
      const result = await this.client.completeCell({ language: scope.language, source: document.getText(), offset: document.offsetAt(position), context: scope.context }, token);
      return result.items.map(toCompletionItem);
    } catch {
      return undefined; // no engine, or the request was cancelled: no completions, not an error in the editor
    }
  }

  async provideHover(document: vscode.TextDocument, position: vscode.Position, token: vscode.CancellationToken): Promise<vscode.Hover | undefined> {
    const found = cellOf(document);
    if (!found || !vscode.workspace.isTrusted) return undefined;
    const scope = cellScope(this.client, found.notebook, found.cell);
    if (!this.hasIntelligence(scope.language)) return undefined;

    try {
      const result = await this.client.hoverCell({ language: scope.language, source: document.getText(), offset: document.offsetAt(position), context: scope.context }, token);
      return result && result.markdown.length > 0 ? new vscode.Hover(result.markdown.map((part) => new vscode.MarkdownString(part))) : undefined;
    } catch {
      return undefined;
    }
  }

  /** The engine has completion and hover for the language (it says so when it starts: before that, only C#, which it always had). */
  private hasIntelligence(language: string): boolean {
    const known = this.client.languages.find((l) => l.id === language);
    return known ? known.hasCompletion : language === 'csharp';
  }

  dispose(): void {
    this.disposables.forEach((d) => d.dispose());
  }
}

/** The completions of a notebook's own directives, in a cell of any language. */
class DirectiveCompletion implements vscode.CompletionItemProvider {
  constructor(private readonly client: SidecarClient) {}

  async provideCompletionItems(document: vscode.TextDocument, position: vscode.Position): Promise<vscode.CompletionItem[] | undefined> {
    const found = cellOf(document);
    if (!found) return undefined;
    const linePrefix = document.lineAt(position.line).text.slice(0, position.character);
    if (!/^\s*[#%!]/.test(linePrefix)) return undefined; // not a directive line: nothing to ask

    const scope = cellScope(this.client, found.notebook, found.cell);
    const languages = this.client.languages;
    const completions = await directiveCompletions(linePrefix, {
      languages,
      cellLanguage: scope.language,
      variablesOf: async (languageId) => {
        try {
          const variables = await this.client.getVariables({ notebookId: notebookIdentity(found.notebook).notebookId });
          const language = languages.find((l) => l.id === languageId);
          return variables.filter((v) => v.kernel === language?.displayName || v.kernel === language?.shortName || v.kernel === languageId).map((v) => v.name);
        } catch {
          return [];
        }
      }
    });
    if (!completions) return undefined;

    const range = new vscode.Range(position.line, completions.from, position.line, position.character);
    return completions.items.map((item) => {
      const result = new vscode.CompletionItem(item.label, directiveKind(item.kind));
      result.detail = item.detail;
      result.insertText = item.insertText ?? item.label;
      result.range = range;
      return result;
    });
  }
}
