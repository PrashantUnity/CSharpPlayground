// Just enough of the 'vscode' module for the serializer's adapter to run in plain Node. The real API is exercised by the
// Extension Host tests; this keeps the adapter's own logic (ids, metadata, summaries) under test where Node is enough.
import Module = require('node:module');

class NotebookCellData {
  metadata: { [key: string]: any } | undefined;
  outputs: NotebookCellOutput[] | undefined;
  executionSummary: { executionOrder?: number; success?: boolean; timing?: { startTime: number; endTime: number } } | undefined;
  constructor(public kind: number, public value: string, public languageId: string) {}
}

class NotebookData {
  metadata: { [key: string]: any } | undefined;
  constructor(public cells: NotebookCellData[]) {}
}

class NotebookCellOutputItem {
  constructor(public data: Uint8Array, public mime: string) {}
}

class NotebookCellOutput {
  constructor(public items: NotebookCellOutputItem[], public metadata?: { [key: string]: any }) {}
}

export const vscodeStub = {
  NotebookCellKind: { Markup: 1, Code: 2 },
  NotebookCellData,
  NotebookData,
  NotebookCellOutput,
  NotebookCellOutputItem
};

const load = (Module as any)._load;
(Module as any)._load = function (request: string, ...rest: unknown[]) {
  return request === 'vscode' ? vscodeStub : load.call(this, request, ...rest);
};
