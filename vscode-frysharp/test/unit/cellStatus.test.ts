import assert from 'node:assert/strict';
import { test } from 'node:test';
import { languageNote, StatusLanguage } from '../../src/notebook/cellStatus';
import { offersLens } from '../../src/run/lensRules';

const languages: StatusLanguage[] = [
  { id: 'csharp', displayName: 'C#', aliases: ['c#', 'cs'], shortName: 'CS', canRunCells: true },
  { id: 'python', displayName: 'Python', aliases: ['py'], shortName: 'PY', canRunCells: true },
  { id: 'javascript', displayName: 'JavaScript', aliases: ['js', 'node'], shortName: 'JS', canRunCells: true }
];

test('a cell that runs as itself has nothing to say', () => {
  assert.equal(languageNote('print(1)', 'python', languages), undefined);
  assert.equal(languageNote('#!share --from csharp rows\nprint(rows)', 'python', languages), undefined);
  assert.equal(languageNote('// just a comment\nvar x = 1;', 'csharp', languages), undefined);
});

test('a #!directive that makes a cell run as another language is said under the cell, with the reason', () => {
  const note = languageNote('#!python\nprint(1)', 'csharp', languages)!;

  assert.equal(note.text, '$(arrow-right) Python');
  assert.match(note.tooltip, /runs as Python: its first line says #!python, not as C#/);
  assert.equal(languageNote('// note\n#!js\nconsole.log(1)', 'csharp', languages)?.text, '$(arrow-right) JavaScript');
});

test('a #!share before the language directive is not mistaken for it, and an unknown name changes nothing', () => {
  const note = languageNote('#!share --from csharp rows\n#!py\nprint(rows)', 'csharp', languages)!;
  assert.equal(note.text, '$(arrow-right) Python');
  assert.match(note.tooltip, /#!py,/);
  assert.equal(languageNote('#!cobol\nDISPLAY', 'csharp', languages), undefined);
});

test('the Run lens is for the languages the engine runs, not for a plain .cs file, a notebook cell or an output', () => {
  const runnable = ['csharp', 'python', 'java'];
  assert.equal(offersLens('python', '/a/b.py', 'file', runnable), true);
  assert.equal(offersLens('csharp', '/a/b.csx', 'file', runnable), true);
  assert.equal(offersLens('csharp', '/a/B.cs', 'file', runnable), false);
  assert.equal(offersLens('ruby', '/a/b.rb', 'file', runnable), false);
  assert.equal(offersLens('python', 'cell', 'vscode-notebook-cell', runnable), false);
  assert.equal(offersLens('python', 'log', 'output', runnable), false);
});
