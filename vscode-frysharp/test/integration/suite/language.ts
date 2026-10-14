import assert from 'node:assert/strict';
import * as vscode from 'vscode';
import { notebookIn, notebookWith, runCell, waitFor } from './notebooks';

// Completion, hover and problems in the cells of a notebook, asked of a real VS Code, which asks the extension, which asks the
// real engine. The first answers can be slow (the compiler warms up), so each waits for what it expects instead of asking once.

const labelOf = (item: vscode.CompletionItem): string => (typeof item.label === 'string' ? item.label : item.label.label);

async function completionsAt(cell: vscode.NotebookCell, line: number, character: number, trigger?: string): Promise<string[]> {
  const list = await vscode.commands.executeCommand<vscode.CompletionList>('vscode.executeCompletionItemProvider', cell.document.uri, new vscode.Position(line, character), trigger);
  return (list?.items ?? []).map(labelOf);
}

// The editor adds the words of the document as suggestions (kind Text): only an item of the engine's own proves the engine answered.
async function engineItemsAt(cell: vscode.NotebookCell, line: number, character: number, trigger?: string): Promise<vscode.CompletionItem[]> {
  const list = await vscode.commands.executeCommand<vscode.CompletionList>('vscode.executeCompletionItemProvider', cell.document.uri, new vscode.Position(line, character), trigger);
  return (list?.items ?? []).filter((item) => item.kind !== vscode.CompletionItemKind.Text);
}

