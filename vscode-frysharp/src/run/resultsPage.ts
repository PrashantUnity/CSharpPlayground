/** Something a program showed that is not text: a table, a chart, an image (a display bundle, as a cell's output is). */
export interface ResultItem {
  id: number;
  /** What ran, and when: "hello.py, 12:03:41". */
  title: string;
  /** MIME type → content: the renderer draws the richest. */
  data: Record<string, unknown>;
}

export function itemFromDisplay(id: number, title: string, data: Record<string, unknown>): ResultItem {
  return { id, title, data };
}

/** JSON for the inside of a script element: a `<` can never close it. */
export function jsonForScript(value: unknown): string {
  return JSON.stringify(value).replace(/</g, '\\u003c').replace(/\u2028/g, '\\u2028').replace(/\u2029/g, '\\u2029');
}

export interface PageSources {
  /** Where the web view can load the output renderer (dist/renderer.js) from. */
  rendererUri: string;
  /** What the web view may load scripts and data from (`webview.cspSource`). */
  cspSource: string;
}

/**
 * The page of the Results panel. Each item is drawn by the same renderer a notebook's outputs are, so a table, an object or a
 * chart looks the same there and here; items are added and cleared by messages, so a table someone has sorted is not redrawn
 * when the next one arrives. What each draws is told back (`rendered` or `error`) so it can be seen to work.
 */
export function resultsHtml(items: ResultItem[], sources: PageSources): string {
  // 'unsafe-inline' scripts: the engine's pages (a chart's) run their own inline script in a sandboxed frame, which has this page's policy
  return `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src data: ${sources.cspSource}; style-src ${sources.cspSource} 'unsafe-inline'; script-src ${sources.cspSource} 'unsafe-inline'; connect-src ${sources.cspSource}; frame-src about:;">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<style>
  body { padding: 0 8px 16px; color: var(--vscode-foreground); font-family: var(--vscode-font-family); font-size: var(--vscode-font-size); }
  .empty { opacity: .7; padding: 16px 4px; }
  .item { margin: 10px 0 18px; }
  .title { font-size: 11px; opacity: .7; margin: 0 0 4px; }
</style>
</head>
<body>
<div id="empty" class="empty">What a program shows (a table, an object, a chart, an image) appears here. Text is in the terminal.</div>
<div id="items"></div>
<script type="module">
  import { renderBundle } from ${jsonForScript(sources.rendererUri)};
  const vscode = acquireVsCodeApi();
  const items = document.getElementById('items');
  const empty = document.getElementById('empty');
  async function add(item) {
    empty.style.display = 'none';
    const box = document.createElement('div');
    box.className = 'item';
    const title = document.createElement('div');
    title.className = 'title';
    title.textContent = item.title;
    const slot = document.createElement('div');
    box.append(title, slot);
    items.appendChild(box);
    box.scrollIntoView({ block: 'nearest' });
    try {
      const mime = await renderBundle(item.data, slot);
      vscode.postMessage({ type: 'rendered', id: item.id, mime });
    } catch (error) {
      slot.textContent = 'This could not be drawn: ' + (error && error.message ? error.message : String(error));
      vscode.postMessage({ type: 'error', id: item.id, message: String(error && error.message || error) });
    }
  }
  function clearAll() { items.textContent = ''; empty.style.display = ''; }
  window.addEventListener('message', (event) => {
    if (event.data.type === 'add') add(event.data.item);
    else if (event.data.type === 'clear') clearAll();
  });
  for (const item of ${jsonForScript(items)}) await add(item);
</script>
</body>
</html>`;
}
