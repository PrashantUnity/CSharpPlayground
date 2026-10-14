import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { chooseDrawing } from '../../src/renderer/chooseMime';
import { assetNames, hexColour, inlineAssets, isFullDocument, themeOf, withHost, withProbe } from '../../src/renderer/htmlPrepare';
import { canOpen, kindOf, startsOpen, toText } from '../../src/renderer/inspectorModel';
import { footer, nextSort, numberOf, TableData, toCsv, toJson, visibleRows } from '../../src/renderer/tableModel';

const people: TableData = {
  title: 'People',
  columns: ['Name', 'Age', 'City'],
  numeric: [false, true, false],
  rows: [['Grace', '45', 'Arlington'], ['Ada', '36', null], ['Linus', '1,200', 'Helsinki, FI'], ['Alan', 'n/a', 'London']]
};

describe('table: which rows show, and in what order', () => {
  it('shows every row as they came', () => {
    assert.deepStrictEqual(visibleRows(people, ''), [0, 1, 2, 3]);
  });

  it('filters on any cell, ignoring case and spaces around the text', () => {
    assert.deepStrictEqual(visibleRows(people, '  LON '), [3]);
    assert.deepStrictEqual(visibleRows(people, 'a'), [0, 1, 3]); // Linus, 1,200, Helsinki, FI has no "a"
    assert.deepStrictEqual(visibleRows(people, 'helsinki'), [2]);
    assert.deepStrictEqual(visibleRows(people, 'nothing'), []);
  });

  it('sorts a numeric column by value, with "1,200" a number, and what is not a number after the numbers it can compare', () => {
    assert.deepStrictEqual(visibleRows(people, '', { column: 1, direction: 'asc' }).slice(0, 3), [1, 0, 2]);
    assert.deepStrictEqual(visibleRows(people, '', { column: 1, direction: 'desc' }), [2, 0, 1, 3]); // "n/a" is last either way
    assert.equal(visibleRows(people, '', { column: 1, direction: 'asc' })[3], 3);
  });

  it('sorts a text column by its text, naturally', () => {
    assert.deepStrictEqual(visibleRows(people, '', { column: 0, direction: 'asc' }), [1, 3, 0, 2]);
    const numbered: TableData = { columns: ['x'], rows: [['item 10'], ['item 2'], ['item 1']] };
    assert.deepStrictEqual(visibleRows(numbered, '', { column: 0, direction: 'asc' }), [2, 1, 0]);
  });

  it('puts an empty cell last whichever way it sorts, and keeps equal rows as they came', () => {
    assert.equal(visibleRows(people, '', { column: 2, direction: 'asc' })[3], 1);
    assert.equal(visibleRows(people, '', { column: 2, direction: 'desc' })[3], 1);
    const same: TableData = { columns: ['k'], rows: [['a'], ['a'], ['a']] };
    assert.deepStrictEqual(visibleRows(same, '', { column: 0, direction: 'desc' }), [0, 1, 2]);
  });

  it('filters and sorts together', () => {
    assert.deepStrictEqual(visibleRows(people, 'a', { column: 0, direction: 'desc' }), [0, 3, 1]); // Grace, Alan, Ada
  });

  it('goes ascending, then descending, then as it came, as a header is clicked', () => {
    const first = nextSort(undefined, 1);
    const second = nextSort(first, 1);
    assert.deepStrictEqual([first, second, nextSort(second, 1)], [{ column: 1, direction: 'asc' }, { column: 1, direction: 'desc' }, undefined]);
    assert.deepStrictEqual(nextSort(second, 2), { column: 2, direction: 'asc' });
  });

  it('reads numbers as the engine writes them', () => {
    assert.equal(numberOf('1,234.5'), 1234.5);
    assert.equal(numberOf('-3'), -3);
    assert.equal(numberOf('12 %'), 12);
    assert.equal(numberOf('$7'), 7);
    assert.equal(numberOf('1e3'), 1000);
    assert.equal(numberOf('n/a'), undefined);
    assert.equal(numberOf(''), undefined);
    assert.equal(numberOf(null), undefined);
    assert.equal(numberOf(true), undefined);
  });
});

