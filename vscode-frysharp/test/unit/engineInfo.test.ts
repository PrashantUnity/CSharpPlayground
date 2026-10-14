import assert from 'node:assert/strict';
import { test } from 'node:test';
import { engineInfoMarkdown } from '../../src/engineInfo';
import type { EngineInfo } from '../../src/sidecar/protocol';

const info: EngineInfo = {
  serverVersion: '1.0.0',
  protocolVersion: 1,
  runtime: '.NET 10.0.0',
  operatingSystem: 'Darwin 25.5.0',
  architecture: 'Arm64',
  workspaceRoot: '/work/space',
  toolchains: [
    { languageId: 'csharp', displayName: 'C#', found: true, name: '.NET 10.0.0', version: '10.0.0', source: 'built into the engine' },
    { languageId: 'java', displayName: 'Java', found: true, name: 'OpenJDK 17.0.20', version: '17.0.20', path: '/usr/bin/java', source: 'PATH' },
    { languageId: 'rust', displayName: 'Rust', found: false, missing: 'Rust toolchain not found\nInstall it from https://rustup.rs\n  • then reload' }
  ]
};

test('the report says what the engine runs on, one line each, and how it was started', () => {
  const text = engineInfoMarkdown(info, {
    extensionVersion: '1.2.3',
    vscodeVersion: '1.135.0',
    launch: { command: '/opt/homebrew/bin/dotnet', description: 'the packaged sidecar (/ext/bin/sidecar/frysharp-sidecar.dll)' },
    logFolder: '/logs',
    settings: [['frysharp.dotnetPath', ''], ['frysharp.notebook.outputTabs', 'true']]
  });

  assert.match(text, /\| Extension \| 1\.2\.3 \|/);
  assert.match(text, /\| Engine \| 1\.0\.0 \(protocol 1\) \|/);
  assert.match(text, /\| Runtime \| \.NET 10\.0\.0 \|/);
  assert.match(text, /\| Engine started as \| the packaged sidecar/);
  assert.match(text, /\| \.NET \| \/opt\/homebrew\/bin\/dotnet \|/);
  assert.match(text, /\| Logs \| \/logs \|/);
});

test('each language is a row: what it found and where, or that it is not found', () => {
  const text = engineInfoMarkdown(info, { extensionVersion: '1', vscodeVersion: '1', settings: [] });

  assert.match(text, /\| C# \| ✓ found \| \.NET 10\.0\.0 \| inside the engine \| built into the engine \|/);
  assert.match(text, /\| Java \| ✓ found \| OpenJDK 17\.0\.20 \| \/usr\/bin\/java \| PATH \|/);
  assert.match(text, /\| Rust \| ✗ \*\*not found\*\* \|/);
});

test('what is missing says how to get it, indented so Markdown shows it as it is written', () => {
  const text = engineInfoMarkdown(info, { extensionVersion: '1', vscodeVersion: '1', settings: [] });

  assert.match(text, /### Rust\n\n {4}Rust toolchain not found\n {4}Install it from https:\/\/rustup\.rs\n {6}• then reload/);
  assert.ok(!engineInfoMarkdown({ ...info, toolchains: [info.toolchains[0]] }, { extensionVersion: '1', vscodeVersion: '1', settings: [] }).includes('What is missing'));
});

test('a setting that is not set says so, and a pipe in a value does not break its table row', () => {
  const text = engineInfoMarkdown({ ...info, workspaceRoot: '/a|b' }, { extensionVersion: '1', vscodeVersion: '1', settings: [['frysharp.sidecarPath', '']] });

  assert.match(text, /- `frysharp\.sidecarPath`: \(not set\)/);
  assert.match(text, /\| Workspace \| \/a\\\|b \|/);
});
