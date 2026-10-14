import assert from 'node:assert/strict';
import * as path from 'node:path';
import { describe, it } from 'node:test';
import { resolveSidecarLaunch } from '../../src/sidecar/launcher';

const ext = path.join('/x', 'ext');
const packaged = path.join(ext, 'bin', 'sidecar', 'frysharp-sidecar.dll');
const built = path.join('/x', 'FrySharp.Sidecar', 'bin', 'Debug', 'net10.0', 'frysharp-sidecar.dll');
const project = path.join('/x', 'FrySharp.Sidecar', 'FrySharp.Sidecar.csproj');
const have = (...files: string[]) => (file: string) => files.includes(file);

describe('which sidecar starts', () => {
  it('the one the setting names comes first, and a wrong setting is an error, not a fallback', () => {
    const launch = resolveSidecarLaunch({ setting: '/my/sidecar.dll', extensionPath: ext, development: false, exists: have('/my/sidecar.dll', packaged) }, '/work');
    assert.deepStrictEqual(launch.args, ['/my/sidecar.dll', '--workspace', '/work']);
    assert.throws(() => resolveSidecarLaunch({ setting: '/missing.dll', extensionPath: ext, development: false, exists: have(packaged) }, '/work'), /frysharp\.sidecarPath.*does not exist/);
  });

  it('then the packaged one, whatever else is lying around', () => {
    const launch = resolveSidecarLaunch({ extensionPath: ext, development: false, exists: have(packaged, built) }, '/work');
    assert.deepStrictEqual(launch.args, [packaged, '--workspace', '/work']);
  });

  it('an installed extension never runs a build it did not bring', () => {
    assert.throws(() => resolveSidecarLaunch({ extensionPath: ext, development: false, exists: have(built, project) }, '/work'), /not part of this installation/);
  });

  it('from the repository: the build there, even over a packaged copy that may be old; else the project by dotnet run; else the packaged one', () => {
    const repoExt = path.join('/x', 'vscode-frysharp');
    const repoPackaged = path.join(repoExt, 'bin', 'sidecar', 'frysharp-sidecar.dll');
    const dev = (...files: string[]) => resolveSidecarLaunch({ extensionPath: repoExt, development: true, exists: have(...files) }, '/work');

    assert.deepStrictEqual(dev(built, project, repoPackaged).args, [built, '--workspace', '/work']);
    assert.deepStrictEqual(dev(project, repoPackaged).args, ['run', '--project', project, '--', '--workspace', '/work']);
    assert.deepStrictEqual(dev(repoPackaged).args, [repoPackaged, '--workspace', '/work']);
    assert.throws(() => dev(), /not part of this installation/);
  });
});