describe('table: copying', () => {
  it('writes CSV with quotes where a cell needs them, for the rows shown in the order shown', () => {
    assert.equal(toCsv(people, [2, 1]), 'Name,Age,City\nLinus,"1,200","Helsinki, FI"\nAda,36,');
    assert.equal(toCsv({ columns: ['q'], rows: [['say "hi"'], ['two\nlines']] }, [0, 1]), 'q\n"say ""hi"""\n"two\nlines"');
  });

  it('writes JSON as a list of records, a number a number where the column is numeric', () => {
    assert.deepStrictEqual(JSON.parse(toJson(people, [1, 3])), [
      { Name: 'Ada', Age: 36, City: null },
      { Name: 'Alan', Age: 'n/a', City: 'London' }
    ]);
  });
});

describe('table: the footer', () => {
  it('says how many rows show, and when there are more than were sent', () => {
    assert.equal(footer(people, 4, false), 'Showing 4 of 4 rows');
    assert.equal(footer(people, 1, true), 'Showing 1 of 4 rows (filtered)');
    assert.equal(footer({ ...people, totalRows: 5000 }, 4, false), 'Showing the first 4 of 5,000 rows');
  });
});

describe('Object Explorer values and nodes', () => {
  it('colours a value by its kind', () => {
    assert.equal(kindOf({ isNull: true }), 'null');
    assert.equal(kindOf({ value: 'Code', isString: true }), 'string');
    assert.equal(kindOf({ value: '42' }), 'number');
    assert.equal(kindOf({ value: '-3.5e2' }), 'number');
    assert.equal(kindOf({ value: '1,200' }), 'number');
    assert.equal(kindOf({ value: '∞' }), 'number');
    assert.equal(kindOf({ value: 'True' }), 'boolean');
    assert.equal(kindOf({ value: '[ 1, 2, 3 ]' }), 'list');
    assert.equal(kindOf({ value: '2026-10-14' }), 'plain');
    assert.equal(kindOf({ value: '42', isString: true }), 'string'); // the text "42" is text
  });

  it('opens the top node, and those below it unless they close a cycle', () => {
    assert.equal(startsOpen(0, {}), true);
    assert.equal(startsOpen(1, {}), true);
    assert.equal(startsOpen(1, { circular: true }), false);
    assert.equal(startsOpen(2, {}), false);
  });

  it('opens whatever has members, a reference back to an object above included, and nothing else', () => {
    assert.equal(canOpen({ properties: [] }), false);
    assert.equal(canOpen({ properties: [{ name: 'a' }] }), true);
    assert.equal(canOpen({ properties: [{ name: 'Name', value: 'x' }] }), true);
  });

  it('writes a node as text, children indented', () => {
    const lines = toText({ title: 'People', properties: [{ name: 'Name', value: 'Code', isString: true }, { name: 'Other', isNull: true }, { name: 'Self', child: { title: 'People', circular: true, properties: [] } }] });
    assert.deepStrictEqual(lines, ['People', '  Name = Code', '  Other = <null>', '  Self:', '    People (circular reference)']);
  });
});

describe('what a display is drawn as', () => {
  it('takes the richest of what the bundle holds', () => {
    assert.deepStrictEqual(chooseDrawing(['text/html', 'application/vnd.fry.table+json', 'text/plain']), { drawing: 'table', mime: 'application/vnd.fry.table+json' });
    assert.deepStrictEqual(chooseDrawing(['text/plain', 'text/html']), { drawing: 'html', mime: 'text/html' });
    assert.deepStrictEqual(chooseDrawing(['application/vnd.fry.inspector+json', 'text/html']), { drawing: 'inspector', mime: 'application/vnd.fry.inspector+json' });
    assert.deepStrictEqual(chooseDrawing(['image/png', 'text/plain']), { drawing: 'image', mime: 'image/png' });
    assert.equal(chooseDrawing(['application/x-unknown']), undefined);
  });
});

