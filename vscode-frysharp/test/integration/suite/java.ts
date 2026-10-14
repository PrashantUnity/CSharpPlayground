import assert from 'node:assert/strict';
import * as fs from 'node:fs';
import * as path from 'node:path';
import * as vscode from 'vscode';
import type { FrySharpApi } from '../../../src/extension';
import { notebookIn, notebookWith, runCell, startRun, waitFor } from './notebooks';
import { itemsOf as items, textOfCell as textOf } from './outputs';
import { needs } from './skip';

// Java cells and files, run by the real JDK (a JShell kernel) through the real engine, in a real VS Code.

const workspace = process.env.FRY_IT_WORKSPACE as string;
const EXTENSION = 'CodeFryDev.frysharp';
const TABLE = 'application/vnd.fry.table+json';
const INSPECTOR = 'application/vnd.fry.inspector+json';

export const javaTests: Array<[string, () => Promise<void>]> = [
  [
    'a Java cell keeps what it declares, shows a list of records as a table and an object as the Object Explorer',
    async () => {
      needs('javac', 'FRY_REQUIRE_JAVA');
      const api = (await vscode.extensions.getExtension(EXTENSION)!.activate()) as FrySharpApi;
      const notebook = await notebookIn(
        'java',
        'record Person(String name, int age) {}\nvar people = List.of(new Person("Ada", 36), new Person("Linus", 55));',
        'people',
        'people.get(0)'
      );
      await runCell(notebook, 0);
      const list = await runCell(notebook, 1);
      const one = await runCell(notebook, 2);

      assert.equal(list.executionSummary?.success, true, textOf(list));
      const [table] = items(list, TABLE) as Array<{ columns: string[]; numeric: boolean[]; rows: string[][] }>;
      assert.deepStrictEqual(table.columns, ['name', 'age']);
      assert.deepStrictEqual(table.numeric, [false, true]);
      assert.deepStrictEqual(table.rows, [['Ada', '36'], ['Linus', '55']]);
      const [tree] = items(one, INSPECTOR) as Array<{ title: string }>;
      assert.equal(tree.title, 'Person');

      const kernels = await api.variables.getChildren();
      const java = kernels.find((node) => /Java/i.test(String(api.variables.getTreeItem(node).label)));
      assert.ok(java, 'the Java kernel is listed');
      const names = (await api.variables.getChildren(java)).map((node) => api.variables.getTreeItem(node).label);
      assert.deepStrictEqual(names, ['people']);
    }
  ],
  [
    'a Java object that points at itself reaches the Object Explorer five levels deep, each reference back listing its members (as the desktop)',
    async () => {
      needs('javac', 'FRY_REQUIRE_JAVA');
      const notebook = await notebookIn(
        'java',
        'public class People { public String name = "Prashant"; public int[] numbers = {1, 34, 45}; public People another; }\nPeople person = new People();\nperson.another = person;\nperson'
      );
      const cell = await runCell(notebook, 0);

      assert.equal(cell.executionSummary?.success, true, textOf(cell));
      type Node = { title: string; circular?: boolean; properties: Array<{ name: string; child?: Node }> };
      const [top] = items(cell, INSPECTOR) as Node[];
      const chain = [top];
      for (let next = chain[0].properties.find((p) => p.name === 'another')?.child; next; next = next.properties.find((p) => p.name === 'another')?.child) chain.push(next);

      assert.equal(chain.length, 5);
      assert.deepStrictEqual(chain.map((node) => node.circular === true), [false, true, true, true, false]);
      assert.deepStrictEqual(chain.slice(0, 4).map((node) => node.properties.map((p) => p.name)), Array(4).fill(['name', 'numbers', 'another']));
      assert.deepStrictEqual(chain[4].properties, []);
    }
  ],
  [
    'a Java compile error is underlined where the compiler found it, and a runtime error is on its line of the cell',
    async () => {
      needs('javac', 'FRY_REQUIRE_JAVA');
      const notebook = await notebookIn('java', 'int a = 1;\n\nString s = 5;\nint c = a;', 'void f(int x) {\n    if (x > 1) throw new IllegalStateException("boom");\n}\n\nf(5);');
      const compile = await runCell(notebook, 0);
      const runtime = await runCell(notebook, 1);

      assert.equal(compile.executionSummary?.success, false);
      const marks = vscode.languages.getDiagnostics(compile.document.uri).filter((d) => d.source === 'FrySharp');
      assert.equal(marks.length, 1);
      assert.equal(marks[0].range.start.line, 2);
      assert.equal(marks[0].range.start.character, 11);
      assert.match(marks[0].message, /incompatible types/);

      assert.equal(runtime.executionSummary?.success, false);
      const thrown = vscode.languages.getDiagnostics(runtime.document.uri).filter((d) => d.source === 'FrySharp');
      assert.equal(thrown.length, 1);
      assert.equal(thrown[0].code, 'IllegalStateException');
      assert.equal(thrown[0].range.start.line, 1, 'on the line inside f where it was thrown');
    }
  ],
  [
    'a Java cell can take a list a C# cell made, with #!share, and its own values go the other way',
    async () => {
      needs('javac', 'FRY_REQUIRE_JAVA');
      const notebook = await notebookWith(
        ['csharp', 'var rows = new[] { new { Name = "Ada", Age = 36 }, new { Name = "Linus", Age = 55 } };'],
        ['java', '#!share --from csharp rows\nSystem.out.println(rows.size() + " rows");'],
        ['java', 'int[] squares = {1, 4, 9, 16};'],
        ['csharp', '#!share --from java squares\nConsole.WriteLine(string.Join(",", squares));']
      );
      await runCell(notebook, 0);
      const inJava = await runCell(notebook, 1);
      await runCell(notebook, 2);
      const back = await runCell(notebook, 3);

      assert.equal(inJava.executionSummary?.success, true, textOf(inJava));
      assert.match(textOf(inJava), /2 rows/);
      assert.equal(back.executionSummary?.success, true, textOf(back));
      assert.match(textOf(back), /1,4,9,16/);
    }
  ],
  [
    'a Java cell that reads a line asks in an input box; closing it is the end of input, and the cell goes on',
    async () => {
      needs('javac', 'FRY_REQUIRE_JAVA');
      const notebook = await notebookIn(
        'java',
        'var reader = new java.io.BufferedReader(new java.io.InputStreamReader(System.in));\nString line = reader.readLine();\nSystem.out.println(line == null ? "end of input" : "got " + line);'
      );
      await vscode.window.showNotebookDocument(notebook);
      await vscode.commands.executeCommand('notebook.selectKernel', { id: 'frysharp-kernel-frysharp-notebook', extension: EXTENSION });
      const cell = notebook.cellAt(0);
      startRun(notebook, 0, 1);

      await new Promise((resolve) => setTimeout(resolve, 9000)); // the box is open by now (the JVM starts first): the cell has not finished
      assert.equal(cell.executionSummary?.success, undefined, 'the cell is waiting for its answer');
      await vscode.commands.executeCommand('workbench.action.closeQuickOpen');
      await waitFor('the cell to finish', () => cell.executionSummary?.success !== undefined, 90_000);

      assert.equal(cell.executionSummary?.success, true, textOf(cell));
      assert.match(textOf(cell), /end of input/);
    }
  ],
  [
    'Stop ends a Java cell that only computes, and what the notebook declared is still there',
    async () => {
      needs('javac', 'FRY_REQUIRE_JAVA');
      const notebook = await notebookIn('java', 'int kept = 41;', 'long i = 0;\nwhile (true) { i++; }', 'kept + 1');
      await runCell(notebook, 0);
      const running = notebook.cellAt(1);
      startRun(notebook, 1, 2);
      await new Promise((resolve) => setTimeout(resolve, 2500)); // long enough for it to be in its loop
      await vscode.commands.executeCommand('notebook.cell.cancelExecution', { ranges: [{ start: 1, end: 2 }], document: notebook.uri });
      await waitFor('the cell to stop', () => running.executionSummary?.success !== undefined, 15_000);

      const after = await runCell(notebook, 2);
      assert.equal(after.executionSummary?.success, true, textOf(after));
      assert.match(textOf(after), /42/);
    }
  ],
  [
    'a .java file that reads a line (Scanner) gets what is typed in the terminal',
    async () => {
      needs('javac', 'FRY_REQUIRE_JAVA');
      const { scripts } = (await vscode.extensions.getExtension(EXTENSION)!.activate()) as FrySharpApi;
      const file = vscode.Uri.file(path.join(workspace, 'Greet.java'));
      fs.writeFileSync(file.fsPath, 'import java.util.Scanner;\n\npublic class Greet {\n    public static void main(String[] args) {\n        System.out.print("Name? ");\n        String name = new Scanner(System.in).nextLine();\n        System.out.println("Hello, " + name);\n    }\n}\n');

      await scripts.run(file);
      const run = scripts.terminalFor(file)!;
      await waitFor('the prompt', () => run.transcript.includes('Name? '), 120_000);
      run.handleInput('Ada');
      run.handleInput('\r');
      await waitFor('the run to end', () => /exited with code/.test(run.transcript), 30_000);

      assert.match(run.transcript, /Hello, Ada/);
      assert.match(run.transcript, /exited with code 0/);
    }
  ],
  [
    'Run with FrySharp runs a .java file in the terminal, and its compile error is a problem at its line',
    async () => {
      needs('javac', 'FRY_REQUIRE_JAVA');
      const { scripts } = (await vscode.extensions.getExtension(EXTENSION)!.activate()) as FrySharpApi;
      const file = vscode.Uri.file(path.join(workspace, 'Report.java'));
      fs.writeFileSync(file.fsPath, 'public class Report {\n    public static void main(String[] args) {\n        int total = "x";\n        System.out.println(total);\n    }\n}\n');

      await scripts.run(file);
      const run = scripts.terminalFor(file)!;
      await waitFor('the run to end', () => /exited with code/.test(run.transcript), 90_000);

      assert.match(run.transcript, /exited with code 1/);
      await waitFor('the problem', () => vscode.languages.getDiagnostics(file).some((d) => d.range.start.line === 2), 10_000);
    }
  ]
];
