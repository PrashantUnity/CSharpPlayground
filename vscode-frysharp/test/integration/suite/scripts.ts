import assert from 'node:assert/strict';
import * as fs from 'node:fs';
import * as path from 'node:path';
import * as vscode from 'vscode';
import type { FrySharpApi } from '../../../src/extension';

// Script runs, in a real VS Code with the real engine: the terminal's text, input, Stop and Run Again, and what lands in
// the Problems panel and the Results panel.

const workspace = process.env.FRY_IT_WORKSPACE as string;
const EXTENSION = 'CodeFryDev.frysharp';

const api = async (): Promise<FrySharpApi> => (await vscode.extensions.getExtension(EXTENSION)!.activate()) as FrySharpApi;

async function waitFor(what: string, condition: () => boolean, ms = 60_000): Promise<void> {
  const until = Date.now() + ms;
  while (!condition()) {
    if (Date.now() > until) throw new Error(`timed out waiting for ${what}`);
    await new Promise((resolve) => setTimeout(resolve, 50));
  }
}

function script(name: string, code: string): vscode.Uri {
  const file = path.join(workspace, name);
  fs.writeFileSync(file, code);
  return vscode.Uri.file(file);
}

// What the terminal shows when it is done: the text with backspaces, carriage returns and colours applied, as a screen does.
function screen(transcript: string): string[] {
  const lines: string[][] = [[]];
  let row = lines[0];
  let column = 0;
  for (let i = 0; i < transcript.length; i++) {
    const c = transcript[i];
    if (c === '\x1b') {
      // eslint-disable-next-line no-control-regex -- an escape sequence is what is being read
      const sequence = /^\x1b\[[0-9;?]*[A-Za-z]/.exec(transcript.slice(i));
      if (sequence) {
        if (sequence[0] === '\x1b[2J') {
          lines.length = 0;
          lines.push((row = []));
          column = 0;
        }
        i += sequence[0].length - 1;
      }
    } else if (c === '\r') column = 0;
    else if (c === '\n') lines.push((row = []));
    else if (c === '\b') column = Math.max(0, column - 1);
    else row[column++] = c;
  }
  return lines.map((line) => Array.from(line, (ch) => ch ?? ' ').join('').trimEnd());
}

const finished = (run: { running: boolean }) => !run.running;

export const scriptTests: Array<[string, () => Promise<void>]> = [
  [
    'a script runs in a terminal, with the program\'s own colours, and says how it ended',
    async () => {
      const { scripts } = await api();
      const file = script('colours.py', "print('\\x1b[32mgreen\\x1b[0m and plain')\n");
      await scripts.run(file);
      const run = scripts.terminalFor(file)!;
      await waitFor('the run to end', () => /exited with code/.test(run.transcript));

      assert.ok(run.transcript.includes('\x1b[32mgreen\x1b[0m and plain'), 'the colours are the program\'s, passed through');
      assert.match(run.transcript, /✓ exited with code 0/);
      assert.match(run.transcript, /press any key to run again/);
      assert.ok(finished(run));
    }
  ],

  [
    'a script that asks for input gets what is typed, and the typed line is on the screen once',
    async () => {
      const { scripts } = await api();
      const file = script('ask.py', "name = input('Name? ')\nprint('Hello', name)\n");
      await scripts.run(file);
      const run = scripts.terminalFor(file)!;
      await waitFor('the prompt', () => run.transcript.includes('Name? '));

      run.handleInput('Ada');
      run.handleInput('\r');
      await waitFor('the run to end', () => /exited with code/.test(run.transcript));

      const lines = screen(run.transcript);
      assert.ok(lines.includes('Name? Ada'), JSON.stringify(lines));
      assert.ok(lines.includes('Hello Ada'), JSON.stringify(lines));
    }
  ],

  [
    'Ctrl+C stops a running script, and any key runs it again from a clean screen',
    async () => {
      const { scripts } = await api();
      const file = script('wait.py', "print('started', flush=True)\nimport time\ntime.sleep(60)\n");
      await scripts.run(file);
      const run = scripts.terminalFor(file)!;
      await waitFor('it to start', () => run.transcript.includes('started'));
      assert.ok(run.running);

      const stopped = Date.now();
      run.handleInput('\x03');
      await waitFor('it to stop', () => /exited with code/.test(run.transcript), 20_000);
      assert.ok(Date.now() - stopped < 10_000);
      assert.match(run.transcript, /✗ exited with code/);
      assert.ok(!run.running);

      run.handleInput('x'); // any key
      await waitFor('it to run again', () => run.running && screen(run.transcript).filter((line) => line === 'started').length >= 1);
      assert.equal(screen(run.transcript).filter((line) => line.includes('exited with code')).length, 0, 'the screen was cleared');
      run.handleInput('\x03');
      await waitFor('it to stop again', () => !run.running, 20_000);
    }
  ],

  [
    'a script that fails shows its error as links, and puts it in the Problems panel',
    async () => {
      const { scripts } = await api();
      const file = script('boom.py', "x = 1\nprint(x / 0)\n");
      await scripts.run(file);
      const run = scripts.terminalFor(file)!;
      await waitFor('the run to end', () => /exited with code/.test(run.transcript));

      assert.match(run.transcript, /ZeroDivisionError/);
      assert.match(run.transcript, /✗ exited with code [1-9]/);
      await waitFor('a problem', () => vscode.languages.getDiagnostics(file).length > 0, 10_000);
      assert.equal(vscode.languages.getDiagnostics(file)[0].range.start.line, 1);
    }
  ],

  [
    'a script that shows a table puts it in the Results panel and says so in the terminal',
    async () => {
      const { scripts, results } = await api();
      results.clear();
      const file = script('table.csx', 'Console.WriteLine("before");\nDisplay.Table(new[] { new { Name = "Ada", Age = 36 } });\n');
      await scripts.run(file);
      const run = scripts.terminalFor(file)!;
      await waitFor('the run to end', () => /exited with code/.test(run.transcript));

      assert.match(run.transcript, /Shown in FrySharp Results/);
      assert.equal(results.count, 1);
      // the panel opened and its page drew the table with the same renderer as a notebook
      await waitFor('the Results page to draw the table', () => results.reports.some((r) => r.type === 'rendered' && r.mime === 'application/vnd.fry.table+json'), 20_000);
      assert.deepStrictEqual(results.reports.filter((r) => r.type === 'error'), []);
      assert.ok(screen(run.transcript).includes('before'));
    }
  ],

  [
    'a C# script with a compile error is marked at its line in the Problems panel',
    async () => {
      const { scripts } = await api();
      const file = script('bad.csx', 'int ok = 1;\nint bad = ;\n');
      await scripts.run(file);
      const run = scripts.terminalFor(file)!;
      await waitFor('the run to end', () => /exited with code/.test(run.transcript));

      assert.match(run.transcript, /✗ exited with code/);
      await waitFor('a problem', () => vscode.languages.getDiagnostics(file).length > 0, 10_000);
      const problem = vscode.languages.getDiagnostics(file)[0];
      assert.equal(problem.range.start.line, 1);
      assert.match(String(problem.code), /^CS/);

      // running it again with the error fixed clears the mark
      fs.writeFileSync(file.fsPath, 'int ok = 1;\n');
      await scripts.run(file);
      await waitFor('the run to end', () => /✓ exited with code 0/.test(run.transcript));
      assert.equal(vscode.languages.getDiagnostics(file).length, 0);
    }
  ],

  [
    'a file the engine cannot run is refused with what it can run',
    async () => {
      const { scripts } = await api();
      const file = script('notes.txt', 'not a program');
      await scripts.run(file);
      const run = scripts.terminalFor(file)!;
      await waitFor('the message', () => /does not run/.test(run.transcript));

      assert.match(run.transcript, /runs .*\.py/);
      assert.ok(!run.running);
    }
  ]
  ,
  [
    'the scratchpad runs a selection from a file, and keeps what it defined for the next one',
    async () => {
      const first = script('pad1.py', "total = 40\nprint('pad says', total + 2)\n");
      const document = await vscode.workspace.openTextDocument(first);
      const editor = await vscode.window.showTextDocument(document);
      editor.selection = new vscode.Selection(0, 0, 1, 0); // the first line: total = 40
      await vscode.commands.executeCommand('frysharp.sendToScratchpad');
      const pad = vscode.window.activeNotebookEditor!.notebook;
      await waitFor('the first cell to run', () => pad.cellCount >= 1 && pad.cellAt(0).executionSummary?.success === true);
      assert.equal(pad.cellAt(0).document.getText().trim(), 'total = 40');

      const editor2 = await vscode.window.showTextDocument(document);
      editor2.selection = new vscode.Selection(1, 0, 1, document.lineAt(1).text.length); // print('pad says', total + 2)
      await vscode.commands.executeCommand('frysharp.sendToScratchpad');
      await waitFor('the second cell to run', () => pad.cellCount >= 2 && pad.cellAt(1).executionSummary?.success === true);

      const text = pad.cellAt(1).outputs.flatMap((o) => o.items).map((i) => new TextDecoder().decode(i.data)).join('');
      assert.match(text, /pad says 42/);
      assert.equal(pad.cellAt(1).document.languageId, 'python');
    }
  ],

  [
    'the Run lens is above a script only when the setting is on, and says Stop while it runs',
    async () => {
      const file = script('lens.py', "import time\nprint('lens', flush=True)\ntime.sleep(60)\n");
      const document = await vscode.workspace.openTextDocument(file);
      const lensesOf = async () => ((await vscode.commands.executeCommand<vscode.CodeLens[]>('vscode.executeCodeLensProvider', file)) ?? []).filter((l) => /FrySharp|Running|Stop/.test(l.command?.title ?? ''));
      const settings = vscode.workspace.getConfiguration('frysharp.codeLens');

      assert.deepStrictEqual(await lensesOf(), [], 'off by default');
      await settings.update('enabled', true, vscode.ConfigurationTarget.Global);
      try {
        for (const until = Date.now() + 30_000; Date.now() < until && !(await lensesOf()).some((l) => l.command?.command === 'frysharp.runScript'); ) await new Promise((resolve) => setTimeout(resolve, 100));
        const lenses = await lensesOf();
        assert.equal(lenses.length, 1, JSON.stringify(lenses.map((l) => l.command?.title)));
        assert.equal(lenses[0].command?.command, 'frysharp.runScript');
        assert.equal(lenses[0].range.start.line, 0);

        const { scripts } = await api();
        await vscode.window.showTextDocument(document);
        await vscode.commands.executeCommand(lenses[0].command!.command, ...(lenses[0].command!.arguments ?? []));
        const run = scripts.terminalFor(file)!;
        await waitFor('the program to print', () => run.transcript.includes('lens'));
        const whileRunning = await lensesOf();
        assert.ok(whileRunning.some((l) => l.command?.command === 'frysharp.stopRun'), JSON.stringify(whileRunning.map((l) => l.command?.title)));
        await vscode.commands.executeCommand('frysharp.stopRun');
        await waitFor('the run to end', () => /exited with code/.test(run.transcript), 20_000);
      } finally {
        await settings.update('enabled', undefined, vscode.ConfigurationTarget.Global);
      }
    }
  ]
];
