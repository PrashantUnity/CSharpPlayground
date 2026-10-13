# The FrySharp Sidecar Session Protocol

The Visual Studio Code extension (`vscode-frysharp`) talks to the headless sidecar (`FrySharp.Sidecar`) with this protocol. It is JSON-RPC 2.0 framed with standard Content-Length headers over stdio (`stdin`/`stdout`).

The definitions are:
- `FrySharp.Sidecar/Protocol/Protocol.cs` holds the messages, error codes, and notification names.
- `FrySharp.Sidecar/Sessions/SidecarRpc.cs` holds the JSON-RPC methods.
- `vscode-frysharp/src/sidecar/protocol.ts` mirrors them in TypeScript for the extension.

The automated tests in `Tests.Sidecar/` verify that all methods, notifications, and execution lifecycles pass in-memory and over streams.

## Transport & Framing

- **Transport**: Standard I/O (`process.stdin` / `process.stdout`) between VS Code and the .NET sidecar process.
- **Framing**: Content-Length header delimited (standard Language Server Protocol / `StreamJsonRpc.HeaderDelimitedMessageHandler` and `vscode-jsonrpc`).
- **Logging**: Sidecar diagnostic logs are directed strictly to `stderr` (`Console.Error`), leaving `stdout` clean for JSON-RPC messages.
- **Initialize**: The client sends `fry/initialize` upon launching the sidecar. The sidecar responds with server capabilities, supported languages, and workspace info.

## Sessions

A session represents an active client connection (VS Code extension instance). The client provides a unique `clientId` during `fry/initialize`.

- **Lifespan**: The sidecar lifecycle is tied to the parent VS Code process or editor workspace session.
- **Process Cleanup**: When the client disconnects or closes, the sidecar cleans up all active runs, language kernels, and child processes via `ProcessRegistry`.
- **Concurrency**: Notebook cell runs and script runs execute asynchronously with streaming output events back to the client.

## Errors

Errors are JSON-RPC errors with one of these codes:

| Code | Name | When |
|---|---|---|
| -32001 | `NotFound` | The file, folder, document or cell doesn't exist. |
| -32002 | `AccessDenied` | The path is outside the workspace (absolute, `..`, or through a link that leads out), or names the workspace itself where that isn't allowed (delete, rename). |
| -32003 | `Conflict` | The file changed on disk since it was read, or the session was taken over. |
| -32004 | `Invalid` | Bad parameters, or a request before `fry/initialize`. |
| -32005 | `TooLarge` | The file is bigger than the editor opens (`maxOpenFileBytes`); download it instead. |
| -32006 | `OutOfSync` | The server's copy of a document is behind the editor's. Send the whole text again with `didOpen`. |
| -32007 | `NotSupported` | The file can't be opened as text, or run, or the client speaks another protocol version. |
| -32008 | `AlreadyExists` | A create, rename or run would replace something that exists. |

## Paths

Paths are relative to the workspace root and use `/`. `""` is the root.

- An absolute path (`/etc/hosts`) is refused, never read as relative.
- So is a path that leaves the workspace with `..`, or through a symbolic link anywhere along it.
- The workspace root itself can't be deleted or renamed.

## Requests (browser → server)

### Session

| Method | Params | Result |
|---|---|---|
| `fry/initialize` | `{ clientId, clientName?, protocolVersion }` | `{ protocolVersion, serverVersion, workspaceRoot, workspaceName, languages[], resumed, maxOpenFileBytes }`. `languages[]` says what each language can do: `canRun`, `canRunCells`, `hasCompletion`, its extensions and new-file template. A different `protocolVersion` answers `NotSupported`. |

### Workspace

