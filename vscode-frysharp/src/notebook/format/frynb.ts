// FrySharp's own notebook file (.frynb, and the older .csnb): the JSON the desktop writes from NotebookDocumentItem and
// NotebookCellItem (FrySharp.Core/Models). Reading keeps every field it does not own, and writing puts the fields in the
// desktop's order, so a notebook saved here and one saved by the desktop differ only by what changed.

import { CellModel, newId, NotebookModel } from './model';
import { fingerprint, outputsFromSlots, Slots, slotsFromOutputs } from './outputs';

type Obj = Record<string, unknown>;

const isObject = (value: unknown): value is Obj => typeof value === 'object' && value !== null && !Array.isArray(value);
const str = (value: unknown): string => (typeof value === 'string' ? value : '');

// What the desktop shows about a cell besides its outputs. Kept as read, and written back as read (the desktop wrote
// PascalCase, other writers camelCase, so a name matches whichever case it has).
const DESKTOP_FLAGS = ['IsExecuting', 'ExecutionTimeText', 'IsMarkdownPreviewMode', 'IsInputCollapsed', 'IsOutputCollapsed', 'IsOutputScrolled', 'HadInteractiveControl'];

/** Looks a field up by name in any case, and remembers which ones were taken. */
class Fields {
  private readonly taken = new Set<string>();
  private readonly byLower = new Map<string, string>();

  constructor(private readonly source: Obj) {
    for (const key of Object.keys(source)) this.byLower.set(key.toLowerCase(), key);
  }

  take(name: string): unknown {
    const key = this.byLower.get(name.toLowerCase());
    if (key === undefined) return undefined;
    this.taken.add(key);
    return this.source[key];
  }

  rest(): Obj {
    const rest: Obj = {};
    for (const key of Object.keys(this.source)) {
      if (!this.taken.has(key)) rest[key] = this.source[key];
    }
    return rest;
  }
}

export function parseFrynb(json: string): NotebookModel {
  return frynbFromJson(JSON.parse(json));
}

export function frynbFromJson(parsed: unknown): NotebookModel {
  if (!isObject(parsed)) throw new Error('A .frynb notebook is a JSON object.');

  const root = new Fields(parsed);
  const cells = root.take('Cells');
  const kernel = str(root.take('Kernel')) || 'csharp';

  return {
    format: 'frynb',
    kernel,
    root: root.rest(),
    cells: (Array.isArray(cells) ? cells : []).filter(isObject).map((cell, index) => parseCell(cell, index))
  };
}

function parseCell(source: Obj, index: number): CellModel {
  const fields = new Fields(source);
  const type = fields.take('Type');
  const markdown = type === 1 || type === '1' || (typeof type === 'string' && type.toLowerCase() === 'markdown');

  const sourceText = fields.take('Source');
  const text = Array.isArray(sourceText) ? sourceText.join('') : str(sourceText); // a Jupyter-style list of lines is also read
  const slots: Slots = {};
  for (const name of [
    'OutputText',
    'HasError',
    'ImageBytes',
    'ImageFormat',
    'ImageWidth',
    'ImageHeight',
    'HtmlContent',
    'MarkdownContent',
    'TableSnapshot',
    'InspectorSnapshot',
    'Visuals',
    'OutputBundles'
  ] as const) {
    (slots as Obj)[name] = fields.take(name);
  }
  const language = str(fields.take('Language'));
  const count = fields.take('ExecutionCount');
  const id = str(fields.take('Id')) || `cell-${index + 1}`;
  for (const derived of ['HasImage', 'HasHtml', 'HasOutput', 'IsCodeCell', 'IsMarkdownCell']) fields.take(derived);

  const extra: Obj = {};
  for (const flag of DESKTOP_FLAGS) {
    const value = fields.take(flag);
    if (value !== undefined) extra[flag] = value;
  }
  Object.assign(extra, fields.rest());

  const outputs = markdown ? [] : outputsFromSlots(slots);
  return {
    id,
    kind: markdown ? 'markdown' : 'code',
    language: markdown || !language ? undefined : language,
    source: text,
    executionCount: typeof count === 'number' ? count : undefined,
    failed: slots.HasError === true,
    outputs,
    state: {
      extra,
      snapshots: {
        table: isObject(slots.TableSnapshot) ? slots.TableSnapshot : undefined,
        inspector: isObject(slots.InspectorSnapshot) ? slots.InspectorSnapshot : undefined
      },
      outputsKey: fingerprint(outputs)
    }
  };
}

