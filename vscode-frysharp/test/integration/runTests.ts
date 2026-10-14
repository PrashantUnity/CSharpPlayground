// Runs the extension in a real VS Code (an Extension Host) with a throwaway profile, against the real engine, and runs
// test/integration/suite inside it. `npm run test:integration` builds the extension first.
//
// Which VS Code: FRY_VSCODE_EXECUTABLE, else a VS Code or VSCodium installed in the usual place, else (only with
// FRY_ALLOW_DOWNLOAD=1) the latest stable, which @vscode/test-electron downloads.

import { runTests } from '@vscode/test-electron';
import * as fs from 'node:fs';
import * as os from 'node:os';
import * as path from 'node:path';

const root = path.resolve(__dirname, '../../..');

function installedExecutable(): string | undefined {
  const candidates = [
    process.env.FRY_VSCODE_EXECUTABLE,
    '/Applications/Visual Studio Code.app/Contents/MacOS/Electron',
    '/Applications/VSCodium.app/Contents/MacOS/VSCodium',
    '/usr/share/code/code',
    '/usr/share/codium/codium',
    'C:\\Program Files\\Microsoft VS Code\\Code.exe'
  ];
  return candidates.find((candidate) => candidate && fs.existsSync(candidate));
}

async function main(): Promise<void> {
  const executable = installedExecutable();
  if (!executable && process.env.FRY_ALLOW_DOWNLOAD !== '1') {
    console.error('No VS Code found. Set FRY_VSCODE_EXECUTABLE to one, or FRY_ALLOW_DOWNLOAD=1 to download the latest stable.');
    process.exit(2);
  }

  // A workspace of the notebooks the tests open and save, copies of the fixtures: the tests change them.
  const scratch = fs.mkdtempSync(path.join(os.tmpdir(), 'frysharp-it-'));
  const workspace = path.join(scratch, 'workspace');
  fs.mkdirSync(workspace);
  for (const name of ['desktop.frynb', 'jupyter.ipynb']) fs.copyFileSync(path.join(root, 'test', 'fixtures', name), path.join(workspace, name));
  fs.writeFileSync(path.join(workspace, 'broken.frynb'), '{ this is not json');
  fs.writeFileSync(path.join(workspace, 'run.frynb'), JSON.stringify({ SchemaVersion: '1.0', Type: 'notebook', Id: 'run', Title: 'Run', Kernel: 'csharp', Cells: [] }));
  process.env.FRY_IT_WORKSPACE = workspace;

  try {
    await runTests({
      ...(executable ? { vscodeExecutablePath: executable } : {}),
      extensionDevelopmentPath: root,
      extensionTestsPath: path.join(__dirname, 'suite', 'index'),
      launchArgs: [workspace, '--disable-extensions', '--disable-workspace-trust', '--skip-welcome', '--skip-release-notes', '--user-data-dir', path.join(scratch, 'user'), '--extensions-dir', path.join(scratch, 'extensions')]
    });
  } finally {
    fs.rmSync(scratch, { recursive: true, force: true });
  }
}

main().catch((error) => {
  console.error('The extension tests failed:', error);
  process.exit(1);
});
