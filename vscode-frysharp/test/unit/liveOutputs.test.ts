import assert from 'node:assert/strict';
import { test } from 'node:test';
import { LiveOutputs, OutputSink, Said } from '../../src/notebook/liveOutputs';
import { CellOutput, OutputItem, STDOUT, text } from '../../src/notebook/format/model';
import { unpack } from '../../src/notebook/format/tabs';
import { PackedCell } from '../../src/shared/outputTabs';

// A cell's outputs as the sink would leave them, and what it was asked, in order.
class Recorder implements OutputSink<number> {
  readonly calls: string[] = [];
  readonly outputs: CellOutput[] = [];

  async append(output: CellOutput) {
    this.calls.push('append');
    this.outputs.push({ items: [...output.items] });
    return this.outputs.length - 1;
  }
  async appendItems(handle: number, items: OutputItem[]) {
    this.calls.push('appendItems');
    this.outputs[handle].items.push(...items);
  }
  async replaceItems(handle: number, items: OutputItem[]) {
    this.calls.push('replaceItems');
    this.outputs[handle].items = [...items];
  }
  async replaceAll(outputs: CellOutput[]) {
    this.calls.push('replaceAll');
    this.outputs.length = 0;
    this.outputs.push(...outputs.map((o) => ({ items: [...o.items] })));
    return outputs.map((_, i) => i);
  }
}

const say = (value: string): Said => ({ kind: 'stream', text: value });
const table = (rows = 2): Said => ({
  kind: 'display',
  data: { 'text/html': '<div class="fry-tbl"></div>', 'application/vnd.fry.table+json': { title: 'T', columns: ['a'], numeric: [true], rows: Array.from({ length: rows }, (_, i) => [i]), totalRows: rows }, 'text/plain': 'T' }
});
const chart = (title: string, displayId?: string, update?: boolean): Said => ({
  kind: 'display',
  data: { 'text/html': '<html></html>', 'application/vnd.fry.chart.v1+json': { title }, 'text/plain': 'chart' },
  displayId,
  update
});

// time held by the test: a timer runs when the test says
function manualClock() {
  const pending: Array<() => void> = [];
  return {
    schedule: (run: () => void) => {
      pending.push(run);
      return () => {
        const at = pending.indexOf(run);
        if (at >= 0) pending.splice(at, 1);
      };
    },
    tick: () => pending.splice(0).forEach((run) => run()),
    get waiting() {
      return pending.length;
    }
  };
}

const packedOf = (output: CellOutput): PackedCell => JSON.parse(text(output.items[0])) as PackedCell;

test('a cell that only prints has its text in one native output, added to as it comes', async () => {
  const sink = new Recorder();
  const live = new LiveOutputs(sink, { tabs: true });

  await live.show(say('a\n'));
  await live.show(say('b\n'));
  await live.finish();

  assert.deepStrictEqual(sink.calls, ['append', 'appendItems']);
  assert.equal(sink.outputs.length, 1);
  assert.equal(sink.outputs[0].items.map(text).join(''), 'a\nb\n');
  assert.equal(live.isTabbed, false);
});

test('a cell that shows only tables has each as an output of its own', async () => {
  const sink = new Recorder();
  const live = new LiveOutputs(sink, { tabs: true });

  await live.show(table());
  await live.show(table(3));

  assert.deepStrictEqual(sink.calls, ['append', 'append']);
  assert.equal(live.isTabbed, false);
});

test('when a second kind of output comes, everything made so far becomes the one output, and stays one', async () => {
  const sink = new Recorder();
  const clock = manualClock();
  const live = new LiveOutputs(sink, { tabs: true, schedule: clock.schedule });

  await live.show(say('hello\n'));
  await live.show(table());

  assert.equal(live.isTabbed, true);
  assert.deepStrictEqual(sink.calls, ['append', 'replaceAll']);
  assert.equal(sink.outputs.length, 1);
  assert.deepStrictEqual(packedOf(sink.outputs[0]).outputs.map((o) => o.items[0].mime), ['application/vnd.code.notebook.stdout', 'text/html']);

  await live.show(say('more\n'));
  await live.show(chart('c'));
  clock.tick();
  await live.finish();

  assert.equal(sink.outputs.length, 1);
  const outputs = unpack(sink.outputs[0]);
  assert.deepStrictEqual(outputs.map((o) => o.items[0].mime), [STDOUT, 'text/html', STDOUT, 'text/html']);
  assert.equal(text(outputs[0].items[0]), 'hello\n');
  assert.equal(text(outputs[2].items[0]), 'more\n');
});

