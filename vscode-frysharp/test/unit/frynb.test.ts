import assert from 'node:assert/strict';
import * as fs from 'node:fs';
import * as path from 'node:path';
import { describe, it } from 'node:test';
import { parseFrynb, writeFrynb } from '../../src/notebook/format/frynb';
import {
  CellModel,
  CellOutput,
  fromBase64,
  INSPECTOR,
  jsonItem,
  STDERR,
  STDOUT,
  TABLE,
  textItem
} from '../../src/notebook/format/model';
import { fingerprint } from '../../src/notebook/format/outputs';

// vscode-frysharp/test/fixtures: desktop.frynb is written by the desktop's own writer (Tests.Core/ExtensionFixturesTests),
// written/ holds what this extension writes from it, which the desktop's tests read back.
const fixtures = path.resolve(__dirname, '../../../test/fixtures');
const read = (name: string) => fs.readFileSync(path.join(fixtures, name), 'utf8');
const NOW = new Date('2026-10-14T09:00:00.000Z');

function withoutTimestamps(json: string): unknown {
  const parsed = JSON.parse(json);
  delete parsed.LastModified;
  return parsed;
}

describe('a notebook the desktop wrote', () => {
  const desktop = read('desktop.frynb');

  it('is written back as the same notebook, field for field', () => {
    const written = writeFrynb(parseFrynb(desktop), NOW);

    assert.deepStrictEqual(withoutTimestamps(written), withoutTimestamps(desktop));
    fs.writeFileSync(path.join(fixtures, 'written', 'desktop.frynb'), written);
  });

  it('shows every output the desktop kept, in VS Code terms', () => {
    const notebook = parseFrynb(desktop);
    const cell = (id: string) => notebook.cells.find((c) => c.id === id) as CellModel;
    const mimes = (id: string) => cell(id).outputs.map((o) => o.items.map((i) => i.mime));

    assert.deepStrictEqual(mimes('text'), [[STDOUT]]);
    assert.deepStrictEqual(mimes('failed'), [[STDERR]]);
    assert.equal(cell('failed').failed, true);

    // the output text first, then the table with the page the desktop drew it with
    assert.deepStrictEqual(mimes('table'), [[STDOUT], ['text/html', TABLE, 'text/plain']]);
    assert.deepStrictEqual(mimes('inspector'), [['text/html', INSPECTOR, 'text/plain']]);
    assert.deepStrictEqual(mimes('chart'), [['text/html', 'application/vnd.fry.chart.v1+json', 'text/plain']]);
    assert.deepStrictEqual(mimes('image'), [['text/html', 'image/png', 'text/plain']]);

    assert.equal(cell('collapsed').language, 'python');
    assert.equal(cell('intro').kind, 'markdown');
    assert.equal(notebook.kernel, 'csharp');
  });

  it('keeps what only the desktop knows: collapse flags, the cell that had a live control, its own table snapshot', () => {
    const written = JSON.parse(writeFrynb(parseFrynb(desktop), NOW));
    const original = JSON.parse(desktop);
    const pick = (notebook: { Cells: Array<Record<string, unknown>> }, id: string) => notebook.Cells.find((c) => c.Id === id) as Record<string, unknown>;

    const collapsed = pick(written, 'collapsed');
    assert.equal(collapsed.IsInputCollapsed, true);
    assert.equal(collapsed.IsOutputScrolled, true);
    assert.equal(collapsed.HadInteractiveControl, true);
    assert.equal(collapsed.ExecutionTimeText, '12 ms');
    assert.deepStrictEqual(pick(written, 'table').TableSnapshot, pick(original, 'table').TableSnapshot);
  });

  it('is not given an OutputBundles copy while its fields say everything', () => {
    const written = JSON.parse(writeFrynb(parseFrynb(desktop), NOW));
    assert.ok(written.Cells.every((c: Record<string, unknown>) => !('OutputBundles' in c)));
  });
});

