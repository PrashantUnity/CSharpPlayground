import assert from 'node:assert/strict';
import * as fs from 'node:fs';
import * as path from 'node:path';
import * as vscode from 'vscode';
import type { FrySharpApi } from '../../../src/extension';
import { notebookIn, notebookWith, runCell, startRun, waitFor } from './notebooks';
import { itemsOf as items, textOfCell as textOf } from './outputs';
import { needs } from './skip';

// Go cells and files, built and run by the real Go toolchain through the real engine, in a real VS Code.

const workspace = process.env.FRY_IT_WORKSPACE as string;
const EXTENSION = 'CodeFryDev.frysharp';
const TABLE = 'application/vnd.fry.table+json';
const INSPECTOR = 'application/vnd.fry.inspector+json';

export const goTests: Array<[string, () => Promise<void>]> = [
  [
    'a Go cell keeps what it makes, shows a list of structs as a table and a struct as the Object Explorer',
    async () => {
      needs('go', 'FRY_REQUIRE_GO');
      const api = (await vscode.extensions.getExtension(EXTENSION)!.activate()) as FrySharpApi;
      const notebook = await notebookIn(
        'go',
        'type Person struct {\n\tName string\n\tAge  int\n}\n\ntotal := 41',
        '[]Person{{"Ada", 36}, {"Linus", 55}}',
        'Person{"Grace", 85}',
        'total + 1'
      );
      await runCell(notebook, 0);
      const list = await runCell(notebook, 1);
      const one = await runCell(notebook, 2);
      const sum = await runCell(notebook, 3);

      assert.equal(list.executionSummary?.success, true, textOf(list));
      const [table] = items(list, TABLE) as Array<{ columns: string[]; numeric: boolean[]; rows: string[][] }>;
      assert.deepStrictEqual(table.columns, ['Name', 'Age']);
      assert.deepStrictEqual(table.numeric, [false, true]);
      assert.deepStrictEqual(table.rows, [['Ada', '36'], ['Linus', '55']]);
      const [tree] = items(one, INSPECTOR) as Array<{ title: string }>;
      assert.equal(tree.title, 'Person');
      assert.equal(sum.executionSummary?.success, true, textOf(sum));
      assert.match(textOf(sum), /42/);

      const kernels = await api.variables.getChildren();
      const go = kernels.find((node) => /^Go/i.test(String(api.variables.getTreeItem(node).label)));
      assert.ok(go, 'the Go kernel is listed');
      const names = (await api.variables.getChildren(go)).map((node) => api.variables.getTreeItem(node).label);
      assert.ok(names.includes('total'), JSON.stringify(names));
    }
  ],
  [
    'a Go compile error and a panic are marked on the line of the cell they are about, and the text names the cell',
    async () => {
      needs('go', 'FRY_REQUIRE_GO');
      const notebook = await notebookIn('go', 'a := 1\n\nvar s string = 5\nfmt.Println(a, s)', 'v := []int{}\n\nfmt.Println(v[3])');
      const compile = await runCell(notebook, 0);
      const panic = await runCell(notebook, 1);

      assert.equal(compile.executionSummary?.success, false);
      const marks = vscode.languages.getDiagnostics(compile.document.uri).filter((d) => d.source === 'FrySharp');
      assert.equal(marks.length, 1);
      assert.equal(marks[0].range.start.line, 2);
      assert.equal(marks[0].range.start.character, 15);
      assert.match(marks[0].message, /cannot use 5/);
      assert.match(textOf(compile), /cell:3:16/);

      assert.equal(panic.executionSummary?.success, false);
      const thrown = vscode.languages.getDiagnostics(panic.document.uri).filter((d) => d.source === 'FrySharp');
      assert.equal(thrown.length, 1);
      assert.equal(thrown[0].range.start.line, 2);
      assert.match(textOf(panic), /cell:3/);
    }
  ],
  [
    'fry.Input in a Go cell asks in an input box; closing it is the end of input, and the cell goes on',
    async () => {
      needs('go', 'FRY_REQUIRE_GO');
      const notebook = await notebookIn('go', 'line, ok := fry.Input("Name? ")\nif ok {\n\tfmt.Println("got", line)\n} else {\n\tfmt.Println("end of input")\n}');
      await vscode.window.showNotebookDocument(notebook);
      await vscode.commands.executeCommand('notebook.selectKernel', { id: 'frysharp-kernel-frysharp-notebook', extension: EXTENSION });
      const cell = notebook.cellAt(0);
      startRun(notebook, 0, 1);

      await new Promise((resolve) => setTimeout(resolve, 8000)); // the box is open by now (the first build is slower): the cell has not finished
      assert.equal(cell.executionSummary?.success, undefined, 'the cell is waiting for its answer');
      await vscode.commands.executeCommand('workbench.action.closeQuickOpen');
      await waitFor('the cell to finish', () => cell.executionSummary?.success !== undefined, 90_000);

      assert.equal(cell.executionSummary?.success, true, textOf(cell));
      assert.match(textOf(cell), /end of input/);
    }
  ],
  [
    'a Go cell can take a list a C# cell made, with #!share, and its own values go the other way',
    async () => {
      needs('go', 'FRY_REQUIRE_GO');
      const notebook = await notebookWith(
        ['csharp', 'var rows = new[] { 3, 4, 5 };'],
        ['go', '#!share --from csharp rows\ntotal := 0\nfor _, n := range rows {\n\ttotal += n\n}\nfmt.Println(len(rows), "rows, sum", total)'],
        ['go', 'squares := []int{1, 4, 9, 16}'],
        ['csharp', '#!share --from go squares\nConsole.WriteLine(string.Join(",", squares));']
      );
      await runCell(notebook, 0);
      const inGo = await runCell(notebook, 1);
      await runCell(notebook, 2);
      const back = await runCell(notebook, 3);

      assert.equal(inGo.executionSummary?.success, true, textOf(inGo));
      assert.match(textOf(inGo), /3 rows, sum 12/);
      assert.equal(back.executionSummary?.success, true, textOf(back));
      assert.match(textOf(back), /1,4,9,16/);
    }
  ],
  [
    'Stop ends a Go cell that is running, and what the notebook made before is still there',
    async () => {
      needs('go', 'FRY_REQUIRE_GO');
      const notebook = await notebookIn('go', 'kept := 41', 'fmt.Println("started")\nfor {\n\ttime.Sleep(50 * time.Millisecond)\n}', 'kept + 1');
      await runCell(notebook, 0);
      const running = notebook.cellAt(1);
      startRun(notebook, 1, 2);
      await waitFor('the program to start', () => textOf(running).includes('started'), 90_000);
      await vscode.commands.executeCommand('notebook.cell.cancelExecution', { ranges: [{ start: 1, end: 2 }], document: notebook.uri });
      await waitFor('the cell to stop', () => running.executionSummary?.success !== undefined, 30_000);

      const after = await runCell(notebook, 2);
      assert.equal(after.executionSummary?.success, true, textOf(after));
      assert.match(textOf(after), /42/);
    }
  ],
  [
    'Run with FrySharp runs a .go file in the terminal, asks for input with fry.Input, and a compile error is a problem at its line',
    async () => {
      needs('go', 'FRY_REQUIRE_GO');
      const { scripts } = (await vscode.extensions.getExtension(EXTENSION)!.activate()) as FrySharpApi;
      const asks = vscode.Uri.file(path.join(workspace, 'asks.go'));
      fs.writeFileSync(asks.fsPath, 'package main\n\nimport (\n\t"fmt"\n\t"fry"\n)\n\nfunc main() {\n\tname, _ := fry.Input("Name? ")\n\tfmt.Println("Hello,", name)\n}\n');

      await scripts.run(asks);
      const ask = scripts.terminalFor(asks)!;
      await waitFor('the prompt', () => ask.transcript.includes('Name? '), 120_000);
      ask.handleInput('Ada');
      ask.handleInput('\r');
      await waitFor('the run to end', () => /exited with code/.test(ask.transcript), 30_000);
      assert.match(ask.transcript, /Hello, Ada/);
      assert.match(ask.transcript, /exited with code 0/);

      const broken = vscode.Uri.file(path.join(workspace, 'broken.go'));
      fs.writeFileSync(broken.fsPath, 'package main\n\nimport "fmt"\n\nfunc main() {\n\tvar total int = "x"\n\tfmt.Println(total)\n}\n');
      await scripts.run(broken);
      const run = scripts.terminalFor(broken)!;
      await waitFor('the run to end', () => /exited with code/.test(run.transcript), 90_000);

      assert.match(run.transcript, /exited with code 1/);
      await waitFor('the problem', () => vscode.languages.getDiagnostics(broken).some((d) => d.range.start.line === 5), 10_000);
    }
  ]
];
