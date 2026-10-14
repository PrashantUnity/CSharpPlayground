import { copyButton, el } from './dom';
import { canOpen, InspectorNode, kindOf, startsOpen, toText } from './inspectorModel';

/** The Object Explorer: an object as a tree of its members, each nested object opening where it stands. */
export function renderInspector(node: InspectorNode, root: HTMLElement): void {
  const card = el('div', 'fry fry-card fry-inspector');

  const head = el('div', 'fry-head');
  head.append(el('span', 'fry-mono', '{..}'), el('span', 'fry-title', 'Object Inspector'), el('span', 'fry-spacer'), copyButton('Text', () => toText(node).join('\n')));

  const tree = el('div', 'fry-tree fry-mono', undefined, { role: 'tree' });
  tree.append(nodeHead(node, 0, undefined));
  card.append(head, tree);
  root.replaceChildren(card);
}

// A node's own line (its chip, and a button to open it when it has members) and, under it, its members: made when it first opens.
function nodeHead(node: InspectorNode, depth: number, name: string | undefined): HTMLElement {
  const wrapper = el('div', undefined, undefined, { role: 'none' });
  const line = el('div', depth === 0 ? 'fry-node-head' : 'fry-row', undefined, { role: 'treeitem' });
  const openable = canOpen(node);
  const children = el('div', 'fry-children', undefined, { role: 'group' });
  let built = false;
  let open = openable && startsOpen(depth, node);

  const toggle = openable ? el('button', 'fry-toggle', undefined, { type: 'button' }) : el('span', 'fry-spacer-toggle');
  const chip = el('span', node.circular ? 'fry-chip fry-circular' : 'fry-chip', `${node.circular ? '↻' : '●'} ${node.title}`);
  if (node.circular) chip.title = 'A reference back to an object above: its members are listed again';

  if (name !== undefined) line.append(toggle, el('span', 'fry-name', name), el('span', 'fry-colon', ':'), chip);
  else line.append(toggle, chip);
  wrapper.append(line, children);

  const show = () => {
    if (open && !built) {
      built = true;
      for (const row of node.properties) children.append(member(row, depth + 1));
    }
    children.hidden = !open;
    if (toggle instanceof HTMLButtonElement) {
      toggle.textContent = open ? '▼' : '▶';
      toggle.setAttribute('aria-expanded', String(open));
      toggle.title = open ? 'Collapse' : 'Expand';
    }
    line.setAttribute('aria-expanded', String(open));
  };
  if (toggle instanceof HTMLButtonElement) {
    toggle.addEventListener('click', () => {
      open = !open;
      show();
    });
  }
  show();
  return wrapper;
}

function member(row: { name: string; value?: string; isNull?: boolean; isString?: boolean; child?: InspectorNode | null }, depth: number): HTMLElement {
  if (row.child) return nodeHead(row.child, depth, row.name);

  const line = el('div', 'fry-row', undefined, { role: 'treeitem' });
  const kind = kindOf(row);
  line.append(el('span', 'fry-spacer-toggle'), el('span', 'fry-name', row.name), el('span', 'fry-colon', ':'), el('span', `fry-value fry-v-${kind}`, row.isNull ? '<null>' : (row.value ?? '')));
  return line;
}
