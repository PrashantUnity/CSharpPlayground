import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { DotnetMissingError, hasRuntime, resolveDotnet } from '../../src/sidecar/runtime';

const listing = (...versions: string[]) => versions.map((v) => `Microsoft.NETCore.App ${v} [/usr/share/dotnet/shared/Microsoft.NETCore.App]`).join('\n');

describe('the .NET runtime', () => {
  it('is found in the output of dotnet --list-runtimes: the shared runtime, version 10 or newer', () => {
    assert.equal(hasRuntime(listing('8.0.1', '10.0.0')), true);
    assert.equal(hasRuntime(listing('11.0.0-preview.1.12345.1')), true);
    assert.equal(hasRuntime(listing('8.0.1', '9.0.4')), false);
    assert.equal(hasRuntime('Microsoft.AspNetCore.App 10.0.0 [/x]\nMicrosoft.WindowsDesktop.App 10.0.0 [/x]'), false);
    assert.equal(hasRuntime(''), false);
    assert.equal(hasRuntime('Microsoft.NETCore.App 10.0.0 [/x]\r\n', 10), true);
  });

  it('takes the dotnet on the PATH when it is new enough (this machine has .NET 10: the tests need it)', async () => {
    const asked: string[] = [];
    const dotnet = await resolveDotnet({ acquire: async () => (asked.push('acquire'), '/downloaded/dotnet'), log: () => undefined });

    assert.equal(dotnet, 'dotnet');
    assert.deepStrictEqual(asked, []); // nothing was downloaded
  });

  it('a setting that cannot be run is an error that names it, not a quiet fallback', async () => {
    await assert.rejects(resolveDotnet({ setting: '/no/such/dotnet', log: () => undefined }), (error: Error) => error instanceof DotnetMissingError && /frysharp\.dotnetPath.*\/no\/such\/dotnet/.test(error.message));
  });
});
