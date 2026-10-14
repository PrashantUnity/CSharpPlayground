import assert from 'node:assert/strict';
import * as fs from 'node:fs';
import * as path from 'node:path';
import * as vscode from 'vscode';
import type { FrySharpApi } from '../../../src/extension';
import { notebookIn, notebookWith, runCell, waitFor } from './notebooks';
import { itemsOf, textOfCell as textOf } from './outputs';
import { needs } from './skip';

// SQL cells and files, run by the real sqlite3 through the real engine, in a real VS Code.

const workspace = process.env.FRY_IT_WORKSPACE as string;
const EXTENSION = 'CodeFryDev.frysharp';
const TABLE = 'application/vnd.fry.table+json';

const tablesOf = (cell: vscode.NotebookCell) => itemsOf(cell, TABLE) as Array<{ columns: string[]; numeric: boolean[]; rows: string[][] }>;

export const sqlTests: Array<[string, () => Promise<void>]> = [
  [
    'a SQL cell shows its rows as a table (not as text), and the notebook\'s tables are its variables',
    async () => {
      needs('sqlite3', 'FRY_REQUIRE_SQL');
      const api = (await vscode.extensions.getExtension(EXTENSION)!.activate()) as FrySharpApi;
      const notebook = await notebookIn(
        'sql',
        "create table people (id integer primary key, name text, age integer);\ninsert into people (name, age) values ('Ada', 36), ('Linus', 55);\nselect id, name, age from people order by id;"
      );
      const cell = await runCell(notebook, 0);

      assert.equal(cell.executionSummary?.success, true, textOf(cell));
      const [table, ...more] = tablesOf(cell);
      assert.deepStrictEqual(more, []);
      assert.deepStrictEqual(table.columns, ['id', 'name', 'age']);
      assert.deepStrictEqual(table.numeric, [true, false, true], 'numbers are right-aligned and sort as numbers');
      assert.deepStrictEqual(table.rows, [['1', 'Ada', '36'], ['2', 'Linus', '55']]);
      assert.equal(textOf(cell), '', 'no text table next to the real one');

      const kernels = await api.variables.getChildren();
      assert.ok(kernels.some((node) => api.variables.getTreeItem(node).label === 'SQL'), 'the SQL kernel is listed');
      const sql = kernels.find((node) => api.variables.getTreeItem(node).label === 'SQL')!;
      const tables = (await api.variables.getChildren(sql)).map((node) => api.variables.getTreeItem(node).label);
      assert.deepStrictEqual(tables, ['people']);
    }
  ],
  [
    'a SQL error is marked on the line of the cell where it is, and says what is wrong',
    async () => {
      needs('sqlite3', 'FRY_REQUIRE_SQL');
      const notebook = await notebookIn('sql', 'select 1 as fine;\nselect * from nosuch;');
      const cell = await runCell(notebook, 0);

      assert.equal(cell.executionSummary?.success, false);
      const marks = vscode.languages.getDiagnostics(cell.document.uri).filter((d) => d.source === 'FrySharp');
      assert.equal(marks.length, 1);
      assert.equal(marks[0].range.start.line, 1);
      assert.match(marks[0].message, /no such table/);
      assert.equal(tablesOf(cell).length, 1, 'the statement before the error still shows its rows');
    }
  ],
  [
    'a SQL cell can query a list a C# cell made, with #!share',
    async () => {
      needs('sqlite3', 'FRY_REQUIRE_SQL');
      const notebook = await notebookWith(
        ['csharp', 'var rows = new[] { new { Name = "Ada", Age = 36 }, new { Name = "Linus", Age = 55 } };'],
        ['sql', '#!share --from csharp rows\nselect Name from rows where Age > 40;']
      );
      await runCell(notebook, 0);
      const cell = await runCell(notebook, 1);

      assert.equal(cell.executionSummary?.success, true, textOf(cell));
      assert.deepStrictEqual(tablesOf(cell).map((t) => t.rows), [[['Linus']]]);
    }
  ],
  [
    'Run with FrySharp runs a .sql file in the terminal, and its error is a link and a problem',
    async () => {
      needs('sqlite3', 'FRY_REQUIRE_SQL');
      const { scripts } = (await vscode.extensions.getExtension(EXTENSION)!.activate()) as FrySharpApi;
      const file = vscode.Uri.file(path.join(workspace, 'report.sql'));
      fs.writeFileSync(file.fsPath, "create table t (name text);\ninsert into t values ('Ada');\nselect name from t;\nselect * from nosuch;\n");

      await scripts.run(file);
      const run = scripts.terminalFor(file)!;
      await waitFor('the run to end', () => /exited with code/.test(run.transcript));

      assert.match(run.transcript, /\| Ada\s+\|/, 'the rows, as sqlite3 shows them');
      assert.match(run.transcript, /no such table/);
      assert.match(run.transcript, /exited with code 1/);
      await waitFor('the problem', () => vscode.languages.getDiagnostics(file).some((d) => /no such table/.test(d.message)), 10_000);
      const problem = vscode.languages.getDiagnostics(file).find((d) => /no such table/.test(d.message))!;
      assert.equal(problem.range.start.line, 3);
    }
  ]
];
