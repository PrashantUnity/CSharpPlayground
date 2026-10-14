// A Jupyter notebook (.ipynb, nbformat 4), as Jupyter, VS Code and GitHub read it. Cells keep the fields this extension
// has no use for (tags, attachments, widget state), and a cell's outputs are written back exactly as read until they
// change.

import { CellModel, CellOutput, ERROR, fromBase64, isBinaryMime, NotebookModel, OutputItem, sortItems, STDERR, STDOUT, text, textItem, toBase64, utf8 } from './model';
import { fingerprint } from './outputs';

type Obj = Record<string, unknown>;

const isObject = (value: unknown): value is Obj => typeof value === 'object' && value !== null && !Array.isArray(value);
const str = (value: unknown): string => (typeof value === 'string' ? value : '');
const lines = (value: unknown): string => (Array.isArray(value) ? value.join('') : str(value));

const DEFAULT_METADATA = {
  kernelspec: { display_name: '.NET (C#)', language: 'C#', name: '.net-csharp' },
  language_info: { name: 'csharp' }
};

// The names Jupyter and Polyglot Notebooks use for a language, as the ids VS Code and FrySharp use.
const LANGUAGE_IDS: Record<string, string> = { 'c#': 'csharp', 'f#': 'fsharp', 'c++': 'cpp', node: 'javascript', js: 'javascript', py: 'python' };

export function languageId(name: string): string {
  const lower = name.toLowerCase();
  return LANGUAGE_IDS[lower] ?? lower;
}

export function parseIpynb(json: string): NotebookModel {
  return ipynbFromJson(JSON.parse(json), json);
}

export function ipynbFromJson(parsed: unknown, json: string): NotebookModel {
  if (!isObject(parsed) || !Array.isArray(parsed.cells)) throw new Error('A Jupyter notebook has a "cells" list.');

  const metadata = isObject(parsed.metadata) ? parsed.metadata : {};
  const info = isObject(metadata.language_info) ? metadata.language_info : {};
  const kernel = languageId(str(info.name) || 'csharp');

  const root: Obj = {};
  for (const key of Object.keys(parsed)) {
    if (key !== 'cells') root[key] = parsed[key];
  }
  const indent = /^\n?([ \t]+)"/m.exec(json.slice(0, 400))?.[1];

  return { format: 'ipynb', kernel, root, indent, cells: parsed.cells.filter(isObject).map((cell, index) => parseCell(cell, index, kernel)) };
}

function parseCell(cell: Obj, index: number, kernel: string): CellModel {
  const markdown = cell.cell_type !== 'code';
  const metadata = isObject(cell.metadata) ? cell.metadata : {};
  const tagged = isObject(metadata.polyglot_notebook) ? str(metadata.polyglot_notebook.kernelName) : isObject(metadata.dotnet_interactive) ? str(metadata.dotnet_interactive.language) : '';
  const language = markdown || !tagged ? undefined : languageId(tagged);

  const extra: Obj = {};
  for (const key of Object.keys(cell)) {
    if (!['cell_type', 'id', 'source', 'outputs', 'execution_count'].includes(key)) extra[key] = cell[key];
  }
  if (cell.cell_type === 'raw') extra.cell_type = 'raw'; // VS Code has code and markup cells; a raw one is written back as raw

  const rawOutputs = !markdown && Array.isArray(cell.outputs) ? cell.outputs : [];
  const outputs = rawOutputs.filter(isObject).map(outputFromJupyter);
  return {
    id: str(cell.id) || `cell-${index + 1}`,
    kind: markdown ? 'markdown' : 'code',
    language: language === kernel ? undefined : language,
    source: lines(cell.source),
    executionCount: typeof cell.execution_count === 'number' ? cell.execution_count : undefined,
    failed: rawOutputs.some((output) => isObject(output) && output.output_type === 'error'),
    outputs,
    state: { extra, snapshots: {}, outputsKey: fingerprint(outputs), rawOutputs }
  };
}

