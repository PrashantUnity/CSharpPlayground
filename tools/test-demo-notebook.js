const fs = require('fs');
const cp = require('child_process');
const path = require('path');
const rpc = require(path.join(__dirname, '..', 'vscode-frysharp', 'node_modules', 'vscode-jsonrpc', 'node.js'));

async function testDemoNotebook() {
  console.log('🚀 Executing All Cells in samples/demo.frynb...');
  const workspaceRoot = path.resolve(__dirname, '..');
  const sidecarDll = path.join(workspaceRoot, 'vscode-frysharp', 'bin', 'sidecar', 'frysharp-sidecar.dll');

  const child = cp.spawn('dotnet', [sidecarDll, '--workspace', workspaceRoot], {
    cwd: workspaceRoot,
    env: { ...process.env, DOTNET_NOLOGO: '1' }
  });

  const connection = rpc.createMessageConnection(
    new rpc.StreamMessageReader(child.stdout),
    new rpc.StreamMessageWriter(child.stdin)
  );

  let displaysByCell = new Map();
  let streamsByCell = new Map();

  connection.onNotification('fry/notebook/output', (n) => {
    if (n.output?.kind === 'display') {
      const list = displaysByCell.get(n.cellId) || [];
      list.push(Object.keys(n.output.data || {}));
      displaysByCell.set(n.cellId, list);
    } else if (n.output?.kind === 'stream') {
      const prev = streamsByCell.get(n.cellId) || '';
      streamsByCell.set(n.cellId, prev + n.output.text);
    }
  });

  connection.listen();

  await connection.sendRequest('fry/initialize', {
    clientId: 'demo-verifier',
    clientName: 'Demo Notebook Verifier',
    protocolVersion: 1
  });

  const raw = fs.readFileSync(path.join(workspaceRoot, 'samples', 'demo.frynb'), 'utf8');
  const nb = JSON.parse(raw);
  const codeCells = nb.Cells.filter(c => c.Type === 0);

  console.log(`Found ${nb.Cells.length} total cells (${codeCells.length} code cells to execute).\n`);

  let failures = 0;
  for (let i = 0; i < codeCells.length; i++) {
    const cell = codeCells[i];
    const lang = cell.Language || nb.Kernel || 'csharp';
    process.stdout.write(`[${i + 1}/${codeCells.length}] Cell '${cell.Id}' (${lang})... `);

    const res = await connection.sendRequest('fry/notebook/execute', {
      path: 'samples/demo.frynb',
      cellId: cell.Id,
      source: cell.Source,
      language: lang
    });

    if (res.success) {
      const disp = displaysByCell.get(cell.Id);
      const dispInfo = disp ? ` [Displays: ${disp.map(d => d.join(',')).join('; ')}]` : '';
      console.log(`✅ Success (${res.durationMs}ms)${dispInfo}`);
    } else {
      console.log(`❌ FAILED (${res.durationMs}ms)`);
      const stream = streamsByCell.get(cell.Id);
      if (stream) console.log(`   Output: ${stream.trim()}`);
      failures++;
    }
  }

  connection.dispose();
  child.kill();

  console.log('\n----------------------------------------');
  if (failures === 0) {
    console.log(`🎉 ALL ${codeCells.length} CELLS PASSED WITH ZERO ERRORS!`);
  } else {
    console.error(`💥 ${failures} cells failed execution.`);
    process.exit(1);
  }
}

testDemoNotebook().catch((err) => {
  console.error(err);
  process.exit(1);
});
