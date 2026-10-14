// What a cell's output is when it is shown as tabs (Table, Console, Charts ... All), as the desktop app shows it: the plain data
// the extension puts in the one output it makes for such a cell, and the questions both sides ask of it (which tabs, how many
// lines, which text). No DOM and no Node here: the notebook renderer (a browser) and the extension (Node) both import it.

/** The one output of a cell with several kinds of output: every output the cell made, kept as it was, for the renderer to draw as tabs. */
export const CELL_OUTPUT_MIME = 'application/vnd.fry.cell-output+json';

export const TABLE_MIME = 'application/vnd.fry.table+json';
export const INSPECTOR_MIME = 'application/vnd.fry.inspector+json';
export const STDOUT_MIME = 'application/vnd.code.notebook.stdout';
export const STDERR_MIME = 'application/vnd.code.notebook.stderr';
export const ERROR_MIME = 'application/vnd.code.notebook.error';

/** One item of an output: text, or base64 for what is not text (an image). */
export interface PackedItem {
  mime: string;
  text?: string;
  base64?: string;
}

export interface PackedOutput {
  items: PackedItem[];
  metadata?: Record<string, unknown>;
  /** Text output only: how many lines were left off its start (shown while a cell runs, so a flood of lines is not sent again and again). */
  omitted?: number;
}

export interface PackedCell {
  v: 1;
  outputs: PackedOutput[];
}

/** The kinds of output a cell has, in the order the desktop lists them (its tabs). */
export const TAB_KINDS = ['table', 'console', 'inspector', 'image', 'chart', 'visualizer', 'plot3d', 'html', 'markdown'] as const;
export type TabKind = (typeof TAB_KINDS)[number];

export const TAB_LABELS: Record<TabKind, string> = {
  table: 'Table',
  console: 'Console',
  inspector: 'Inspector',
  image: 'Image',
  chart: 'Chart',
  visualizer: 'Visualizer',
  plot3d: '3D',
  html: 'HTML',
  markdown: 'Markdown'
};

/** What one output is, from the MIME types it holds: the richest type decides (a table output also holds its page and a text fallback). Undefined for one that is none of these. */
export function kindOfMimes(mimes: readonly string[]): TabKind | undefined {
  if (mimes.includes(TABLE_MIME)) return 'table';
  if (mimes.includes(INSPECTOR_MIME)) return 'inspector';
  for (const mime of mimes) {
    const visual = /^application\/vnd\.fry\.(chart|plot3d|visualizer)\.v\d+\+json$/.exec(mime);
    if (visual) return visual[1] as TabKind;
  }
  if (mimes.includes(STDOUT_MIME) || mimes.includes(STDERR_MIME)) return 'console';
  if (mimes.some((mime) => mime.startsWith('image/'))) return 'image';
  if (mimes.includes('text/html')) return 'html';
  if (mimes.includes('text/markdown')) return 'markdown';
  if (mimes.includes('text/plain')) return 'console';
  return undefined;
}

export const kindOf = (output: PackedOutput): TabKind | undefined => kindOfMimes(output.items.map((item) => item.mime));

export interface TabInfo {
  /** 'all' shows every output, in order. */
  id: TabKind | 'all';
  label: string;
  /** The badge: rows of the table, lines of the console, how many outputs of that kind (when more than one). */
  count?: number;
  /** The outputs the tab shows, by their place in the cell's list. */
  outputs: number[];
}

/** The text of a console output (what a cell printed: stdout and stderr, or the plain text of a display). */
export function consoleTextOf(output: PackedOutput): string {
  const printed = output.items.filter((item) => item.mime === STDOUT_MIME || item.mime === STDERR_MIME);
  const items = printed.length > 0 ? printed : output.items.filter((item) => item.mime === 'text/plain');
  return items.map((item) => item.text ?? '').join('');
}

/** How many lines text has (a last line break does not start another). */
export function countLines(text: string): number {
  if (text.length === 0) return 0;
  const breaks = text.split('\n').length - 1;
  return text.endsWith('\n') ? breaks : breaks + 1;
}

