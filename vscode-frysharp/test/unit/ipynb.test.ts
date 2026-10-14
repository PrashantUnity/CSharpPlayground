import assert from 'node:assert/strict';
import * as fs from 'node:fs';
import * as path from 'node:path';
import { describe, it } from 'node:test';
import { parseIpynb, writeIpynb } from '../../src/notebook/format/ipynb';
import { ERROR, STDERR, STDOUT, textItem } from '../../src/notebook/format/model';

// A notebook Jupyter wrote (test/fixtures/jupyter.ipynb).
const text = fs.readFileSync(path.resolve(__dirname, '../../../test/fixtures/jupyter.ipynb'), 'utf8');

describe('a Jupyter notebook', () => {
  it('is written back exactly as it was read', () => {
    assert.equal(writeIpynb(parseIpynb(text)), text);
  });

  it('keeps cell tags, notebook metadata, and a raw cell as raw', () => {
    const written = JSON.parse(writeIpynb(parseIpynb(text)));
    assert.deepStrictEqual(written.cells[0].metadata.tags, ['intro']);
    assert.deepStrictEqual(written.cells[1].metadata, { collapsed: false, tags: ['slow'] });
    assert.deepStrictEqual(written.metadata.custom, { keep: 'me' });
    assert.equal(written.cells[4].cell_type, 'raw');
  });

  it('reads outputs as VS Code has them: stdout and stderr, an error, an image, a page, JSON', () => {
    const cells = parseIpynb(text).cells;
    const mimes = (index: number) => cells[index].outputs.map((o) => o.items.map((i) => i.mime));

    assert.deepStrictEqual(mimes(1), [[STDOUT], [STDERR]]);
    assert.deepStrictEqual(mimes(2), [['image/png', 'text/plain'], ['text/html', 'application/json', 'text/plain']]);
    assert.deepStrictEqual(mimes(3), [[ERROR]]);
    assert.equal(cells[3].failed, true);
    assert.equal(cells[3].language, 'python');
    assert.equal(cells[1].executionCount, 1);
  });

  it('keeps an error an error, and stderr stderr, when the outputs change', () => {
    const notebook = parseIpynb(text);
    notebook.cells[1].outputs = [{ items: [textItem(STDERR, 'now an error stream\n')] }, ...notebook.cells[3].outputs];
    notebook.cells[3].outputs = [];

    const written = JSON.parse(writeIpynb(notebook));
    assert.deepStrictEqual(written.cells[1].outputs[0], { output_type: 'stream', name: 'stderr', text: ['now an error stream\n'] });
    assert.equal(written.cells[1].outputs[1].output_type, 'error');
    assert.equal(written.cells[1].outputs[1].ename, 'ZeroDivisionError');
    assert.deepStrictEqual(written.cells[3].outputs, []);
    assert.deepStrictEqual(written.cells[0], JSON.parse(text).cells[0]); // untouched cells are as they were
  });

  it('writes a new cell as Jupyter does, with its language', () => {
    const notebook = parseIpynb(text);
    notebook.cells.push({ id: 'new', kind: 'code', language: 'javascript', source: 'a\nb', failed: false, outputs: [], state: { extra: {}, snapshots: {}, outputsKey: '' } });

    const cell = JSON.parse(writeIpynb(notebook)).cells[5];
    assert.deepStrictEqual(cell.source, ['a\n', 'b']);
    assert.equal(cell.execution_count, null);
    assert.equal(cell.metadata.polyglot_notebook.kernelName, 'javascript');
  });

  it('is rejected when it is not a notebook', () => {
    assert.throws(() => parseIpynb('{"nbformat":4}'));
  });
});
