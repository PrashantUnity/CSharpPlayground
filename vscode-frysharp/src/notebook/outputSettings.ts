import * as vscode from 'vscode';

/** Whether a cell with several kinds of output shows them as tabs in one output (the setting `frysharp.notebook.outputTabs`, on unless turned off). */
export const outputTabsEnabled = (): boolean => vscode.workspace.getConfiguration('frysharp.notebook').get<boolean>('outputTabs', true);
