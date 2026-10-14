// Builds dist/: the extension (extension.js, one file with vscode-jsonrpc inside, so an installed extension starts by
// reading one file and ships no node_modules) and the output renderer (renderer.js, an ES module the notebook loads in its
// own web view) with the chart libraries it hands to the engine's pages (assets/). `node esbuild.js --production` is what
// the package build runs.
const esbuild = require('esbuild');
const fs = require('fs');
const path = require('path');

const production = process.argv.includes('--production');
const watch = process.argv.includes('--watch');

// The libraries the engine's pages name with data-fry-asset: the same files the desktop app embeds.
const assetsFrom = path.resolve(__dirname, '..', 'FrySharp.Core', 'Assets', 'echarts');

function copyAssets() {
  const to = path.join(__dirname, 'dist', 'assets');
  fs.mkdirSync(to, { recursive: true });
  for (const file of fs.readdirSync(assetsFrom).filter((name) => name.endsWith('.js'))) {
    fs.copyFileSync(path.join(assetsFrom, file), path.join(to, file));
  }
}

const shared = { bundle: true, minify: production, sourcemap: !production, sourcesContent: false, logLevel: 'info' };

async function main() {
  const contexts = await Promise.all([
    esbuild.context({ ...shared, entryPoints: ['src/extension.ts'], format: 'cjs', platform: 'node', target: 'node20', outfile: 'dist/extension.js', external: ['vscode'] }),
    esbuild.context({ ...shared, entryPoints: ['src/renderer/index.ts'], format: 'esm', platform: 'browser', target: 'es2022', outfile: 'dist/renderer.js', tsconfig: 'src/renderer/tsconfig.json' })
  ]);
  copyAssets();

  if (watch) {
    await Promise.all(contexts.map((context) => context.watch()));
  } else {
    await Promise.all(contexts.map((context) => context.rebuild()));
    await Promise.all(contexts.map((context) => context.dispose()));
  }
}

main().catch((error) => {
  console.error(error);
  process.exit(1);
});
