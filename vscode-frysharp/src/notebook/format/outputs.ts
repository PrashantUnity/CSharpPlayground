// A cell's outputs in the two forms they take: the desktop's fields in a .frynb (OutputText, ImageBytes, HtmlContent,
// TableSnapshot, InspectorSnapshot, Visuals...: one slot for each kind of output) and VS Code's list of outputs made of
// MIME items. Reading turns the first into the second; writing turns it back, and when a cell has more than the slots
// can say (two tables, text between two charts) it also saves the list itself as OutputBundles, which the desktop
// ignores and this extension reads in preference.

import { createHash } from 'node:crypto';
import {
  CellOutput,
  ENGINE_ERROR,
  ERROR,
  fromBase64,
  INSPECTOR,
  isBinaryMime,
  isVisualMime,
  jsonItem,
  OutputItem,
  sortItems,
  STDERR,
  STDOUT,
  TABLE,
  text,
  textItem,
  toBase64,
  utf8
} from './model';

type Obj = Record<string, unknown>;

/** The fields of a cell that hold its outputs, as a .frynb has them. */
export interface Slots {
  OutputText?: unknown;
  HasError?: unknown;
  ImageBytes?: unknown;
  ImageFormat?: unknown;
  ImageWidth?: unknown;
  ImageHeight?: unknown;
  HtmlContent?: unknown;
  MarkdownContent?: unknown;
  TableSnapshot?: unknown;
  InspectorSnapshot?: unknown;
  Visuals?: unknown;
  OutputBundles?: unknown;
}

export interface WrittenSlots {
  OutputText: string;
  HasError: boolean;
  ImageBytes: string | null;
  ImageFormat: string | null;
  ImageWidth: number | null;
  ImageHeight: number | null;
  HtmlContent: string | null;
  MarkdownContent: string | null;
  TableSnapshot: unknown;
  InspectorSnapshot: unknown;
  Visuals: unknown[] | null;
  OutputBundles: Bundle[] | null;
}

type RichKind = 'image' | 'table' | 'inspector' | 'visual' | 'html';

const isObject = (value: unknown): value is Obj => typeof value === 'object' && value !== null && !Array.isArray(value);
const array = (value: unknown): unknown[] => (Array.isArray(value) ? value : []);
const str = (value: unknown): string => (typeof value === 'string' ? value : '');
const num = (value: unknown): number | undefined => (typeof value === 'number' && Number.isFinite(value) ? value : undefined);

// ─────────────────────────────────────────────────────────────────────────────────────────── slots to outputs

export function outputsFromSlots(slots: Slots): CellOutput[] {
  if (Array.isArray(slots.OutputBundles)) {
    return outputsFromBundles(slots.OutputBundles);
  }

  const outputs: CellOutput[] = [];
  const stream = str(slots.OutputText);
  if (stream) {
    outputs.push({ items: [textItem(slots.HasError === true ? STDERR : STDOUT, stream)] });
  }

  const rich: Array<{ output: CellOutput; kind: RichKind }> = [];

  const image = typeof slots.ImageBytes === 'string' ? fromBase64(slots.ImageBytes) : new Uint8Array();
  if (image.length > 0) {
    const mime = imageMime(slots.ImageFormat);
    const output: CellOutput = { items: [{ mime, data: image }, textItem('text/plain', '<image>')] };
    const width = num(slots.ImageWidth);
    const height = num(slots.ImageHeight);
    if (width !== undefined && height !== undefined) {
      output.metadata = { [mime]: { width, height } };
    }
    rich.push({ output, kind: 'image' });
  }

  if (isObject(slots.TableSnapshot)) {
    const table = tableToMime(slots.TableSnapshot);
    rich.push({ output: { items: [jsonItem(TABLE, table), textItem('text/plain', table.title)] }, kind: 'table' });
  }

  if (isObject(slots.InspectorSnapshot)) {
    const node = inspectorToMime(slots.InspectorSnapshot);
    rich.push({ output: { items: [jsonItem(INSPECTOR, node), textItem('text/plain', inspectorText(node))] }, kind: 'inspector' });
  }

  for (const visual of array(slots.Visuals)) {
    if (!isObject(visual)) continue;
    const mime = str(visual.MimeType);
    if (isObject(visual.Data) && isVisualMime(mime)) {
      rich.push({ output: { items: [jsonItem(mime, visual.Data), textItem('text/plain', `<${visualFamily(mime)}>`)] }, kind: 'visual' });
    } else {
      const title = str(visual.Title) || visualFamily(mime);
      rich.push({
        output: { items: [textItem('text/plain', `${title}: too large to save with the notebook; run the cell again to redraw it.`)] },
        kind: 'visual'
      });
    }
  }

  const html = str(slots.HtmlContent);
  const markdown = str(slots.MarkdownContent);
  if (html || markdown) {
    const last = rich[rich.length - 1];
    if (!markdown && last && isPageFor(last.kind, html)) {
      last.output.items.unshift(textItem('text/html', html));
    } else {
      const items: OutputItem[] = [];
      if (markdown) items.push(textItem('text/markdown', markdown));
      if (html) items.push(textItem('text/html', html));
      rich.push({ output: { items }, kind: 'html' });
    }
  }

  outputs.push(...rich.map((r) => r.output));
  return outputs;
}

