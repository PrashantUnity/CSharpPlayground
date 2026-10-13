import * as path from 'path';
import * as vscode from 'vscode';
import { VariableDto } from '../sidecar/protocol';

let currentPanel: vscode.WebviewPanel | undefined;

export function showVariablesWebview(
  context: vscode.ExtensionContext,
  notebookPath: string,
  initialVariables: VariableDto[],
  fetchVariables: () => Promise<VariableDto[]>
): void {
  const title = `Variables: ${path.basename(notebookPath)}`;

  if (currentPanel) {
    currentPanel.title = title;
    currentPanel.reveal(vscode.ViewColumn.Beside, true);
    currentPanel.webview.html = renderVariablesHtml(notebookPath, initialVariables);
    return;
  }

  currentPanel = vscode.window.createWebviewPanel(
    'frysharpVariables',
    title,
    { viewColumn: vscode.ViewColumn.Beside, preserveFocus: true },
    {
      enableScripts: true,
      retainContextWhenHidden: true
    }
  );

  currentPanel.webview.html = renderVariablesHtml(notebookPath, initialVariables);

  currentPanel.webview.onDidReceiveMessage(async (message) => {
    if (message.command === 'refresh') {
      try {
        const vars = await fetchVariables();
        if (currentPanel) {
          currentPanel.webview.html = renderVariablesHtml(notebookPath, vars);
        }
      } catch (err: any) {
        vscode.window.showErrorMessage(`Failed to refresh variables: ${err.message}`);
      }
    } else if (message.command === 'copyShare') {
      await vscode.env.clipboard.writeText(message.snippet);
      vscode.window.showInformationMessage(`Copied to clipboard: ${message.snippet}`);
    }
  });

  currentPanel.onDidDispose(() => {
    currentPanel = undefined;
  });
}

