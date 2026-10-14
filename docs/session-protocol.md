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
| `fry/engine/info` | `{}` | `{ serverVersion, protocolVersion, runtime, operatingSystem, architecture, workspaceRoot, toolchains: [{ languageId, displayName, found, name?, version?, path?, source?, missing? }] }`: what the engine runs on and the tool each language found (C# is the engine itself), or what is missing and how to get it. Probes the machine, so the first answer can take seconds. "FrySharp: Show Engine Info" shows it. |

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

### Language features in notebook cells

A client that holds its cells itself (a notebook in VS Code) asks about a cell's text directly: no document is open on the server. The language is the cell's, after any `#!language` line.

| Method | Params | Result |
|---|---|---|
| `fry/language/completeCell` | `{ language, source, offset, context? }` | As `fry/language/completion`. |
| `fry/language/hoverCell` | `{ language, source, offset, context? }` | As `fry/language/hover`. |
| `fry/language/diagnoseCell` | `{ language, source, context? }` | `{ diagnostics: [{ severity, code, message, startLine, startColumn, endLine, endColumn }] }`, on the cell's own lines. |

`context` is the text of the cells before this one **in the same language**, in order. For C# they are put in front of the cell, so what they define is in scope (their `using` lines are hoisted, directive lines emptied, and what only joining cells makes wrong, such as defining a name twice, is not reported). For SQL the tables and views they create are offered. `hasCompletion` in `fry/initialize` says which languages answer `completeCell` and `hoverCell` (C# and SQL): the others get none. `diagnoseCell` reports a notebook directive that is not one (`#!pyton`, code `FRY0001`) in any language, and compiler errors for C#.

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

#### A notebook the client holds (VS Code)

A client that keeps its notebook's cells itself (an editor with its own document model) has the server hold only the kernels. Every notebook request then takes `notebookId` instead of `path`: any text the client keeps stable for that notebook (its URI, an untitled notebook's name). Two notebooks with the same file name in different folders are two notebooks.

| Method | Params | Result |
|---|---|---|
| `fry/notebook/execute` | `{ path, cellId, source, language?, notebookId?, workingDirectory?, executionId? }` | `{ executionCount, success, durationMs, lastSeq, diagnostics? }`. `path` is any text when `notebookId` is given. `workingDirectory` is the folder the notebook's kernels run in (its own folder, even outside the workspace). `diagnostics` say where the cell went wrong, in the cell's own lines and columns (1-based): the compiler's errors, a traceback's line, SQLite's error line. |
| `fry/notebook/{interrupt,restart,variables,close}` | `{ notebookId }` | As above. |

`executionId` is chosen by the client for each run. Every `fry/notebook/output` and `fry/notebook/cellState` about the run carries it and a `seq` (1, 2, 3, ... in the order they were produced), and the answer to `fry/notebook/execute` carries `lastSeq`: the answer can arrive before some notifications, so the client waits until it has handled `lastSeq` of them. A `display` output may carry `displayId` and `update`: a redraw of an earlier display (a chart a program updates) replaces it instead of adding another. A stream output is one per cell, the text in the order it was printed, ANSI colours kept.

The server's request `fry/input/request` also says which cell asks: `{ prompt, password, path?, cellId?, executionId? }`.

Languages that run cells: C#, F# (`#!fsharp`, `#!fs`), Java (`#!java`, `#!jdk`), Rust (`#!rust`, `#!rs`), Go (`#!go`, `#!golang`), C++ (`#!cpp`, `#!c++`), Python, JavaScript and SQL (`#!sql`, `#!sqlite`). A SQL cell runs on the SQLite 3 CLI (`sqlite3`) installed on the machine, against a database that lives as long as the notebook's kernel. The rows a statement returns are one `application/vnd.fry.table+json` display each (titled `Result`, or `Result 1`, `Result 2` when there are several); the text a script prints (`.print`) and SQLite's errors are the stream. The notebook's tables are its variables, and `#!share --from csharp rows` makes a list, an object or a single value a table in the SQL cell.

An F# cell runs in the notebook's F# Interactive session (one `dotnet fsi` per notebook, so it needs the .NET SDK): what a cell defines the next one sees. The value of its last expression is a display (a list of records is a table, a record or an object the Object Explorer, a scalar or a short list text), compile errors are `diagnostics` with their columns, and the values are the variables (a function written `let f x = ...` is not a value to F# Interactive and is not listed). `#!share` hands a value to another kernel as JSON, and takes one as an F# value: a list of objects is an array of dictionaries (`rows.[0].["name"]`). A package is a line of the cell, `#r "nuget: Package, 1.2.3"`, which F# Interactive restores and references itself. Stop ends a cell that waits (`Thread.Sleep`, a read); one that only computes ends the kernel, and its values are gone.

A Java cell runs in the notebook's JShell session (one `java FryKernel.java` per notebook, so it needs a JDK): a variable, method, class or record a cell declares the next one uses. The value of an expression is a display (a list of records, beans or maps is a table, an object the Object Explorer, a scalar or a short list text); a declaration shows nothing. A compile error is a `diagnostic` with its column, and a runtime error names its real kind (`IllegalStateException`) and the line of the cell it went through (the innermost frame in a snippet of the cell). `//DEPS group:artifact:version` (or `%maven install ...`) at the top of a cell downloads the library from Maven Central and puts it on the classpath, even when it is the first cell of the notebook. Stop ends any cell, even one that only computes, and keeps what the notebook declared.