test('text after a table is a console output of its own, in order, and text that comes after it is added to it', async () => {
  const sink = new Recorder();
  const live = new LiveOutputs(sink, { tabs: true, schedule: manualClock().schedule });

  await live.show(say('1\n'));
  await live.show(table());
  await live.show(say('2\n'));
  await live.show(say('3\n'));
  await live.finish();

  const outputs = unpack(sink.outputs[0]);
  assert.equal(outputs.length, 3);
  assert.equal(text(outputs[2].items[0]), '2\n3\n');
});

test('a flood of text is written out a few times, not once for each piece', async () => {
  const sink = new Recorder();
  const clock = manualClock();
  const live = new LiveOutputs(sink, { tabs: true, schedule: clock.schedule });

  await live.show(table());
  await live.show(say('x\n'));
  for (let i = 0; i < 500; i++) await live.show(say(`line ${i}\n`));

  assert.equal(clock.waiting, 1);
  assert.deepStrictEqual(sink.calls, ['append', 'replaceAll']); // nothing yet: the timer has not run
  clock.tick();
  await live.show(say('last\n'));
  await live.finish();
  assert.equal(sink.calls.filter((c) => c === 'replaceItems').length, 2); // the one the timer ran, and the last one when the cell ended
});

test('while the cell runs a long console carries its last lines and says how many it left off; the end has all of it', async () => {
  const sink = new Recorder();
  const clock = manualClock();
  const live = new LiveOutputs(sink, { tabs: true, liveConsoleLines: 5, schedule: clock.schedule });

  await live.show(table());
  for (let i = 0; i < 20; i++) await live.show(say(`line ${i}\n`));
  clock.tick();
  await live.show(say('wait\n'));

  const during = packedOf(sink.outputs[0]);
  assert.equal(during.outputs[1].omitted, 15);
  assert.equal(during.outputs[1].items[0].text, 'line 15\nline 16\nline 17\nline 18\nline 19\n');

  await live.finish();
  const after = unpack(sink.outputs[0]);
  assert.equal(text(after[1].items[0]), Array.from({ length: 20 }, (_, i) => `line ${i}\n`).join('') + 'wait\n');
  assert.equal(packedOf(sink.outputs[0]).outputs[1].omitted, undefined);
});

test('a chart the program redraws is redrawn where it is, whether the output is native or tabbed', async () => {
  const native = new Recorder();
  const live = new LiveOutputs(native, { tabs: true, schedule: manualClock().schedule });
  await live.show(chart('before', 'c1'));
  await live.show(chart('after', 'c1', true));
  assert.deepStrictEqual(native.calls, ['append', 'replaceItems']);
  assert.match(text(native.outputs[0].items[1]), /after/);

  const sink = new Recorder();
  const tabbed = new LiveOutputs(sink, { tabs: true, schedule: manualClock().schedule });
  await tabbed.show(chart('before', 'c2'));
  await tabbed.show(say('text\n'));
  await tabbed.show(chart('after', 'c2', true));
  await tabbed.finish();
  const outputs = unpack(sink.outputs[0]);
  assert.equal(outputs.length, 2);
  assert.match(text(outputs[0].items[1]), /after/);
});

test('with the tabs off, a cell with several kinds of output keeps them as outputs one after another', async () => {
  const sink = new Recorder();
  const live = new LiveOutputs(sink, { tabs: false });

  await live.show(say('hello\n'));
  await live.show(table());
  await live.show(say('more\n'));
  await live.finish();

  assert.equal(live.isTabbed, false);
  assert.deepStrictEqual(sink.calls, ['append', 'append', 'append']);
});

test('output that comes while another piece is being shown waits its turn', async () => {
  const order: string[] = [];
  const slow: OutputSink<number> = {
    append: async () => {
      order.push('append start');
      await new Promise((resolve) => setTimeout(resolve, 20));
      order.push('append end');
      return 0;
    },
    appendItems: async () => {
      order.push('appendItems');
    },
    replaceItems: async () => undefined,
    replaceAll: async () => [0]
  };
  const live = new LiveOutputs(slow, { tabs: false });

  await Promise.all([live.show(say('a')), live.show(say('b'))]);

  assert.deepStrictEqual(order, ['append start', 'append end', 'appendItems']);
});
