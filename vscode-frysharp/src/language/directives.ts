/** A language of the engine, as the directives name it. */
export interface DirectiveLanguage {
  id: string;
  displayName: string;
  shortName: string;
  aliases: string[];
  /** Cells can be written in it (it has a notebook kernel). */
  canRunCells: boolean;
}

export interface DirectiveItem {
  label: string;
  insertText?: string;
  detail?: string;
  kind: 'directive' | 'language' | 'variable' | 'option' | 'snippet';
}

export interface DirectiveContext {
  languages: readonly DirectiveLanguage[];
  /** The language the cell is in. */
  cellLanguage: string;
  /** The names of the variables a language's kernel holds. */
  variablesOf(languageId: string): Promise<string[]>;
}

export interface DirectiveCompletions {
  items: DirectiveItem[];
  /** Where in the line the text being completed starts: the editor replaces from here to the caret. */
  from: number;
}

/**
 * What to offer after the text of a line up to the caret, when the line is a directive of the notebook: `#!` and the language
 * names, `#!share` and its options, the languages after `--from`, the variables of that language, and the package lines
 * (`%pip install`). Undefined when the line is not one.
 */
export async function directiveCompletions(linePrefix: string, context: DirectiveContext): Promise<DirectiveCompletions | undefined> {
  const indent = linePrefix.length - linePrefix.trimStart().length;
  const text = linePrefix.slice(indent);
  const runnable = context.languages.filter((language) => language.canRunCells);

  // "#!": the directive's name
  const name = /^#!(?<word>[^\s]*)$/.exec(text);
  if (name) {
    const items: DirectiveItem[] = [{ label: 'share', detail: 'Copy a variable from another language: #!share --from <language> <name>', kind: 'directive' }];
    for (const language of runnable) {
      for (const word of [language.id, ...language.aliases.filter((alias) => alias.toLowerCase() !== language.id)]) {
        items.push({ label: word, detail: `Run this cell as ${language.displayName}`, kind: 'language' });
      }
    }
    return { items, from: indent + 2 };
  }

  const share = /^#!share\s+(?<rest>.*)$/.exec(text);
  if (share) {
    const rest = share.groups!.rest;
    const argumentsStart = indent + text.length - rest.length;
    const words = rest.split(/\s+/);
    const typing = words[words.length - 1];
    const before = words.slice(0, -1);
    const from = argumentsStart + rest.length - typing.length;

    if (before.length === 0) {
      return { items: [{ label: '--from', detail: 'The language the variable comes from', kind: 'option' }], from };
    }
    if (before[before.length - 1] === '--from') {
      return { items: runnable.map((language) => ({ label: language.id, detail: language.displayName, kind: 'language' as const })), from };
    }
    if (before[0] === '--from' && before.length === 2) {
      const source = runnable.find((language) => language.id === before[1] || language.aliases.some((alias) => alias.toLowerCase() === before[1].toLowerCase()));
      if (!source) return { items: [], from };
      const variables = await context.variablesOf(source.id);
      return { items: variables.map((variable) => ({ label: variable, detail: `A variable of ${source.displayName}`, kind: 'variable' as const })), from };
    }
    if (before.length >= 3 && !before.includes('--as')) {
      return { items: [{ label: '--as', detail: 'Name it differently here: --as <new name>', kind: 'option' }], from };
    }
    return { items: [], from };
  }

  // package lines: "%pip install", "%npm install"
  const magic = /^[%!](?<word>[A-Za-z0-9]*)$/.exec(text);
  if (magic) {
    const items: DirectiveItem[] = [];
    if (context.cellLanguage === 'python') items.push({ label: 'pip install', insertText: 'pip install ', detail: 'Install a Python package into the notebook\'s environment', kind: 'snippet' });
    if (context.cellLanguage === 'javascript') items.push({ label: 'npm install', insertText: 'npm install ', detail: 'Install an npm package', kind: 'snippet' });
    if (context.cellLanguage === 'csharp') items.push({ label: 'nuget', insertText: 'nuget ', detail: 'Reference a NuGet package', kind: 'snippet' });
    return items.length > 0 ? { items, from: indent + 1 } : undefined;
  }
  return undefined;
}
