import { loadAsset } from './assets';
import { el } from './dom';
import { assetNames, hexColour, HostTheme, inlineAssets, isFullDocument, themeOf, withHost, withProbe } from './htmlPrepare';

function hostTheme(): HostTheme {
  const kind = document.body?.dataset.vscodeThemeKind;
  const background = hexColour(getComputedStyle(document.documentElement).getPropertyValue('--vscode-editor-background'));
  return { theme: themeOf(kind), background };
}

/**
 * HTML an engine wrote. A whole page (a chart is one, built to fill its own window) is drawn in a frame with no access to
 * the notebook, its libraries written into it and told what the editor looks like; a piece of a page (a visual's SVG and its
 * script) is put where it goes, with its scripts run, in order.
 */
export async function renderHtml(html: string, root: HTMLElement): Promise<{ chartDrawn?: boolean }> {
  const texts = new Map<string, string>();
  await Promise.all(
    assetNames(html).map(async (name) => {
      const text = await loadAsset(name);
      if (text !== undefined) texts.set(name, text);
    })
  );

  const host = hostTheme();
  if (isFullDocument(html)) {
    const frame = el('iframe', 'fry-frame', undefined, { sandbox: 'allow-scripts', title: 'Chart' });
    const token = Math.random().toString(36).slice(2);
    const drawn = chartDrawn(frame, token);
    frame.srcdoc = withProbe(withHost(inlineAssets(html, texts), host), token);
    root.replaceChildren(frame);
    return { chartDrawn: await drawn };
  }

  (window as unknown as { fryHost?: HostTheme }).fryHost = host;
  const template = document.createElement('template');
  template.innerHTML = html;
  const wrapper = el('div', 'fry');
  wrapper.append(template.content);
  root.replaceChildren(wrapper);
  await runScripts(wrapper, texts);
  return {};
}

// The page in the frame says, once it has run, whether it drew a chart; false if it says nothing in a few seconds.
function chartDrawn(frame: HTMLIFrameElement, token: string): Promise<boolean> {
  return new Promise((resolve) => {
    const finish = (result: boolean) => {
      window.removeEventListener('message', onMessage);
      clearTimeout(timer);
      resolve(result);
    };
    const onMessage = (event: MessageEvent) => {
      if (event.source === frame.contentWindow && event.data?.fryProbe === token) finish(event.data.drawn === true);
    };
    const timer = setTimeout(() => finish(false), 5000);
    window.addEventListener('message', onMessage);
  });
}

// Scripts put in a page by innerHTML do not run: make each one again, in order, waiting for those that load a file.
async function runScripts(container: HTMLElement, texts: ReadonlyMap<string, string>): Promise<void> {
  for (const old of Array.from(container.querySelectorAll('script'))) {
    const asset = old.getAttribute('data-fry-asset');
    const made = document.createElement('script');
    for (const attribute of Array.from(old.attributes)) made.setAttribute(attribute.name, attribute.value);

    if (asset !== null) {
      const text = texts.get(asset);
      if (text === undefined) continue; // the page says the library did not load
      made.textContent = text;
    } else {
      made.textContent = old.textContent;
    }

    const loaded = made.src ? new Promise<void>((resolve) => ((made.onload = () => resolve()), (made.onerror = () => resolve()))) : undefined;
    old.replaceWith(made);
    if (loaded) await loaded;
  }
}
