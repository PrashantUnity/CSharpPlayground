import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { DirectiveContext, directiveCompletions } from '../../src/language/directives';

const languages = [
  { id: 'csharp', displayName: 'C#', shortName: 'C#', aliases: ['c#', 'cs'], canRunCells: true },
  { id: 'python', displayName: 'Python', shortName: 'PY', aliases: ['py'], canRunCells: true },
  { id: 'java', displayName: 'Java', shortName: 'JV', aliases: [], canRunCells: false }
];
const variables: Record<string, string[]> = { csharp: ['count', 'name'], python: ['df'] };
const context = (cellLanguage = 'csharp'): DirectiveContext => ({ languages, cellLanguage, variablesOf: async (id) => variables[id] ?? [] });
const labels = async (line: string, cellLanguage?: string) => (await directiveCompletions(line, context(cellLanguage)))?.items.map((i) => i.label);

describe('completing a directive', () => {
  it('offers share and the languages the engine runs in cells, after #!', async () => {
    assert.deepStrictEqual(await labels('#!'), ['share', 'csharp', 'c#', 'cs', 'python', 'py']);
    assert.deepStrictEqual(await labels('#!py'), ['share', 'csharp', 'c#', 'cs', 'python', 'py']); // the editor filters by what is typed
    assert.equal((await directiveCompletions('#!', context()))!.from, 2);
    assert.equal((await directiveCompletions('  #!', context()))!.from, 4);
  });

  it('offers --from after #!share, then the languages, then that language\'s variables, then --as', async () => {
    assert.deepStrictEqual(await labels('#!share '), ['--from']);
    assert.deepStrictEqual(await labels('#!share --from '), ['csharp', 'python']);
    assert.deepStrictEqual(await labels('#!share --from csharp '), ['count', 'name']);
    assert.deepStrictEqual(await labels('#!share --from py '), ['df']); // by alias
    assert.deepStrictEqual(await labels('#!share --from csharp count '), ['--as']);
    assert.deepStrictEqual(await labels('#!share --from csharp count --as '), []);
    assert.deepStrictEqual(await labels('#!share --from nonsense '), []);
  });

  it('says where the word being typed starts', async () => {
    assert.equal((await directiveCompletions('#!share --from cs', context()))!.from, '#!share --from '.length);
    assert.equal((await directiveCompletions('#!share --from csharp co', context()))!.from, '#!share --from csharp '.length);
  });

  it('offers the package line of the cell\'s language after %', async () => {
    assert.deepStrictEqual(await labels('%', 'python'), ['pip install']);
    assert.deepStrictEqual(await labels('%np', 'javascript'), ['npm install']);
    assert.deepStrictEqual(await labels('!', 'python'), ['pip install']);
    assert.equal(await labels('%', 'java'), undefined);
  });

  it('is not about a line of code', async () => {
    assert.equal(await directiveCompletions('var a = 1;', context()), undefined);
    assert.equal(await directiveCompletions('x = 5 % 3', context()), undefined);
    assert.equal(await directiveCompletions('// #!', context()), undefined);
  });
});
