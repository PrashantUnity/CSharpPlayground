import assert from 'node:assert/strict';
import * as fs from 'node:fs';
import * as os from 'node:os';
import * as path from 'node:path';
import { describe, it } from 'node:test';
import { SidecarClient, SidecarState } from '../../src/sidecar/sidecarClient';
import { resolveSidecarLaunch } from '../../src/sidecar/launcher';

// The client against the real engine (FrySharp.Sidecar, built with `dotnet build FrySharp.Sidecar`): start, ask, kill,
// ask again. FRY_REQUIRE_SIDECAR=1 makes a missing build a failure rather than a skip.
const dll = path.resolve(__dirname, '../../../../FrySharp.Sidecar/bin/Debug/net10.0/frysharp-sidecar.dll');
const skip = fs.existsSync(dll) || process.env.FRY_REQUIRE_SIDECAR === '1' ? false : 'Build the sidecar first: dotnet build FrySharp.Sidecar';

function newClient(workspace: string, log: string[] = []) {
  return new SidecarClient({
    log: (message) => log.push(message),
    prepare: async () => {
      const launch = resolveSidecarLaunch({ setting: dll, extensionPath: '', development: false, exists: fs.existsSync }, workspace);
      return { command: 'dotnet', args: launch.args, cwd: workspace };
    }
  });
}

const workspace = () => fs.mkdtempSync(path.join(os.tmpdir(), 'frysharp-client-'));

describe('the sidecar client, against the real engine', { skip }, () => {
  it('starts when first asked, once, however many ask at the same time; and says what the engine can do', async () => {
    const client = newClient(workspace());
    const states: SidecarState[] = [];
    client.onDidChangeState((state) => states.push(state));
    try {
      assert.equal(client.state, 'stopped');
      const [a, b] = await Promise.all([client.ensureStarted(), client.ensureStarted()]);

      assert.strictEqual(a, b);
      assert.deepStrictEqual(states, ['starting', 'ready']);
      assert.ok(client.languages.some((language) => language.id === 'csharp' && language.canRunCells));
    } finally {
      client.dispose();
    }
  });

  it('runs a cell of a notebook it names by id, with the output in a notification', async () => {
    const client = newClient(workspace());
    const output: string[] = [];
    client.onNotebookOutput((n) => n.output.text && output.push(n.output.text));
    try {
      const result = await client.executeCell({ path: 'nb.frynb', cellId: 'a', source: 'Console.WriteLine("from a held notebook");', language: 'csharp', notebookId: 'file:///x/nb.frynb' });

      assert.equal(result.success, true);
      assert.ok(output.join('').includes('from a held notebook'));
    } finally {
      client.dispose();
    }
  });

  it('notices that the engine died, says the notebooks\' state is gone, and starts it again on the next request', async () => {
    const log: string[] = [];
    const client = newClient(workspace(), log);
    const lost: string[] = [];
    client.onDidLoseState(({ reason }) => lost.push(reason));
    try {
      await client.executeCell({ path: '', cellId: '1', source: 'int kept = 1;', language: 'csharp', notebookId: 'nb' });
      const pid = (client as unknown as { child: { pid: number; kill(): void } }).child.pid;

      process.kill(pid, 'SIGKILL');
      await waitFor(() => client.state === 'crashed');
      assert.equal(lost.length, 1);
      assert.match(lost[0], /stopped unexpectedly/);

      // the next request starts a new engine: with a new, empty notebook
      const again = await client.executeCell({ path: '', cellId: '2', source: 'Console.WriteLine(kept);', language: 'csharp', notebookId: 'nb' });
      assert.equal(again.success, false);
      assert.equal(client.state, 'ready');
      assert.equal(lost.length, 1); // said once, when it happened, not again when it was started
    } finally {
      client.dispose();
    }
  });

  it('gives every request a deadline, and a cell none', async () => {
    const client = newClient(workspace());
    try {
      await client.ensureStarted();
      const slow = client.request('fry/notebook/execute', { path: '', cellId: 's', source: 'System.Threading.Thread.Sleep(4000);', language: 'csharp', notebookId: 'slow' }, { timeoutMs: 300 });
      await assert.rejects(slow, /did not answer fry\/notebook\/execute within/);

      const cell = await client.executeCell({ path: '', cellId: 'c', source: 'System.Threading.Thread.Sleep(600);', language: 'csharp', notebookId: 'slow2' });
      assert.equal(cell.success, true);
    } finally {
      client.dispose();
    }
  });

  it('restart() stops the engine and starts another', async () => {
    const client = newClient(workspace());
    try {
      await client.ensureStarted();
      const first = (client as unknown as { child: { pid: number } }).child.pid;
      const lost: string[] = [];
      client.onDidLoseState(({ reason }) => lost.push(reason));
      await client.restart();

      assert.equal(lost.length, 0); // asked for, so nothing to warn about
      assert.equal(client.state, 'ready');
      assert.notEqual((client as unknown as { child: { pid: number } }).child.pid, first);
    } finally {
      client.dispose();
    }
  });

  it('does not start again once disposed', async () => {
    const client = newClient(workspace());
    client.dispose();
    await assert.rejects(client.ensureStarted(), /disposed/);
  });
});

async function waitFor(condition: () => boolean, ms = 10_000): Promise<void> {
  const until = Date.now() + ms;
  while (!condition()) {
    if (Date.now() > until) throw new Error('timed out waiting');
    await new Promise((resolve) => setTimeout(resolve, 25));
  }
}