function rowsOf(output: PackedOutput): number {
  const item = output.items.find((i) => i.mime === TABLE_MIME);
  if (!item?.text) return 0;
  try {
    const table = JSON.parse(item.text) as { rows?: unknown[]; totalRows?: number };
    return typeof table.totalRows === 'number' ? table.totalRows : Array.isArray(table.rows) ? table.rows.length : 0;
  } catch {
    return 0;
  }
}

/** The tabs of a cell's outputs: one for each kind present, as the desktop orders them, and All when there is more than one. */
export function tabsOf(cell: PackedCell): TabInfo[] {
  const tabs: TabInfo[] = [];
  for (const kind of TAB_KINDS) {
    const outputs = cell.outputs.flatMap((output, index) => (kindOf(output) === kind ? [index] : []));
    if (outputs.length === 0) continue;
    const shown = outputs.map((index) => cell.outputs[index]);
    let count: number | undefined;
    if (kind === 'table') count = shown.reduce((sum, output) => sum + rowsOf(output), 0);
    else if (kind === 'console') count = shown.reduce((sum, output) => sum + countLines(consoleTextOf(output)) + (output.omitted ?? 0), 0);
    else if (outputs.length > 1) count = outputs.length;
    tabs.push({ id: kind, label: TAB_LABELS[kind], count, outputs });
  }
  if (tabs.length > 1) tabs.push({ id: 'all', label: 'All', outputs: cell.outputs.map((_, index) => index) });
  return tabs;
}

/** The text a short description of the cell's output: what a place that cannot draw tabs shows (and an accessible reading of it). */
export function summaryOf(cell: PackedCell, maxLines = 40): string {
  const tabs = tabsOf(cell).filter((tab) => tab.id !== 'all');
  const parts = tabs.map((tab) => (tab.count === undefined ? tab.label : `${tab.label} (${tab.count})`));
  const lines = cell.outputs.filter((output) => kindOf(output) === 'console').map(consoleTextOf).join('').split('\n');
  const shown = lines.slice(0, maxLines).join('\n').trimEnd();
  return `[${parts.join(' · ')}]${shown ? '\n' + shown : ''}${lines.length > maxLines ? '\n…' : ''}`;
}

/** Keeps the last `maxLines` lines of a text, and says how many it left off. */
export function lastLines(text: string, maxLines: number): { text: string; omitted: number } {
  const lines = text.split('\n');
  const trailing = text.endsWith('\n');
  const count = trailing ? lines.length - 1 : lines.length;
  if (count <= maxLines) return { text, omitted: 0 };
  const kept = lines.slice(trailing ? count - maxLines : lines.length - maxLines, trailing ? count : undefined);
  return { text: kept.join('\n') + (trailing ? '\n' : ''), omitted: count - maxLines };
}

// ── ANSI text ────────────────────────────────────────────────────────────────────────────────────────────────────

export interface AnsiSpan {
  text: string;
  /** A CSS colour (a theme variable with a fallback, or rgb()). */
  fg?: string;
  bg?: string;
  bold?: boolean;
  dim?: boolean;
  italic?: boolean;
  underline?: boolean;
}

const BASIC = ['Black', 'Red', 'Green', 'Yellow', 'Blue', 'Magenta', 'Cyan', 'White'];
const FALLBACK = ['#000000', '#cd3131', '#0dbc79', '#e5e510', '#2472c8', '#bc3fbc', '#11a8cd', '#e5e5e5', '#666666', '#f14c4c', '#23d18b', '#f5f543', '#3b8eea', '#d670d6', '#29b8db', '#e5e5e5'];

// The terminal colours of the editor's theme (--vscode-terminal-ansiRed ...), so the console looks like VS Code's own.
function theme(index: number): string {
  const name = (index >= 8 ? 'Bright' : '') + BASIC[index % 8];
  return `var(--vscode-terminal-ansi${name}, ${FALLBACK[index]})`;
}

