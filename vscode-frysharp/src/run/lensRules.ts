/**
 * Whether a file gets the "Run | Stop" lens at its top. Never a plain `.cs` file (the C# extension owns those lenses), never a notebook
 * cell, and only a language the engine runs.
 */
export function offersLens(languageId: string, fileName: string, scheme: string, runnable: readonly string[]): boolean {
  if (scheme === 'vscode-notebook-cell' || scheme === 'output' || scheme === 'vscode-userdata') return false;
  if (!runnable.includes(languageId)) return false;
  return !(languageId === 'csharp' && /\.cs$/i.test(fileName));
}