// The desktop saves the page it drew a table, chart or image with next to the data, as one HtmlContent for the last
// of them. It belongs to the output it was drawn for; one that is not that output's page is an output of its own.
function isPageFor(kind: RichKind, html: string): boolean {
  switch (kind) {
    case 'visual':
      return html.includes('<svg');
    case 'table':
      return html.includes('fry-tbl');
    case 'inspector':
      return html.includes('fry-inspector');
    case 'image':
      return html.includes('<img');
    default:
      return false;
  }
}

function imageMime(format: unknown): string {
  const name = str(format).toLowerCase();
  if (name.startsWith('image/')) return name;
  if (name === 'jpg' || name === 'jpeg') return 'image/jpeg';
  if (name === 'svg') return 'image/svg+xml';
  return `image/${name || 'png'}`;
}

function imageFormat(mime: string): string {
  const name = mime.slice('image/'.length);
  return name === 'svg+xml' ? 'SVG' : name.toUpperCase();
}

function visualFamily(mime: string): string {
  if (mime.includes('plot3d')) return 'Plot3D';
  if (mime.includes('visualizer')) return 'Visualizer';
  return 'Chart';
}

// ─────────────────────────────────────────────────────────────────────────────────────────── outputs to slots

/**
 * The fields a .frynb keeps for these outputs. What the desktop keeps once (one table, one page) is the last one
 * there is, as it keeps it; `reuse` is the desktop's own table and inspector snapshots, passed while the outputs are
 * as they were read, since they hold a little more than the MIME form does.
 */
export function slotsFromOutputs(outputs: CellOutput[], failed: boolean, reuse: { table?: unknown; inspector?: unknown } = {}): WrittenSlots {
  // the line the extension adds to a failed cell is not kept: its text is in what the cell printed already
  outputs = outputs.filter((output) => output.metadata?.[ENGINE_ERROR] !== true);
  const slots: WrittenSlots = {
    OutputText: '',
    HasError: failed,
    ImageBytes: null,
    ImageFormat: null,
    ImageWidth: null,
    ImageHeight: null,
    HtmlContent: null,
    MarkdownContent: null,
    TableSnapshot: null,
    InspectorSnapshot: null,
    Visuals: null,
    OutputBundles: null
  };

  for (const output of outputs) {
    const rich = output.items.some((item) => item.mime !== 'text/plain' && item.mime !== STDOUT && item.mime !== STDERR && item.mime !== ERROR);
    for (const item of output.items) {
      const mime = item.mime;
      if (mime === STDOUT || mime === STDERR) {
        slots.OutputText += text(item);
      } else if (mime === ERROR) {
        slots.OutputText += errorText(item);
        slots.HasError = true;
      } else if (mime === 'text/plain') {
        if (!rich) slots.OutputText += text(item);
      } else if (mime === 'text/html') {
        slots.HtmlContent = text(item);
      } else if (mime === 'text/markdown') {
        slots.MarkdownContent = text(item);
      } else if (mime === 'image/svg+xml') {
        slots.HtmlContent = `<!DOCTYPE html><html><body style="margin:0">${text(item)}</body></html>`;
      } else if (isBinaryMime(mime)) {
        slots.ImageBytes = toBase64(item.data);
        slots.ImageFormat = imageFormat(mime);
        const size = output.metadata?.[mime];
        slots.ImageWidth = isObject(size) ? num(size.width) ?? null : null;
        slots.ImageHeight = isObject(size) ? num(size.height) ?? null : null;
      } else if (mime === TABLE) {
        slots.TableSnapshot = reuse.table ?? tableFromMime(parse(item));
      } else if (mime === INSPECTOR) {
        slots.InspectorSnapshot = reuse.inspector ?? inspectorFromMime(parse(item));
      } else if (isVisualMime(mime)) {
        const data = parse(item);
        if (data !== undefined) {
          (slots.Visuals ??= []).push({ MimeType: mime, Data: data, Title: isObject(data) && typeof data.title === 'string' ? data.title : null });
        }
      }
    }
  }

  // More than the fields can say: the list itself, too.
  if (fingerprint(outputsFromSlots({ ...slots, OutputBundles: undefined }), true) !== fingerprint(outputs, true)) {
    slots.OutputBundles = bundlesFromOutputs(outputs);
  }
  return slots;
}