function xterm256(n: number): string | undefined {
  if (n < 0 || n > 255) return undefined;
  if (n < 16) return theme(n);
  if (n >= 232) {
    const level = 8 + (n - 232) * 10;
    return `rgb(${level}, ${level}, ${level})`;
  }
  const cube = n - 16;
  const channel = (value: number) => (value === 0 ? 0 : 55 + value * 40);
  return `rgb(${channel(Math.floor(cube / 36))}, ${channel(Math.floor(cube / 6) % 6)}, ${channel(cube % 6)})`;
}

/**
 * Applies carriage returns as a terminal does, for the progress bars that redraw one line: what follows a return replaces the line so far
 * (only the part it covers: a shorter redraw leaves the rest of the old line behind, as it would on screen).
 */
export function applyCarriageReturns(text: string): string {
  if (!text.includes('\r')) return text;
  return text
    .replace(/\r\n/g, '\n')
    .split('\n')
    .map((line) => {
      if (!line.includes('\r')) return line;
      let shown = '';
      for (const part of line.split('\r')) shown = part + shown.slice(part.length);
      return shown;
    })
    .join('\n');
}

/** Text with ANSI colours and styles as runs of text and what to draw them with; other escape sequences are dropped. */
export function parseAnsi(text: string): AnsiSpan[] {
  const spans: AnsiSpan[] = [];
  let style: Omit<AnsiSpan, 'text'> = {};
  let position = 0;
  // the pattern is made of the escape character: that is what is being looked for
  // eslint-disable-next-line no-control-regex
  const pattern = /\u001b\[([?<>=]?[0-9;:]*)([A-Za-z])|\u001b\][^\u0007\u001b]*(?:\u0007|\u001b\\)|\u001b[()][A-Za-z0-9]/g;
  const push = (end: number) => {
    if (end > position) spans.push({ text: text.slice(position, end), ...style });
  };

  for (let match = pattern.exec(text); match; match = pattern.exec(text)) {
    push(match.index);
    position = match.index + match[0].length;
    if (match[2] !== 'm') continue; // cursor movement, clearing: not drawn
    const codes = (match[1] === '' ? '0' : match[1]).split(/[;:]/).map((code) => (code === '' ? 0 : Number(code)));
    for (let i = 0; i < codes.length; i++) {
      const code = codes[i];
      if (code === 0) style = {};
      else if (code === 1) style = { ...style, bold: true };
      else if (code === 2) style = { ...style, dim: true };
      else if (code === 3) style = { ...style, italic: true };
      else if (code === 4) style = { ...style, underline: true };
      else if (code === 22) style = { ...style, bold: undefined, dim: undefined };
      else if (code === 23) style = { ...style, italic: undefined };
      else if (code === 24) style = { ...style, underline: undefined };
      else if (code >= 30 && code <= 37) style = { ...style, fg: theme(code - 30) };
      else if (code >= 90 && code <= 97) style = { ...style, fg: theme(code - 90 + 8) };
      else if (code >= 40 && code <= 47) style = { ...style, bg: theme(code - 40) };
      else if (code >= 100 && code <= 107) style = { ...style, bg: theme(code - 100 + 8) };
      else if (code === 39) style = { ...style, fg: undefined };
      else if (code === 49) style = { ...style, bg: undefined };
      else if (code === 38 || code === 48) {
        const key = code === 38 ? 'fg' : 'bg';
        if (codes[i + 1] === 5) {
          const colour = xterm256(codes[i + 2]);
          if (colour) style = { ...style, [key]: colour };
          i += 2;
        } else if (codes[i + 1] === 2) {
          const [r, g, b] = [codes[i + 2], codes[i + 3], codes[i + 4]];
          if ([r, g, b].every((n) => Number.isInteger(n) && n >= 0 && n <= 255)) style = { ...style, [key]: `rgb(${r}, ${g}, ${b})` };
          i += 4;
        }
      }
    }
  }

  push(text.length);
  return spans;
}
