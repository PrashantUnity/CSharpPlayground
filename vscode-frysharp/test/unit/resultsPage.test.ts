import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { itemFromDisplay, jsonForScript, resultsHtml } from '../../src/run/resultsPage';

const sources = { rendererUri: 'https://file+.vscode-resource.test/dist/renderer.js', cspSource: 'https://file+.vscode-resource.test' };

describe('what the Results panel keeps of a display', () => {
  it('keeps the whole bundle, for the renderer to choose from, and the title it was given', () => {
    const item = itemFromDisplay(7, 'hello.py, 12:03:41', { 'text/html': '<b>h</b>', 'text/plain': 'x' });
    assert.deepStrictEqual(item, { id: 7, title: 'hello.py, 12:03:41', data: { 'text/html': '<b>h</b>', 'text/plain': 'x' } });
  });
});

describe('the page of the Results panel', () => {
  it('loads scripts and data only from the extension, and lets a chart\'s own page run in a frame with no access to the panel', () => {
    const page = resultsHtml([], sources);

    assert.match(page, /script-src https:\/\/file\+\.vscode-resource\.test 'unsafe-inline'/);
    assert.match(page, /connect-src https:\/\/file\+\.vscode-resource\.test/);
    assert.match(page, /default-src 'none'/);
    assert.match(page, /frame-src about:/);
    assert.match(page, /import \{ renderBundle \} from "https:\/\/file\+\.vscode-resource\.test\/dist\/renderer\.js"/);
  });

  it('carries the items already shown, and nothing in them can end the script that carries them', () => {
    const page = resultsHtml([{ id: 1, title: 'x</script><script>alert(1)</script>', data: { 'text/html': '</script><img src=x onerror=alert(2)>' } }], sources);

    assert.ok(!page.includes('</script><script>alert'));
    assert.ok(!page.includes('</script><img'));
    const json = jsonForScript([{ t: '</script>' }]);
    assert.ok(!json.includes('<'));
    assert.deepStrictEqual(JSON.parse(json), [{ t: '</script>' }]);
  });

  it('writes the title as text, not as markup, and says back what it drew', () => {
    const page = resultsHtml([], sources);
    assert.match(page, /title\.textContent = item\.title/);
    assert.match(page, /postMessage\(\{ type: 'rendered'/);
    assert.match(page, /postMessage\(\{ type: 'error'/);
  });
});
