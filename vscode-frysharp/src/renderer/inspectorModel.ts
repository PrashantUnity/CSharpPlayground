// The Object Explorer apart from drawing it: what kind of value a row has (it is coloured by kind), and which nodes start open.

/** An Object Explorer node as the engine sends it (application/vnd.fry.inspector+json). */
export interface InspectorNode {
  title: string;
  circular?: boolean;
  properties: InspectorRow[];
}

export interface InspectorRow {
  name: string;
  value?: string;
  isNull?: boolean;
  isString?: boolean;
  child?: InspectorNode | null;
}

export type ValueKind = 'null' | 'string' | 'number' | 'boolean' | 'list' | 'plain';

/** The kind of a row's value, from what the engine says about it and what the text is. */
export function kindOf(row: Pick<InspectorRow, 'value' | 'isNull' | 'isString'>): ValueKind {
  if (row.isNull) return 'null';
  if (row.isString) return 'string';
  const text = row.value ?? '';
  if (/^(true|false)$/i.test(text)) return 'boolean';
  if (/^[-+]?(\d[\d,]*\.?\d*|\.\d+)(e[-+]?\d+)?$/i.test(text) || text === 'NaN' || text === '∞' || text === '-∞') return 'number';
  if (text.startsWith('[') && text.endsWith(']')) return 'list';
  return 'plain';
}

/** The top node is open, and the ones just below it unless they close a cycle; deeper ones start closed (as in the desktop). */
export function startsOpen(depth: number, node: Pick<InspectorNode, 'circular'>): boolean {
  return depth === 0 || (!node.circular && depth < 2);
}

/** Whether a node has anything to open. A reference back to an object above lists its members again (down to the engine's depth limit), as the desktop does. */
export function canOpen(node: Pick<InspectorNode, 'properties'>): boolean {
  return node.properties.length > 0;
}

/** The text of a node and what is under it, as lines (for Copy). */
export function toText(node: InspectorNode, indent = 0): string[] {
  const lines = [`${' '.repeat(indent)}${node.title}${node.circular ? ' (circular reference)' : ''}`];
  for (const row of node.properties) {
    if (row.child) {
      lines.push(`${' '.repeat(indent + 2)}${row.name}:`, ...toText(row.child, indent + 4));
    } else {
      lines.push(`${' '.repeat(indent + 2)}${row.name} = ${row.isNull ? '<null>' : (row.value ?? '')}`);
    }
  }
  return lines;
}
