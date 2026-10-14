import './vscodeStub'; // first: it stands in for 'vscode' before the serializer asks for it
import assert from 'node:assert/strict';
import * as fs from 'node:fs';
import * as path from 'node:path';
import { describe, it } from 'node:test';
import { outputFromDisplay } from '../../src/notebook/format/outputs';
import { INSPECTOR, STDERR, STDOUT, TABLE } from '../../src/notebook/format/model';
import { FryNotebookSerializer, toVsCodeOutput } from '../../src/notebook/notebookSerializer';
import { CELL_OUTPUT_MIME } from '../../src/notebook/format/tabs';

const fixtures = path.resolve(__dirname, '../../../test/fixtures');
const desktop = fs.readFileSync(path.join(fixtures, 'desktop.frynb'), 'utf8');
const jupyter = fs.readFileSync(path.join(fixtures, 'jupyter.ipynb'), 'utf8');
const token = {} as never;
const serializer = new FryNotebookSerializer();

const open = (json: string) => serializer.deserializeNotebook(new TextEncoder().encode(json), token);
const save = async (data: Parameters<FryNotebookSerializer['serializeNotebook']>[0]) => new TextDecoder().decode(await serializer.serializeNotebook(data, token));

describe('the notebook adapter', () => {
  it('opens a desktop notebook and saves it back unchanged', async () => {
    const saved = JSON.parse(await save(await open(desktop)));
    const original = JSON.parse(desktop);
    delete saved.LastModified;
    delete original.LastModified;

    assert.deepStrictEqual(saved, original);
  });

  it('opens a Jupyter notebook and saves it back unchanged', async () => {
    assert.equal(await save(await open(jupyter)), jupyter);
  });

  it('refuses a file that is not a notebook, instead of opening an empty one that a save would write over it', async () => {
    await assert.rejects(open('{ this is not json'), /not a FrySharp or Jupyter notebook/);
    await assert.rejects(open('[1, 2]'), /not a FrySharp or Jupyter notebook/);
  });

  it('opens an empty file as a new notebook', async () => {
    const data = await open('  \n');
    assert.equal(data.cells.length, 0);
    assert.equal(data.metadata?.kernel, 'csharp');
  });

  it('gives a new cell an id, and a copied cell its own', async () => {
    const data = await open(desktop);
    const first = data.cells[0];
    const copy = Object.assign(Object.create(Object.getPrototypeOf(first)), first, { metadata: { ...first.metadata } });
    const fresh = Object.assign(Object.create(Object.getPrototypeOf(first)), first, { metadata: undefined });
    data.cells.push(copy, fresh);

    const ids = JSON.parse(await save(data)).Cells.map((c: { Id: string }) => c.Id);
    assert.equal(new Set(ids).size, ids.length);
    assert.equal(ids[0], 'intro');
    assert.ok(ids.slice(-2).every((id: string) => id.length > 0));
  });

  it('writes the outputs of a run that came from the engine, and what the desktop reads of them', async () => {
    const data = await open(desktop);
    const cell = data.cells.find((c) => c.metadata?.id === 'text')!;
    cell.outputs = [
      toVsCodeOutput(outputFromDisplay({ 'text/html': '<table class="fry-tbl-container"></table>', [TABLE]: { title: 'T', columns: ['A'], numeric: [true], rows: [['1']], totalRows: 1, totalColumns: 1 }, 'text/plain': 'T' }))
    ];
    cell.executionSummary = { executionOrder: 9, success: true, timing: { startTime: 1000, endTime: 1042 } };

    const written = JSON.parse(await save(data)).Cells.find((c: { Id: string }) => c.Id === 'text');
    assert.equal(written.ExecutionCount, 9);
    assert.equal(written.ExecutionTimeText, '42 ms');
    assert.equal(written.OutputText, '');
    assert.equal(written.TableSnapshot.Title, 'T');
    assert.equal(written.TableSnapshot.Columns[0].IsNumeric, true);
    assert.equal(written.HtmlContent, '<table class="fry-tbl-container"></table>');
  });

  it('writes a failed run as a failed cell and keeps the language of a cell that is not the notebook\'s', async () => {
    const data = await open(desktop);
    const cell = data.cells.find((c) => c.metadata?.id === 'text')!;
    cell.executionSummary = { executionOrder: 2, success: false };
    cell.languageId = 'javascript';

    const written = JSON.parse(await save(data)).Cells.find((c: { Id: string }) => c.Id === 'text');
    assert.equal(written.HasError, true);
    assert.equal(written.Language, 'javascript');
  });
});

describe('an output from the engine', () => {
  it('puts the page first and the text last, and decodes an image', () => {
    const output = outputFromDisplay({ 'text/plain': 'x', [INSPECTOR]: { title: 't', properties: [] }, 'image/png': 'iVBORw0KGgo=', 'text/html': '<b/>' });

    assert.deepStrictEqual(output.items.map((i) => i.mime), ['text/html', INSPECTOR, 'image/png', 'text/plain']);
    assert.equal(output.items[2].data[0], 0x89); // the PNG signature, not the base64 text
    assert.deepStrictEqual(JSON.parse(new TextDecoder().decode(output.items[1].data)), { title: 't', properties: [] });
  });

  it('keeps the metadata an image came with', () => {
    assert.deepStrictEqual(outputFromDisplay({ 'image/png': 'AA==' }, { 'image/png': { width: 3, height: 2 } }).metadata, { 'image/png': { width: 3, height: 2 } });
    assert.equal(outputFromDisplay({ 'text/plain': 'x' }, {}).metadata, undefined);
  });

  it('is not what a stream is: text a cell prints stays stdout and stderr', () => {
    assert.ok([STDOUT, STDERR].every((mime) => mime.startsWith('application/vnd.code.notebook.')));
  });
});


describe('a cell with several kinds of output, shown as tabs', () => {
  const tabbed = new FryNotebookSerializer('frynb', () => true);
  const openTabbed = (json: string) => tabbed.deserializeNotebook(new TextEncoder().encode(json), token);
  const saveTabbed = async (data: Parameters<FryNotebookSerializer['serializeNotebook']>[0]) => new TextDecoder().decode(await tabbed.serializeNotebook(data, token));

  it('opens the desktop notebook\'s text-and-table cell as one output, and saves it back unchanged', async () => {
    const data = await openTabbed(desktop);
    const cell = data.cells.find((c) => c.metadata?.id === 'table')!;

    assert.equal(cell.outputs!.length, 1);
    assert.equal(cell.outputs![0].items[0].mime, CELL_OUTPUT_MIME);
    // a cell with one kind of output is left as it is
    assert.equal(data.cells.find((c) => c.metadata?.id === 'text')!.outputs!.length, 1);
    assert.equal(data.cells.find((c) => c.metadata?.id === 'text')!.outputs![0].items[0].mime, STDOUT);

    const saved = JSON.parse(await saveTabbed(data));
    const original = JSON.parse(desktop);
    delete saved.LastModified;
    delete original.LastModified;
    assert.deepStrictEqual(saved, original);
  });

  it('is the same file whether the tabs are on or off', async () => {
    const withTabs = JSON.parse(await saveTabbed(await openTabbed(desktop)));
    const without = JSON.parse(await save(await open(desktop)));
    delete withTabs.LastModified;
    delete without.LastModified;

    assert.deepStrictEqual(withTabs, without);
  });

  it('opens a Jupyter notebook with tabs and saves it back byte for byte', async () => {
    assert.equal(await saveTabbed(await openTabbed(jupyter)), jupyter);
  });
});
