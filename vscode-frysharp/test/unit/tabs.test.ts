import assert from 'node:assert/strict';
import { test } from 'node:test';
import { CELL_OUTPUT_MIME, canPack, forShowing, pack, unpack, unpackAll } from '../../src/notebook/format/tabs';
import { CellOutput, jsonItem, STDERR, STDOUT, TABLE, textItem } from '../../src/notebook/format/model';
import { PackedCell } from '../../src/shared/outputTabs';

const stdout = (value: string): CellOutput => ({ items: [textItem(STDOUT, value)] });
const table: CellOutput = { items: [textItem('text/html', '<div class="fry-tbl"></div>'), jsonItem(TABLE, { title: 'T', columns: ['a'], numeric: [true], rows: [[1], [2]], totalRows: 2 }), textItem('text/plain', 'T')] };
const png: CellOutput = { items: [{ mime: 'image/png', data: Uint8Array.from([0x89, 0x50, 0x4e, 0x47, 0, 255, 128]) }, textItem('text/plain', '<image>')], metadata: { 'image/png': { width: 4, height: 2 } } };

test('console text and a table are two kinds, so they are one output; one kind of output is left as it is', () => {
  assert.equal(canPack([stdout('a'), table]), true);
  assert.equal(canPack([stdout('a'), stdout('b')]), false);
  assert.equal(canPack([table]), false);
  assert.equal(canPack([]), false);
});

test('an output of a kind the tabs cannot draw is not packed (it has a way of drawing it already)', () => {
  const widget: CellOutput = { items: [textItem('application/vnd.jupyter.widget-view+json', '{}')] };
  assert.equal(canPack([stdout('a'), widget]), false);
  assert.equal(canPack([stdout('a'), table, widget]), false);
});

test('an error after the outputs stays after the tabs, as VS Code shows it; an error among them means they are left as they are', () => {
  const error: CellOutput = { items: [textItem('application/vnd.code.notebook.error', '{"name":"E","message":"m"}')] };

  assert.equal(canPack([stdout('a'), table, error]), true);
  const shown = forShowing([stdout('a'), table, error], true);
  assert.equal(shown.length, 2);
  assert.equal(shown[0].items[0].mime, CELL_OUTPUT_MIME);
  assert.deepStrictEqual(shown[1], error);
  assert.deepStrictEqual(unpackAll(shown), [stdout('a'), table, error]);

  // an error with only one kind before it: nothing to tab
  assert.equal(canPack([stdout('a'), error]), false);
  assert.equal(canPack([stdout('a'), error, table]), false);
  assert.deepStrictEqual(forShowing([stdout('a'), error, table], true), [stdout('a'), error, table]);
});

test('packing and unpacking give back the same outputs: items, bytes, metadata and order', () => {
  const outputs = [stdout('one\ntwo ✓ \u65e5\u672c\n'), table, png, { items: [textItem(STDERR, 'oops\n')] }, stdout('after')];

  const packed = pack(outputs);

  assert.equal(packed.items[0].mime, CELL_OUTPUT_MIME);
  assert.equal(packed.items[1].mime, 'text/plain');
  assert.deepStrictEqual(unpack(packed), outputs);
});

test('bytes that are not text are kept as they are', () => {
  const odd: CellOutput = { items: [{ mime: 'application/vnd.code.notebook.stdout', data: Uint8Array.from([0x61, 0xff, 0xfe, 0x62]) }] };

  assert.deepStrictEqual(unpack(pack([odd, table])), [odd, table]);
});

test('while a cell runs, a console is cut to its last lines and says how many it left off; the full text is still what unpacks when nothing was cut', () => {
  const lines = Array.from({ length: 50 }, (_, i) => `line ${i}`).join('\n') + '\n';

  const live = JSON.parse(new TextDecoder().decode(pack([stdout(lines), table], { maxConsoleLines: 10 }).items[0].data)) as PackedCell;

  assert.equal(live.outputs[0].omitted, 40);
  assert.equal(live.outputs[0].items[0].text, Array.from({ length: 10 }, (_, i) => `line ${i + 40}`).join('\n') + '\n');
  assert.equal(live.outputs[1].omitted, undefined);
  assert.deepStrictEqual(unpack(pack([stdout('short\n'), table], { maxConsoleLines: 10 })), [stdout('short\n'), table]);
});

test('an output that is not packed unpacks to itself, and a damaged one is left as it is', () => {
  assert.deepStrictEqual(unpack(stdout('x')), [stdout('x')]);
  const damaged: CellOutput = { items: [textItem(CELL_OUTPUT_MIME, '{not json')] };
  assert.deepStrictEqual(unpack(damaged), [damaged]);
  assert.deepStrictEqual(unpackAll([stdout('a'), pack([stdout('b'), table])]), [stdout('a'), stdout('b'), table]);
});

test('outputs are shown as one when there is more than one kind and the setting is on, else as they are', () => {
  const outputs = [stdout('a'), table];
  assert.equal(forShowing(outputs, true).length, 1);
  assert.deepStrictEqual(forShowing(outputs, false), outputs);
  assert.deepStrictEqual(forShowing([stdout('a')], true), [stdout('a')]);
});
