import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { CellLike, contextFor, effectiveLanguage, resolverFor } from '../../src/language/cellContext';

const resolve = resolverFor([
  { id: 'csharp', aliases: ['c#', 'cs'], shortName: 'C#' },
  { id: 'python', aliases: ['py'] },
  { id: 'javascript', aliases: ['js', 'node'] }
]);
const code = (language: string, text: string): CellLike => ({ kind: 'code', language, text });

describe('the language a cell runs in', () => {
  it('is its own, unless its first lines say another', () => {
    assert.equal(effectiveLanguage('var a = 1;', 'csharp', resolve), 'csharp');
    assert.equal(effectiveLanguage('#!python\nprint(1)', 'csharp', resolve), 'python');
    assert.equal(effectiveLanguage('#!js\nconsole.log(1)', 'csharp', resolve), 'javascript');
    assert.equal(effectiveLanguage('#!C#\nvar a = 1;', 'python', resolve), 'csharp');
  });

  it('lets blank lines, comments and #!share come before the directive, and ends at the first line of code', () => {
    assert.equal(effectiveLanguage('\n// note\n#!share --from csharp n\n#!python\nx = 1', 'csharp', resolve), 'python');
    assert.equal(effectiveLanguage('var a = 1;\n#!python\nx = 1', 'csharp', resolve), 'csharp');
  });

  it('does not take #!share, an attribute (#![...]) or a shebang for a language', () => {
    assert.equal(effectiveLanguage('#!share --from csharp n', 'python', resolve), 'python');
    assert.equal(effectiveLanguage('#![allow(unused)]\nfn main() {}', 'rust', resolve), 'rust');
    assert.equal(effectiveLanguage('#!/usr/bin/env python\nprint(1)', 'python', resolve), 'python');
  });

  it('keeps its own language for a directive that names none', () => {
    assert.equal(effectiveLanguage('#!pyton\nx = 1', 'csharp', resolve), 'csharp');
  });
});

describe('what the earlier cells give a cell', () => {
  const cells: CellLike[] = [
    { kind: 'markdown', language: 'markdown', text: '# notes' },
    code('csharp', 'int a = 1;'),
    code('python', 'import os'),
    code('csharp', '#!python\nprint(1)'),
    code('csharp', 'var b = a + 1;'),
    code('csharp', 'b.')
  ];

  it('is the code cells before it that run in the same language, in order', () => {
    assert.deepStrictEqual(contextFor(cells, 5, 'csharp', resolve), ['int a = 1;', 'var b = a + 1;']);
    assert.deepStrictEqual(contextFor(cells, 5, 'python', resolve), ['import os', '#!python\nprint(1)']);
  });

  it('is nothing for the first cell, and never includes the cell itself or what follows', () => {
    assert.deepStrictEqual(contextFor(cells, 0, 'csharp', resolve), []);
    assert.deepStrictEqual(contextFor(cells, 2, 'csharp', resolve), ['int a = 1;']);
  });
});
