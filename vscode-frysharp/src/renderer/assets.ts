/** The libraries the engine's pages name (`data-fry-asset`), shipped with the extension so they work offline. */
const KNOWN = new Set(['echarts.min.js', 'echarts-gl.min.js']);
const loading = new Map<string, Promise<string | undefined>>();

/** The text of a library, fetched once from beside this module (dist/assets); undefined if it is not one of ours or cannot be read. */
export function loadAsset(name: string): Promise<string | undefined> {
  if (!KNOWN.has(name)) return Promise.resolve(undefined);
  let text = loading.get(name);
  if (!text) {
    text = fetch(new URL(`./assets/${name}`, import.meta.url))
      .then((response) => (response.ok ? response.text() : undefined))
      .catch(() => undefined);
    loading.set(name, text);
  }
  return text;
}