| Method | Params | Result |
|---|---|---|
| `fry/workspace/listFolder` | `{ path }` | `{ path, entries[], truncated }`. Folders come first. As in VS Code, only `.git`, `.svn`, `.hg`, `CVS`, `.DS_Store` and `Thumbs.db` are hidden; links that lead outside the workspace are left out too. |
| `fry/workspace/findFiles` | `{ query, limit }` | `{ matches[], indexing }`. Fuzzy file-name search over the workspace index (Go to File). |
| `fry/workspace/search` | `{ searchId, query, isRegex, matchCase, wholeWord }` | `{ filesSearched, filesWithMatches, matches, hitLimit }`, once the search ends. Hits stream as `fry/workspace/searchHits` while it runs. Cancel it with `$/cancelRequest`. |
| `fry/workspace/createFile` | `{ path, content? }` | `{ path }`. Without `content`, the file gets its language's template. Never replaces a file (`AlreadyExists`). |
| `fry/workspace/createFolder` | `{ path }` | `{ path }` |
| `fry/workspace/delete` | `{ path }` | — Deleted for good (the server has no trash), so the studio asks first. |
| `fry/workspace/rename` | `{ from, to }` | `{ path }`. Never overwrites (`AlreadyExists`). A change of case only is allowed. A folder can't move into itself (`Invalid`). |

### Documents

The server holds the text of every document the tab has open, so language features take only a position.

| Method | Params | Result |
|---|---|---|
| `fry/document/open` | `{ path }` | `{ path, text, languageId, diskVersion, isReadOnly }`. `diskVersion` fingerprints the file as read. |
| `fry/document/didOpen` | `{ path, version, text, diskVersion }` | Notification. The editor's text at `version`. It is also sent again, with the whole text, after `OutOfSync` or `fry/document/resync`. |
| `fry/document/didChange` | `{ path, version, changes: [{ offset, length, text }] }` | Notification. `version` is one more than the last one. Each change's offset is in the text *before* the batch. A skipped version gets `fry/document/resync`. |
| `fry/document/didClose` | `{ path }` | Notification. |
| `fry/document/save` | `{ path, version, force? }` | `{ diskVersion }`. Writes the server's text at `version`, atomically (temp file, then rename), keeping the encoding, its BOM and the file's permissions. The answer is `Conflict` if the file changed on disk since it was read (repeat with `force: true` to overwrite), or `OutOfSync` if `version` isn't what the server holds. |

### Language features (C#)

| Method | Params | Result |
|---|---|---|
| `fry/language/completion` | `{ path, version, offset }` | `{ items: [{ label, insertText, kind, detail?, documentation?, sortText }] }` |
| `fry/language/hover` | `{ path, version, offset }` | `{ markdown: [] }` or `null` |

Diagnostics come by themselves as `fry/language/diagnostics`, about 400 ms after the last change. Statements are checked as the C# script they run as: a trailing expression is the result, not an error.

### Runs

