import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { before, describe, it } from 'node:test';
import { loadWASM, OnigScanner, OnigString } from 'vscode-oniguruma';
import { INITIAL, IGrammar, parseRawGrammar, Registry } from 'vscode-textmate';

const root = path.resolve(__dirname, '../../..');

// What the host language's grammar does with a "#" line: it is a comment. The directive grammar is injected into it.
const comments = { scopeName: 'source.python', patterns: [{ match: '#.*$', name: 'comment.line.number-sign' }] };
const directives = readFileSync(path.join(root, 'syntaxes', 'frysharp-directives.injection.json'), 'utf8');

let grammar: IGrammar;

before(async () => {
  const wasm = readFileSync(path.join(path.dirname(require.resolve('vscode-oniguruma')), 'onig.wasm'));
  await loadWASM(wasm.buffer.slice(wasm.byteOffset, wasm.byteOffset + wasm.byteLength) as ArrayBuffer);
  const registry = new Registry({
    onigLib: Promise.resolve({ createOnigScanner: (patterns) => new OnigScanner(patterns), createOnigString: (text) => new OnigString(text) }),
    loadGrammar: async (scopeName) => {
      if (scopeName === 'source.python') return parseRawGrammar(JSON.stringify(comments), 'comments.json');
      if (scopeName === 'frysharp.directives.injection') return parseRawGrammar(directives, 'directives.json');
      return null;
    },
    getInjections: (scopeName) => (scopeName === 'source.python' ? ['frysharp.directives.injection'] : undefined)
  });
  const loaded = await registry.loadGrammar('source.python');
  assert.ok(loaded, 'the grammar loads');
  grammar = loaded;
});

/** The tokens of a line as [text, scopes it has beyond the language's own]. */
function tokens(line: string): Array<[string, string[]]> {
  const result = grammar.tokenizeLine(line, INITIAL);
  return result.tokens.map((token) => [line.slice(token.startIndex, token.endIndex), token.scopes.filter((scope) => scope.endsWith('.frysharp'))]);
}

const scopeOf = (line: string, text: string): string | undefined => tokens(line).find(([part]) => part === text)?.[1][0];

describe('the grammar of a cell\'s directives', () => {
  it('marks #!share, its options, the language, the variable and the new name', () => {
    const line = '#!share --from csharp people --as team';
    assert.equal(scopeOf(line, 'share'), 'keyword.control.directive.share.frysharp');
    assert.equal(scopeOf(line, '--from'), 'keyword.operator.option.frysharp');
    assert.equal(scopeOf(line, 'csharp'), 'entity.name.type.language.frysharp');
    assert.equal(scopeOf(line, 'people'), 'variable.other.shared.frysharp');
    assert.equal(scopeOf(line, '--as'), 'keyword.operator.option.frysharp');
    assert.equal(scopeOf(line, 'team'), 'variable.other.alias.frysharp');
  });

  it('marks the language directives', () => {
    assert.equal(scopeOf('#!python', 'python'), 'entity.name.function.directive.language.frysharp');
    assert.equal(scopeOf('#!c#', 'c#'), 'entity.name.function.directive.language.frysharp');
    assert.equal(scopeOf('  #!javascript', 'javascript'), 'entity.name.function.directive.language.frysharp');
  });

  it('marks package lines', () => {
    const line = '%pip install requests';
    assert.equal(scopeOf(line, 'pip'), 'keyword.control.directive.package.frysharp');
    assert.equal(scopeOf(line, 'install'), 'keyword.operator.option.frysharp');
    assert.equal(scopeOf(line, 'requests'), 'string.unquoted.package.frysharp');
    assert.equal(scopeOf('%npm install lodash', 'npm'), 'keyword.control.directive.package.frysharp');
  });

  it('leaves a shebang, a Rust attribute and a plain comment to the language', () => {
    for (const line of ['#!/usr/bin/env python3', '#![allow(unused)]', '# not a directive', 'x = 1 #!python']) {
      assert.deepStrictEqual(
        tokens(line).filter(([, scopes]) => scopes.length > 0),
        [],
        line
      );
    }
  });
});
