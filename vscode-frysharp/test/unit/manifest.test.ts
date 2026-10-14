import assert from 'node:assert/strict';
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { describe, it } from 'node:test';

const root = path.resolve(__dirname, '../../..');
const manifest = JSON.parse(readFileSync(path.join(root, 'package.json'), 'utf8'));
const contributes = manifest.contributes;
const declared = new Set<string>(contributes.commands.map((c: { command: string }) => c.command));

const menuCommands = (): string[] =>
  Object.values(contributes.menus as Record<string, Array<{ command?: string }>>)
    .flat()
    .map((item) => item.command)
    .filter((command): command is string => command !== undefined);

describe('the manifest', () => {
  it('points at files that exist', () => {
    const files: string[] = [
      ...contributes.grammars.map((g: { path: string }) => g.path),
      ...contributes.snippets.map((s: { path: string }) => s.path),
      ...contributes.walkthroughs.flatMap((w: { steps: Array<{ media: { markdown: string } }> }) => w.steps.map((step) => step.media.markdown))
    ];
    assert.ok(files.length > 0);
    for (const file of files) assert.ok(existsSync(path.join(root, file)), `${file} is declared but missing`);
  });

  it('has snippets that are JSON with a prefix and a body', () => {
    for (const { path: file } of contributes.snippets as Array<{ path: string }>) {
      const snippets = JSON.parse(readFileSync(path.join(root, file), 'utf8')) as Record<string, { prefix: string; body: string[] }>;
      for (const [name, snippet] of Object.entries(snippets)) {
        assert.ok(snippet.prefix.startsWith('fry'), `${file}: ${name} has the fry prefix`);
        assert.ok(snippet.body.length > 0, `${file}: ${name} has a body`);
      }
    }
  });

  it('declares every command its menus, keybindings, walkthroughs and activation use', () => {
    const used = [
      ...menuCommands(),
      ...contributes.keybindings.map((k: { command: string }) => k.command),
      ...(manifest.activationEvents as string[]).filter((e) => e.startsWith('onCommand:')).map((e) => e.slice('onCommand:'.length)),
      ...contributes.walkthroughs.flatMap((w: { steps: Array<{ completionEvents?: string[] }> }) => w.steps.flatMap((s) => s.completionEvents ?? []).map((e) => e.slice('onCommand:'.length)))
    ];
    for (const command of used) assert.ok(declared.has(command), `${command} is used but not declared`);
  });

  it('offers its run buttons by what the engine runs, not by a list of languages in the manifest', () => {
    const whens = [
      ...Object.values(contributes.menus as Record<string, Array<{ when?: string }>>).flat().map((item) => item.when),
      ...contributes.keybindings.map((k: { when?: string }) => k.when)
    ].filter((when): when is string => when !== undefined);

    assert.deepStrictEqual(whens.filter((when) => /resourceLangId\s*==/.test(when)), []);
    assert.ok(whens.some((when) => when.includes('resourceLangId in frysharp.runnableLanguages')));
  });

  it('declares every setting the code reads', () => {
    const properties = Object.keys(contributes.configuration.properties);
    const reads: string[] = [];
    for (const file of sourceFiles(path.join(root, 'src'))) {
      const text = readFileSync(file, 'utf8');
      for (const match of text.matchAll(/getConfiguration\('(frysharp[\w.]*)'\)\s*\.get(?:<[^>]*>)?\('([\w.]+)'/g)) reads.push(`${match[1]}.${match[2]}`);
      for (const variable of text.matchAll(/const (\w+) = vscode\.workspace\.getConfiguration\('(frysharp[\w.]*)'\)/g)) {
        for (const match of text.matchAll(new RegExp(`\\b${variable[1]}\\.get(?:<[^>]*>)?\\('([\\w.]+)'`, 'g'))) reads.push(`${variable[2]}.${match[1]}`);
      }
    }
    assert.ok(reads.length >= 6, `the check found ${reads.length} reads of settings: it looks for the wrong thing`);
    for (const read of reads) assert.ok(properties.includes(read), `${read} is read but not declared`);
  });
});

function sourceFiles(folder: string): string[] {
  return readdirSync(folder, { withFileTypes: true }).flatMap((entry) => (entry.isDirectory() ? sourceFiles(path.join(folder, entry.name)) : entry.name.endsWith('.ts') ? [path.join(folder, entry.name)] : []));
}