| Method | Params | Result |
|---|---|---|
| `fry/run/start` | `{ runId, path?, code?, languageId?, arguments? }` | `{ runId, title }`. Runs the saved file at `path`, or `code` (C# only). |
| `fry/run/stop` | `{ runId }` | `true` if the run was going. C# that ignores Stop is abandoned after a grace period. Either way the run ends with exit code 130. |
| `fry/run/input` | `{ runId, text }` | — A line typed into the run (`Console.ReadLine`, `input()`). |
| `fry/run/list` | `{}` | `[{ runId, title, path?, output, running, exitCode? }]`: the tab's runs with what they printed (up to 1 MB each), for a tab that reconnected. |

`runId` is chosen by the client: 1 to 64 letters, digits, `-` or `_`, unique in the session. A run's notifications can arrive before the answer to `fry/run/start`, and the client already knows which run they belong to.

Each language's runner reads the run's errors from its output; they arrive as `problems` in `fry/run/exited`. Arguments are passed to the program one by one, never joined into a command line.

Exit codes:
- 0 means success, 1 means a C# compile error or exception, 130 means stopped.
- Other languages report the program's own exit code.

### Notebooks

The server owns an open notebook: its cells, outputs and kernels. Every tab that opens the same file shares it.

| Method | Params | Result |
|---|---|---|
| `fry/notebook/open` | `{ path }` | `{ path, format, title, defaultLanguage, diskVersion, cells: [{ id, type, language?, source, executionCount?, hasError, outputs[] }] }`. `format` is `frynb` or `ipynb`. |
| `fry/notebook/edit` | `{ path, edits: [{ op, cellId, index?, type?, language?, source? }] }` | — `op` is `insert`, `delete`, `move`, `source`, `language` or `type`. The client makes new cells' ids. |
| `fry/notebook/execute` | `{ path, cellId, source }` | `{ executionCount, success, durationMs }`, once the cell has run. Cells run one at a time, in the order asked. Output streams as `fry/notebook/output`. A dropped connection doesn't stop the cell. |
| `fry/notebook/interrupt` | `{ path }` | — Stops the running cell. |
| `fry/notebook/restart` | `{ path }` | — Stops it and forgets every kernel's state. |
| `fry/notebook/variables` | `{ path }` | `[{ name, type, value, kernel }]` |
| `fry/notebook/save` | `{ path, force? }` | `{ diskVersion }`. Saved in the notebook's own format. A `.ipynb` stays valid nbformat 4.5. Nothing is written if nothing changed. `Conflict` works as for documents. |
| `fry/notebook/close` | `{ path }` | — The kernels end when the last tab closes it. |

A cell's first lines can pick its language (`#!python`) and copy a variable from another kernel (`#!share --from csharp nums [--as numbers]`). See [kernel-protocol.md](kernel-protocol.md) for how the other languages' kernels run.

## Notifications (server → browser)

| Method | Params |
|---|---|
| `fry/workspace/changed` | `{ structure }`: files changed on disk. `structure` means files or folders were added or removed, so the client lists open folders again. |
| `fry/workspace/searchHits` | `{ searchId, path, hits: [{ line, column, length, text }] }` |
| `fry/document/changedOnDisk` | `{ path }`: an open document's file changed outside the studio. |
| `fry/document/resync` | `{ path }`: the server missed an edit. Send the whole text with `didOpen`. |
| `fry/language/diagnostics` | `{ path, version, diagnostics: [{ severity, code, message, startLine, startColumn, endLine, endColumn }] }`. Lines and columns start at 1. |
| `fry/run/output` | `{ runId, text }`: batched (sent at 8 KB or 50 ms, whichever comes first), in order, with terminal line endings (`\r\n`) and ANSI colours kept. |
| `fry/run/display` | `{ runId, data, metadata }`: a display as a MIME bundle (see below). |
| `fry/run/inputRequested` | `{ runId }`: the program is waiting for a line. |
| `fry/run/exited` | `{ runId, exitCode, durationMs, problems: [{ path?, severity, code, message, line, column }] }`. Sent exactly once per run. |
| `fry/notebook/output` | `{ path, cellId, output: { kind, text?, data?, metadata? } }`. `kind` is `stream`, `display` or `error`. |
| `fry/notebook/cellState` | `{ path, cellId, running, executionCount?, success?, durationMs? }` |
| `fry/session/replaced` | `{}`: another connection took this session over. |

### Requests the server makes

| Method | Params | Answer |
|---|---|---|
| `fry/input/request` | `{ prompt, password }` | The line typed, or `null` (dismissed). A notebook cell asks with this: `Console.ReadLine()` in C#, `input()` in Python, `prompt()` in JavaScript. The server cancels the request (`$/cancelRequest`) if the cell is stopped. |

## Displays

Displays travel as MIME bundles, like Jupyter's. Each bundle also has `text/plain`, so any client can show something.

| MIME type | From | Shape |
|---|---|---|
| `application/vnd.fry.table+json` | `Dump`, `Display.Table`, `fry.table` | `{ title, columns[], numeric[], rows[][], totalRows, totalColumns }`. At most 1,000 rows are sent; `totalRows` says how many there are. |
| `application/vnd.fry.inspector+json` | an object's `Dump` | `{ title, properties: [{ name, value, isNull, isString, child? }] }` |
| `application/vnd.fry.chart.v1+json`, `…plot3d.v1+json`, `…visualizer.v1+json` | charts, 3D plots, visualizers | the visual's spec ([visual-protocol.md](visual-protocol.md)) |
| `image/png`, `image/jpeg`, `image/gif`, `image/svg+xml` | `Display.Image`, plots | base64 (SVG as text). `metadata[mime]` may give `{ width, height }`. |
| `text/html` | `Display.Html`, ECharts | shown in the sandbox frame only |
| `text/markdown` | `Display.Markdown`, `fry.markdown`, Jupyter kernels | rendered and sanitized, with highlighted code blocks |

A live desktop control (`Display.Control`, `Display.Animate`) has no bundle and isn't sent.
