import assert from 'node:assert/strict';
import { test } from 'node:test';
import {
  applyCarriageReturns,
  consoleTextOf,
  countLines,
  kindOfMimes,
  lastLines,
  PackedCell,
  parseAnsi,
  summaryOf,
  tabsOf
} from '../../src/shared/outputTabs';

const stdout = (text: string) => ({ items: [{ mime: 'application/vnd.code.notebook.stdout', text }] });
const table = (rows: number) => ({
  items: [
    { mime: 'text/html', text: '<div class="fry-tbl"></div>' },
    { mime: 'application/vnd.fry.table+json', text: JSON.stringify({ title: 'T', columns: ['a'], numeric: [true], rows: Array.from({ length: rows }, (_, i) => [i]), totalRows: rows }) },
    { mime: 'text/plain', text: 'T' }
  ]
});

test('an output is the richest thing it holds: a table with its page and text is a table, a chart with its page is a chart', () => {
  assert.equal(kindOfMimes(['text/html', 'application/vnd.fry.table+json', 'text/plain']), 'table');
  assert.equal(kindOfMimes(['text/html', 'application/vnd.fry.chart.v1+json', 'text/plain']), 'chart');
  assert.equal(kindOfMimes(['application/vnd.fry.plot3d.v2+json']), 'plot3d');
  assert.equal(kindOfMimes(['application/vnd.fry.visualizer.v1+json', 'text/plain']), 'visualizer');
  assert.equal(kindOfMimes(['application/vnd.fry.inspector+json', 'text/plain']), 'inspector');
  assert.equal(kindOfMimes(['application/vnd.code.notebook.stderr']), 'console');
  assert.equal(kindOfMimes(['image/png', 'text/plain']), 'image');
  assert.equal(kindOfMimes(['text/markdown', 'text/plain']), 'markdown');
  assert.equal(kindOfMimes(['text/html']), 'html');
  assert.equal(kindOfMimes(['text/plain']), 'console');
  assert.equal(kindOfMimes(['application/vnd.plotly.v1+json']), undefined);
});

test('the tabs of a cell are the desktop\'s: Table with its rows, Console with its lines, then All', () => {
  const cell: PackedCell = { v: 1, outputs: [stdout('one\ntwo\n'), table(8), stdout('three\n')] };

  const tabs = tabsOf(cell);

  assert.deepStrictEqual(tabs.map((t) => [t.id, t.label, t.count]), [['table', 'Table', 8], ['console', 'Console', 3], ['all', 'All', undefined]]);
  assert.deepStrictEqual(tabs[0].outputs, [1]);
  assert.deepStrictEqual(tabs[1].outputs, [0, 2]);
  assert.deepStrictEqual(tabs[2].outputs, [0, 1, 2]);
});

test('one kind of output has one tab and no All; several outputs of a kind are counted', () => {
  assert.deepStrictEqual(tabsOf({ v: 1, outputs: [stdout('x')] }).map((t) => t.id), ['console']);
  const charts = tabsOf({ v: 1, outputs: [{ items: [{ mime: 'application/vnd.fry.chart.v1+json', text: '{}' }] }, { items: [{ mime: 'application/vnd.fry.chart.v1+json', text: '{}' }] }, stdout('a')] });
  assert.deepStrictEqual(charts.map((t) => [t.id, t.count]), [['console', 1], ['chart', 2], ['all', undefined]]);
});

test('a line count does not count the line break at the end, and a console tab counts lines left off the start', () => {
  assert.equal(countLines(''), 0);
  assert.equal(countLines('a'), 1);
  assert.equal(countLines('a\n'), 1);
  assert.equal(countLines('a\nb\n'), 2);
  const cell: PackedCell = { v: 1, outputs: [{ items: [{ mime: 'application/vnd.code.notebook.stdout', text: 'c\nd\n' }], omitted: 98 }, table(1)] };
  assert.equal(tabsOf(cell)[1].count, 100);
});

test('the console text of an output is what was printed, or the plain text of a display', () => {
  assert.equal(consoleTextOf(stdout('hi')), 'hi');
  assert.equal(consoleTextOf({ items: [{ mime: 'text/plain', text: 'plain' }] }), 'plain');
  assert.equal(consoleTextOf({ items: [{ mime: 'application/vnd.code.notebook.stdout', text: 'out' }, { mime: 'application/vnd.code.notebook.stderr', text: 'err' }] }), 'outerr');
});

test('the last lines of a text keep the end and say how many were left off', () => {
  assert.deepStrictEqual(lastLines('1\n2\n3\n4\n5\n', 2), { text: '4\n5\n', omitted: 3 });
  assert.deepStrictEqual(lastLines('1\n2\n3', 2), { text: '2\n3', omitted: 1 });
  assert.deepStrictEqual(lastLines('1\n2\n', 5), { text: '1\n2\n', omitted: 0 });
});

test('the summary is a line of what the tabs hold, then the start of the console', () => {
  const cell: PackedCell = { v: 1, outputs: [stdout('a\nb\n'), table(3)] };

  assert.equal(summaryOf(cell), '[Table (3) · Console (2)]\na\nb');
  assert.ok(summaryOf({ v: 1, outputs: [stdout(Array.from({ length: 100 }, (_, i) => `l${i}`).join('\n')), table(1)] }, 5).endsWith('\n…'));
});

test('ANSI colours and styles become runs of text, and other escapes are dropped', () => {
  const spans = parseAnsi('plain \u001b[1;31mred bold\u001b[0m \u001b[4mline\u001b[24m \u001b[38;5;202morange\u001b[39m \u001b[38;2;1;2;3mrgb\u001b[0m\u001b[2K\u001b[?25h end');

  assert.deepStrictEqual(spans.map((s) => s.text), ['plain ', 'red bold', ' ', 'line', ' ', 'orange', ' ', 'rgb', ' end']);
  assert.equal(spans[1].bold, true);
  assert.match(spans[1].fg!, /ansiRed/);
  assert.equal(spans[3].underline, true);
  assert.equal(spans[4].underline, undefined);
  assert.equal(spans[5].fg, 'rgb(255, 95, 0)');
  assert.equal(spans[7].fg, 'rgb(1, 2, 3)');
  assert.equal(spans[8].fg, undefined);
  assert.deepStrictEqual(parseAnsi('no escapes').map((s) => s.text), ['no escapes']);
  assert.deepStrictEqual(parseAnsi(''), []);
});

test('a carriage return redraws a line, as a progress bar does', () => {
  assert.equal(applyCarriageReturns('10%\r50%\r100%\ndone\n'), '100%\ndone\n');
  assert.equal(applyCarriageReturns('long line\rshort'), 'shortline');
  assert.equal(applyCarriageReturns('a\r\nb'), 'a\nb');
  assert.equal(applyCarriageReturns('nothing'), 'nothing');
});
