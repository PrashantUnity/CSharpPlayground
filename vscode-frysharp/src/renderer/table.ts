import { copyButton, el } from './dom';
import { footer, nextSort, Sort, TableData, textOf, toCsv, toJson, visibleRows } from './tableModel';

/** The table: sorted by a click on a column, filtered as you type, a row opening to all its columns, copied as CSV or JSON. */
export function renderTable(table: TableData, root: HTMLElement): void {
  const card = el('div', 'fry fry-card fry-table');
  let filter = '';
  let sort: Sort | undefined;
  let shown: number[] = [];
  const open = new Set<number>(); // rows opened to their detail, by row index: kept through a sort or a filter

  // the bar: title, size, filter, copy
  const head = el('div', 'fry-head');
  const columns = table.columns.length;
  const search = el('input', 'fry-filter', undefined, { type: 'search', placeholder: 'Filter rows…', 'aria-label': 'Filter rows' });
  head.append(
    el('span', 'fry-title', table.title || 'Table'),
    el('span', 'fry-pill', `${(table.totalRows ?? table.rows.length).toLocaleString('en-US')} rows × ${(table.totalColumns ?? columns).toLocaleString('en-US')} cols`),
    el('span', 'fry-spacer'),
    search,
    copyButton('CSV', () => toCsv(table, shown)),
    copyButton('JSON', () => toJson(table, shown))
  );

  // the grid
  const scroll = el('div', 'fry-scroll');
  const grid = el('table', undefined, undefined, { role: 'grid' });
  const headRow = el('tr');
  headRow.append(el('th', 'fry-expander'));
  const arrows: HTMLElement[] = [];
  table.columns.forEach((name, column) => {
    const th = el('th', `fry-sortable${table.numeric?.[column] ? ' fry-num' : ''}`, undefined, { tabindex: '0', title: `Sort by ${name}` });
    const arrow = el('span', 'fry-arrow', '↕');
    arrows.push(arrow);
    th.append(name, arrow);
    const sortBy = () => {
      sort = nextSort(sort, column);
      draw();
    };
    th.addEventListener('click', sortBy);
    th.addEventListener('keydown', (event) => {
      if (event.key === 'Enter' || event.key === ' ') {
        event.preventDefault();
        sortBy();
      }
    });
    headRow.append(th);
  });
  const thead = el('thead');
  thead.append(headRow);
  const tbody = el('tbody');
  grid.append(thead, tbody);
  scroll.append(grid);

  const foot = el('div', 'fry-foot');
  card.append(head, scroll, foot);
  root.replaceChildren(card);

  function cell(value: string | number | boolean | null, numeric: boolean): HTMLElement {
    const td = el('td');
    if (value === null || value === undefined) {
      td.className = 'fry-v-null';
      td.textContent = 'null';
    } else {
      const text = textOf(value);
      td.textContent = text;
      if (numeric) td.className = 'fry-num fry-v-number';
      else if (/^(true|false)$/i.test(text)) td.className = 'fry-v-boolean';
    }
    return td;
  }

  function draw(): void {
    shown = visibleRows(table, filter, sort);
    const rows = document.createDocumentFragment();
    for (const index of shown) {
      const row = table.rows[index];
      const tr = el('tr', 'fry-data');
      const expander = el('td', 'fry-expander');
      const toggle = el('button', 'fry-toggle', open.has(index) ? '▼' : '▶', { type: 'button', 'aria-expanded': String(open.has(index)), title: 'Show every column of this row' });
      toggle.addEventListener('click', () => {
        if (!open.delete(index)) open.add(index);
        draw();
      });
      expander.append(toggle);
      tr.append(expander);
      table.columns.forEach((_, column) => tr.append(cell(row[column] ?? null, table.numeric?.[column] === true)));
      rows.append(tr);

      if (open.has(index)) {
        const detail = el('tr', 'fry-detail');
        const td = el('td', undefined, undefined, { colspan: String(columns + 1) });
        const gridOfValues = el('div', 'fry-grid');
        table.columns.forEach((name, column) => {
          const item = el('div');
          const value = row[column] ?? null;
          item.append(el('div', 'fry-key', name), el('div', value === null ? 'fry-v-null' : 'fry-value', value === null ? 'null' : textOf(value)));
          gridOfValues.append(item);
        });
        td.append(gridOfValues);
        detail.append(td);
        rows.append(detail);
      }
    }
    tbody.replaceChildren(rows);
    if (shown.length === 0) {
      const none = el('tr');
      none.append(el('td', 'fry-empty', filter ? 'No rows match the filter.' : 'No rows.', { colspan: String(columns + 1) }));
      tbody.append(none);
    }

    headRow.querySelectorAll('th.fry-sortable').forEach((th, column) => {
      const active = sort?.column === column;
      if (active) th.setAttribute('aria-sort', sort!.direction === 'asc' ? 'ascending' : 'descending');
      else th.removeAttribute('aria-sort');
      arrows[column].textContent = active ? (sort!.direction === 'asc' ? '▲' : '▼') : '↕';
    });
    foot.textContent = footer(table, shown.length, filter.trim() !== '');
  }

  search.addEventListener('input', () => {
    filter = search.value;
    draw();
  });
  draw();
}
