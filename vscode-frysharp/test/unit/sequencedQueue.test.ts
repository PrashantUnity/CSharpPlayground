import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { SequencedQueue } from '../../src/notebook/sequencedQueue';

const tick = () => new Promise((resolve) => setTimeout(resolve, 5));

describe('handling a run\'s notifications in order', () => {
  it('handles them in the order they were made, whatever order they arrive in', async () => {
    const seen: string[] = [];
    const queue = new SequencedQueue<string>(async (item) => void seen.push(item));

    queue.push(3, 'c');
    queue.push(1, 'a');
    queue.push(2, 'b');

    assert.equal(await queue.processed(3), true);
    assert.deepStrictEqual(seen, ['a', 'b', 'c']);
  });

  it('handles one at a time: a slow one is finished before the next starts', async () => {
    const events: string[] = [];
    const queue = new SequencedQueue<string>(async (item) => {
      events.push(`start ${item}`);
      await tick();
      events.push(`end ${item}`);
    });

    queue.push(1, 'a');
    queue.push(2, 'b');
    await queue.processed(2);

    assert.deepStrictEqual(events, ['start a', 'end a', 'start b', 'end b']);
  });

  it('says when everything up to a number is done, and not before', async () => {
    const queue = new SequencedQueue<number>(async () => undefined);
    let done = false;
    const waiting = queue.processed(3).then((result) => ((done = result), result));

    queue.push(1, 1);
    queue.push(3, 3);
    await tick();
    assert.equal(done, false); // 2 has not come

    queue.push(2, 2);
    assert.equal(await waiting, true);
  });

  it('gives up waiting for one that never comes, and says so', async () => {
    const queue = new SequencedQueue<number>(async () => undefined);
    queue.push(1, 1);
    assert.equal(await queue.processed(2, 30), false);
  });

  it('is done at once for a run that sent nothing', async () => {
    assert.equal(await new SequencedQueue<number>(async () => undefined).processed(0), true);
  });

  it('keeps going when one fails, and ignores a repeat', async () => {
    const seen: number[] = [];
    const queue = new SequencedQueue<number>(async (item) => {
      if (item === 2) throw new Error('cannot show this');
      seen.push(item);
    });

    [1, 2, 3, 3, 1].forEach((n) => queue.push(n, n));
    await queue.processed(3);

    assert.deepStrictEqual(seen, [1, 3]);
  });

  it('handles notifications with no number, from an older engine, in the order they came', async () => {
    const seen: string[] = [];
    const queue = new SequencedQueue<string>(async (item) => {
      await tick();
      seen.push(item);
    });

    queue.push(undefined, 'a');
    queue.push(0, 'b');
    await new Promise((resolve) => setTimeout(resolve, 40));

    assert.deepStrictEqual(seen, ['a', 'b']);
  });
});