// What the sidecar sends for a cell: the engine's own MIME bundles (RichOutputMime), each with its page and a text fallback.
describe('a notebook saved after a run', () => {
  const desktop = JSON.parse(read('desktop.frynb'));
  const cellOf = (id: string) => desktop.Cells.find((c: Record<string, unknown>) => c.Id === id);
  const tableHtml: string = cellOf('table').HtmlContent;
  const inspectorHtml: string = cellOf('inspector').HtmlContent;
  const chartHtml: string = cellOf('chart').HtmlContent;
  const chart = cellOf('chart').Visuals[0];
  const imageHtml: string = cellOf('image').HtmlContent;

  const table: CellOutput = {
    items: [
      textItem('text/html', tableHtml),
      jsonItem(TABLE, { title: 'People', columns: ['Name', 'Age'], numeric: [false, true], rows: [['Ada', '36'], ['Grace', null]], totalRows: 2, totalColumns: 2 }),
      textItem('text/plain', 'People')
    ]
  };
  const inspector: CellOutput = {
    items: [
      textItem('text/html', inspectorHtml),
      jsonItem(INSPECTOR, {
        title: 'People',
        properties: [
          { name: 'name', value: 'Code', isNull: false, isString: true, child: null },
          { name: 'another', value: '', isNull: false, isString: false, child: { title: 'People', circular: true, properties: [] } }
        ]
      }),
      textItem('text/plain', 'People')
    ]
  };
  const visual: CellOutput = { items: [textItem('text/html', chartHtml), jsonItem(chart.MimeType, chart.Data), textItem('text/plain', '<Chart>')] };
  const image: CellOutput = {
    items: [textItem('text/html', imageHtml), { mime: 'image/png', data: fromBase64(cellOf('image').ImageBytes) }, textItem('text/plain', '<image>')],
    metadata: { 'image/png': { width: 1, height: 1 } }
  };
  const stream: CellOutput = { items: [textItem(STDOUT, 'Result:\n')] };

  const run = (outputs: CellOutput[], failed = false): CellModel => ({
    id: 'run',
    kind: 'code',
    source: 'Display.Everything();',
    executionCount: 3,
    executionMs: 12,
    failed,
    outputs,
    state: { extra: {}, snapshots: {}, outputsKey: '' }
  });

  const notebookWith = (...cells: CellModel[]) => ({ format: 'frynb' as const, kernel: 'csharp', root: { Id: 'after-a-run', Title: 'After a run' }, cells });

  it('keeps a table, an inspector, a chart and an image in the fields the desktop reads, and the whole list besides', () => {
    const failed: CellModel = { ...run([{ items: [textItem(STDOUT, 'boom\n')] }], true), id: 'failed', executionCount: 4, executionMs: undefined };
    const written = writeFrynb(notebookWith(run([stream, table, inspector, visual, image]), failed), NOW);
    fs.writeFileSync(path.join(fixtures, 'written', 'after-a-run.frynb'), written);

    const cell = JSON.parse(written).Cells[0];
    assert.equal(cell.OutputText, 'Result:\n');
    assert.equal(cell.ExecutionTimeText, '12 ms');
    assert.equal(cell.TableSnapshot.Title, 'People');
    assert.equal(cell.InspectorSnapshot.HeaderTitle, 'People');
    assert.equal(cell.Visuals.length, 1);
    assert.equal(cell.ImageFormat, 'PNG');
    assert.equal(cell.ImageWidth, 1);
    assert.equal(cell.HasOutput, true);
    // the desktop has one slot for each kind and an order of its own: the extension also keeps the outputs as they were
    assert.equal(cell.OutputBundles.length, 5);
  });

  it('reads back as the outputs it was given', () => {
    const outputs = [stream, table, inspector, visual, image];
    const back = parseFrynb(writeFrynb(notebookWith(run(outputs)), NOW)).cells[0];

    assert.equal(fingerprint(back.outputs), fingerprint(outputs));
    assert.equal(back.executionCount, 3);
  });

  it('does not save the list again when the fields say it all', () => {
    // (the desktop keeps one page, the last output's: earlier ones that came with a page of their own need the list)
    const bare = (output: CellOutput): CellOutput => ({ ...output, items: output.items.filter((item) => item.mime !== 'text/html') });
    for (const outputs of [[table], [stream, table], [inspector], [visual], [image], [stream, bare(image), bare(table), bare(inspector), visual]]) {
      const cell = JSON.parse(writeFrynb(notebookWith(run(outputs)), NOW)).Cells[0];
      assert.equal('OutputBundles' in cell, false, `${outputs.length} output(s)`);
      assert.equal(fingerprint(parseFrynb(writeFrynb(notebookWith(run(outputs)), NOW)).cells[0].outputs, true), fingerprint(outputs, true));
    }
  });

  it('saves the list when each output has a page of its own, since the desktop keeps only the last', () => {
    const cell = JSON.parse(writeFrynb(notebookWith(run([table, inspector])), NOW)).Cells[0];
    assert.equal(cell.OutputBundles.length, 2);
  });

  it('keeps two tables of one cell, which the desktop has one slot for', () => {
    const second: CellOutput = { items: [jsonItem(TABLE, { title: 'Other', columns: ['X'], numeric: [true], rows: [['1']], totalRows: 1, totalColumns: 1 }), textItem('text/plain', 'Other')] };
    const written = writeFrynb(notebookWith(run([table, stream, second])), NOW);

    const back = parseFrynb(written).cells[0];
    assert.equal(fingerprint(back.outputs), fingerprint([table, stream, second]));
    assert.equal(JSON.parse(written).Cells[0].TableSnapshot.Title, 'Other'); // the desktop shows the last
  });

  it('a failed cell is a failed cell', () => {
    const cell = JSON.parse(writeFrynb(notebookWith(run([{ items: [textItem(STDERR, 'boom\n')] }], true)), NOW)).Cells[0];
    assert.equal(cell.HasError, true);
    assert.equal(cell.OutputText, 'boom\n');
  });

  it('the error line the extension adds to a failed cell is not written: its text is in the cell\'s text already', () => {
    const error: CellOutput = { items: [textItem('application/vnd.code.notebook.error', '{"name":"CS0029","message":"Cannot convert"}')], metadata: { fryEngineError: true } };

    const cell = JSON.parse(writeFrynb(notebookWith(run([{ items: [textItem(STDOUT, 'Line 1: CS0029 Cannot convert\n')] }, error], true)), NOW)).Cells[0];

    assert.equal(cell.HasError, true);
    assert.equal(cell.OutputText, 'Line 1: CS0029 Cannot convert\n');
    assert.equal(cell.OutputBundles ?? null, null);
  });
});

