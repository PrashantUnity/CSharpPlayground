import type * as vscode from 'vscode';
import { unpack } from '../../../src/notebook/format/tabs';
import { CELL_OUTPUT_MIME } from '../../../src/shared/outputTabs';

// What a cell's output holds, as the tests ask about it. A cell that made several kinds of output has them as one output drawn as tabs
// (the setting frysharp.notebook.outputTabs, on unless turned off): these see the outputs it was made of, as a saved file has them.

export interface PlainOutput {
  items: Array<{ mime: string; data: Uint8Array }>;
  metadata?: Record<string, unknown>;
}

const decode = (data: Uint8Array) => new TextDecoder().decode(data);

/** The outputs of a cell, with a tabbed output as the outputs it holds. */
export function outputsOf(cell: vscode.NotebookCell): PlainOutput[] {
  return cell.outputs.flatMap((output) => unpack({ items: output.items.map((item) => ({ mime: item.mime, data: item.data })), metadata: output.metadata }));
}

/** Whether the cell's output is the one output drawn as tabs. */
export const isTabbed = (cell: vscode.NotebookCell): boolean => cell.outputs.some((output) => output.items.some((item) => item.mime === CELL_OUTPUT_MIME));

/** The items of a MIME type in a cell's outputs, read as JSON. */
export const itemsOf = (cell: vscode.NotebookCell, mime: string): unknown[] =>
  outputsOf(cell)
    .flatMap((output) => output.items)
    .filter((item) => item.mime === mime)
    .map((item) => JSON.parse(decode(item.data)));

/** What the cell printed (its stdout and stderr items), in order. */
export const textOfCell = (cell: vscode.NotebookCell): string =>
  outputsOf(cell)
    .flatMap((output) => output.items)
    .filter((item) => item.mime.startsWith('application/vnd.code.notebook.std'))
    .map((item) => decode(item.data))
    .join('');