function errorText(item: OutputItem): string {
  try {
    const error = JSON.parse(text(item)) as { name?: string; message?: string };
    return `${error.name ?? 'Error'}: ${error.message ?? ''}\n`;
  } catch {
    return text(item);
  }
}

function parse(item: OutputItem): unknown {
  try {
    return JSON.parse(text(item));
  } catch {
    return undefined;
  }
}

// ─────────────────────────────────────────────────────────────────────────────────────────── table

// The table of the studio's MIME type: {title, columns, numeric, rows, totalRows, totalColumns}.
function tableToMime(snapshot: Obj): { title: string; columns: string[]; numeric: boolean[]; rows: Array<Array<string | null>>; totalRows: number; totalColumns: number; label?: string } {
  const columns = array(snapshot.Columns).filter(isObject);
  const rows = array(snapshot.Rows).filter(isObject);
  const label = typeof snapshot.Label === 'string' ? snapshot.Label : undefined;
  // "showing the first 1,000 of 5,000 rows": the label says how many there are in all
  const total = label ? /of ([\d,]+) rows/.exec(label) : null;
  const table: ReturnType<typeof tableToMime> = {
    title: str(snapshot.Title) || 'Table',
    columns: columns.map((column) => str(column.Header)),
    numeric: columns.map((column) => column.IsNumeric === true),
    rows: rows.map((row) => array(row.Cells).map((cell) => (isObject(cell) && cell.IsNull !== true ? str(cell.DisplayText) : null))),
    totalRows: total ? parseInt(total[1].replace(/,/g, ''), 10) : rows.length,
    totalColumns: columns.length
  };
  if (label && !total) table.label = label;
  return table;
}

function tableFromMime(table: unknown): Obj | null {
  if (!isObject(table)) return null;
  const columns = array(table.columns);
  const numeric = array(table.numeric);
  const rows = array(table.rows);
  const totalRows = num(table.totalRows) ?? rows.length;
  const label =
    typeof table.label === 'string'
      ? table.label
      : totalRows > rows.length
        ? `showing the first ${rows.length.toLocaleString('en-US')} of ${totalRows.toLocaleString('en-US')} rows`
        : null;
  return {
    Title: str(table.title),
    Label: label,
    Columns: columns.map((header, index) => ({ Header: str(header), IsNumeric: numeric[index] === true })),
    Rows: rows.map((row) => ({
      Cells: array(row).map((value) =>
        value === null || value === undefined
          ? { DisplayText: '', IsNumeric: false, IsBoolean: false, IsNull: true }
          : { DisplayText: String(value), IsNumeric: typeof value === 'number', IsBoolean: typeof value === 'boolean', IsNull: false }
      )
    }))
  };
}

// ─────────────────────────────────────────────────────────────────────────────────────────── inspector

// The Object Explorer of the studio's MIME type: {title, circular?, properties: [{name, value, isNull, isString, child}]}.
interface InspectorNodeMime {
  title: string;
  circular?: boolean;
  properties: Array<{ name: string; value: string; isNull: boolean; isString: boolean; child: InspectorNodeMime | null }>;
}

function inspectorToMime(snapshot: Obj): InspectorNodeMime {
  const node: InspectorNodeMime = {
    title: str(snapshot.HeaderTitle),
    properties: array(snapshot.Properties)
      .filter(isObject)
      .map((property) => ({
        name: str(property.Name),
        value: str(property.SimpleValueText),
        isNull: property.IsNull === true,
        isString: false,
        child: isObject(property.ChildNode) ? inspectorToMime(property.ChildNode) : null
      }))
  };
  if (snapshot.IsCircularReference === true) node.circular = true;
  return node;
}

function inspectorFromMime(node: unknown): Obj | null {
  if (!isObject(node)) return null;
  return {
    HeaderTitle: str(node.title),
    IsExpandable: true,
    IsCircularReference: node.circular === true,
    Properties: array(node.properties)
      .filter(isObject)
      .map((property) => ({
        Name: str(property.name),
        SimpleValueText: property.isNull === true ? '<null>' : str(property.value),
        IsNull: property.isNull === true,
        IsComplexChild: isObject(property.child),
        ChildNode: inspectorFromMime(property.child)
      }))
  };
}

