/** What typing into a run's terminal did: what to show, the lines to send to the program, whether Ctrl+C was pressed. */
export interface Typed {
  /** Text to write to the terminal now. */
  echo: string;
  /** Lines the person finished (Enter), for the program to read. */
  lines: string[];
  interrupt: boolean;
}

const ERASE = '\b \b';

/**
 * The line a person types to a running program. The program's input is not a terminal, so nothing edits the line for
 * them: this shows what is typed, takes back a character on Backspace, and on Enter takes back the whole of what it
 * showed, because the engine writes the line it was sent into the output (as a terminal would have), so that the line
 * is not on the screen twice. Arrow keys and other escape sequences do nothing.
 */
export class LineEditor {
  private buffer = '';

  /** The text typed and not yet sent. */
  get pending(): string {
    return this.buffer;
  }

  reset(): void {
    this.buffer = '';
  }

  feed(data: string): Typed {
    const typed: Typed = { echo: '', lines: [], interrupt: false };
    const characters = [...data];

    for (let i = 0; i < characters.length; i++) {
      const character = characters[i];
      if (character === '\x1b') {
        // an escape sequence (an arrow key, a paste marker): skip it, final byte included
        if (characters[i + 1] === '[' || characters[i + 1] === 'O') {
          i += 2;
          while (i < characters.length && !/[A-Za-z~]/.test(characters[i])) i++;
        } else {
          i++;
        }
      } else if (character === '\x03') {
        typed.interrupt = true;
        this.buffer = '';
      } else if (character === '\r' || character === '\n') {
        if (character === '\n' && characters[i - 1] === '\r') continue; // CR LF is one Enter
        typed.echo += ERASE.repeat([...this.buffer].length);
        typed.lines.push(this.buffer);
        this.buffer = '';
      } else if (character === '\x7f' || character === '\b') {
        const chars = [...this.buffer];
        if (chars.length > 0) {
          chars.pop();
          this.buffer = chars.join('');
          typed.echo += ERASE;
        }
      } else if (character >= ' ') {
        this.buffer += character;
        typed.echo += character;
      }
    }
    return typed;
  }
}
