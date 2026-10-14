import { frynbFromJson, writeFrynb } from './frynb';
import { ipynbFromJson, writeIpynb } from './ipynb';
import { NotebookModel } from './model';

export * from './model';

/** Reads the text of a notebook file in either format: a Jupyter notebook has "nbformat" and "cells", a .frynb has "Cells". */
export function parseNotebook(json: string): NotebookModel {
  const parsed: unknown = JSON.parse(json);
  const jupyter = typeof parsed === 'object' && parsed !== null && 'nbformat' in parsed && 'cells' in parsed;
  return jupyter ? ipynbFromJson(parsed, json) : frynbFromJson(parsed);
}

/** The text of a notebook file, in the format the notebook was read in. */
export function writeNotebook(notebook: NotebookModel, now: Date = new Date()): string {
  return notebook.format === 'ipynb' ? writeIpynb(notebook) : writeFrynb(notebook, now);
}