describe('getting an engine page ready', () => {
  const chart = '<!DOCTYPE html>\n<html><head><title>t</title>\n<script data-fry-asset="echarts.min.js"></script>\n</head><body><div id="chart"></div></body></html>';

  it('finds the libraries a page names, in order', () => {
    assert.deepStrictEqual(assetNames('<script data-fry-asset="echarts.min.js"></script><p/><script data-fry-asset="echarts-gl.min.js"></script>'), ['echarts.min.js', 'echarts-gl.min.js']);
    assert.deepStrictEqual(assetNames('<script>var x</script>'), []);
  });

  it('tells a whole page from a piece of one', () => {
    assert.equal(isFullDocument(chart), true);
    assert.equal(isFullDocument('  <!doctype html><body>'), true);
    assert.equal(isFullDocument('<html lang="en">'), true);
    assert.equal(isFullDocument('<div class="fry-tbl-container"><script>1</script></div>'), false);
  });

  it('puts a library where its tag stands, and cannot be ended early by the library', () => {
    const page = inlineAssets(chart, new Map([['echarts.min.js', 'var echarts={};/*</script>*/']]));
    assert.ok(!page.includes('data-fry-asset'));
    assert.ok(page.includes('<script>var echarts={};/*<\\/script>*/</script>'));
    assert.equal(inlineAssets(chart, new Map()), chart); // no text for it: the tag stays
  });

  it('sets what the editor looks like before the page\'s own scripts', () => {
    const page = withHost(chart, { theme: 'dark', background: '#1e1e1e' });
    assert.ok(page.indexOf('window.fryHost={theme:"dark",background:"#1e1e1e"}') < page.indexOf('data-fry-asset'));
    assert.ok(withHost('<p>x</p>', { theme: 'light' }).startsWith('<script>window.fryHost={theme:"light"};</script>'));
    assert.ok(!withHost('<p>x</p>', { theme: 'light', background: 'red;alert(1)' }).includes('background'));
  });

  it('reads the editor\'s theme and colours', () => {
    assert.equal(themeOf('vscode-light'), 'light');
    assert.equal(themeOf('vscode-high-contrast-light'), 'light');
    assert.equal(themeOf('vscode-dark'), 'dark');
    assert.equal(themeOf('vscode-high-contrast'), 'dark');
    assert.equal(themeOf(undefined), 'dark');
    assert.equal(hexColour('#1E1E1E'), '#1E1E1E');
    assert.equal(hexColour('#fff'), '#ffffff');
    assert.equal(hexColour('rgb(30, 30, 30)'), '#1e1e1e');
    assert.equal(hexColour('var(--x)'), undefined);
  });
});

describe('knowing a chart drew', () => {
  it('adds a probe before the end of the page that names its frame', () => {
    const page = withProbe('<html><body><div id="c"></div></body></html>', 'tok');
    assert.ok(page.indexOf('fryProbe:"tok"') > page.indexOf('<div id="c">'));
    assert.ok(page.indexOf('fryProbe') < page.indexOf('</body>'));
    assert.ok(page.includes('window.__fryChart'));
  });

  it('adds it at the end of a page that has no body to end, and cannot be broken out of by its token', () => {
    assert.ok(withProbe('<p>x</p>', 't').endsWith('</script>'));
    assert.ok(withProbe('<body></body>', '"};alert(1);//').includes('fryProbe:"\\"};alert(1);//"')); // the quote is escaped: it stays inside the string
    assert.ok(!withProbe('<body></body>', '</script><script>alert(1)</script>').includes('</script><script>alert'));
  });
});
