import assert from 'node:assert/strict';
import * as fs from 'node:fs';
import * as path from 'node:path';
import * as vscode from 'vscode';
import type { FrySharpApi } from '../../../src/extension';
import { notebookIn, notebookWith, runCell, startRun, waitFor } from './notebooks';
import { itemsOf as items, textOfCell as textOf } from './outputs';
import { needs } from './skip';

// C++ cells and files, built and run by a real compiler through the real engine, in a real VS Code.

const workspace = process.env.FRY_IT_WORKSPACE as string;
const EXTENSION = 'CodeFryDev.frysharp';
const TABLE = 'application/vnd.fry.table+json';
const INSPECTOR = 'application/vnd.fry.inspector+json';

// clang++ or g++ (the engine finds one the same way)
const needsCompiler = (): void => {
  if (process.env.FRY_REQUIRE_CPP === '1') return;
  try {
    needs('clang++', 'FRY_REQUIRE_CPP');
  } catch {
    needs('g++', 'FRY_REQUIRE_CPP');
  }
};

export const cppTests: Array<[string, () => Promise<void>]> = [
  [
    'a C++ cell keeps what it makes, shows a list of structs as a table and a struct as the Object Explorer',
    async () => {
      needsCompiler();
      const api = (await vscode.extensions.getExtension(EXTENSION)!.activate()) as FrySharpApi;
      const notebook = await notebookIn(
        'cpp',
        'struct Person {\n    std::string name;\n    int age;\n};\n\nint total = 41;',
        'std::vector<Person>{{"Ada", 36}, {"Linus", 55}}',
        'Person{"Grace", 85}',
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
      const cpp = kernels.find((node) => /C\+\+/i.test(String(api.variables.getTreeItem(node).label)));
      assert.ok(cpp, 'the C++ kernel is listed');
      const names = (await api.variables.getChildren(cpp)).map((node) => api.variables.getTreeItem(node).label);
      assert.ok(names.includes('total'), JSON.stringify(names));
    }
  ],
  [
    'a C++ compile error is marked on the line of the cell it is about, and the text names the cell',
    async () => {
      needsCompiler();
      const notebook = await notebookIn('cpp', 'int a = 1;\n\nint s = "text";\nstd::cout << a << s;');
      const compile = await runCell(notebook, 0);

      assert.equal(compile.executionSummary?.success, false);
      const marks = vscode.languages.getDiagnostics(compile.document.uri).filter((d) => d.source === 'FrySharp');
      assert.equal(marks.length, 1);
      assert.equal(marks[0].range.start.line, 2);
      assert.equal(marks[0].range.start.character, 4);
      assert.match(textOf(compile), /cell:3:5/);
    }
  ],
  [
    'fry::input in a C++ cell asks in an input box; closing it is the end of input, and the cell goes on',
    async () => {
      needsCompiler();
      const notebook = await notebookIn('cpp', 'auto line = fry::input("Name? ");\nstd::cout << (line ? "got " + *line : std::string("end of input")) << "\\n";');
      await vscode.window.showNotebookDocument(notebook);
      await vscode.commands.executeCommand('notebook.selectKernel', { id: 'frysharp-kernel-frysharp-notebook', extension: EXTENSION });
      const cell = notebook.cellAt(0);
      startRun(notebook, 0, 1);

      await new Promise((resolve) => setTimeout(resolve, 10000)); // the box is open by now (the build is slow): the cell has not finished
      assert.equal(cell.executionSummary?.success, undefined, 'the cell is waiting for its answer');
      await vscode.commands.executeCommand('workbench.action.closeQuickOpen');
      await waitFor('the cell to finish', () => cell.executionSummary?.success !== undefined, 90_000);

      assert.equal(cell.executionSummary?.success, true, textOf(cell));
      assert.match(textOf(cell), /end of input/);
    }
  ],
  [
    'a C++ cell can take a list a C# cell made, with #!share, and its own values go the other way',
    async () => {
      needsCompiler();
      const notebook = await notebookWith(
        ['csharp', 'var rows = new[] { 3, 4, 5 };'],
        ['cpp', '#!share --from csharp rows\nlong long total = 0;\nfor (auto n : rows) total += n;\nstd::cout << rows.size() << " rows, sum " << total << "\\n";'],
        ['cpp', 'std::vector<int> squares{1, 4, 9, 16};'],
        ['csharp', '#!share --from cpp squares\nConsole.WriteLine(string.Join(",", squares));']
      );
      await runCell(notebook, 0);
      const inCpp = await runCell(notebook, 1);
      await runCell(notebook, 2);
      const back = await runCell(notebook, 3);

      assert.equal(inCpp.executionSummary?.success, true, textOf(inCpp));
      assert.match(textOf(inCpp), /3 rows, sum 12/);
      assert.equal(back.executionSummary?.success, true, textOf(back));
      assert.match(textOf(back), /1,4,9,16/);
    }
  ],
  [
    'Stop ends a C++ cell that is running, and what the notebook made before is still there',
    async () => {
      needsCompiler();
      const notebook = await notebookIn('cpp', 'int kept = 41;', 'std::cout << "started\\n";\nwhile (true) { std::this_thread::sleep_for(std::chrono::milliseconds(50)); }', 'kept + 1');
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
    'Run with FrySharp runs a .cpp file in the terminal, and a compile error is a problem at its line',
    async () => {
      needsCompiler();
      const { scripts } = (await vscode.extensions.getExtension(EXTENSION)!.activate()) as FrySharpApi;
      const hello = vscode.Uri.file(path.join(workspace, 'hello.cpp'));
      fs.writeFileSync(hello.fsPath, '#include <fry/values.hpp>\n\nint main() {\n    auto name = fry::input("Name? ");\n    std::cout << "Hello, " << name.value_or("?") << std::endl;\n}\n');

      await scripts.run(hello);
      const ask = scripts.terminalFor(hello)!;
      await waitFor('the prompt', () => ask.transcript.includes('Name? '), 120_000);
      ask.handleInput('Ada');
      ask.handleInput('\r');
      await waitFor('the run to end', () => /exited with code/.test(ask.transcript), 30_000);
      assert.match(ask.transcript, /Hello, Ada/);
      assert.match(ask.transcript, /exited with code 0/);

      const broken = vscode.Uri.file(path.join(workspace, 'broken.cpp'));
      fs.writeFileSync(broken.fsPath, '#include <iostream>\n\nint main() {\n    int total = "x";\n    std::cout << total;\n}\n');
      await scripts.run(broken);
      const run = scripts.terminalFor(broken)!;
      await waitFor('the run to end', () => /exited with code/.test(run.transcript), 90_000);

      assert.match(run.transcript, /exited with code 1/);
      await waitFor('the problem', () => vscode.languages.getDiagnostics(broken).some((d) => d.range.start.line === 3), 10_000);
    }
  ]
];
