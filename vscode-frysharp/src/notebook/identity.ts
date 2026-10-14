import * as path from 'path';
import type { NotebookRef } from '../sidecar/protocol';

/** What identifies a notebook to the engine (type-only imports of vscode keep this runnable in plain Node). */
export interface NotebookIdentity extends NotebookRef {
  notebookId: string;
  /** A name for it (its path from the workspace folder, or its file name): for messages and the engine's log. */
  path: string;
  /** The folder its kernels run in. */
  workingDirectory?: string;
}

export interface UriLike {
  scheme: string;
  fsPath: string;
  path: string;
  toString(): string;
}

/**
 * A notebook is its URI: the same file is the same notebook, an untitled one has its own, and two files named alike in
 * different folders are two. Where it runs is its own folder (it need not be in the workspace), else the workspace folder.
 */
export function identityOf(uri: UriLike, workspaceFolder: (uri: UriLike) => string | undefined): NotebookIdentity {
  const folder = workspaceFolder(uri);
  if (uri.scheme !== 'file') {
    return { notebookId: uri.toString(), path: uri.path.replace(/^\/+/, '') || uri.toString(), workingDirectory: folder };
  }

  const relative = folder ? path.relative(folder, uri.fsPath).replace(/\\/g, '/') : '';
  const inside = relative !== '' && !relative.startsWith('..') && !path.isAbsolute(relative);
  return { notebookId: uri.toString(), path: inside ? relative : path.basename(uri.fsPath), workingDirectory: path.dirname(uri.fsPath) };
}
