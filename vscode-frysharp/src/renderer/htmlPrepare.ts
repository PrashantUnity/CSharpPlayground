// Getting an engine page ready to draw in a notebook: the pieces of text work, apart from the DOM that uses them.

/** The libraries the engine's pages name with `<script data-fry-asset="name"></script>` instead of carrying them. */
export const ASSET_TAG = /<script data-fry-asset="(?<name>[A-Za-z0-9._-]+)"><\/script>/g;

/** Names of the libraries a page asks for, in order. */
export function assetNames(html: string): string[] {
  return [...html.matchAll(ASSET_TAG)].map((match) => match.groups!.name);
}

/** A whole page (an ECharts chart is one) rather than a piece of a page; it is drawn in a frame of its own. */
export function isFullDocument(html: string): boolean {
  return /^\s*(<!doctype\s+html|<html[\s>])/i.test(html);
}

/** Puts each library's text where its tag stands. A name with no text keeps its tag (the page then says the library did not load). */
export function inlineAssets(html: string, texts: ReadonlyMap<string, string>): string {
  return html.replace(ASSET_TAG, (tag, _name, _offset, _whole, groups: { name: string }) => {
    const text = texts.get(groups.name);
    // a script's text cannot contain its own closing tag: ECharts and ECharts GL do not
    return text === undefined ? tag : `<script>${text.replace(/<\/script/gi, '<\\/script')}</script>`;
  });
}

export interface HostTheme {
  theme: 'dark' | 'light';
  background?: string;
}

/** The script that tells a page what the editor looks like (`window.fryHost`), as the studio does before a page's own scripts run. */
export function hostScript(host: HostTheme): string {
  const background = host.background && /^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$/.test(host.background) ? `,background:"${host.background}"` : '';
  return `<script>window.fryHost={theme:"${host.theme}"${background}};</script>`;
}

/** The page with `window.fryHost` set first, whether it is a whole document (after <head>) or a piece. */
export function withHost(html: string, host: HostTheme): string {
  const script = hostScript(host);
  const head = /<head[^>]*>/i.exec(html);
  return head ? html.slice(0, head.index + head[0].length) + script + html.slice(head.index + head[0].length) : script + html;
}

/** The theme VS Code says it has (the output's `data-vscode-theme-kind`), as dark or light. */
export function themeOf(kind: string | undefined): 'dark' | 'light' {
  return kind === 'vscode-light' || kind === 'vscode-high-contrast-light' ? 'light' : 'dark';
}

/** A colour from the editor in the form a page can read (`#rrggbb`), or nothing when it is not a plain hex or rgb colour. */
export function hexColour(value: string | undefined): string | undefined {
  if (!value) return undefined;
  const text = value.trim();
  if (/^#[0-9a-fA-F]{6}$/.test(text)) return text;
  if (/^#[0-9a-fA-F]{3}$/.test(text)) return '#' + [...text.slice(1)].map((c) => c + c).join('');
  const rgb = /^rgba?\(\s*(\d{1,3})[\s,]+(\d{1,3})[\s,]+(\d{1,3})/.exec(text);
  return rgb ? '#' + [rgb[1], rgb[2], rgb[3]].map((n) => Math.min(255, +n).toString(16).padStart(2, '0')).join('') : undefined;
}

/**
 * Adds, at the end of a whole page, a script that tells the frame's parent whether a chart was drawn (the engine's chart pages
 * set `window.__fryChart`): the renderer waits for it, and says so to the extension. `token` says which frame it came from.
 */
export function withProbe(html: string, token: string): string {
  const name = JSON.stringify(token).replace(/</g, '\\u003c'); // a token can neither end its string nor its script
  const probe = `<script>try{parent.postMessage({fryProbe:${name},drawn:!!window.__fryChart},"*")}catch(e){}</script>`;
  const end = html.search(/<\/body\s*>(?![\s\S]*<\/body\s*>)/i);
  return end >= 0 ? html.slice(0, end) + probe + html.slice(end) : html + probe;
}
