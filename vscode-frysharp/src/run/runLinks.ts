/** A place in a file that a line of a program's output names. */
export interface OutputLink {
  /** Where in the line the text of the link starts, and how long it is. */
  start: number;
  length: number;
  file: string;
  line: number;
  column: number;
}

// What the compilers and runtimes FrySharp runs write when they point at code:
//   Program.cs(12,5): error CS1002        .NET            File "tool.py", line 3         Python
//   ./main.go:5:2: undefined: x            Go, Node        --> src/main.rs:3:5            Rust
const PATTERNS: Array<{ expression: RegExp; read: (match: RegExpExecArray) => { file: string; line: number; column: number } }> = [
  {
    expression: /(?<![A-Za-z0-9_])(?<file>(?:[A-Za-z]:[\\/])?[^\s:()"'<>|*?]+\.[A-Za-z0-9]+)\((?<line>\d+),(?<column>\d+)\)/g,
    read: (m) => ({ file: m.groups!.file, line: +m.groups!.line, column: +m.groups!.column })
  },
  {
    expression: /File "(?<file>[^"]+)", line (?<line>\d+)/g,
    read: (m) => ({ file: m.groups!.file, line: +m.groups!.line, column: 1 })
  },
  {
    expression: /(?<![A-Za-z0-9_])(?<file>(?:[A-Za-z]:[\\/])?[^\s:()"'<>|*?]+\.[A-Za-z0-9]+):(?<line>\d+)(?::(?<column>\d+))?/g,
    read: (m) => ({ file: m.groups!.file, line: +m.groups!.line, column: m.groups!.column ? +m.groups!.column : 1 })
  }
];

/** The places a line of output names, left to right, none overlapping another. */
export function findLinks(line: string): OutputLink[] {
  const found: OutputLink[] = [];
  for (const { expression, read } of PATTERNS) {
    expression.lastIndex = 0;
    for (let match = expression.exec(line); match; match = expression.exec(line)) {
      const link = { start: match.index, length: match[0].length, ...read(match) };
      // a URL (https://host:8080) is not a file and a line
      if (link.line < 1 || link.file.startsWith('//') || found.some((other) => link.start < other.start + other.length && other.start < link.start + link.length)) continue;
      found.push(link);
    }
  }
  return found.sort((a, b) => a.start - b.start);
}