function renderVariablesHtml(notebookPath: string, variables: VariableDto[]): string {
  const fileName = path.basename(notebookPath);

  const rows = variables.map((v) => {
    const kernelLower = (v.kernel || '').toLowerCase();
    let badgeClass = 'badge-csharp';
    let kernelName = 'C#';

    if (kernelLower.includes('python')) {
      badgeClass = 'badge-python';
      kernelName = 'Python';
    } else if (kernelLower.includes('node') || kernelLower.includes('js') || kernelLower.includes('javascript')) {
      badgeClass = 'badge-js';
      kernelName = 'Node.js';
    }

    const shareDirective = `#!share --from ${kernelName.toLowerCase() === 'python' ? 'python' : kernelName.toLowerCase() === 'node.js' ? 'javascript' : 'csharp'} ${v.name}`;

    return `
      <tr>
        <td class="col-name"><code>${escapeHtml(v.name)}</code></td>
        <td class="col-kernel"><span class="badge ${badgeClass}">${escapeHtml(kernelName)}</span></td>
        <td class="col-type"><code>${escapeHtml(v.type)}</code></td>
        <td class="col-value"><span class="val-preview" title="${escapeHtml(v.value)}">${escapeHtml(v.value)}</span></td>
        <td class="col-action">
          <button class="btn-copy" onclick="copyShare('${escapeAttr(shareDirective)}')" title="Copy '#!share' directive">
            #!share
          </button>
        </td>
      </tr>
    `;
  }).join('');

  const emptyState = `
    <div class="empty-state">
      <div class="empty-icon">🪐</div>
      <div class="empty-title">No Active Variables in Memory</div>
      <div class="empty-desc">Execute code cells in C#, Python, or JavaScript to populate session variables.</div>
    </div>
  `;

  return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>FrySharp Variables</title>
  <style>
    :root {
      --bg: var(--vscode-editor-background, #1e1e1e);
      --fg: var(--vscode-editor-foreground, #d4d4d4);
      --card-bg: var(--vscode-sideBar-background, #252526);
      --border: var(--vscode-panel-border, #333333);
      --accent: var(--vscode-button-background, #0e639c);
      --accent-hover: var(--vscode-button-hoverBackground, #1177bb);
    }
    body {
      background-color: var(--bg);
      color: var(--fg);
      font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
      margin: 0;
      padding: 16px;
      font-size: 13px;
    }
    .header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 14px;
      padding-bottom: 10px;
      border-bottom: 1px solid var(--border);
    }
    .header h2 {
      margin: 0;
      font-size: 15px;
      font-weight: 600;
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .btn-refresh {
      background: var(--accent);
      color: #ffffff;
      border: none;
      padding: 5px 12px;
      border-radius: 4px;
      cursor: pointer;
      font-size: 12px;
      font-weight: 500;
      display: inline-flex;
      align-items: center;
      gap: 6px;
      transition: background 0.15s ease;
    }
    .btn-refresh:hover {
      background: var(--accent-hover);
    }
    .tbl-wrap {
      border: 1px solid var(--border);
      border-radius: 6px;
      overflow: hidden;
      background: var(--card-bg);
    }
    table {
      width: 100%;
      border-collapse: collapse;
      text-align: left;
    }
    th {
      background: rgba(255, 255, 255, 0.04);
      color: var(--fg);
      padding: 8px 12px;
      font-size: 11px;
      text-transform: uppercase;
      letter-spacing: 0.5px;
      font-weight: 600;
      border-bottom: 1px solid var(--border);
    }
    td {
      padding: 8px 12px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.04);
      vertical-align: middle;
    }
    tr:last-child td {
      border-bottom: none;
    }
    tr:hover td {
      background: rgba(255, 255, 255, 0.02);
    }
    code {
      font-family: 'Cascadia Code', Consolas, 'Courier New', monospace;
      font-size: 12px;
    }
    .badge {
      display: inline-block;
      padding: 2px 7px;
      border-radius: 12px;
      font-size: 11px;
      font-weight: 600;
      letter-spacing: 0.2px;
    }
    .badge-csharp {
      background: rgba(88, 166, 255, 0.15);
      color: #58a6ff;
      border: 1px solid rgba(88, 166, 255, 0.3);
    }
    .badge-python {
      background: rgba(63, 185, 80, 0.15);
      color: #3fb950;
      border: 1px solid rgba(63, 185, 80, 0.3);
    }
    .badge-js {
      background: rgba(210, 153, 34, 0.15);
      color: #d29922;
      border: 1px solid rgba(210, 153, 34, 0.3);
    }
    .val-preview {
      display: inline-block;
      max-width: 220px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
      color: #a5d6ff;
    }
    .btn-copy {
      background: rgba(255, 255, 255, 0.06);
      border: 1px solid var(--border);
      color: var(--fg);
      padding: 3px 8px;
      border-radius: 4px;
      font-size: 11px;
      font-family: monospace;
      cursor: pointer;
      transition: all 0.15s ease;
    }
    .btn-copy:hover {
      background: var(--accent);
      color: #ffffff;
      border-color: var(--accent);
    }
    .empty-state {
      padding: 40px 20px;
      text-align: center;
      color: #8b949e;
    }
    .empty-icon {
      font-size: 32px;
      margin-bottom: 8px;
    }
    .empty-title {
      font-size: 14px;
      font-weight: 600;
      margin-bottom: 4px;
      color: var(--fg);
    }
    .empty-desc {
      font-size: 12px;
    }
  </style>
</head>
<body>
  <div class="header">
    <h2>🪐 Polyglot Variables <span>&bull; ${escapeHtml(fileName)}</span></h2>
    <button class="btn-refresh" onclick="refresh()">↻ Refresh</button>
  </div>

  ${variables.length > 0 ? `
  <div class="tbl-wrap">
    <table>
      <thead>
        <tr>
          <th>Name</th>
          <th>Kernel</th>
          <th>Type</th>
          <th>Value</th>
          <th>Share</th>
        </tr>
      </thead>
      <tbody>
        ${rows}
      </tbody>
    </table>
  </div>
  ` : emptyState}

  <script>
    const vscode = acquireVsCodeApi();
    function refresh() {
      vscode.postMessage({ command: 'refresh' });
    }
    function copyShare(snippet) {
      vscode.postMessage({ command: 'copyShare', snippet: snippet });
    }
  </script>
</body>
</html>`;
}

function escapeHtml(text: string): string {
  return String(text || '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

function escapeAttr(text: string): string {
  return String(text || '')
    .replace(/"/g, '&quot;')
    .replace(/'/g, "\\'");
}
