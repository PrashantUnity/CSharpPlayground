import assert from 'node:assert/strict';
import * as fs from 'node:fs';
import * as path from 'node:path';
import * as vscode from 'vscode';
import type { FrySharpApi } from '../../../src/extension';
import { isTabbed, outputsOf } from './outputs';

// These run inside a real VS Code, with the real engine (the sidecar), on copies of the fixtures in the workspace.

const workspace = process.env.FRY_IT_WORKSPACE as string;
const EXTENSION = 'CodeFryDev.frysharp';
const TABLE = 'application/vnd.fry.table+json';
const decode = (data: Uint8Array) => new TextDecoder().decode(data);

export async function waitFor(what: string, condition: () => boolean, ms = 60_000): Promise<void> {
  const until = Date.now() + ms;
  while (!condition()) {
    if (Date.now() > until) throw new Error(`timed out waiting for ${what}`);
    await new Promise((resolve) => setTimeout(resolve, 50));
  }
}

async function open(name: string): Promise<vscode.NotebookDocument> {
  return vscode.workspace.openNotebookDocument(vscode.Uri.file(path.join(workspace, name)));
}

// A change that makes the notebook dirty and means nothing, so that a save writes it.
async function touch(notebook: vscode.NotebookDocument): Promise<void> {
  const edit = new vscode.WorkspaceEdit();
  const cell = notebook.cellAt(0);
  edit.set(notebook.uri, [vscode.NotebookEdit.updateCellMetadata(0, { ...cell.metadata, touched: true })]);
  assert.ok(await vscode.workspace.applyEdit(edit));
  assert.equal(notebook.isDirty, true);
}

async function save(notebook: vscode.NotebookDocument): Promise<void> {
  assert.ok(await notebook.save());
  assert.equal(notebook.isDirty, false);
}

export async function runCell(notebook: vscode.NotebookDocument, index: number): Promise<vscode.NotebookCell> {
  await vscode.window.showNotebookDocument(notebook);
  await vscode.commands.executeCommand('notebook.selectKernel', { id: `frysharp-kernel-${notebook.notebookType}`, extension: EXTENSION });
  const cell = notebook.cellAt(index);
  await vscode.commands.executeCommand('notebook.cell.execute', { ranges: [{ start: index, end: index + 1 }], document: notebook.uri });
  await waitFor(`cell ${index} to finish`, () => cell.executionSummary?.success !== undefined);
  return cell;
}

// A new notebook of C# cells, shown, with the FrySharp kernel selected.
async function notebookOf(...codes: string[]): Promise<vscode.NotebookDocument> {
  return notebookIn('csharp', ...codes);
}

export async function notebookIn(language: string, ...codes: string[]): Promise<vscode.NotebookDocument> {
  return notebookWith(...codes.map((code): [string, string] => [language, code]));
}

// A new notebook of cells in their own languages, shown, with the FrySharp kernel selected.
export async function notebookWith(...contents: Array<[language: string, code: string]>): Promise<vscode.NotebookDocument> {
  const cells = contents.map(([language, code]) => new vscode.NotebookCellData(vscode.NotebookCellKind.Code, code, language));
  const notebook = await vscode.workspace.openNotebookDocument('frysharp-notebook', new vscode.NotebookData(cells));
  await vscode.window.showNotebookDocument(notebook);
  await vscode.commands.executeCommand('notebook.selectKernel', { id: 'frysharp-kernel-frysharp-notebook', extension: EXTENSION });
  return notebook;
}

// Starts the cells running and returns at once: the command itself only finishes when the cells have, which is no use to
// a test that wants to see them while they run (or to stop them).
export function startRun(notebook: vscode.NotebookDocument, start: number, end: number): void {
  void Promise.resolve(vscode.commands.executeCommand('notebook.cell.execute', { ranges: [{ start, end }], document: notebook.uri })).catch(() => undefined);
}

const textOf = (cell: vscode.NotebookCell): string => outputsOf(cell).flatMap((output) => output.items).filter((item) => item.mime.startsWith('application/vnd.code.notebook.std') || item.mime === 'text/plain').map((item) => decode(item.data)).join('');

const withoutStamp = (json: string): unknown => {
  const parsed = JSON.parse(json);
  delete parsed.LastModified;
  return parsed;
};