/** The text of a .frynb for this notebook. `now` stamps LastModified, as the desktop's save does. */
export function writeFrynb(notebook: NotebookModel, now: Date = new Date()): string {
  const root = notebook.root;
  const cells = notebook.cells.map((cell) => writeCell(cell, notebook.kernel));
  const executionCount = Math.max(typeof root.ExecutionCount === 'number' ? root.ExecutionCount : 0, ...notebook.cells.map((cell) => cell.executionCount ?? 0));

  const written: Obj = {
    SchemaVersion: root.SchemaVersion ?? '1.0',
    Type: root.Type ?? 'notebook',
    Id: root.Id ?? newId(),
    Title: root.Title ?? 'Untitled Notebook',
    Description: root.Description ?? '',
    Category: root.Category ?? 'Interactive',
    Kernel: notebook.kernel,
    Cells: cells,
    Created: root.Created ?? now.toISOString(),
    LastModified: now.toISOString(),
    ExecutionCount: executionCount
  };
  for (const key of Object.keys(root)) {
    if (!(key in written)) written[key] = root[key];
  }
  return JSON.stringify(written, null, 2);
}

function writeCell(cell: CellModel, kernel: string): Obj {
  const markdown = cell.kind === 'markdown';
  const unchanged = fingerprint(cell.outputs) === cell.state.outputsKey;
  const slots = markdown ? undefined : slotsFromOutputs(cell.outputs, cell.failed, unchanged ? cell.state.snapshots : {});
  const extra = cell.state.extra;

  const hasImage = !!slots?.ImageBytes;
  const hasHtml = !!slots?.HtmlContent;
  const hasVisuals = !!slots?.Visuals && slots.Visuals.length > 0;
  // A live control cannot be saved, so the desktop only records that there was one; a cell that ran again has none.
  const hadControl = unchanged && extra.HadInteractiveControl === true;

  // In the order the desktop writes them (NotebookCellItem's properties).
  const written: Obj = { Id: cell.id, Type: markdown ? 1 : 0 };
  if (cell.language && cell.language !== kernel) written.Language = cell.language;
  written.Source = cell.source;
  written.OutputText = slots?.OutputText ?? '';
  written.ExecutionCount = cell.executionCount ?? null;
  written.IsExecuting = extra.IsExecuting ?? false;
  written.ExecutionTimeText = cell.executionMs !== undefined ? `${Math.round(cell.executionMs).toLocaleString('en-US')} ms` : extra.ExecutionTimeText ?? '';
  written.HasError = slots?.HasError ?? false;
  written.IsMarkdownPreviewMode = extra.IsMarkdownPreviewMode ?? markdown;
  written.IsInputCollapsed = extra.IsInputCollapsed ?? false;
  written.IsOutputCollapsed = extra.IsOutputCollapsed ?? false;
  written.IsOutputScrolled = extra.IsOutputScrolled ?? false;
  written.ImageBytes = slots?.ImageBytes ?? null;
  written.ImageFormat = slots?.ImageFormat ?? null;
  written.ImageWidth = slots?.ImageWidth ?? null;
  written.ImageHeight = slots?.ImageHeight ?? null;
  written.HtmlContent = slots?.HtmlContent ?? null;
  if (slots?.MarkdownContent) written.MarkdownContent = slots.MarkdownContent;
  written.TableSnapshot = slots?.TableSnapshot ?? null;
  written.InspectorSnapshot = slots?.InspectorSnapshot ?? null;
  if (hasVisuals) written.Visuals = slots?.Visuals;
  written.HadInteractiveControl = hadControl;
  written.HasImage = hasImage;
  written.HasHtml = hasHtml;
  written.HasOutput = !!(slots?.OutputText || hasImage || hasHtml || slots?.TableSnapshot || slots?.InspectorSnapshot || hadControl || hasVisuals);
  written.IsCodeCell = !markdown;
  written.IsMarkdownCell = markdown;

  // Whatever else the cell had, and the full list of outputs when the fields above cannot say it.
  for (const key of Object.keys(extra)) {
    if (!(key in written)) written[key] = extra[key];
  }
  if (slots?.OutputBundles) written.OutputBundles = slots.OutputBundles;
  return written;
}
