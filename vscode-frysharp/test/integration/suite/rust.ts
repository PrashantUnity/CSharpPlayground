import assert from 'node:assert/strict';
import * as fs from 'node:fs';
import * as path from 'node:path';
import * as vscode from 'vscode';
import type { FrySharpApi } from '../../../src/extension';
import { notebookIn, notebookWith, runCell, startRun, waitFor } from './notebooks';
import { itemsOf as items, textOfCell as textOf } from './outputs';
import { needsCargo } from './skip';

// Rust cells and files, built and run by the real Cargo through the real engine, in a real VS Code.

const workspace = process.env.FRY_IT_WORKSPACE as string;
const EXTENSION = 'CodeFryDev.frysharp';
const TABLE = 'application/vnd.fry.table+json';
const INSPECTOR = 'application/vnd.fry.inspector+json';

export const rustTests: Array<[string, () => Promise<void>]> = [
  [
    'a Rust cell keeps what it makes, shows a list of structs as a table and a struct as the Object Explorer',
    async () => {
      needsCargo('FRY_REQUIRE_RUST');
      const api = (await vscode.extensions.getExtension(EXTENSION)!.activate()) as FrySharpApi;
      const notebook = await notebookIn(
        'rust',
        '#[derive(Debug)]\nstruct Person { name: String, age: u32 }\nlet total = 41;',
        'vec![Person { name: "Ada".into(), age: 36 }, Person { name: "Linus".into(), age: 55 }]',
        'Person { name: "Grace".into(), age: 85 }',
        'total + 1'
      );
      await runCell(notebook, 0);
      const list = await runCell(notebook, 1);
      const one = await runCell(notebook, 2);
      const sum = await runCell(notebook, 3);

      assert.equal(list.executionSummary?.success, true, textOf(list));
      const [table] = items(list, TABLE) as Array<{ columns: string[]; numeric: boolean[]; rows: string[][] }>;
      assert.deepStrictEqual(table.columns, ['name', 'age']);
      assert.deepStrictEqual(table.numeric, [false, true]);
      assert.deepStrictEqual(table.rows, [['Ada', '36'], ['Linus', '55']]);
      const [tree] = items(one, INSPECTOR) as Array<{ title: string }>;
      assert.equal(tree.title, 'Person');
      assert.equal(sum.executionSummary?.success, true, textOf(sum));
      assert.match(textOf(sum), /42/);

      const kernels = await api.variables.getChildren();
      const rust = kernels.find((node) => /Rust/i.test(String(api.variables.getTreeItem(node).label)));
      assert.ok(rust, 'the Rust kernel is listed');
      const names = (await api.variables.getChildren(rust)).map((node) => api.variables.getTreeItem(node).label);
      assert.ok(names.includes('total'), JSON.stringify(names));
    }
  ],
  [
    'a Rust compile error and a panic are marked on the line of the cell they are about, and the text names the cell',
    async () => {
      needsCargo('FRY_REQUIRE_RUST');
      const notebook = await notebookIn('rust', 'let a = 1;\n\nlet s: String = 5;\nlet c = a;', 'let v: Vec<i32> = Vec::new();\n\nprintln!("{}", v[3]);');
      const compile = await runCell(notebook, 0);
      const panic = await runCell(notebook, 1);

      assert.equal(compile.executionSummary?.success, false);
      const marks = vscode.languages.getDiagnostics(compile.document.uri).filter((d) => d.source === 'FrySharp');
      assert.equal(marks.length, 1);
      assert.equal(marks[0].range.start.line, 2);
      assert.equal(marks[0].range.start.character, 16);
      assert.match(marks[0].message, /mismatched types/);
      assert.match(textOf(compile), /--> cell:3:17/);

      assert.equal(panic.executionSummary?.success, false);
      const thrown = vscode.languages.getDiagnostics(panic.document.uri).filter((d) => d.source === 'FrySharp');
      assert.equal(thrown.length, 1);
      assert.equal(thrown[0].range.start.line, 2);
      assert.match(textOf(panic), /panicked at cell:3:/);
    }
  ],
  [
    'fry::input in a Rust cell asks in an input box; closing it is the end of input, and the cell goes on',
    async () => {
      needsCargo('FRY_REQUIRE_RUST');
      const notebook = await notebookIn('rust', 'let line = fry::input("Name? ");\nprintln!("{}", match line { Some(l) => format!("got {}", l), None => "end of input".to_string() });');
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
    'a Rust cell can take a list a C# cell made, with #!share, and its own values go the other way',
    async () => {
      needsCargo('FRY_REQUIRE_RUST');
      const notebook = await notebookWith(
        ['csharp', 'var rows = new[] { 3, 4, 5 };'],
        ['rust', '#!share --from csharp rows\nprintln!("{} rows, sum {}", rows.len(), rows.iter().sum::<i64>());'],
        ['rust', 'let squares = vec![1, 4, 9, 16];\nfry::share!(squares);'],
        ['csharp', '#!share --from rust squares\nConsole.WriteLine(string.Join(",", squares));']
      );
      await runCell(notebook, 0);
      const inRust = await runCell(notebook, 1);
      await runCell(notebook, 2);
      const back = await runCell(notebook, 3);

      assert.equal(inRust.executionSummary?.success, true, textOf(inRust));
      assert.match(textOf(inRust), /3 rows, sum 12/);
      assert.equal(back.executionSummary?.success, true, textOf(back));
      assert.match(textOf(back), /1,4,9,16/);
    }
  ],
  [
    'Stop ends a Rust cell that is running, and what the notebook made before is still there',
    async () => {
      needsCargo('FRY_REQUIRE_RUST');
      const notebook = await notebookIn('rust', 'let kept = 41;', 'println!("started");\nloop { std::thread::sleep(std::time::Duration::from_millis(50)); }', 'kept + 1');
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
    'Run with FrySharp runs a .rs file in the terminal, asks for input with fry::input, and a compile error is a problem at its line',
    async () => {
      needsCargo('FRY_REQUIRE_RUST');
      const { scripts } = (await vscode.extensions.getExtension(EXTENSION)!.activate()) as FrySharpApi;
      const asks = vscode.Uri.file(path.join(workspace, 'asks.rs'));
      fs.writeFileSync(asks.fsPath, 'fn main() {\n    let name = fry::input("Name? ").unwrap_or_default();\n    println!("Hello, {}", name);\n}\n');

      await scripts.run(asks);
      const ask = scripts.terminalFor(asks)!;
      await waitFor('the prompt', () => ask.transcript.includes('Name? '), 120_000);
      ask.handleInput('Ada');
      ask.handleInput('\r');
      await waitFor('the run to end', () => /exited with code/.test(ask.transcript), 30_000);
      assert.match(ask.transcript, /Hello, Ada/);
      assert.match(ask.transcript, /exited with code 0/);

      const broken = vscode.Uri.file(path.join(workspace, 'broken.rs'));
      fs.writeFileSync(broken.fsPath, 'fn main() {\n    let total: i32 = 1;\n    let bad: i32 = "x";\n    println!("{} {}", total, bad);\n}\n');
      await scripts.run(broken);
      const run = scripts.terminalFor(broken)!;
      await waitFor('the run to end', () => /exited with code/.test(run.transcript), 90_000);

      assert.match(run.transcript, /exited with code 1/);
      await waitFor('the problem', () => vscode.languages.getDiagnostics(broken).some((d) => d.range.start.line === 2), 10_000);
    }
  ]
];
