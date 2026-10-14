import {
  CELL_OUTPUT_MIME,
  consoleTextOf,
  kindOf,
  PackedCell,
  PackedOutput,
  TabInfo,
  tabsOf
} from '../shared/outputTabs';
import { ConsoleState, renderConsole } from './console';
import { copyText, el } from './dom';
import { renderBundle } from './render';
import { TableData, toCsv } from './tableModel';

export { CELL_OUTPUT_MIME };

// A glyph for each tab, as the desktop has an icon for each (the notebook's web view has no icon font to draw from).
const ICONS: Record<string, string> = { table: '⊞', console: '›_', inspector: '{ }', image: '▨', chart: '∿', visualizer: '◫', plot3d: '◈', html: '</>', markdown: 'M↓', all: '☰' };

/** What the tabs of one output remember between drawings: the output is drawn again each time more of the cell's output arrives. */
interface TabsState {
  selected?: string;
  collapsed: boolean;
  console: ConsoleState;
  /** The tab the slots below belong to. */
  slotsFor?: string;
  slots: Map<string, HTMLElement>;
  /** Draws that are still going: a newer one replaces them. */
  generation: number;
}

const states = new WeakMap<HTMLElement, TabsState>();

function stateOf(root: HTMLElement): TabsState {
  let state = states.get(root);
  if (!state) {
    state = { collapsed: false, console: { showAll: false }, slots: new Map(), generation: 0 };
    states.set(root, state);
  }
  return state;
}

// What changed in an output, cheaply: a page that did not change is not drawn again (a chart in a frame would start over).
function fingerprint(output: PackedOutput): string {
  let hash = 2166136261;
  const add = (text: string) => {
    for (let i = 0; i < text.length; i++) hash = Math.imul(hash ^ text.charCodeAt(i), 16777619);
  };
  for (const item of output.items) {
    add(item.mime);
    add(item.text ?? item.base64 ?? '');
  }
  return `${output.items.length}:${(hash >>> 0).toString(36)}`;
}

function dataOf(output: PackedOutput): Record<string, unknown> {
  return Object.fromEntries(output.items.map((item) => [item.mime, item.text ?? item.base64 ?? '']));
}

// What the copy button of a tab copies: a console's text, a table as CSV; nothing for a chart.
function copyTextOf(tab: TabInfo, cell: PackedCell): string | undefined {
  if (tab.id === 'console' || tab.id === 'all') {
    const text = cell.outputs.filter((output) => kindOf(output) === 'console').map(consoleTextOf).join('');
    if (text || tab.id === 'console') return text;
  }
  if (tab.id === 'table' || tab.id === 'all') {
    for (const index of tab.outputs) {
      const item = cell.outputs[index].items.find((i) => i.mime === 'application/vnd.fry.table+json');
      if (item?.text && kindOf(cell.outputs[index]) === 'table') {
        try {
          const table = JSON.parse(item.text) as TableData;
          return toCsv(table, table.rows.map((_, row) => row));
        } catch {
          return undefined;
        }
      }
    }
  }
  return undefined;
}

/**
 * A cell's output drawn as tabs, as the desktop app draws it: Table, Console, Chart ... and All, each with how much is in it. What is
 * drawn again while the cell runs keeps the tab, the scrolling and the pages (a chart is not redrawn unless it changed).
 * Returns whether every chart on the selected tab drew.
 */
export async function renderTabs(cell: PackedCell, root: HTMLElement): Promise<{ tabs: string[]; selected: string; chartDrawn?: boolean }> {
  const state = stateOf(root);
  const generation = ++state.generation;
  const tabs = tabsOf(cell);
  if (tabs.length === 0) {
    root.replaceChildren();
    return { tabs: [], selected: '' };
  }

  if (!tabs.some((tab) => tab.id === state.selected)) state.selected = tabs[0].id;
  const selected = tabs.find((tab) => tab.id === state.selected)!;

  // the strip and the bar are made again each time (they hold counts); the body is kept where it can be
  let shell = root.querySelector<HTMLElement>(':scope > .fry-tabs');
  const consoleBox = root.querySelector<HTMLElement>('.fry-console');
  const stuck = consoleBox ? consoleBox.scrollHeight - consoleBox.scrollTop - consoleBox.clientHeight < 8 : true;
  const scrollTop = consoleBox?.scrollTop ?? 0;

  let body = shell?.querySelector<HTMLElement>(':scope > .fry-tabbody') ?? undefined;
  if (!shell || !body) {
    shell = el('div', 'fry fry-card fry-tabs');
    body = el('div', 'fry-tabbody', undefined, { role: 'tabpanel' });
    shell.append(el('div', 'fry-head fry-tabbar'), body);
    root.replaceChildren(shell);
  }

  const bar = shell.querySelector<HTMLElement>(':scope > .fry-tabbar')!;
  bar.replaceChildren(...barOf(tabs, selected, cell, state, root));
  body.hidden = state.collapsed;
  body.setAttribute('aria-labelledby', `fry-tab-${selected.id}`);

  const drawn = await drawBody(body, selected, cell, state, () => void renderTabs(cell, root), generation);

  const after = body.querySelector<HTMLElement>('.fry-console');
  if (after && generation === state.generation) after.scrollTop = stuck ? after.scrollHeight : scrollTop;
  return { tabs: tabs.map((tab) => tab.label), selected: selected.label, chartDrawn: drawn };
}

