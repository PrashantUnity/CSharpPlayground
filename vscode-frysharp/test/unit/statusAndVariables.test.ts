import './vscodeStub'; // first: it stands in for 'vscode' before the modules ask for it
import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { describeEngine } from '../../src/notebook/statusBar';
import { preview } from '../../src/notebook/variablesTree';

describe('the engine in the status bar', () => {
  it('says what state it is in', () => {
    assert.match(describeEngine('stopped', 0).text, /FrySharp$/);
    assert.match(describeEngine('starting', 0).text, /starting/);
    assert.match(describeEngine('ready', 0).text, /ready/);
    assert.match(describeEngine('crashed', 0).text, /stopped/);
  });

  it('says a cell is running, and how many when more than one is', () => {
    assert.match(describeEngine('ready', 1).text, /running$/);
    assert.match(describeEngine('ready', 3).text, /running \(3\)/);
  });

  it('says what stopping loses', () => {
    assert.match(describeEngine('crashed', 0).tooltip, /variables are gone/);
  });
});

describe('a variable\'s value in a line of the tree', () => {
  it('is on one line, whatever it was', () => {
    assert.equal(preview('line one\n  line two\r\n\tline three'), 'line one line two line three');
  });

  it('is cut where there is no more room, and says so', () => {
    const cut = preview('x'.repeat(200), 20);
    assert.equal(cut.length, 20);
    assert.ok(cut.endsWith('…'));
    assert.equal(preview('short'), 'short');
  });
});
