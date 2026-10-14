import assert from 'node:assert/strict';
import * as fs from 'node:fs';
import * as path from 'node:path';
import * as vscode from 'vscode';
import type { FrySharpApi } from '../../../src/extension';
import { notebookIn, notebookWith, runCell, waitFor } from './notebooks';
import { itemsOf as items, textOfCell as textOf } from './outputs';
import { needs } from './skip';

// F# cells and files, run by the real `dotnet fsi` through the real engine, in a real VS Code.

const workspace = process.env.FRY_IT_WORKSPACE as string;
const EXTENSION = 'CodeFryDev.frysharp';
const TABLE = 'application/vnd.fry.table+json';
const INSPECTOR = 'application/vnd.fry.inspector+json';

export const fsharpTests: Array<[string, () => Promise<void>]> = [
  [
    'an F# cell keeps what it defines for the next one, shows records as a table, and the values are the notebook\'s variables',
    async () => {
      needs('dotnet', 'FRY_REQUIRE_FSHARP');
      const api = (await vscode.extensions.getExtension(EXTENSION)!.activate()) as FrySharpApi;
      const notebook = await notebookIn(
        'fsharp',
        'type Person = { Name: string; Age: int }\nlet people = [ { Name = "Ada"; Age = 36 }; { Name = "Linus"; Age = 55 } ]',
        'people |> List.filter (fun p -> p.Age > 40)',
        'people.Head'
      );
      await runCell(notebook, 0);
      const filtered = await runCell(notebook, 1);
      const head = await runCell(notebook, 2);

      assert.equal(filtered.executionSummary?.success, true, textOf(filtered));
      const [table] = items(filtered, TABLE) as Array<{ columns: string[]; rows: string[][] }>;
      assert.deepStrictEqual(table.columns, ['Name', 'Age']);
      assert.deepStrictEqual(table.rows, [['Linus', '55']]);
      const [tree] = items(head, INSPECTOR) as Array<{ title: string; properties: Array<{ name: string; value: string }> }>;
      assert.equal(tree.title, 'Person');
      assert.deepStrictEqual(tree.properties.map((p) => [p.name, p.value]), [['Name', 'Ada'], ['Age', '36']]);

      const kernels = await api.variables.getChildren();
      const fsharp = kernels.find((node) => /F#/.test(String(api.variables.getTreeItem(node).label)));
      assert.ok(fsharp, 'the F# kernel is listed');
      const names = (await api.variables.getChildren(fsharp)).map((node) => api.variables.getTreeItem(node).label);
      assert.deepStrictEqual(names, ['people']);
    }
  ],
  [
    'an F# compile error is underlined where the compiler found it, on the line and columns of the cell',
    async () => {
      needs('dotnet', 'FRY_REQUIRE_FSHARP');
      const notebook = await notebookIn('fsharp', 'let a = 1\n\nlet b = a + nope\nb');
      const cell = await runCell(notebook, 0);

      assert.equal(cell.executionSummary?.success, false);
      const marks = vscode.languages.getDiagnostics(cell.document.uri).filter((d) => d.source === 'FrySharp');
      assert.equal(marks.length, 1);
      assert.equal(marks[0].code, 'FS0039');
      assert.equal(marks[0].range.start.line, 2);
      assert.equal(marks[0].range.start.character, 12);
      assert.equal(marks[0].range.end.character, 16);
      assert.match(marks[0].message, /nope/);
    }
  ],
  [
    'an F# cell can take a list a C# cell made, with #!share, and its own values go the other way',
    async () => {
      needs('dotnet', 'FRY_REQUIRE_FSHARP');
      const notebook = await notebookWith(
        ['csharp', 'var rows = new[] { new { Name = "Ada", Age = 36 }, new { Name = "Linus", Age = 55 } };'],
        ['fsharp', '#!share --from csharp rows\nrows |> Array.map (fun r -> r.["Name"] :?> string) |> String.concat ", "'],
        ['fsharp', 'let squares = [ for i in 1 .. 4 -> i * i ]'],
        ['csharp', '#!share --from fsharp squares\nConsole.WriteLine(string.Join(",", squares));']
      );
      await runCell(notebook, 0);
      const inFsharp = await runCell(notebook, 1);
      await runCell(notebook, 2);
      const back = await runCell(notebook, 3);

      assert.equal(inFsharp.executionSummary?.success, true, textOf(inFsharp));
      assert.match(textOf(inFsharp), /Ada, Linus/);
      assert.equal(back.executionSummary?.success, true, textOf(back));
      assert.match(textOf(back), /1,4,9,16/);
    }
  ],
  [
    'Run with FrySharp runs an .fsx file in the terminal, and its error is a problem at its line',
    async () => {
      needs('dotnet', 'FRY_REQUIRE_FSHARP');
      const { scripts } = (await vscode.extensions.getExtension(EXTENSION)!.activate()) as FrySharpApi;
      const file = vscode.Uri.file(path.join(workspace, 'report.fsx'));
      fs.writeFileSync(file.fsPath, 'printfn "total: %d" (List.sum [ 1; 2; 3 ])\nlet bad = 1 + "x"\n');

      await scripts.run(file);
      const run = scripts.terminalFor(file)!;
      await waitFor('the run to end', () => /exited with code/.test(run.transcript), 90_000);

      assert.match(run.transcript, /exited with code 1/);
      await waitFor('the problem', () => vscode.languages.getDiagnostics(file).some((d) => /FS0001|type/i.test(String(d.code) + d.message)), 10_000);
      const problem = vscode.languages.getDiagnostics(file).find((d) => d.range.start.line === 1);
      assert.ok(problem, 'a problem on line 2');
    }
  ]
];
