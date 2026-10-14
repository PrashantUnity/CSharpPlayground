import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { placeDiagnostics } from '../../src/notebook/cellDiagnostics';

const diagnostic = (startLine: number, startColumn: number, endLine = startLine, endColumn = startColumn + 1, severity = 'error') => ({ severity, code: 'X1', message: 'm', startLine, startColumn, endLine, endColumn });

describe('placing diagnostics in a cell', () => {
  const text = 'int a = 1;\nint b = ;\nConsole.WriteLine(b);';

  it('counts from 0, where the engine counts from 1', () => {
    const [placed] = placeDiagnostics([diagnostic(2, 9, 2, 10)], text);
    assert.deepStrictEqual([placed.startLine, placed.startCharacter, placed.endLine, placed.endCharacter], [1, 8, 1, 9]);
  });

  it('takes a column past the end of the line as the end of the line (a traceback names only the line)', () => {
    const [placed] = placeDiagnostics([diagnostic(3, 1, 3, 2147483647)], text);
    assert.deepStrictEqual([placed.startCharacter, placed.endCharacter], [0, 'Console.WriteLine(b);'.length]);
  });

  it('gives a range something to underline when the engine gave an empty one', () => {
    const [placed] = placeDiagnostics([diagnostic(1, 5, 1, 5)], text);
    assert.equal(placed.endCharacter - placed.startCharacter, 1);
  });

  it('stays inside the cell when the cell has changed since', () => {
    const [placed] = placeDiagnostics([diagnostic(40, 10, 41, 99)], 'one line');
    assert.deepStrictEqual([placed.startLine, placed.startCharacter, placed.endLine, placed.endCharacter], [0, 8, 0, 8]);
  });

  it('says the severity as an editor does, and keeps the code and message', () => {
    const placed = placeDiagnostics([diagnostic(1, 1, 1, 2, 'warning'), diagnostic(1, 1, 1, 2, 'info'), diagnostic(1, 1)], text);
    assert.deepStrictEqual(placed.map((p) => p.severity), ['warning', 'information', 'error']);
    assert.equal(placed[0].code, 'X1');
  });

  it('is empty when there is nothing to place', () => {
    assert.deepStrictEqual(placeDiagnostics(undefined, text), []);
    assert.deepStrictEqual(placeDiagnostics([], text), []);
  });
});
