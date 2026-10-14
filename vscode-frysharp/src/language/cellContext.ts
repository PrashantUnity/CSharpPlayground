/** A cell as the language features see it. */
export interface CellLike {
  kind: 'code' | 'markdown';
  /** The language the editor says the cell is in (the notebook's, or the one chosen for the cell). */
  language: string;
  text: string;
}

/** Maps a name used in `#!name` (an id or alias: "python", "c#", "js") to a language id; undefined when it is none. */
export type ResolveLanguage = (name: string) => string | undefined;

/**
 * The language a cell runs in: its own, unless its first lines (blank lines, comments and other directives may come before)
 * pick another with `#!python`, `#!csharp`, as the engine reads them. `#!share` does not change the language.
 */
export function effectiveLanguage(text: string, declared: string, resolve: ResolveLanguage): string {
  let language = declared;
  for (const raw of text.split(/\r?\n/)) {
    const line = raw.trim();
    if (line === '' || line.startsWith('//') || (line.startsWith('#') && !line.startsWith('#!'))) continue;
    if (line.startsWith('#!') && !line.startsWith('#![') && !line.startsWith('#!/')) {
      const name = /^#!\s*([^\s]+)/.exec(line)?.[1];
      if (name && name !== 'share') language = resolve(name) ?? language;
      continue;
    }
    break; // the first line of code ends the directives
  }
  return language;
}

/**
 * The text of the code cells before `index` that run in `language`, in order: what they define is in scope for the cell.
 * Markdown cells and cells in other languages are not part of it.
 */
export function contextFor(cells: readonly CellLike[], index: number, language: string, resolve: ResolveLanguage): string[] {
  const context: string[] = [];
  for (let i = 0; i < index && i < cells.length; i++) {
    const cell = cells[i];
    if (cell.kind === 'code' && effectiveLanguage(cell.text, cell.language, resolve) === language) context.push(cell.text);
  }
  return context;
}

/** Builds a language-name resolver from the engine's languages (an id, or one of its aliases, in any case). */
export function resolverFor(languages: ReadonlyArray<{ id: string; aliases: string[]; shortName?: string }>): ResolveLanguage {
  const byName = new Map<string, string>();
  for (const language of languages) {
    byName.set(language.id.toLowerCase(), language.id);
    for (const alias of language.aliases) byName.set(alias.toLowerCase(), language.id);
    if (language.shortName) byName.set(language.shortName.toLowerCase(), language.id);
  }
  return (name) => byName.get(name.toLowerCase());
}
