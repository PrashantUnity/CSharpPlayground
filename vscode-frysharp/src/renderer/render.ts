import { chooseDrawing, INSPECTOR_MIME, TABLE_MIME } from './chooseMime';
import { ensureStyles, el } from './dom';
import { renderHtml } from './html';
import { renderInspector } from './inspector';
import { renderMarkdown } from './markdown';
import { InspectorNode } from './inspectorModel';
import { renderTable } from './table';
import { TableData } from './tableModel';

/** A value of a display bundle as an object: the engine sends the structured types as JSON text or as objects. */
function asObject<T>(value: unknown): T {
  return (typeof value === 'string' ? JSON.parse(value) : value) as T;
}

/** Draws a display bundle (MIME type → content) in an element, as its richest type. Used for a cell's output, and for the Results panel. */
export async function renderBundle(data: Record<string, unknown>, root: HTMLElement): Promise<string | undefined> {
  ensureStyles();
  const chosen = chooseDrawing(Object.keys(data));
  if (!chosen) return undefined;
  const value = data[chosen.mime];

  switch (chosen.drawing) {
    case 'table':
      renderTable(asObject<TableData>(value), root);
      break;
    case 'inspector':
      renderInspector(asObject<InspectorNode>(value), root);
      break;
    case 'html':
      await renderHtml(chosen.mime === 'image/svg+xml' ? `<div>${String(value)}</div>` : String(value), root);
      break;
    case 'markdown':
      renderMarkdown(String(value), root);
      break;
    case 'image': {
      const image = el('img', 'fry', undefined, { alt: 'Output image' });
      image.src = `data:${chosen.mime};base64,${String(value)}`;
      image.style.maxWidth = '100%';
      root.replaceChildren(image);
      break;
    }
    default: {
      const text = el('pre', 'fry fry-mono', String(value));
      text.style.whiteSpace = 'pre-wrap';
      root.replaceChildren(text);
    }
  }
  return chosen.mime;
}

export { INSPECTOR_MIME, TABLE_MIME };
