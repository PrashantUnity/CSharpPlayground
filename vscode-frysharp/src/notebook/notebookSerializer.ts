import * as vscode from 'vscode';
import { CellModel, CellOutput, NotebookModel, newId, parseNotebook, writeNotebook } from './format';
import { forShowing, unpackAll } from './format/tabs';

// The only place where a notebook file meets VS Code: the pure model (./format) in one direction, NotebookData in the other.

const EMPTY_STATE = (): CellModel['state'] => ({ extra: {}, snapshots: {}, outputsKey: '' });

export class FryNotebookSerializer implements vscode.NotebookSerializer {
  /**
   * @param defaultFormat what a new, empty file becomes: the Jupyter type is for .ipynb files.
   * @param outputTabs whether a cell with several kinds of output (text, a table, a chart ...) opens with them as one output, drawn as tabs.
   *   The file is the same either way: what is saved is always the outputs themselves.
   */
  constructor(
    private readonly defaultFormat: 'frynb' | 'ipynb' = 'frynb',
    private readonly outputTabs: () => boolean = () => false
  ) {}

  async deserializeNotebook(content: Uint8Array, _token: vscode.CancellationToken): Promise<vscode.NotebookData> {
    const json = new TextDecoder().decode(content);
    if (!json.trim()) {
      return this.toNotebookData({ format: this.defaultFormat, kernel: 'csharp', root: {}, cells: [] });
    }

    let model: NotebookModel;
    try {
      model = parseNotebook(json);
    } catch (error) {
      // Not an empty notebook: saving that would replace the file.
      throw new Error(`This file is not a FrySharp or Jupyter notebook: ${error instanceof Error ? error.message : String(error)}`, { cause: error });
    }
    return this.toNotebookData(model);
  }

  async serializeNotebook(data: vscode.NotebookData, _token: vscode.CancellationToken): Promise<Uint8Array> {
    return new TextEncoder().encode(writeNotebook(this.toModel(data)));
  }

  private toNotebookData(model: NotebookModel): vscode.NotebookData {
    const showTabs = this.outputTabs();
    const cells = model.cells.map((cell) => {
      const markup = cell.kind === 'markdown';
      const data = new vscode.NotebookCellData(markup ? vscode.NotebookCellKind.Markup : vscode.NotebookCellKind.Code, cell.source, markup ? 'markdown' : (cell.language ?? model.kernel));
      data.metadata = { id: cell.id, fry: cell.state };
      if (cell.executionCount !== undefined) {
        data.executionSummary = { executionOrder: cell.executionCount, success: !cell.failed };
      }
      data.outputs = forShowing(cell.outputs, showTabs).map(toVsCodeOutput);
      return data;
    });

    const data = new vscode.NotebookData(cells);
    data.metadata = { format: model.format, kernel: model.kernel, title: model.root.Title, fry: { root: model.root, indent: model.indent } };
    return data;
  }

  private toModel(data: vscode.NotebookData): NotebookModel {
    const metadata = data.metadata ?? {};
    const kernel = typeof metadata.kernel === 'string' ? metadata.kernel : 'csharp';
    const root: Record<string, unknown> = { ...(metadata.fry?.root ?? {}) };
    if (root.Title === undefined && typeof metadata.title === 'string') root.Title = metadata.title;

    const seen = new Set<string>();
    const cells = data.cells.map((cell): CellModel => {
      // A copied cell brings its id with it, and a new one has none: a cell's id is its own.
      let id = typeof cell.metadata?.id === 'string' && cell.metadata.id ? cell.metadata.id : newId();
      if (seen.has(id)) id = newId();
      seen.add(id);

      const markup = cell.kind === vscode.NotebookCellKind.Markup;
      const timing = cell.executionSummary?.timing;
      return {
        id,
        kind: markup ? 'markdown' : 'code',
        language: markup || cell.languageId === kernel ? undefined : cell.languageId,
        source: cell.value,
        executionCount: cell.executionSummary?.executionOrder,
        failed: cell.executionSummary?.success === false,
        outputs: unpackAll((cell.outputs ?? []).map(fromVsCodeOutput)),
        executionMs: timing ? timing.endTime - timing.startTime : undefined,
        state: cell.metadata?.fry ?? EMPTY_STATE()
      };
    });

    return { format: metadata.format === 'ipynb' || metadata.format === 'frynb' ? metadata.format : this.defaultFormat, kernel, root, indent: metadata.fry?.indent, cells };
  }
}

export function toVsCodeOutput(output: CellOutput): vscode.NotebookCellOutput {
  return new vscode.NotebookCellOutput(
    output.items.map((item) => new vscode.NotebookCellOutputItem(item.data, item.mime)),
    output.metadata
  );
}

function fromVsCodeOutput(output: vscode.NotebookCellOutput): CellOutput {
  const items = output.items.map((item) => ({ mime: item.mime, data: item.data }));
  return output.metadata && Object.keys(output.metadata).length > 0 ? { items, metadata: output.metadata } : { items };
}
