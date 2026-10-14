import { CSS, STYLE_ID } from './styles';

/** Makes an element: its class, text and attributes. */
export function el<K extends keyof HTMLElementTagNameMap>(tag: K, className?: string, text?: string, attributes?: Record<string, string>): HTMLElementTagNameMap[K] {
  const element = document.createElement(tag);
  if (className) element.className = className;
  if (text !== undefined) element.textContent = text;
  if (attributes) for (const [name, value] of Object.entries(attributes)) element.setAttribute(name, value);
  return element;
}

/** Puts the renderer's styles in the page, once. */
export function ensureStyles(): void {
  if (document.getElementById(STYLE_ID)) return;
  const style = el('style');
  style.id = STYLE_ID;
  style.textContent = CSS;
  document.head.appendChild(style);
}

/** Puts text on the clipboard: the clipboard API where the page may use it, else the old way. Says whether it worked. */
export async function copyText(text: string): Promise<boolean> {
  try {
    await navigator.clipboard.writeText(text);
    return true;
  } catch {
    const area = el('textarea');
    area.value = text;
    area.style.cssText = 'position:fixed;opacity:0;pointer-events:none';
    document.body.appendChild(area);
    area.select();
    try {
      return document.execCommand('copy');
    } finally {
      area.remove();
    }
  }
}

/** A button that copies something, and says so for a moment. */
export function copyButton(label: string, text: () => string): HTMLButtonElement {
  const button = el('button', 'fry-button', label, { type: 'button', title: `Copy as ${label}` });
  button.addEventListener('click', async () => {
    const worked = await copyText(text());
    button.textContent = worked ? '✓ Copied' : 'Could not copy';
    setTimeout(() => (button.textContent = label), 1500);
  });
  return button;
}