async function eventually<T>(what: string, ask: () => Thenable<T>, accept: (answer: T) => boolean, ms = 60_000): Promise<T> {
  const until = Date.now() + ms;
  for (;;) {
    const answer = await ask();
    if (accept(answer)) return answer;
    if (Date.now() > until) throw new Error(`timed out waiting for ${what}; the last answer was ${JSON.stringify(answer)}`);
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
}

const problemsOf = (cell: vscode.NotebookCell): vscode.Diagnostic[] => vscode.languages.getDiagnostics(cell.document.uri).filter((d) => d.source === 'FrySharp');

async function replaceText(cell: vscode.NotebookCell, text: string): Promise<void> {
  const edit = new vscode.WorkspaceEdit();
  edit.replace(cell.document.uri, new vscode.Range(0, 0, cell.document.lineCount, 0), text);
  assert.ok(await vscode.workspace.applyEdit(edit));
}

export const languageTests: Array<[string, () => Promise<void>]> = [
  [
    'C# completion in a cell knows what the cells before it define',
    async () => {
      const notebook = await notebookIn('csharp', 'var greeting = "hello";', 'greeting.');
      const cell = notebook.cellAt(1);
      const labels = await eventually('completions after greeting.', () => completionsAt(cell, 0, 'greeting.'.length, '.'), (items) => items.includes('ToUpper'));
      assert.ok(labels.includes('Length'), 'a member of string');
      const names = await eventually('completions of a name', () => engineItemsAt(cell, 0, 0), (items) => items.some((item) => labelOf(item) === 'greeting'));
      assert.ok(names.some((item) => labelOf(item) === 'greeting'), 'the variable of the cell before, from the engine and not from the words of the page');
    }
  ],
  [
    'hover in a C# cell shows the type, with the cells before it in scope',
    async () => {
      const notebook = await notebookIn('csharp', 'var count = 42;', 'var next = count + 1;');
      const cell = notebook.cellAt(1);
      const hovers = await eventually(
        'a hover on count',
        () => vscode.commands.executeCommand<vscode.Hover[]>('vscode.executeHoverProvider', cell.document.uri, new vscode.Position(0, 'var next = c'.length)),
        (items) => (items ?? []).length > 0
      );
      const text = hovers.flatMap((hover) => hover.contents).map((content) => (typeof content === 'string' ? content : content.value)).join('\n');
      assert.match(text, /int/);
    }
  ],
  [
    'directives are completed in a cell of any language',
    async () => {
      const notebook = await notebookIn('python', '#!');
      const labels = await eventually('directive completions', () => completionsAt(notebook.cellAt(0), 0, 2, '!'), (items) => items.includes('share'));
      assert.ok(labels.includes('python') && labels.includes('csharp'), 'the languages of the engine');
    }
  ],
  [
    '#!share completes the variables the other kernel holds',
    async () => {
      const notebook = await notebookWith(['csharp', 'var population = 8;'], ['python', '#!share --from csharp ']);
      await runCell(notebook, 0);
      const labels = await eventually('the variable of the C# kernel', () => completionsAt(notebook.cellAt(1), 0, '#!share --from csharp '.length), (items) => items.includes('population'));
      assert.ok(labels.includes('population'));
    }
  ],
  [
    'a JavaScript cell gets the editor\'s own completion, next to the notebook\'s directives',
    async () => {
      const notebook = await notebookIn('javascript', 'console.');
      const labels = await eventually('completions after console.', () => completionsAt(notebook.cellAt(0), 0, 'console.'.length, '.'), (items) => items.includes('log'));
      assert.ok(labels.includes('log'));
    }
  ],
  [
    'SQL cells get completion (keywords, functions, the tables earlier cells created, dot-commands) and hover',
    async () => {
      const notebook = await notebookIn('sql', 'create table people (id integer);', 'select * from peo', 'select CO', '.ta', 'select name from people group by name');

      const tables = await eventually('the table of the cell before', () => engineItemsAt(notebook.cellAt(1), 0, 'select * from peo'.length), (items) => items.some((item) => labelOf(item) === 'people'));
      assert.equal(tables.find((item) => labelOf(item) === 'people')?.kind, vscode.CompletionItemKind.Struct);
      const functions = await eventually('functions', () => engineItemsAt(notebook.cellAt(2), 0, 'select CO'.length), (items) => items.some((item) => labelOf(item) === 'COUNT'));
      assert.ok(functions.some((item) => labelOf(item) === 'COALESCE'));

      // after the dot that was typed, the command inserts its name, not a second dot
      const list = await vscode.commands.executeCommand<vscode.CompletionList>('vscode.executeCompletionItemProvider', notebook.cellAt(3).document.uri, new vscode.Position(0, 3), '.');
      const tablesCommand = list.items.find((item) => labelOf(item) === '.tables');
      assert.equal(tablesCommand?.insertText, 'tables');

      const hovers = await eventually(
        'a hover on group',
        () => vscode.commands.executeCommand<vscode.Hover[]>('vscode.executeHoverProvider', notebook.cellAt(4).document.uri, new vscode.Position(0, 'select name from people gr'.length)),
        (items) => (items ?? []).length > 0
      );
      const text = hovers.flatMap((hover) => hover.contents).map((content) => (typeof content === 'string' ? content : content.value)).join('\n');
      assert.match(text, /GROUP BY column1, column2/);
    }
  ],
  [
    'a compile error is marked in the cell as it is typed, and gone when it is fixed',
    async () => {
      const notebook = await notebookIn('csharp', 'var a = 1;', 'var b = missing + a;');
      const cell = notebook.cellAt(1);
      const marked = await eventually('the error to be marked', async () => problemsOf(cell), (problems) => problems.some((p) => p.message.includes('missing')));
      assert.equal(marked[0].severity, vscode.DiagnosticSeverity.Error);
      assert.equal(marked[0].range.start.line, 0, 'on the line of the cell, not of the cells joined before it');

      await replaceText(cell, 'var b = a + 1;');
      await eventually('the mark to go', async () => problemsOf(cell), (problems) => problems.length === 0);
    }
  ],
  [
    'defining a name again in a later cell is not an error, and the cells after an error are still checked',
    async () => {
      const notebook = await notebookIn('csharp', 'var x = 1;', 'var x = 2;', 'int y = "text";');
      await eventually('the last cell to be checked', async () => problemsOf(notebook.cellAt(2)), (problems) => problems.length > 0);
      assert.deepStrictEqual(problemsOf(notebook.cellAt(0)).map((p) => p.message), []);
      assert.deepStrictEqual(problemsOf(notebook.cellAt(1)).map((p) => p.message), [], 'redefinition is what cells are for');
    }
  ],
  [
    'a directive that is not one is marked, in a Python cell',
    async () => {
      const notebook = await notebookIn('python', '#!pyton\nprint(1)');
      const problems = await eventually('the directive to be marked', async () => problemsOf(notebook.cellAt(0)), (found) => found.length > 0);
      assert.equal(problems[0].code, 'FRY0001');
      assert.equal(problems[0].range.start.line, 0);
    }
  ],
  [
    'the problems of a run and of typing are one list, each problem once',
    async () => {
      const notebook = await notebookIn('csharp', 'int n = "text";');
      const cell = notebook.cellAt(0);
      const typed = await eventually('the error to be marked', async () => problemsOf(cell), (problems) => problems.length > 0);
      await runCell(notebook, 0);
      await waitFor('the run to be done', () => cell.executionSummary?.success === false);
      await new Promise((resolve) => setTimeout(resolve, 500));
      assert.equal(problemsOf(cell).length, typed.length, 'running it does not mark the same error twice');
    }
  ]
];
