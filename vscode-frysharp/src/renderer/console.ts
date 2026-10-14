import { AnsiSpan, applyCarriageReturns, countLines, parseAnsi } from '../shared/outputTabs';
import { el } from './dom';

/** A console longer than this shows its last lines, with a button for the rest: a flood of lines would make the page slow for nothing. */
export const MAX_CONSOLE_LINES = 5000;

function spanElement(span: AnsiSpan): HTMLElement | Text {
  if (!span.fg && !span.bg && !span.bold && !span.dim && !span.italic && !span.underline) return document.createTextNode(span.text);
  const element = el('span', undefined, span.text);
  if (span.fg) element.style.color = span.fg;
  if (span.bg) element.style.backgroundColor = span.bg;
  if (span.bold) element.style.fontWeight = '700';
  if (span.dim) element.style.opacity = '0.7';
  if (span.italic) element.style.fontStyle = 'italic';
  if (span.underline) element.style.textDecoration = 'underline';
  return element;
}

export interface ConsoleState {
  /** The person asked for every line of a long console. */
  showAll: boolean;
}

/**
 * What a cell printed, as a terminal shows it: colours and styles from ANSI codes, a progress bar redrawn in place. `omitted` is the
 * number of lines left off the start by the sender (while a cell runs), `state` remembers a click on "Show all lines".
 */
export function renderConsole(text: string, omitted: number, root: HTMLElement, state: ConsoleState, redraw: () => void): void {
  const box = el('div', 'fry-console fry-mono');
  const screen = applyCarriageReturns(text);
  const total = countLines(screen);
  let shown = screen;
  let hidden = 0;

  if (!state.showAll && total > MAX_CONSOLE_LINES) {
    const lines = screen.split('\n');
    const trailing = screen.endsWith('\n');
    const count = trailing ? lines.length - 1 : lines.length;
    shown = lines.slice(count - MAX_CONSOLE_LINES, trailing ? count : undefined).join('\n') + (trailing ? '\n' : '');
    hidden = count - MAX_CONSOLE_LINES;
  }

  if (omitted > 0) box.append(el('div', 'fry-note', `… ${omitted.toLocaleString('en-US')} earlier lines are shown when the cell has finished`));
  if (hidden > 0) {
    const more = el('button', 'fry-button fry-more', `Show all ${total.toLocaleString('en-US')} lines (${hidden.toLocaleString('en-US')} more at the start)`, { type: 'button' });
    more.addEventListener('click', () => {
      state.showAll = true;
      redraw();
    });
    box.append(more);
  }

  const pre = el('pre', 'fry-pre');
  const spans = parseAnsi(shown);
  if (spans.length === 0) pre.append(document.createTextNode(''));
  for (const span of spans) pre.append(spanElement(span));
  box.append(pre);
  root.replaceChildren(box);
}
