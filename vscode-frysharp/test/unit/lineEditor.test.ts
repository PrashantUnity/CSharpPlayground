import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { LineEditor } from '../../src/run/lineEditor';

describe('typing to a running program', () => {
  it('shows what is typed, and sends the line on Enter after taking back what it showed', () => {
    const editor = new LineEditor();

    assert.deepStrictEqual(editor.feed('Ada'), { echo: 'Ada', lines: [], interrupt: false });
    // the engine prints "Ada" and a newline itself: the three characters shown are taken back first
    assert.deepStrictEqual(editor.feed('\r'), { echo: '\b \b\b \b\b \b', lines: ['Ada'], interrupt: false });
    assert.equal(editor.pending, '');
  });

  it('takes a character back on Backspace, and does nothing on an empty line', () => {
    const editor = new LineEditor();
    editor.feed('Adx');

    assert.equal(editor.feed('\x7f').echo, '\b \b');
    assert.equal(editor.pending, 'Ad');
    editor.feed('\x7f\x7f');
    assert.equal(editor.feed('\x7f').echo, '');
  });

  it('sends several lines pasted at once, and the empty line of a bare Enter', () => {
    const editor = new LineEditor();

    assert.deepStrictEqual(editor.feed('one\ntwo\r\nthree\r').lines, ['one', 'two', 'three']);
    assert.deepStrictEqual(editor.feed('\r').lines, ['']);
  });

  it('keeps what is typed when it comes in pieces', () => {
    const editor = new LineEditor();
    editor.feed('He');
    editor.feed('llo');
    assert.deepStrictEqual(editor.feed('\r').lines, ['Hello']);
  });

  it('says Ctrl+C was pressed, and forgets what was typed', () => {
    const editor = new LineEditor();
    editor.feed('half');
    const typed = editor.feed('\x03');

    assert.equal(typed.interrupt, true);
    assert.equal(editor.pending, '');
  });

  it('ignores arrow keys and other escape sequences, and tab', () => {
    const editor = new LineEditor();

    assert.deepStrictEqual(editor.feed('a\x1b[Ab\x1b[1;5C\x1bOP\tc'), { echo: 'abc', lines: [], interrupt: false });
    assert.equal(editor.pending, 'abc');
  });

  it('counts a character of more than one code unit as one', () => {
    const editor = new LineEditor();
    editor.feed('é😀');

    assert.equal(editor.feed('\x7f').echo, '\b \b');
    assert.equal(editor.pending, 'é');
    assert.equal(editor.feed('\r').echo, '\b \b');
  });
});