function inspectorText(node: InspectorNodeMime): string {
  return `${node.title}\n${node.properties.map((p) => `  ${p.name} = ${p.value}`).join('\n')}`;
}

// ─────────────────────────────────────────────────────────────────────────────────────────── bundles

/** The list of outputs itself, as a .frynb saves it when its fields cannot say it all: an item is text, or base64 for an image. */
export interface Bundle {
  Items: Array<{ Mime: string; Text?: string; Base64?: string }>;
  Metadata?: Record<string, unknown>;
}

export function bundlesFromOutputs(outputs: CellOutput[]): Bundle[] {
  return outputs.map((output) => {
    const bundle: Bundle = {
      Items: output.items.map((item) => {
        const asText = isBinaryMime(item.mime) ? undefined : text(item);
        return asText !== undefined && Buffer.compare(Buffer.from(utf8(asText)), Buffer.from(item.data)) === 0
          ? { Mime: item.mime, Text: asText }
          : { Mime: item.mime, Base64: toBase64(item.data) };
      })
    };
    if (output.metadata && Object.keys(output.metadata).length > 0) bundle.Metadata = output.metadata;
    return bundle;
  });
}

export function outputsFromBundles(bundles: unknown[]): CellOutput[] {
  const outputs: CellOutput[] = [];
  for (const bundle of bundles) {
    if (!isObject(bundle)) continue;
    const items: OutputItem[] = [];
    for (const item of array(bundle.Items)) {
      if (!isObject(item) || typeof item.Mime !== 'string') continue;
      if (typeof item.Text === 'string') items.push(textItem(item.Mime, item.Text));
      else if (typeof item.Base64 === 'string') items.push({ mime: item.Mime, data: fromBase64(item.Base64) });
    }
    if (items.length === 0) continue;
    outputs.push(isObject(bundle.Metadata) ? { items, metadata: bundle.Metadata } : { items });
  }
  return outputs;
}

// ─────────────────────────────────────────────────────────────────────────────────────────── comparing outputs

/**
 * Identifies outputs by what they say. With `forSlots` it ignores what the fields cannot keep: which of stdout and
 * stderr a line was, a text fallback next to something richer, and the order of an output's items.
 */
export function fingerprint(outputs: CellOutput[], forSlots = false): string {
  const hash = createHash('sha1');
  for (const output of outputs) {
    const items = output.items
      .filter((item) => !(forSlots && item.mime === 'text/plain' && output.items.some((other) => other.mime !== 'text/plain')))
      .map((item) => ({ mime: forSlots && item.mime === STDERR ? STDOUT : item.mime, body: canonical(item, forSlots) }));
    if (forSlots) items.sort((a, b) => (a.mime + a.body).localeCompare(b.mime + b.body));
    hash.update(JSON.stringify(items));
    hash.update('\u0000');
  }
  return hash.digest('hex');
}

function canonical(item: OutputItem, forSlots: boolean): string {
  if (isBinaryMime(item.mime)) return createHash('sha1').update(item.data).digest('hex');
  const value = text(item);
  if (item.mime.endsWith('json')) {
    try {
      // the desktop's inspector snapshot has no "isString" (it only colours a value), so it is not a reason to save the list
      return JSON.stringify(sortKeys(JSON.parse(value), forSlots && item.mime === INSPECTOR ? 'isString' : undefined));
    } catch {
      return value;
    }
  }
  return value;
}

function sortKeys(value: unknown, without?: string): unknown {
  if (Array.isArray(value)) return value.map((item) => sortKeys(item, without));
  if (isObject(value)) {
    return Object.fromEntries(
      Object.keys(value)
        .filter((key) => key !== without)
        .sort()
        .map((key) => [key, sortKeys(value[key], without)])
    );
  }
  return value;
}

// ─────────────────────────────────────────────────────────────────────────────────────────── from the engine

/**
 * An output as the sidecar sends it while a cell runs: a MIME bundle (RichOutputMime), with an image as base64 and JSON
 * as an object. The engine already put a page and a text fallback in every bundle, so nothing is drawn here.
 */
export function outputFromDisplay(data: Record<string, unknown>, metadata?: Record<string, unknown>): CellOutput {
  const items: OutputItem[] = Object.entries(data).map(([mime, value]) => {
    if (isBinaryMime(mime) && typeof value === 'string') return { mime, data: fromBase64(value) };
    return { mime, data: utf8(typeof value === 'string' ? value : JSON.stringify(value)) };
  });
  const output: CellOutput = { items: sortItems(items) };
  if (metadata && Object.keys(metadata).length > 0) output.metadata = metadata;
  return output;
}
