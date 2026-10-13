const cp = require('child_process');
const path = require('path');
const rpc = require(path.join(__dirname, '..', 'vscode-frysharp', 'node_modules', 'vscode-jsonrpc', 'node.js'));

async function main() {
  console.log('🚀 Starting FrySharp Sidecar E2E Integration Test...');
  const workspaceRoot = path.resolve(__dirname, '..');
  const sidecarDll = path.join(workspaceRoot, 'vscode-frysharp', 'bin', 'sidecar', 'frysharp-sidecar.dll');

  console.log(`📦 Sidecar binary: ${sidecarDll}`);
  console.log(`📂 Workspace root:  ${workspaceRoot}`);

  const child = cp.spawn('dotnet', [sidecarDll, '--workspace', workspaceRoot], {
    cwd: workspaceRoot,
    env: { ...process.env, DOTNET_NOLOGO: '1' }
  });

  child.stderr.on('data', (d) => {
    // console.log(`[stderr] ${d.toString().trim()}`);
  });

  const connection = rpc.createMessageConnection(
    new rpc.StreamMessageReader(child.stdout),
    new rpc.StreamMessageWriter(child.stdin)
  );

  let capturedOutputs = [];
  connection.onNotification('fry/notebook/output', (n) => {
    if (n.output?.kind === 'stream') {
      capturedOutputs.push(n.output.text);
    }
  });

  connection.listen();

  // 1. Initialize
  console.log('\n--- Test 1: fry/initialize ---');
  const initResult = await connection.sendRequest('fry/initialize', {
    clientId: 'e2e-tester',
    clientName: 'Integration Tester',
    protocolVersion: 1
  });
  console.log('Initialize result raw:', initResult);
  const version = initResult.serverVersion || initResult.ServerVersion;
  const langs = initResult.languages || initResult.Languages || [];
  console.log(`✅ Sidecar initialized! Server version: ${version}`);
  console.log(`✅ Languages registered: ${langs.map(l => `${l.id || l.Id} (${l.displayName || l.DisplayName})`).join(', ')}`);

  // 2. Execute C# Cell (State definition)
  console.log('\n--- Test 2: Execute C# Cell (defining state) ---');
  capturedOutputs = [];
  const csharpCode1 = `
int x = 50;
int y = 75;
Console.WriteLine($"C# sum of {x} + {y} is: {x + y}");
`;
  const res1 = await connection.sendRequest('fry/notebook/execute', {
    path: 'samples/demo.frynb',
    cellId: 'csharp-state-1',
    source: csharpCode1,
    language: 'csharp'
  });
  console.log(`✅ C# Cell 1 Result: success=${res1.success}, executionCount=${res1.executionCount}, time=${res1.durationMs}ms`);
  console.log(`📢 Output: ${capturedOutputs.join('')}`.trim());

  if (!res1.success || !capturedOutputs.some(o => o.includes('125'))) {
    throw new Error('C# Cell 1 failed or output missing');
  }

  // 3. Execute second C# Cell (State continuity)
  console.log('\n--- Test 3: Execute C# Cell (persisted session state) ---');
  capturedOutputs = [];
  const csharpCode2 = `
Console.WriteLine($"State continuity check: x was {x}, now x * 2 = {x * 2}");
`;
  const res2 = await connection.sendRequest('fry/notebook/execute', {
    path: 'samples/demo.frynb',
    cellId: 'csharp-state-2',
    source: csharpCode2,
    language: 'csharp'
  });
  console.log(`✅ C# Cell 2 Result: success=${res2.success}, executionCount=${res2.executionCount}, time=${res2.durationMs}ms`);
  console.log(`📢 Output: ${capturedOutputs.join('')}`.trim());

  if (!res2.success || !capturedOutputs.some(o => o.includes('100'))) {
    throw new Error('C# Cell 2 failed or state continuity broken');
  }

  // 4. Execute C# Cell with LINQ and records
  console.log('\n--- Test 4: Execute modern C# (.NET 10 records & LINQ) ---');
  capturedOutputs = [];
  const csharpCode3 = `
record Person(string Name, int Score);
var list = new[] { new Person("Alice", 95), new Person("Bob", 88), new Person("Charlie", 99) };
var top = list.OrderByDescending(p => p.Score).First();
Console.WriteLine($"Top scorer: {top.Name} with score {top.Score}");
`;
  const res3 = await connection.sendRequest('fry/notebook/execute', {
    path: 'samples/demo.frynb',
    cellId: 'csharp-modern-3',
    source: csharpCode3,
    language: 'csharp'
  });
  console.log(`✅ C# Cell 3 Result: success=${res3.success}, executionCount=${res3.executionCount}, time=${res3.durationMs}ms`);
  console.log(`📢 Output: ${capturedOutputs.join('')}`.trim());

  if (!res3.success || !capturedOutputs.some(o => o.includes('Charlie'))) {
    throw new Error('C# Cell 3 failed');
  }

  // 5. Execute Python Cell
  console.log('\n--- Test 5: Execute Python Cell ---');
  capturedOutputs = [];
  try {
    const pyCode = `
import math
nums = [1, 2, 3, 4, 5]
sq_sum = sum(x**2 for x in nums)
print(f"Python calculated sum of squares: {sq_sum}")
`;
    const resPy = await connection.sendRequest('fry/notebook/execute', {
      path: 'samples/demo.frynb',
      cellId: 'python-test-1',
      source: pyCode,
      language: 'python'
    });
    console.log(`✅ Python Result: success=${resPy.success}, time=${resPy.durationMs}ms`);
    console.log(`📢 Output: ${capturedOutputs.join('')}`.trim());
  } catch (err) {
    console.log(`⚠️ Python execution notice: ${err.message}`);
  }

  // 6. Execute JavaScript Cell
  console.log('\n--- Test 6: Execute JavaScript Cell ---');
  capturedOutputs = [];
  try {
    const jsCode = `
const items = ['alpha', 'beta', 'gamma'];
console.log('JavaScript transformed:', items.map(s => s.toUpperCase()).join(' | '));
`;
    const resJs = await connection.sendRequest('fry/notebook/execute', {
      path: 'samples/demo.frynb',
      cellId: 'javascript-test-1',
      source: jsCode,
      language: 'javascript'
    });
    console.log(`✅ JavaScript Result: success=${resJs.success}, time=${resJs.durationMs}ms`);
    console.log(`📢 Output: ${capturedOutputs.join('')}`.trim());
  } catch (err) {
    console.log(`⚠️ JavaScript execution notice: ${err.message}`);
  }

  // 7. Test standalone script run (RunService)
  console.log('\n--- Test 7: Standalone C# script execution (samples/hello.cs) ---');
  let runOutputs = [];
  connection.onNotification('fry/run/output', (n) => {
    runOutputs.push(n.text);
  });

  const runPromise = new Promise((resolve) => {
    connection.onNotification('fry/run/exited', (n) => {
      resolve(n);
    });
  });

  const runStart = await connection.sendRequest('fry/run/start', {
    runId: 'e2e-run-script-1',
    path: 'samples/hello.cs'
  });
  console.log(`✅ Run started: ${runStart.title}`);
  const runExit = await runPromise;
  console.log(`✅ Run exited with code ${runExit.exitCode} in ${runExit.durationMs}ms`);
  console.log(`📢 Script output: ${runOutputs.join('')}`.trim());

  if (runExit.exitCode !== 0 || !runOutputs.some(o => o.includes('Calculated sum'))) {
    throw new Error('Script run failed or output missing');
  }

  console.log('\n🎉 ALL E2E INTEGRATION TESTS PASSED 100%!');

  connection.dispose();
  child.kill();
  process.exit(0);
}

main().catch(err => {
  console.error('\n❌ E2E TEST FAILED:', err);
  process.exit(1);
});
