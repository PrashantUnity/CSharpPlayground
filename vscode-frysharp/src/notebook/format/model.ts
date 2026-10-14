// The notebook as the extension holds it between a file and VS Code: plain data, so everything in this folder runs (and is
// tested) without VS Code. src/notebook/notebookSerializer.ts is the one place that turns it into vscode types.

export const STDOUT = 'application/vnd.code.notebook.stdout';
export const STDERR = 'application/vnd.code.notebook.stderr';
export const ERROR = 'application/vnd.code.notebook.error';
export const TABLE = 'application/vnd.fry.table+json';
export const INSPECTOR = 'application/vnd.fry.inspector+json';

/** The metadata of the error output the extension adds when a cell fails: the engine's text is in the cell's output already, and the file does not keep this one's. */
export const ENGINE_ERROR = 'fryEngineError';

/** The chart, 3D plot and visualizer specs (any major version: a newer one is kept as it is, not drawn). */
export function isVisualMime(mime: string): boolean {
  return /^application\/vnd\.fry\.(chart|plot3d|visualizer)\.v\d+\+json$/.test(mime);
}

export interface OutputItem {
  mime: string;
  data: Uint8Array;
}

/** One output of a cell, as VS Code has it: items that are the same thing in different formats, richest first. */
export interface CellOutput {
  items: OutputItem[];
  metadata?: Record<string, unknown>;
}

export type CellKind = 'code' | 'markdown';

/** What the file holds about a cell that the model does not own, and what is needed to write it back unchanged. */
export interface CellState {
  /** Fields of the cell's JSON that nothing here interprets (the desktop's collapse flags, anything newer), written back as they were. */
  extra: Record<string, unknown>;
  /** The desktop's own copies of a table and an inspector, which hold more than their MIME form does. Reused while the outputs are untouched. */
  snapshots: { table?: unknown; inspector?: unknown };
  /** Identifies the outputs as they were read, to tell later whether they changed. */
  outputsKey: string;
  /** A Jupyter cell's outputs as they were read, written back while the outputs are untouched. */
  rawOutputs?: unknown[];
}

export interface CellModel {
  id: string;
  kind: CellKind;
  /** The cell's language when it is not the notebook's; markdown cells have none. */
  language?: string;
  source: string;
  executionCount?: number;
  /** The cell's last run failed. */
  failed: boolean;
  outputs: CellOutput[];
  /** How long the last run in this session took, when there was one. */
  executionMs?: number;
  state: CellState;
}

export interface NotebookModel {
  format: 'frynb' | 'ipynb';
  /** The notebook's default language id. */
  kernel: string;
  /** The notebook's fields other than its cells and kernel (title, id, dates, or a Jupyter notebook's metadata), kept for writing back. */
  root: Record<string, unknown>;
  cells: CellModel[];
  /** How the file was indented, so a save does not change every line of it. */
  indent?: string;
}

const encoder = new TextEncoder();
const decoder = new TextDecoder('utf-8');

export function utf8(text: string): Uint8Array {
  return encoder.encode(text);
}

export function text(item: OutputItem): string {
  return decoder.decode(item.data);
}

export function textItem(mime: string, value: string): OutputItem {
  return { mime, data: utf8(value) };
}

export function jsonItem(mime: string, value: unknown): OutputItem {
  return { mime, data: utf8(JSON.stringify(value)) };
}

export function fromBase64(value: string): Uint8Array {
  return Uint8Array.from(Buffer.from(value, 'base64'));
}

export function toBase64(data: Uint8Array): string {
  return Buffer.from(data).toString('base64');
}

/** True for an item whose bytes are not text (a PNG); SVG, JSON and the rest are text. */
export function isBinaryMime(mime: string): boolean {
  return mime.startsWith('image/') && !mime.endsWith('+xml');
}

export function newId(): string {
  return crypto.randomUUID().replace(/-/g, '');
}

// The items of one output, richest first: the page, this extension's own types, SVG, images, markdown, the rest, the text.
const ORDER = ['text/html', 'application/vnd.fry.', 'image/svg+xml', 'image/png', 'image/jpeg', 'image/gif', 'text/markdown'];

function rank(mime: string): number {
  const index = ORDER.findIndex((entry) => (entry.endsWith('.') ? mime.startsWith(entry) : mime === entry));
  if (index >= 0) return index;
  return mime === 'text/plain' ? ORDER.length + 1 : ORDER.length;
}

export function sortItems(items: OutputItem[]): OutputItem[] {
  return [...items].sort((a, b) => rank(a.mime) - rank(b.mime));
}
