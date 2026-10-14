export const TABLE_MIME = 'application/vnd.fry.table+json';
export const INSPECTOR_MIME = 'application/vnd.fry.inspector+json';

export type Drawing = 'table' | 'inspector' | 'html' | 'markdown' | 'image' | 'text';

/** What the renderer draws a display bundle (MIME type → content) as: the richest of what it holds. */
export function chooseDrawing(mimes: readonly string[]): { drawing: Drawing; mime: string } | undefined {
  const order: Array<[string, Drawing]> = [
    [TABLE_MIME, 'table'],
    [INSPECTOR_MIME, 'inspector'],
    ['text/html', 'html'],
    ['image/svg+xml', 'html'],
    ['image/png', 'image'],
    ['image/jpeg', 'image'],
    ['image/gif', 'image'],
    ['text/markdown', 'markdown'],
    ['text/plain', 'text']
  ];
  for (const [mime, drawing] of order) if (mimes.includes(mime)) return { drawing, mime };
  return undefined;
}
