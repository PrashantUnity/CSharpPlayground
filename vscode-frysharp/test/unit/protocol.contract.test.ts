import assert from 'node:assert/strict';
import * as fs from 'node:fs';
import * as path from 'node:path';
import { describe, it } from 'node:test';

// The extension's protocol.ts mirrors the sidecar's Protocol.cs by hand. This reads both and fails when they drift:
// a field the extension has that the engine does not, a field the engine requires that the extension does not know,
// or a method or notification the extension names that the engine does not have.
const root = path.resolve(__dirname, '../../..');
const csharp = fs.readFileSync(path.join(root, '..', 'FrySharp.Sidecar', 'Protocol', 'Protocol.cs'), 'utf8');
const rpc = fs.readFileSync(path.join(root, '..', 'FrySharp.Sidecar', 'Sessions', 'SidecarRpc.cs'), 'utf8');
const typescript = fs.readFileSync(path.join(root, '..', 'vscode-frysharp', 'src', 'sidecar', 'protocol.ts'), 'utf8');

interface Field {
  name: string;
  optional: boolean;
}

const camel = (name: string) => name[0].toLowerCase() + name.slice(1);

function splitTopLevel(text: string): string[] {
  const parts: string[] = [];
  let depth = 0;
  let current = '';
  for (const char of text) {
    if ('<([{'.includes(char)) depth++;
    if ('>)]}'.includes(char)) depth--;
    if (char === ',' && depth === 0) {
      parts.push(current);
      current = '';
    } else {
      current += char;
    }
  }
  if (current.trim()) parts.push(current);
  return parts;
}

function csharpRecords(): Map<string, Field[]> {
  const records = new Map<string, Field[]>();
  for (const match of csharp.matchAll(/public sealed record (\w+)\s*\(([^;]*?)\)\s*;/gs)) {
    const fields = splitTopLevel(match[2]).map((parameter): Field => {
      const [declaration, defaultValue] = parameter.split('=');
      const words = declaration.trim().split(/\s+/);
      return { name: camel(words[words.length - 1]), optional: defaultValue !== undefined || /\?\s+\w+$/.test(declaration.trim()) };
    });
    records.set(match[1], fields);
  }
  return records;
}

function typescriptInterfaces(): Map<string, Field[]> {
  const interfaces = new Map<string, Field[]>();
  const header = /export interface (\w+)(?:\s+extends\s+\w+)?\s*\{/g;
  for (let match = header.exec(typescript); match; match = header.exec(typescript)) {
    let depth = 1;
    let index = header.lastIndex;
    const start = index;
    while (depth > 0 && index < typescript.length) {
      if (typescript[index] === '{') depth++;
      if (typescript[index] === '}') depth--;
      index++;
    }
    const body = typescript.slice(start, index - 1);
    // only the top level of the body: fields of nested object types belong to another shape
    let level = 0;
    let top = '';
    for (const char of body) {
      if (char === '{') level++;
      if (level === 0) top += char;
      if (char === '}') level--;
    }
    const fields = [...top.matchAll(/^\s*(\w+)(\?)?\s*:/gm)].map((field): Field => ({ name: field[1], optional: field[2] === '?' }));
    interfaces.set(match[1], fields);
  }
  return interfaces;
}

describe('protocol.ts and Protocol.cs', () => {
  const records = csharpRecords();
  const interfaces = typescriptInterfaces();

  it('read both files (so the checks below mean something)', () => {
    assert.ok(records.size > 30, `${records.size} C# records`);
    assert.ok(interfaces.size > 10, `${interfaces.size} TypeScript interfaces`);
    assert.deepStrictEqual(records.get('ExecuteCellParams')?.map((f) => f.name), ['path', 'cellId', 'source', 'language', 'notebookId', 'workingDirectory', 'executionId']);
  });

  it('every interface of the extension that is a record of the engine has fields the engine has', () => {
    const mirrored = [...interfaces.keys()].filter((name) => records.has(name));
    assert.ok(mirrored.length >= 12, mirrored.join(', '));

    for (const name of mirrored) {
      const engine = new Set(records.get(name)!.map((f) => f.name));
      for (const field of interfaces.get(name)!) {
        assert.ok(engine.has(field.name), `${name}.${field.name} is in protocol.ts, not in Protocol.cs`);
      }
    }
  });

  it('every field the engine needs in a request the extension sends is in the extension\'s interface', () => {
    for (const name of ['ExecuteCellParams', 'RunStartParams', 'InitializeParams', 'NotebookRef']) {
      const ours = new Set(interfaces.get(name)!.map((f) => f.name));
      for (const field of records.get(name)!.filter((f) => !f.optional)) {
        assert.ok(ours.has(field.name), `${name}.${field.name} is required by the engine and missing from protocol.ts`);
      }
    }
  });

  it('every field of an answer or notification the extension reads is optional or there in its interface', () => {
    for (const name of ['InitializeResult', 'ExecuteCellResult', 'NotebookOutputNotification', 'CellStateNotification', 'RunStartResult', 'RunOutputNotification', 'RunExitedNotification', 'VariableDto', 'LanguageInfo', 'DiagnosticDto', 'InputRequest', 'CellOutput']) {
      const ours = new Set(interfaces.get(name)!.map((f) => f.name));
      for (const field of records.get(name)!) {
        assert.ok(ours.has(field.name), `${name}.${field.name} is sent by the engine and missing from protocol.ts`);
      }
    }
  });

  it('every method and notification the extension names exists in the engine', () => {
    const engineMethods = new Set([...rpc.matchAll(/JsonRpcMethod\("([^"]+)"/g)].map((m) => m[1]));
    const engineNotifications = new Set([...csharp.matchAll(/const string \w+ = "(fry\/[^"]+)"/g)].map((m) => m[1]));
    const sources = ['src/sidecar/sidecarClient.ts', 'src/extension.ts', 'src/notebook/notebookController.ts'].map((file) => fs.readFileSync(path.join(root, '..', 'vscode-frysharp', file), 'utf8')).join('\n');

    const requested = [...sources.matchAll(/(?:request|sendRequest)(?:<[^>(]*>)?\(\s*'(fry\/[^']+)'/g)].map((m) => m[1]);
    const notified = [...sources.matchAll(/on(?:Notification|Request)\(\s*'(fry\/[^']+)'/g)].map((m) => m[1]);
    assert.ok(requested.length >= 7, requested.join(', '));
    assert.ok(notified.length >= 5, notified.join(', ')); // four notifications, and the engine's request for input

    for (const method of requested) assert.ok(engineMethods.has(method), `${method} is requested by the extension and is not a method of the engine`);
    for (const method of notified) assert.ok(engineNotifications.has(method), `${method} is listened to by the extension and is not a notification of the engine`);
  });
});
