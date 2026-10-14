// What a table does, apart from drawing it: which rows show, in what order, and the text of the table as CSV or JSON.

/** A table as the engine sends it (application/vnd.fry.table+json). */
export interface TableData {
  title?: string;
  columns: string[];
  numeric?: boolean[];
  rows: Array<Array<string | number | boolean | null>>;
  totalRows?: number;
  totalColumns?: number;
  label?: string;
}

export type SortDirection = 'asc' | 'desc';

export interface Sort {
  column: number;
  direction: SortDirection;
}

/** A number in a cell's text, as the engine formats it: "1,234.5", "-3", "12 %" are numbers; "n/a" is not. */
export function numberOf(cell: string | number | boolean | null): number | undefined {
  if (typeof cell === 'number') return Number.isFinite(cell) ? cell : undefined;
  if (typeof cell !== 'string') return undefined;
  const cleaned = cell.trim().replace(/[,\s%$€£]/g, '');
  if (cleaned === '' || !/^[-+]?(\d+\.?\d*|\.\d+)(e[-+]?\d+)?$/i.test(cleaned)) return undefined;
  return parseFloat(cleaned);
}

export const textOf = (cell: string | number | boolean | null): string => (cell === null || cell === undefined ? '' : String(cell));

/** The rows to show, as indexes into the table's rows: those with the filter's text in any cell, in the order asked for. */
export function visibleRows(table: TableData, filter: string, sort?: Sort): number[] {
  const needle = filter.trim().toLowerCase();
  let indexes = table.rows.map((_, index) => index);
  if (needle) {
    indexes = indexes.filter((index) => table.rows[index].some((cell) => textOf(cell).toLowerCase().includes(needle)));
  }
  if (!sort) return indexes;

  const numeric = table.numeric?.[sort.column] === true;
  const sign = sort.direction === 'asc' ? 1 : -1;
  const keyed = indexes.map((index) => ({ index, cell: table.rows[index][sort.column] ?? null }));
  keyed.sort((a, b) => {
    // an empty cell goes last, whichever way the column is sorted
    const aEmpty = a.cell === null || textOf(a.cell) === '';
    const bEmpty = b.cell === null || textOf(b.cell) === '';
    if (aEmpty || bEmpty) return aEmpty === bEmpty ? a.index - b.index : aEmpty ? 1 : -1;

    if (numeric) {
      const an = numberOf(a.cell);
      const bn = numberOf(b.cell);
      // in a column of numbers, what is not one ("n/a") comes after the numbers, whichever way they are sorted
      if (an !== undefined && bn !== undefined) {
        return an === bn ? a.index - b.index : sign * (an - bn);
      }
      if (an !== undefined || bn !== undefined) return an !== undefined ? -1 : 1;
    }
    const order = textOf(a.cell).localeCompare(textOf(b.cell), undefined, { numeric: true, sensitivity: 'base' });
    return order === 0 ? a.index - b.index : sign * order;
  });
  return keyed.map((entry) => entry.index);
}

/** Clicking a column's header: ascending, then descending, then as it came. */
export function nextSort(current: Sort | undefined, column: number): Sort | undefined {
  if (!current || current.column !== column) return { column, direction: 'asc' };
  return current.direction === 'asc' ? { column, direction: 'desc' } : undefined;
}

const csvCell = (cell: string | number | boolean | null): string => {
  const text = textOf(cell);
  return /[",\r\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
};

/** The rows (those shown, in the order shown) as CSV, with the column names first. */
export function toCsv(table: TableData, indexes: number[]): string {
  return [table.columns.map(csvCell).join(','), ...indexes.map((index) => table.rows[index].map(csvCell).join(','))].join('\n');
}

/** The rows as JSON: a list of objects named by the columns (a number stays a number where the column says it is one). */
export function toJson(table: TableData, indexes: number[]): string {
  const records = indexes.map((index) => {
    const record: Record<string, unknown> = {};
    table.columns.forEach((column, i) => {
      const cell = table.rows[index][i] ?? null;
      record[column] = table.numeric?.[i] === true && numberOf(cell) !== undefined ? numberOf(cell) : cell;
    });
    return record;
  });
  return JSON.stringify(records, null, 2);
}

/** What the footer says about the rows: all of them, those a filter leaves, or the first rows of a longer table. */
export function footer(table: TableData, shown: number, filtered: boolean): string {
  const total = table.totalRows ?? table.rows.length;
  if (filtered) return `Showing ${shown.toLocaleString('en-US')} of ${table.rows.length.toLocaleString('en-US')} rows (filtered)`;
  if (total > table.rows.length) return `Showing the first ${table.rows.length.toLocaleString('en-US')} of ${total.toLocaleString('en-US')} rows`;
  return `Showing ${shown.toLocaleString('en-US')} of ${table.rows.length.toLocaleString('en-US')} rows`;
}