A Rust cell is built with Cargo as a small program (one `cargo build` of a crate per cell, so it needs the Rust toolchain; the first cell also builds the `fry` display crate, a few seconds, later cells about a second): the functions, types and imports earlier cells defined are written in front of it, and the `let` variables of plain data (numbers, text, lists, options, tuples, maps, sets) are written back as source for the next cell. A variable of a type the cell defined is not kept (the cell says so; `fry::share!(name)` keeps it as data for other languages). The value of a last expression without a semicolon is a display: a scalar or `Some(3)` is text, a short list of scalars is one line `[ 1, 2, 3 ]`, an empty list `[ ]`, a list of structs, tuples or maps a table, and a struct, tuple or enum variant with data the Object Explorer (read from the value's `Debug` text, so the type needs `Debug`). `fry::markdown`, `fry::table`, `fry::line_chart` ... show output. What the program prints keeps its order, stderr among it; a compile error and a panic are `diagnostics` on the cell's own line (the text says `--> cell:3:18` and `panicked at cell:3:2`). A plain `std::io::stdin()` read sees the end of input; `fry::input("Prompt: ")` and `fry::password(...)` ask (`fry/input/request`), and `None` is the end of input. `// #crate: name = "1"` or `%cargo add name` fetches a crate. Stop ends the program and keeps what earlier cells made.

A Go cell is built with the Go toolchain as a small program (one `go build` per cell in a module of the notebook's own, against one build cache for everything, so it needs `go` 1.18 or newer; the first cell also builds the standard library and the `fry` package, a few seconds, later cells a fraction of a second). The functions, methods, types, constants, variables and imports earlier cells declared are written in front of it (a declaration written again takes the old one's place), and the variables of the cell's `main` are given back their values at its start, in an outer scope, so `x := 5` may make again what an earlier cell made. A variable made with `:=` or declared at the top with `var` is handed on as JSON with its Go type (`fry.Persist`, which checks that the value comes back the same); one that can't be saved (a func, a channel, a struct with lowercase fields, an error) isn't, and the cell says so once, and says why when a later cell uses the name. A variable declared at the top is a package variable, so a function in a later cell sees it; one made with `:=` is local to the cells' `main`. The value of a last expression is a display: a scalar is text (a string quoted), a short list of scalars is one line `[ 1, 2, 3 ]`, an empty one `[ ]`, a slice of structs or maps a table, a map a key/value table, and a struct or pointer to one the Object Explorer; a statement that prints isn't shown, and a call that returns nothing is run as it is. `fry.Markdown`, `fry.Table`, `fry.LineChart` ... show output. What the program prints keeps its order, stderr among it; a compile error and a panic are `diagnostics` on the cell's own line (the text says `cell:3:16`). A plain `os.Stdin` read sees the end of input; `fry.Input("Prompt: ")` and `fry.Password(...)` ask (`fry/input/request`), and false is the end of input. `#!share --from csharp rows` makes the list a typed variable (`[]int`, `[]float64`, `[]string`, `map[string]any`, `[]map[string]any` ...), and `#!share --from go name` hands on any variable that can be saved, or what `fry.Share("name", value)` offered. `%go get github.com/user/module` downloads a module (into a scratch module that every notebook build then requires); cells import it. Stop ends the program and keeps what earlier cells made.

A C++ cell is built with a C++20 compiler (clang++, g++ or MSVC `cl`) as a small program (one compile per cell, a second or two, because the `<fry/display.hpp>` and `<fry/values.hpp>` headers that show values are included): the includes, functions (overloads live together, one with the same signature replaces the old), types, aliases, macros and `static`, `constexpr`, `const` or `extern` variables earlier cells declared are written in front of it, and the variables of the cell's `main` are given back their values at its start, in an outer scope, so `int x = 5;` may make again what an earlier cell made. A variable declared in a cell is handed on as JSON with its type as C++ writes it (`fry::persist`, which checks that the value comes back the same): numbers, text, vectors, arrays, deques, lists, sets, maps with text or number keys, optionals, pairs, tuples and structs of those; a struct's public data members are read from its definition by the notebook, which writes a `fry::fields<Type>` for it. One that can't be saved (a pointer, a C array, a lambda, a class with private members) isn't, and the cell says so once, and says why when a later cell uses the name. A `static` variable at the top is a global, so a function in a later cell sees it; one declared in a statement is local to the cells' `main`. The value of a last expression written without a `;` is a display: a scalar is text (a string quoted), a short list of scalars is one line `[ 1, 2, 3 ]`, an empty one `[ ]`, a vector of structs, pairs or maps a table, a map a key/value table, and a struct, pair or tuple the Object Explorer (a type the notebook doesn't know the members of is its `operator<<` text, or its name); a statement that prints isn't shown, and a call that returns nothing is run as it is. `fry::markdown`, `fry::display::table`, `fry::line_chart` ... show output. What the program prints keeps its order, stderr among it (output is unbuffered); a compile error is a `diagnostic` on the cell's own line (the text says `cell:3:5`), and an uncaught exception or a crash fails the cell naming its cause (C++ has no line to give). A plain `std::cin` read sees the end of input; `fry::input("Prompt: ")` and `fry::password(...)` ask (`fry/input/request`), and an empty `std::optional` is the end of input. `#!share --from csharp rows` makes the list a typed variable (`std::vector<long long>`, `std::vector<double>`, `std::vector<std::string>`, `std::vector<std::vector<...>>`; an object or a mixed list is a `fry::Json` the cell reads with `config["name"].as_string()`), and `#!share --from cpp name` hands on any variable that can be saved, or what `fry::share("name", value)` offered. `%vcpkg install name` adds a package. Stop ends the program and keeps what earlier cells made.

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
