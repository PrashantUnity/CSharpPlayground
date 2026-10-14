// A cell's outputs as the one output the notebook shows them in when there are several kinds (console text, a table, a chart ...),
// and back. The file never sees it: the serializer packs what it read, and unpacks what it is about to write, so a notebook saved
// from here is the notebook that was opened (the same outputs, in the same order, byte for byte).

import { CellOutput, ERROR, fromBase64, isBinaryMime, jsonItem, OutputItem, STDERR, STDOUT, text, textItem, toBase64, utf8 } from './model';
import { CELL_OUTPUT_MIME, kindOfMimes, lastLines, PackedCell, PackedItem, PackedOutput, summaryOf, TabKind } from '../../shared/outputTabs';

export { CELL_OUTPUT_MIME };

const strict = new TextDecoder('utf-8', { fatal: true });

// text when the bytes are text that writes back as the same bytes, else base64
function packItem(item: OutputItem): PackedItem {
  if (!isBinaryMime(item.mime)) {
    try {
      const value = strict.decode(item.data);
      if (Buffer.compare(Buffer.from(utf8(value)), Buffer.from(item.data)) === 0) return { mime: item.mime, text: value };
    } catch {
      // not UTF-8: kept as it is
    }
  }
  return { mime: item.mime, base64: toBase64(item.data) };
}

function unpackItem(item: PackedItem): OutputItem {
  return item.text !== undefined ? textItem(item.mime, item.text) : { mime: item.mime, data: fromBase64(item.base64 ?? '') };
}

const isError = (output: CellOutput): boolean => output.items.some((item) => item.mime === ERROR);

// The outputs that are shown as tabs, and the errors after them, which are shown as VS Code shows an error. An error with anything
// after it, or anything of a kind the tabs cannot draw (a Jupyter widget, a Plotly figure), means the outputs are left as they are.
function parts(outputs: CellOutput[]): { head: CellOutput[]; errors: CellOutput[]; kinds: TabKind[] } | undefined {
  const first = outputs.findIndex(isError);
  const head = first < 0 ? outputs : outputs.slice(0, first);
  const errors = first < 0 ? [] : outputs.slice(first);
  if (!errors.every(isError)) return undefined;

  const kinds: TabKind[] = [];
  for (const output of head) {
    const kind = kindOfMimes(output.items.map((item) => item.mime));
    if (kind === undefined) return undefined;
    kinds.push(kind);
  }
  return { head, errors, kinds };
}

/** Whether the outputs are more than one kind of thing, every one of which the tabs can draw: only then are they one output. */
export function canPack(outputs: CellOutput[]): boolean {
  const found = parts(outputs);
  return found !== undefined && new Set(found.kinds).size > 1;
}

export interface PackOptions {
  /** Keep only this many lines at the end of each console output (the rest is counted): used while a cell runs, so a flood of lines is not sent again and again. */
  maxConsoleLines?: number;
}

/** The outputs as the cell's one output: the data for the tabs, and a text that says what is in them for anywhere that cannot draw them. */
export function pack(outputs: CellOutput[], options: PackOptions = {}): CellOutput {
  const packed: PackedCell = {
    v: 1,
    outputs: outputs.map((output): PackedOutput => {
      const result: PackedOutput = { items: output.items.map(packItem) };
      if (output.metadata && Object.keys(output.metadata).length > 0) result.metadata = output.metadata;
      if (options.maxConsoleLines !== undefined && result.items.every((item) => item.mime === STDOUT || item.mime === STDERR)) {
        let omitted = 0;
        for (const item of result.items) {
          const cut = lastLines(item.text ?? '', options.maxConsoleLines);
          item.text = cut.text;
          omitted += cut.omitted;
        }
        if (omitted > 0) result.omitted = omitted;
      }
      return result;
    })
  };
  return { items: [jsonItem(CELL_OUTPUT_MIME, packed), textItem('text/plain', summaryOf(packed))] };
}

/** The outputs an output was made of; an output that is not one of these is itself. */
export function unpack(output: CellOutput): CellOutput[] {
  const item = output.items.find((i) => i.mime === CELL_OUTPUT_MIME);
  if (!item) return [output];
  try {
    const packed = JSON.parse(text(item)) as PackedCell;
    if (packed?.v !== 1 || !Array.isArray(packed.outputs)) return [output];
    return packed.outputs.map((one) => {
      const result: CellOutput = { items: one.items.map(unpackItem) };
      if (one.metadata) result.metadata = one.metadata;
      return result;
    });
  } catch {
    return [output]; // damaged: left as it is rather than lost
  }
}

export const unpackAll = (outputs: CellOutput[]): CellOutput[] => outputs.flatMap(unpack);

/** The outputs of a cell as they are shown: one output when there is more than one kind (an error at the end stays after it), else as they are. */
export function forShowing(outputs: CellOutput[], enabled: boolean): CellOutput[] {
  const found = enabled ? parts(outputs) : undefined;
  return found && new Set(found.kinds).size > 1 ? [pack(found.head), ...found.errors] : outputs;
}
