import { effectiveLanguage, resolverFor } from '../language/cellContext';

export interface StatusLanguage {
  id: string;
  displayName: string;
  aliases: string[];
  shortName?: string;
  canRunCells: boolean;
}

export interface CellStatusItem {
  text: string;
  tooltip: string;
}

/**
 * What the strip under a code cell says about the language it runs in, when that is not the one the editor shows: a first line of
 * `#!python` runs the cell as Python whatever the cell is set to, which is easy to forget. Undefined when the cell runs as itself.
 */
export function languageNote(text: string, declared: string, languages: readonly StatusLanguage[]): CellStatusItem | undefined {
  const effective = effectiveLanguage(text, declared, resolverFor(languages));
  if (effective === declared) return undefined;
  const language = languages.find((l) => l.id === effective);
  const name = language?.displayName ?? effective;
  const directive = /^\s*#!\s*([^\s]+)/m.exec(text.split(/\r?\n/).find((line) => /^\s*#!\s*(?!share\b)[^\s[/]/.test(line)) ?? '')?.[1];
  return { text: `$(arrow-right) ${name}`, tooltip: `This cell runs as ${name}${directive ? `: its first line says #!${directive}` : ''}, not as ${languages.find((l) => l.id === declared)?.displayName ?? declared}.` };
}
