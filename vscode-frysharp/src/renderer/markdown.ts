import { el } from './dom';

// The Markdown an engine sends (Display.Markdown): headings, paragraphs, lists, quotes, code, emphasis, links. It is built with DOM
// calls and never as HTML text, and a link goes only where http, https and mailto do, so nothing a program prints can run in the page.

const SAFE_LINK = /^(https?:|mailto:)/i;

// `code`, **bold**, *italic* / _italic_, [text](url) inside a line
function inline(text: string, into: HTMLElement): void {
  const pattern = /`([^`]+)`|\*\*([^*]+)\*\*|__([^_]+)__|\*([^*\s][^*]*)\*|(?<![\w])_([^_\s][^_]*)_(?![\w])|\[([^\]]+)\]\(([^)\s]+)\)/g;
  let last = 0;
  for (let match = pattern.exec(text); match; match = pattern.exec(text)) {
    if (match.index > last) into.append(text.slice(last, match.index));
    if (match[1] !== undefined) into.append(el('code', 'fry-mono', match[1]));
    else if (match[2] !== undefined || match[3] !== undefined) into.append(el('strong', undefined, match[2] ?? match[3]));
    else if (match[4] !== undefined || match[5] !== undefined) into.append(el('em', undefined, match[4] ?? match[5]));
    else if (match[6] !== undefined) {
      if (SAFE_LINK.test(match[7])) into.append(el('a', undefined, match[6], { href: match[7], target: '_blank', rel: 'noreferrer noopener' }));
      else into.append(`${match[6]} (${match[7]})`);
    }
    last = match.index + match[0].length;
  }
  if (last < text.length) into.append(text.slice(last));
}

/** Draws Markdown into an element. */
export function renderMarkdown(source: string, root: HTMLElement): void {
  const page = el('div', 'fry fry-markdown');
  const lines = source.replace(/\r\n/g, '\n').split('\n');
  let i = 0;

  while (i < lines.length) {
    const line = lines[i];
    if (line.trim() === '') {
      i++;
    } else if (/^```/.test(line)) {
      const code: string[] = [];
      for (i++; i < lines.length && !/^```/.test(lines[i]); i++) code.push(lines[i]);
      i++;
      page.append(el('pre', 'fry-pre fry-mono', code.join('\n')));
    } else if (/^#{1,6}\s/.test(line)) {
      const level = /^#+/.exec(line)![0].length;
      const heading = el(`h${level}` as 'h1');
      inline(line.replace(/^#+\s+/, '').trim(), heading);
      page.append(heading);
      i++;
    } else if (/^(-{3,}|\*{3,}|_{3,})\s*$/.test(line)) {
      page.append(el('hr'));
      i++;
    } else if (/^>\s?/.test(line)) {
      const quote = el('blockquote');
      const text: string[] = [];
      for (; i < lines.length && /^>\s?/.test(lines[i]); i++) text.push(lines[i].replace(/^>\s?/, ''));
      inline(text.join(' '), quote);
      page.append(quote);
    } else if (/^\s*([-*+]|\d+\.)\s+/.test(line)) {
      const ordered = /^\s*\d+\./.test(line);
      const list = el(ordered ? 'ol' : 'ul');
      for (; i < lines.length && /^\s*([-*+]|\d+\.)\s+/.test(lines[i]); i++) {
        const item = el('li');
        inline(lines[i].replace(/^\s*([-*+]|\d+\.)\s+/, ''), item);
        list.append(item);
      }
      page.append(list);
    } else {
      const paragraph = el('p');
      const text: string[] = [];
      for (; i < lines.length && lines[i].trim() !== '' && !/^(```|#{1,6}\s|>\s?|\s*([-*+]|\d+\.)\s+)/.test(lines[i]); i++) text.push(lines[i].trim());
      inline(text.join(' '), paragraph);
      page.append(paragraph);
    }
  }

  root.replaceChildren(page);
}