export const tests: Array<[string, () => Promise<void>]> = [
  [
    'the extension is there, starts, and offers its commands',
    async () => {
      const extension = vscode.extensions.getExtension(EXTENSION);
      assert.ok(extension, `${EXTENSION} is not loaded`);
      await extension.activate();
      const commands = await vscode.commands.getCommands(true);
      for (const command of ['frysharp.newNotebook', 'frysharp.runScript', 'frysharp.restartNotebook', 'frysharp.showLog']) assert.ok(commands.includes(command), command);
    }
  ],

  [
    'a notebook the desktop wrote opens with each of its outputs',
    async () => {
      const notebook = await open('desktop.frynb');
      assert.equal(notebook.notebookType, 'frysharp-notebook');
      assert.equal(notebook.cellCount, 8);

      const byId = (id: string) => notebook.getCells().find((cell) => cell.metadata.id === id) as vscode.NotebookCell;
      // text and a table are two kinds of output: one output, drawn as tabs, that holds both
      assert.equal(isTabbed(byId('table')), true);
      assert.deepStrictEqual(outputsOf(byId('table')).map((output) => output.items.map((item) => item.mime)), [['application/vnd.code.notebook.stdout'], ['text/html', TABLE, 'text/plain']]);
      assert.equal(isTabbed(byId('text')), false);
      assert.equal(decode(byId('text').outputs[0].items[0].data), 'hi\n');
      assert.equal(byId('collapsed').document.languageId, 'python');
      assert.equal(byId('intro').kind, vscode.NotebookCellKind.Markup);
      assert.equal(byId('failed').executionSummary?.success, false);

      const table = JSON.parse(decode(outputsOf(byId('table'))[1].items[1].data));
      assert.deepStrictEqual(table.columns, ['Name', 'Age', 'Active']);
      assert.deepStrictEqual(table.rows[0], ['Ada', '36', 'True']);
    }
  ],

  [
    'saving it from VS Code writes the same notebook (nothing the desktop kept is lost)',
    async () => {
      const file = path.join(workspace, 'desktop.frynb');
      const before = fs.readFileSync(file, 'utf8');
      const notebook = await open('desktop.frynb');

      await touch(notebook);
      await save(notebook);

      assert.deepStrictEqual(withoutStamp(fs.readFileSync(file, 'utf8')), withoutStamp(before));
    }
  ],

  [
    'a Jupyter notebook opened with FrySharp is saved back byte for byte',
    async () => {
      const file = path.join(workspace, 'jupyter.ipynb');
      const before = fs.readFileSync(file, 'utf8');
      await vscode.commands.executeCommand('vscode.openWith', vscode.Uri.file(file), 'frysharp-jupyter');
      await waitFor('the notebook to open', () => vscode.workspace.notebookDocuments.some((n) => n.uri.fsPath === file && n.notebookType === 'frysharp-jupyter'));
      const notebook = vscode.workspace.notebookDocuments.find((n) => n.uri.fsPath === file) as vscode.NotebookDocument;

      assert.equal(notebook.cellCount, 5);
      await touch(notebook);
      await save(notebook);

      assert.equal(fs.readFileSync(file, 'utf8'), before);
    }
  ],

  [
    'a file that is not a notebook is refused, not opened empty for a save to write over it',
    async () => {
      const file = path.join(workspace, 'broken.frynb');
      await assert.rejects(async () => vscode.workspace.openNotebookDocument(vscode.Uri.file(file)));
      assert.equal(fs.readFileSync(file, 'utf8'), '{ this is not json');
    }
  ],

  [
    'an untitled notebook runs C# cells, and what one defines the next sees',
    async () => {
      await vscode.commands.executeCommand('frysharp.newNotebook');
      const notebook = vscode.window.activeNotebookEditor?.notebook as vscode.NotebookDocument;
      assert.equal(notebook.uri.scheme, 'untitled');

      const first = await runCell(notebook, 1);
      assert.equal(first.executionSummary?.success, true);
      assert.match(textOf(first), /Hello from FrySharp! a = 42/);

      const edit = new vscode.WorkspaceEdit();
      edit.set(notebook.uri, [vscode.NotebookEdit.insertCells(2, [new vscode.NotebookCellData(vscode.NotebookCellKind.Code, 'Console.WriteLine(a + 1);', 'csharp')])]);
      await vscode.workspace.applyEdit(edit);
      const second = await runCell(notebook, 2);

      assert.equal(second.executionSummary?.success, true);
      assert.match(textOf(second), /43/);
    }
  ],

  [
    'a run that shows a table is saved so the desktop can read it, and opens again with its table',
    async () => {
      const file = path.join(workspace, 'run.frynb');
      const notebook = await open('run.frynb');
      const edit = new vscode.WorkspaceEdit();
      edit.set(notebook.uri, [vscode.NotebookEdit.insertCells(0, [new vscode.NotebookCellData(vscode.NotebookCellKind.Code, 'Console.WriteLine("before");\nDisplay.Table(new[] { new { Name = "Ada", Age = 36 }, new { Name = "Grace", Age = 45 } }, "People");', 'csharp')])]);
      await vscode.workspace.applyEdit(edit);

      const cell = await runCell(notebook, 0);
      assert.equal(cell.executionSummary?.success, true, textOf(cell));
      assert.ok(outputsOf(cell).some((output) => output.items.some((item) => item.mime === TABLE)), 'a table output');
      await save(notebook);

      const saved = JSON.parse(fs.readFileSync(file, 'utf8')).Cells[0];
      assert.equal(saved.OutputText, 'before\n');
      assert.ok(saved.TableSnapshot.Title, 'the table has a title');
      assert.deepStrictEqual(saved.TableSnapshot.Columns.map((c: { Header: string }) => c.Header), ['Name', 'Age']);
      assert.equal(saved.TableSnapshot.Rows[1].Cells[0].DisplayText, 'Grace');
      assert.ok(saved.HtmlContent.includes('fry-tbl'));
      assert.equal(saved.ExecutionCount, 1);
      assert.match(saved.ExecutionTimeText, /ms$/);

      await vscode.commands.executeCommand('workbench.action.closeAllEditors');
      const reopened = await vscode.workspace.openNotebookDocument(vscode.Uri.file(file));
      assert.ok(outputsOf(reopened.cellAt(0)).some((output) => output.items.some((item) => item.mime === TABLE)));
    }
  ]
  ,
  [
    'a cell that prints 20,000 lines has one output with all of them',
    async () => {
      const notebook = await notebookOf('for (var i = 0; i < 20000; i++) Console.WriteLine("line " + i);');
      const cell = await runCell(notebook, 0);

      assert.equal(cell.executionSummary?.success, true);
      assert.equal(cell.outputs.length, 1, `${cell.outputs.length} outputs`);
      const text = textOf(cell);
      assert.ok(text.includes('line 0\n'));
      assert.ok(text.trimEnd().endsWith('line 19999'), text.slice(-40));
      assert.equal(text.split('\n').filter(Boolean).length, 20000);
    }
  ],

  [
    'text a cell prints, a table, and text again, are three outputs in that order (in the one output drawn as tabs)',
    async () => {
      const notebook = await notebookOf('Console.WriteLine("one");\nDisplay.Table(new[] { new { A = 1 } });\nConsole.WriteLine("two");');
      const cell = await runCell(notebook, 0);

      assert.equal(cell.executionSummary?.success, true, textOf(cell));
      assert.equal(isTabbed(cell), true);
      assert.equal(cell.outputs.length, 1);
      const kinds = outputsOf(cell).map((output) => (output.items.some((item) => item.mime === TABLE) ? 'table' : decode(output.items[0].data).trim()));
      assert.deepStrictEqual(kinds, ['one', 'table', 'two']);
    }
  ],

  [
    'a cell with text and a table is drawn as tabs: Table with its rows, Console with its lines, and All',
    async () => {
      const api = (await vscode.extensions.getExtension(EXTENSION)!.activate()) as FrySharpApi;
      const notebook = await notebookOf('Console.WriteLine("one");\nConsole.WriteLine("two");\nDisplay.Table(new[] { new { A = 1 }, new { A = 2 }, new { A = 3 } }, "Numbers");');
      const before = api.rendererReports.length;
      const cell = await runCell(notebook, 0);

      assert.equal(cell.executionSummary?.success, true, textOf(cell));
      await waitFor('the tabs to be drawn', () => api.rendererReports.slice(before).some((r) => r.mime === 'application/vnd.fry.cell-output+json' && r.type === 'rendered'), 30_000);
      const report = api.rendererReports.slice(before).filter((r) => r.mime === 'application/vnd.fry.cell-output+json').pop()!;
      assert.deepStrictEqual(report.tabs, ['Table', 'Console', 'All']);
      assert.equal(report.selected, 'Table');
      assert.deepStrictEqual(api.rendererReports.slice(before).filter((r) => r.type === 'error'), []);
    }
  ],

  [
    'a cell that makes a second kind of output while it runs becomes tabs, and the text printed after is in them',
    async () => {
      const notebook = await notebookOf('Console.WriteLine("start");\nDisplay.Table(new[] { new { A = 1 } });\nSystem.Threading.Thread.Sleep(700);\nConsole.WriteLine("after");');
      const cell = await runCell(notebook, 0);

      assert.equal(cell.executionSummary?.success, true, textOf(cell));
      assert.equal(isTabbed(cell), true);
      assert.equal(cell.outputs.length, 1);
      const kinds = outputsOf(cell).map((output) => (output.items.some((item) => item.mime === TABLE) ? 'table' : decode(output.items[0].data).trim()));
      assert.deepStrictEqual(kinds, ['start', 'table', 'after']);
    }
  ],

  [
    'with the tabs off, the same cell has its outputs one after another, as before',
    async () => {
      const settings = vscode.workspace.getConfiguration('frysharp.notebook');
      await settings.update('outputTabs', false, vscode.ConfigurationTarget.Global);
      try {
        const notebook = await notebookOf('Console.WriteLine("one");\nDisplay.Table(new[] { new { A = 1 } });\nConsole.WriteLine("two");');
        const cell = await runCell(notebook, 0);

        assert.equal(cell.executionSummary?.success, true, textOf(cell));
        assert.equal(isTabbed(cell), false);
        assert.equal(cell.outputs.length, 3, `${cell.outputs.length} outputs`);
      } finally {
        await settings.update('outputTabs', undefined, vscode.ConfigurationTarget.Global);
      }
    }
  ],

  [
    'a cell that fails shows why as VS Code shows an error, after what it printed, and the file does not keep that line',
    async () => {
      const file = path.join(workspace, 'failed.frynb');
      fs.copyFileSync(path.join(workspace, 'run.frynb'), file);
      const notebook = await open('failed.frynb');
      const edit = new vscode.WorkspaceEdit();
      edit.set(notebook.uri, [vscode.NotebookEdit.insertCells(0, [new vscode.NotebookCellData(vscode.NotebookCellKind.Code, 'Console.WriteLine("before");\nthrow new InvalidOperationException("boom");', 'csharp')])]);
      await vscode.workspace.applyEdit(edit);

      const cell = await runCell(notebook, 0);

      assert.equal(cell.executionSummary?.success, false);
      const last = cell.outputs[cell.outputs.length - 1];
      assert.equal(last.items[0].mime, 'application/vnd.code.notebook.error');
      const error = JSON.parse(decode(last.items[0].data));
      assert.equal(error.name, 'InvalidOperationException');
      assert.match(error.message, /boom/);
      assert.match(textOf(cell), /before/);
      assert.equal(isTabbed(cell), false); // printed text and an error: one kind of output and the error

      await save(notebook);
      const saved = JSON.parse(fs.readFileSync(file, 'utf8')).Cells[0];
      assert.equal(saved.HasError, true);
      assert.ok(!JSON.stringify(saved).includes('fryEngineError'), 'the error line is not in the file');
      assert.equal((saved.OutputText.match(/boom/g) ?? []).length, 1, `the message is in the cell's text once: ${saved.OutputText}`);
    }
  ],

  [
    'a cell with text and a table that then fails is tabs followed by the error',
    async () => {
      const notebook = await notebookOf('Console.WriteLine("one");\nDisplay.Table(new[] { new { A = 1 } });\nthrow new InvalidOperationException("late");');
      const cell = await runCell(notebook, 0);

      assert.equal(cell.executionSummary?.success, false);
      assert.equal(cell.outputs.length, 2, `${cell.outputs.length} outputs`);
      assert.equal(cell.outputs[0].items[0].mime, 'application/vnd.fry.cell-output+json');
      assert.equal(cell.outputs[1].items[0].mime, 'application/vnd.code.notebook.error');
    }
  ],

  [
    'a cell that is stopped is not shown as an error',
    async () => {
      const notebook = await notebookIn('python', 'import time\nprint("going", flush=True)\ntime.sleep(60)');
      const cell = notebook.cellAt(0);
      startRun(notebook, 0, 1);
      await waitFor('the cell to print', () => textOf(cell).includes('going'), 60_000);
      await vscode.commands.executeCommand('notebook.cell.cancelExecution', { ranges: [{ start: 0, end: 1 }], document: notebook.uri });
      await waitFor('the cell to stop', () => cell.executionSummary?.success !== undefined, 30_000);

      assert.ok(!cell.outputs.some((output) => output.items.some((item) => item.mime === 'application/vnd.code.notebook.error')), 'no error output for Stop');
    }
  ],

  [
    'FrySharp: Show Engine Info opens a page with the engine, its runtime and the tool each language found',
    async () => {
      await vscode.commands.executeCommand('frysharp.showEngineInfo');
      await waitFor('the page', () => vscode.workspace.textDocuments.some((d) => d.getText().startsWith('# FrySharp engine info')), 60_000);

      const page = vscode.workspace.textDocuments.find((d) => d.getText().startsWith('# FrySharp engine info'))!.getText();
      assert.match(page, /\| Engine \| \d+\.\d+\.\d+ \(protocol 1\)/);
      assert.match(page, /\| Runtime \| \.NET /);
      assert.match(page, /\| C# \| ✓ found/);
      assert.match(page, /\| (Python|Java) \| /);
      assert.match(page, /## Languages/);
      await vscode.commands.executeCommand('workbench.action.closeAllEditors');
    }
  ],

  [
    'a notebook saved with tabs is the same file as one saved without: the file holds the outputs, not the tabs',
    async () => {
      const file = path.join(workspace, 'tabs.frynb');
      const notebook = await open('run.frynb');
      fs.copyFileSync(path.join(workspace, 'run.frynb'), file);
      const edit = new vscode.WorkspaceEdit();
      edit.set(notebook.uri, [vscode.NotebookEdit.insertCells(0, [new vscode.NotebookCellData(vscode.NotebookCellKind.Code, 'Console.WriteLine("before");\nDisplay.Table(new[] { new { Name = "Ada" } }, "People");', 'csharp')])]);
      await vscode.workspace.applyEdit(edit);
      const cell = await runCell(notebook, 0);
      assert.equal(isTabbed(cell), true);
      await save(notebook);

      const saved = JSON.parse(fs.readFileSync(path.join(workspace, 'run.frynb'), 'utf8')).Cells[0];
      assert.equal(saved.OutputText, 'before\n');
      assert.equal(saved.TableSnapshot.Columns[0].Header, 'Name');
      assert.equal(JSON.stringify(saved).includes('vnd.fry.cell-output'), false, 'the tabs are not written to the file');
    }
  ],

  [
    'a cell that reads a line asks in an input box; closing it is the end of input, and the cell goes on',
    async () => {
      const notebook = await notebookOf('var line = Console.ReadLine();\nConsole.WriteLine(line == null ? "end of input" : "got " + line);');
      await vscode.window.showNotebookDocument(notebook);
      await vscode.commands.executeCommand('notebook.selectKernel', { id: 'frysharp-kernel-frysharp-notebook', extension: EXTENSION });
      const cell = notebook.cellAt(0);
      startRun(notebook, 0, 1);

      await new Promise((resolve) => setTimeout(resolve, 3000)); // the box is open by now: the cell has not finished
      assert.equal(cell.executionSummary?.success, undefined, 'the cell is waiting for its answer');
      await vscode.commands.executeCommand('workbench.action.closeQuickOpen');
      await waitFor('the cell to finish', () => cell.executionSummary?.success !== undefined);

      assert.equal(cell.executionSummary?.success, true);
      assert.match(textOf(cell), /end of input/);
    }
  ],

  [
    'a compile error is marked in the cell, and the mark goes when the code changes',
    async () => {
      const notebook = await notebookOf('int ok = 1;\nint bad = ;');
      const cell = await runCell(notebook, 0);

      assert.equal(cell.executionSummary?.success, false);
      const marks = vscode.languages.getDiagnostics(cell.document.uri);
      assert.equal(marks.length, 1);
      assert.equal(marks[0].range.start.line, 1);
      assert.equal(marks[0].source, 'FrySharp');
      assert.match(String(marks[0].code), /^CS/);
      assert.match(textOf(cell), /CS\d+/, 'the error is also in the cell\'s output');

      const edit = new vscode.WorkspaceEdit();
      edit.insert(cell.document.uri, new vscode.Position(1, 0), '// fixed\n');
      await vscode.workspace.applyEdit(edit);
      await waitFor('the mark to go', () => vscode.languages.getDiagnostics(cell.document.uri).length === 0, 5000);
    }
  ],

  [
    'Run All stops at the cell that fails, and the cells after it are not run',
    async () => {
      const notebook = await notebookOf('Console.WriteLine("first");', 'throw new System.Exception("stop here");', 'Console.WriteLine("never");');
      startRun(notebook, 0, 3);
      await waitFor('the failing cell', () => notebook.cellAt(1).executionSummary?.success === false);
      await new Promise((resolve) => setTimeout(resolve, 1500));

      assert.equal(notebook.cellAt(0).executionSummary?.success, true);
      assert.equal(notebook.cellAt(2).executionSummary?.success, undefined, 'the third cell was not run');
      assert.deepStrictEqual(notebook.cellAt(2).outputs, []);
    }
  ],

  [
    'Stop ends a long Python cell at once, and the notebook runs on with the variables it had',
    async () => {
      // (a C# cell blocked in a call cannot be interrupted: the engine gives up on it after 3 s and the kernel stays busy until it returns)
      const notebook = await notebookIn('python', 'kept = 5', 'import time\ntime.sleep(60)', 'print(kept + 1)');
      await runCell(notebook, 0);

      const long = notebook.cellAt(1);
      startRun(notebook, 1, 2);
      await new Promise((resolve) => setTimeout(resolve, 2500));
      assert.equal(long.executionSummary?.success, undefined, 'the long cell is running');

      const stopped = Date.now();
      await vscode.commands.executeCommand('notebook.cell.cancelExecution', { ranges: [{ start: 1, end: 2 }], document: notebook.uri });
      await waitFor('the long cell to end', () => long.executionSummary?.success !== undefined, 20_000);
      assert.ok(Date.now() - stopped < 8_000, `it ended ${Date.now() - stopped} ms after Stop`);
      assert.equal(long.executionSummary?.success, false);

      const next = await runCell(notebook, 2);
      assert.equal(next.executionSummary?.success, true, textOf(next));
      assert.match(textOf(next), /6/);
    }
  ],

  [
    'a chart the program redraws is one output, redrawn',
    async () => {
      const notebook = await notebookOf('var h = Display.LineChart(new[] { 1.0, 2.0, 3.0 }, "Before");\nh.Update(s => s.Title = "After");');
      const cell = await runCell(notebook, 0);

      assert.equal(cell.executionSummary?.success, true, textOf(cell));
      assert.equal(cell.outputs.length, 1, `${cell.outputs.length} outputs`);
      const page = decode(cell.outputs[0].items.find((item) => item.mime === 'text/html')!.data);
      assert.ok(page.includes('After'));
      assert.ok(!page.includes('Before'));
    }
  ]
  ,
  [
    'the Variables view lists what the notebook defined, and its status says the engine is ready',
    async () => {
      const api = (await vscode.extensions.getExtension(EXTENSION)!.activate()) as FrySharpApi;
      const notebook = await notebookOf('int count = 42;\nstring name = "Ada";');
      const cell = await runCell(notebook, 0);
      assert.equal(cell.executionSummary?.success, true, textOf(cell));

      const kernels = await api.variables.getChildren();
      assert.deepStrictEqual(kernels.map((node) => api.variables.getTreeItem(node).label), ['C#']);
      const variables = await api.variables.getChildren(kernels[0]);
      const items = variables.map((node) => api.variables.getTreeItem(node));
      assert.deepStrictEqual(items.map((item) => item.label).sort(), ['count', 'name']);
      assert.match(String(items.find((item) => item.label === 'count')?.description), /42/);

      assert.match(api.statusText(), /ready/);
    }
  ],

  [
    'the status says a cell is running while it runs',
    async () => {
      const api = (await vscode.extensions.getExtension(EXTENSION)!.activate()) as FrySharpApi;
      const notebook = await notebookIn('python', 'import time\ntime.sleep(60)');
      startRun(notebook, 0, 1);
      await waitFor('the status to say running', () => /running/.test(api.statusText()), 15_000);
      await vscode.commands.executeCommand('notebook.cell.cancelExecution', { ranges: [{ start: 0, end: 1 }], document: notebook.uri });
      await waitFor('the status to say ready again', () => /ready/.test(api.statusText()), 15_000);
    }
  ]
  ,
  [
    'the output renderer draws an object, a table and an ECharts chart, and the chart really drew',
    async () => {
      const api = (await vscode.extensions.getExtension(EXTENSION)!.activate()) as FrySharpApi;
      const notebook = await notebookOf(
        'class P { public string Name = "Code"; public int[] Numbers = { 1, 2, 3 }; }\nnew P()',
        'Display.Table(new[] { new { Name = "Ada", Age = 36 }, new { Name = "Grace", Age = 45 } });',
        'EChart.Line(new[] { 3.0, 5.0, 2.0, 8.0 }, "Sales").Title("Sales").Show();'
      );
      const before = api.rendererReports.length;
      for (let i = 0; i < 3; i++) assert.equal((await runCell(notebook, i)).executionSummary?.success, true);

      await waitFor('the three outputs to be drawn', () => api.rendererReports.slice(before).filter((r) => r.type === 'rendered').length >= 3, 30_000);
      const reports = api.rendererReports.slice(before);
      assert.deepStrictEqual(reports.filter((r) => r.type === 'error'), []);
      const mimes = reports.map((r) => r.mime);
      assert.ok(mimes.includes('application/vnd.fry.inspector+json'), mimes.join());
      assert.ok(mimes.includes('application/vnd.fry.table+json'), mimes.join());
      const chart = reports.find((r) => r.mime === 'text/html');
      assert.ok(chart, 'the chart page was drawn by the renderer');
      assert.equal(chart.chartDrawn, true, 'ECharts drew its chart inside the frame');
    }
  ]
  ,
  // Not a check: runs cells of every kind and leaves the window open (FRY_IT_PAUSE_MS) to be looked at.
  ...(process.env.FRY_IT_PAUSE_MS
    ? ([
        [
          'show every kind of output (FRY_IT_PAUSE_MS)',
          async () => {
            await vscode.commands.executeCommand('frysharp.newNotebook');
            const notebook = vscode.window.activeNotebookEditor?.notebook as vscode.NotebookDocument;
            const code = (source: string) => new vscode.NotebookCellData(vscode.NotebookCellKind.Code, source, 'csharp');
            const edit = new vscode.WorkspaceEdit();
            edit.set(notebook.uri, [
              vscode.NotebookEdit.replaceCells(new vscode.NotebookRange(0, notebook.cellCount), [
                code('public class People { public string Name { get; set; } public int[] Numbers { get; set; } public People Another { get; set; } }\nvar people = new People { Name = "Code", Numbers = new[] { 1, 34, 45 } };\npeople.Another = people;\npeople'),
                code('Display.Table(new[] { new { Name = "Ada", Age = 36, Active = true }, new { Name = "Grace", Age = 45, Active = false } }, "People");'),
                code('Display.LineChart(new[] { 3.0, 1.0, 4.0, 1.0, 5.0 }, "Latency");')
              ])
            ]);
            await vscode.workspace.applyEdit(edit);
            for (let i = 0; i < 3; i++) await runCell(notebook, i);
            await new Promise((resolve) => setTimeout(resolve, Number(process.env.FRY_IT_PAUSE_MS)));
          }
        ]
      ] as Array<[string, () => Promise<void>]>)
    : [])
];
