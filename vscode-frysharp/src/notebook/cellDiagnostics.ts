import type { DiagnosticDto } from '../sidecar/protocol';

/** A diagnostic placed in a cell: 0-based, as an editor has it. */
export interface PlacedDiagnostic {
  startLine: number;
  startCharacter: number;
  endLine: number;
  endCharacter: number;
  severity: 'error' | 'warning' | 'information';
  code: string;
  message: string;
}

/**
 * Where the engine's diagnostics go in a cell's text. The engine counts lines and columns from 1 and may name the end of a
 * line as a very large column; the editor counts from 0 and has the line it has. A range always has something in it when
 * the line does, so it can be seen.
 */
export function placeDiagnostics(diagnostics: DiagnosticDto[] | undefined, text: string): PlacedDiagnostic[] {
  if (!diagnostics || diagnostics.length === 0) return [];
  const lines = text.split(/\r\n|\r|\n/);
  const clamp = (value: number, max: number) => Math.max(0, Math.min(value, max));

  return diagnostics.map((diagnostic) => {
    const startLine = clamp(diagnostic.startLine - 1, lines.length - 1);
    const endLine = clamp(Math.max(diagnostic.endLine, diagnostic.startLine) - 1, lines.length - 1);
    const startCharacter = clamp(diagnostic.startColumn - 1, lines[startLine].length);
    let endCharacter = clamp(diagnostic.endColumn - 1, lines[endLine].length);
    if (endLine === startLine && endCharacter <= startCharacter) {
      endCharacter = Math.min(startCharacter + 1, lines[endLine].length);
    }
    return {
      startLine,
      startCharacter,
      endLine,
      endCharacter,
      severity: diagnostic.severity === 'warning' ? 'warning' : diagnostic.severity === 'info' ? 'information' : 'error',
      code: diagnostic.code,
      message: diagnostic.message
    };
  });
}