describe('reading', () => {
  it('keeps fields it does not know, in a cell and in the notebook', () => {
    const json = JSON.stringify({
      SchemaVersion: '1.0',
      Kernel: 'csharp',
      FutureRootField: { a: 1 },
      Cells: [{ Id: 'a', Type: 0, Source: 'x', FutureCellField: [1, 2], IsOutputCollapsed: true }]
    });

    const written = JSON.parse(writeFrynb(parseFrynb(json), NOW));
    assert.deepStrictEqual(written.FutureRootField, { a: 1 });
    assert.deepStrictEqual(written.Cells[0].FutureCellField, [1, 2]);
    assert.equal(written.Cells[0].IsOutputCollapsed, true);
  });

  it('reads camelCase names, a cell with no id, a list of source lines, and an unknown cell type as code', () => {
    const notebook = parseFrynb(
      JSON.stringify({ kernel: 'python', cells: [{ type: 'markdown', source: ['# a\n', 'b'] }, { source: 'print(1)', language: 'python' }, { type: 'weird', source: 'z' }] })
    );

    assert.equal(notebook.kernel, 'python');
    assert.deepStrictEqual(notebook.cells.map((c) => [c.id, c.kind, c.source]), [['cell-1', 'markdown', '# a\nb'], ['cell-2', 'code', 'print(1)'], ['cell-3', 'code', 'z']]);
  });

  it('writes a language only when it is not the notebook\'s', () => {
    const cells: CellModel[] = [
      { id: 'a', kind: 'code', language: 'csharp', source: '', failed: false, outputs: [], state: { extra: {}, snapshots: {}, outputsKey: '' } },
      { id: 'b', kind: 'code', language: 'python', source: '', failed: false, outputs: [], state: { extra: {}, snapshots: {}, outputsKey: '' } }
    ];
    const written = JSON.parse(writeFrynb({ format: 'frynb', kernel: 'csharp', root: {}, cells }, NOW));
    assert.equal('Language' in written.Cells[0], false);
    assert.equal(written.Cells[1].Language, 'python');
  });

  it('rejects text that is not a notebook', () => {
    assert.throws(() => parseFrynb('[1,2]'));
    assert.throws(() => parseFrynb('not json'));
  });
});