function barOf(tabs: TabInfo[], selected: TabInfo, cell: PackedCell, state: TabsState, root: HTMLElement): HTMLElement[] {
  const strip = el('div', 'fry-tabstrip', undefined, { role: 'tablist' });
  const choose = (tab: TabInfo) => {
    state.selected = tab.id;
    state.collapsed = false;
    void renderTabs(cell, root).then(() => strip.ownerDocument.getElementById(`fry-tab-${tab.id}`)?.focus());
  };

  tabs.forEach((tab, position) => {
    const button = el('button', `fry-tab${tab === selected ? ' fry-tab-selected' : ''}`, undefined, {
      type: 'button',
      role: 'tab',
      id: `fry-tab-${tab.id}`,
      'aria-selected': String(tab === selected),
      tabindex: tab === selected ? '0' : '-1'
    });
    button.append(el('span', 'fry-tab-icon', ICONS[tab.id] ?? ''), el('span', undefined, tab.label));
    if (tab.count !== undefined) button.append(el('span', 'fry-pill fry-tab-count', tab.count.toLocaleString('en-US')));
    button.addEventListener('click', () => choose(tab));
    button.addEventListener('keydown', (event) => {
      const step = event.key === 'ArrowRight' ? 1 : event.key === 'ArrowLeft' ? -1 : 0;
      if (step === 0) return;
      event.preventDefault();
      choose(tabs[(position + step + tabs.length) % tabs.length]);
    });
    strip.append(button);
  });

  const actions = el('div', 'fry-tabactions');
  const text = copyTextOf(selected, cell);
  if (text !== undefined) {
    const copy = el('button', 'fry-button', 'Copy', { type: 'button', title: selected.id === 'table' ? 'Copy the table as CSV' : 'Copy the text' });
    copy.addEventListener('click', async () => {
      copy.textContent = (await copyText(text)) ? '✓ Copied' : 'Could not copy';
      setTimeout(() => (copy.textContent = 'Copy'), 1500);
    });
    actions.append(copy);
  }

  const fold = el('button', 'fry-button fry-fold', state.collapsed ? '▸' : '▾', { type: 'button', title: state.collapsed ? 'Show the output' : 'Hide the output', 'aria-expanded': String(!state.collapsed) });
  fold.addEventListener('click', () => {
    state.collapsed = !state.collapsed;
    void renderTabs(cell, root);
  });
  actions.append(fold);

  return [strip, el('span', 'fry-spacer'), actions];
}

// The body is a list of slots, one for each thing the tab shows; a slot whose content did not change is left alone.
async function drawBody(body: HTMLElement, tab: TabInfo, cell: PackedCell, state: TabsState, redraw: () => void, generation: number): Promise<boolean | undefined> {
  if (state.slotsFor !== tab.id) {
    state.slots.clear();
    state.slotsFor = tab.id;
    body.replaceChildren();
  }

  interface Wanted {
    key: string;
    draw: (slot: HTMLElement) => Promise<boolean | undefined> | boolean | undefined;
  }
  const wanted: Wanted[] = [];

  if (tab.id === 'console') {
    const outputs = tab.outputs.map((index) => cell.outputs[index]);
    wanted.push({
      key: 'console',
      draw: (slot) => {
        renderConsole(outputs.map(consoleTextOf).join(''), outputs.reduce((sum, output) => sum + (output.omitted ?? 0), 0), slot, state.console, redraw);
        return undefined;
      }
    });
  } else {
    for (const index of tab.outputs) {
      const output = cell.outputs[index];
      if (kindOf(output) === 'console') {
        wanted.push({
          key: `console:${index}`,
          draw: (slot) => {
            renderConsole(consoleTextOf(output), output.omitted ?? 0, slot, state.console, redraw);
            return undefined;
          }
        });
      } else {
        wanted.push({
          key: `${index}:${fingerprint(output)}`,
          draw: async (slot) => {
            const mime = await renderBundle(dataOf(output), slot);
            return mime === 'text/html' && kindOf(output) !== 'html' ? slot.querySelector('iframe') !== null : undefined;
          }
        });
      }
    }
  }

  let chartDrawn: boolean | undefined;
  const keep = new Set(wanted.map((w) => w.key));
  for (const [key, slot] of [...state.slots]) {
    if (!keep.has(key)) {
      slot.remove();
      state.slots.delete(key);
    }
  }

  for (let position = 0; position < wanted.length; position++) {
    const { key, draw } = wanted[position];
    let slot = state.slots.get(key);
    const fresh = !slot;
    if (!slot) {
      slot = el('div', 'fry-slot');
      state.slots.set(key, slot);
    }

    // placed first, drawn after: the order of the tab does not wait for a slow page
    if (body.children[position] !== slot) body.insertBefore(slot, body.children[position] ?? null);
    // a console is drawn again each time (it grew); a page only when it is new
    if (fresh || key.startsWith('console')) {
      const result = await draw(slot);
      if (result !== undefined) chartDrawn = (chartDrawn ?? true) && result;
    }
    if (generation !== state.generation) return chartDrawn; // a newer draw has taken over
  }

  return chartDrawn;
}
