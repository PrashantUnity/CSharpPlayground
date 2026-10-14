import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { identityOf, UriLike } from '../../src/notebook/identity';

const file = (fsPath: string): UriLike => ({ scheme: 'file', fsPath, path: fsPath, toString: () => `file://${fsPath}` });
const untitled = (name: string): UriLike => ({ scheme: 'untitled', fsPath: name, path: name, toString: () => `untitled:${name}` });
const folder = (root?: string) => () => root;

describe('a notebook\'s identity', () => {
  it('is its URI, so two files with one name in two folders are two notebooks', () => {
    const a = identityOf(file('/work/a/notebook.frynb'), folder('/work'));
    const b = identityOf(file('/work/b/notebook.frynb'), folder('/work'));

    assert.notEqual(a.notebookId, b.notebookId);
    assert.equal(a.path, 'a/notebook.frynb');
    assert.equal(a.workingDirectory, '/work/a');
  });

  it('works for a notebook outside every workspace folder, which runs in its own folder', () => {
    const outside = identityOf(file('/elsewhere/deep/nb.frynb'), folder('/work'));

    assert.equal(outside.path, 'nb.frynb');
    assert.equal(outside.workingDirectory, '/elsewhere/deep');
  });

  it('works with no folder open at all', () => {
    assert.equal(identityOf(file('/home/me/nb.frynb'), folder(undefined)).path, 'nb.frynb');
  });

  it('works for an untitled notebook, which runs in the workspace folder', () => {
    const one = identityOf(untitled('Untitled-1.frynb'), folder('/work'));
    const two = identityOf(untitled('Untitled-2.frynb'), folder('/work'));

    assert.notEqual(one.notebookId, two.notebookId);
    assert.equal(one.path, 'Untitled-1.frynb');
    assert.equal(one.workingDirectory, '/work');
    assert.equal(identityOf(untitled('Untitled-3.frynb'), folder(undefined)).workingDirectory, undefined);
  });
});