export function writeIpynb(notebook: NotebookModel): string {
  const cells = notebook.cells.map((cell) => writeCell(cell, notebook.kernel));
  const written: Obj = { cells };
  for (const key of Object.keys(notebook.root)) written[key] = notebook.root[key];
  written.metadata ??= DEFAULT_METADATA;
  written.nbformat ??= 4;
  written.nbformat_minor ??= 5;
  return JSON.stringify(written, null, notebook.indent ?? 1) + '\n';
}

function writeCell(cell: CellModel, kernel: string): Obj {
  const extra = cell.state.extra;
  const metadata: Obj = isObject(extra.metadata) ? { ...extra.metadata } : {};
  const source = splitLines(cell.source);

  if (cell.kind === 'markdown') {
    return { ...passthrough(extra), cell_type: extra.cell_type === 'raw' ? 'raw' : 'markdown', id: cell.id, metadata, source };
  }

  if (cell.language && cell.language !== kernel) {
    metadata.polyglot_notebook = { ...(isObject(metadata.polyglot_notebook) ? metadata.polyglot_notebook : {}), kernelName: cell.language };
  }
  const unchanged = cell.state.rawOutputs !== undefined && fingerprint(cell.outputs) === cell.state.outputsKey;
  return {
    ...passthrough(extra),
    cell_type: 'code',
    execution_count: cell.executionCount ?? null,
    id: cell.id,
    metadata,
    outputs: unchanged ? cell.state.rawOutputs : cell.outputs.map(outputToJupyter),
    source
  };
}

function passthrough(extra: Obj): Obj {
  const rest: Obj = {};
  for (const key of Object.keys(extra)) {
    if (key !== 'metadata' && key !== 'cell_type') rest[key] = extra[key];
  }
  return rest;
}

// Jupyter's list of lines: each keeps its newline, and empty text is an empty list.
function splitLines(value: string): string[] {
  return value.match(/[^\n]*\n|[^\n]+$/g) ?? [];
}

// ─────────────────────────────────────────────────────────────────────────────────────────── outputs

function outputFromJupyter(output: Obj): CellOutput {
  switch (output.output_type) {
    case 'stream':
      return { items: [textItem(output.name === 'stderr' ? STDERR : STDOUT, lines(output.text))] };
    case 'error': {
      const traceback = Array.isArray(output.traceback) ? output.traceback.join('\n') : str(output.traceback);
      return { items: [textItem(ERROR, JSON.stringify({ name: str(output.ename) || 'Error', message: str(output.evalue), stack: traceback }))] };
    }
    default: {
      const data = isObject(output.data) ? output.data : {};
      const items: OutputItem[] = Object.entries(data).map(([mime, value]) => itemFromJupyter(mime, value));
      const sorted = sortItems(items);
      return isObject(output.metadata) && Object.keys(output.metadata).length > 0 ? { items: sorted, metadata: output.metadata } : { items: sorted };
    }
  }
}

function itemFromJupyter(mime: string, value: unknown): OutputItem {
  if (isBinaryMime(mime)) return { mime, data: fromBase64(lines(value).replace(/\s+/g, '')) };
  return { mime, data: utf8(typeof value === 'string' || Array.isArray(value) ? lines(value) : JSON.stringify(value)) };
}

function outputToJupyter(output: CellOutput): Obj {
  const [first] = output.items;
  if (output.items.length === 1 && (first.mime === STDOUT || first.mime === STDERR)) {
    return { output_type: 'stream', name: first.mime === STDERR ? 'stderr' : 'stdout', text: splitLines(text(first)) };
  }

  const error = output.items.find((item) => item.mime === ERROR);
  if (error) {
    const parsed = safeParse(text(error)) as { name?: string; message?: string; stack?: string };
    return { output_type: 'error', ename: parsed.name ?? 'Error', evalue: parsed.message ?? '', traceback: (parsed.stack ?? '').split('\n') };
  }

  const data: Obj = {};
  for (const item of output.items) {
    data[item.mime] = isBinaryMime(item.mime) ? toBase64(item.data) : item.mime.endsWith('json') ? safeParse(text(item), text(item)) : splitLines(text(item));
  }
  return { output_type: 'display_data', data, metadata: output.metadata ?? {} };
}

function safeParse(value: string, fallback: unknown = {}): unknown {
  try {
    return JSON.parse(value);
  } catch {
    return fallback;
  }
}
