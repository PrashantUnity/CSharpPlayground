# FrySharp Web Mode — Plan

> **Concept**: `frysharp serve` starts a local server, and the browser becomes the IDE, as with JupyterLab. The same engine that runs in the FryPDF desktop app (Roslyn scripting, polyglot notebooks, kernels, visuals) runs behind a React UI with the same 5-zone layout.

*Last checked against the code: 2026-10-09 (branch `UiIssues`).*

> [!IMPORTANT]
> **Decision (2026-10-09): Web is the preferred surface.** The VS Code extension is deferred (§12). Because the web client talks to the engine through one protocol (§5), the extension can reuse that protocol later instead of needing its own backend.

---

## 1. Why web

- One UI that works on every OS, on a remote or headless machine (`ssh -L 5000:localhost:5000 box`), and on locked-down machines where the desktop app can't be installed.
- Notebooks and scripts are a natural fit for the browser, and JupyterLab proved the model.
- Timing: Microsoft deprecated **Polyglot Notebooks** (support ended 2026-03-27) and retired **Azure Data Studio**, leaving C# and SQL notebook users without a tool ([DevClass](https://www.devclass.com/databases/2026/02/14/microsoft-deprecates-polyglot-notebooks-developers-react/4091167), [The Register](https://forums.theregister.com/forum/all/2026/02/12/polyglot_notebooks_deprecation/)). FrySharp already has C#, Python, JavaScript, Java and SQL cells with `#!share --from <language> <name>`.

---

## 2. Architecture

```
$ frysharp serve [--port 5000] [--workspace DIR] [--open]
  → http://127.0.0.1:5000/?token=…   (token swapped for an HttpOnly cookie)

┌──────────────────────────────────────────────────────────────────────┐
│ FrySharp.Web  (ASP.NET Core, references FrySharp.Core only)          │
│  ├─ GET  /            the SPA, embedded in the assembly              │
│  ├─ GET  /files/raw   large downloads; POST /files/upload            │
│  ├─ WS   /session     JSON-RPC 2.0: everything interactive (§5)      │
│  └─ FrySharp.Core     Roslyn, kernels, processes, workspace index,   │
│                       storage, visuals render models, debugging      │
└──────────────────────────────────────────────────────────────────────┘
                 ↕ one authenticated WebSocket per browser tab
┌──────────────────────────────────────────────────────────────────────┐
│ SPA  (Vite + React + TypeScript)                                     │
│  ├─ Monaco, coloured by the desktop's own TextMate grammars          │
│  ├─ 5 zones: Activity Bar · Side Bar · Editor · Bottom Deck · Status │
│  ├─ Zustand state · TanStack Virtual lists, tree and tables          │
│  ├─ Renderers: tables, charts/3D/visualizers, ECharts, inspector     │
│  └─ xterm.js for program output (ANSI colours kept)                  │
└──────────────────────────────────────────────────────────────────────┘
```

**No ViewModels on the server.** 29 of the 84 ViewModels depend on `Dispatcher.UIThread`. The server talks to services; the SPA owns UI state.

---

## 3. Starting point (verified in the code)

### Reusable as is

| Piece | Where | Notes |
|---|---|---|
| Script execution | `Services/Execution/*`, `Services/Roslyn/RoslynCompilerService.cs` | `Execution` has no Avalonia |
| Processes | `Services/Processes/*` (`ProcessLauncher`, `ProcessRegistry`, `ScriptRunner`) | No Avalonia. `TerminalTextBuffer` **drops colour codes** and handles `\r`/backspace. The web terminal should get the raw stream and let xterm.js render it |
| Workspace at scale | `Services/Workspace/*`: `WorkspaceFileIndex` (200k files in ~0.75 s), `WorkspaceTextSearch`, `WorkspaceWatcher`, `ListFolderAsync` | The web Explorer, Go to File and Find in Files sit directly on these |
| Storage, toolchains, debugging, Git | `Services/Storage`, `Toolchains`, `Debugging`, `Workspace/Git` | No Avalonia |
| Kernels and routing | `Services/Kernels/*` | Only `NotebookExecutionKernel.cs` uses Avalonia |
| **Visuals** | `Visuals/*` (56 files: `Spec`, `Building`, `Rendering`, `Json`, …) | **No Avalonia.** `ChartRenderModelBuilder`, `Plot3DRenderModelBuilder` and `VisualizerRenderModelBuilder` turn a spec into the model the chart control draws, on any thread. The server can send that model, so the web draws the **same chart** as the desktop |
| ECharts outputs | `Services/Display/ECharts/*`, `HtmlAssets.cs` | Already HTML + ECharts JS (embedded). Serve those same files to the browser |
| Notebook formats | `.frynb` (native), `.ipynb` / `.csnb` (import) | |

### Coupled to Avalonia (to untangle in Phase 0)
- **76 of 466** `Services/` files import Avalonia, and **16** call `Dispatcher.UIThread`.
- 41 of those are in `Languages/`, because `ILanguageDefinition` mixes engine members (toolchain, runner, kernels, packages, debugger) with editor members (`IHighlightingDefinition`, `IIndentationStrategy`, folding, editor assistants).
- `Display/`: `InteractiveDisplayService`, `RichCellOutput`, `ImageDecoder` and `InteractiveDisplayContext` use Avalonia bitmaps.
- `Extensibility/`: 19 files that build Avalonia UI or themes (`ExtensibilityUiService`, `DynamicViewFactory`, `VisualTreeManager`, `DynamicThemeEngine`, `LayoutToken*`, …). See §9.
- **No PTY anywhere**, so the desktop has no interactive shell terminal. A real shell in the browser is new work (decision in §14).

---

## 4. Phase 0 — Headless core (incremental, unblocks web early)

**Goal:** a `FrySharp.Core` assembly with **no Avalonia reference**, so a regression is a build error, not a code-review catch.

Do it in slices so web work can start after the first one:

- [ ] **0a — Minimal core (unblocks W1–W3):** `Execution`, `Processes`, `Storage`, `Workspace`, `Toolchains`, `Visuals`, plus the **C#** engine. Replace the `Dispatcher.UIThread` calls on these paths with events the UI marshals itself.
- [ ] **0b — Language split:** `ILanguageDefinition` becomes `ILanguageEngine` (Core) + `ILanguageEditor` (desktop). `LanguageRegistry` maps an id to both. Move one language per PR (Python, JavaScript, SQL, Java, F#, Go, Rust, C++). Each one appears in web mode as it moves.
- [ ] **0c — Kernels + Display:** untangle `NotebookExecutionKernel` and the four Display files. Core emits MIME bundles; the desktop turns them into bitmaps and controls.

**Done when:** `dotnet list FrySharp.Core package --include-transitive` shows no Avalonia. A console test opens a sample `.frynb`, runs C#, Python and SQL cells with `#!share`, and gets the same MIME bundles as the desktop. The desktop keeps 0 warnings, all tests green and unchanged `UiSnapshots perf` numbers.

---

## 5. The session protocol (one for web now, VS Code later)

The Fry kernel protocol (`docs/kernel-protocol.md`) is how the studio drives **one language process**. Polyglot routing (`NotebookKernelRouter`), `#!share` and the in-process C# kernel sit above it. Browsers need a **session** protocol one level up.

- **Transport:** JSON-RPC 2.0 over **one WebSocket per browser tab**. `StreamJsonRpc` (`WebSocketMessageHandler`) on the server, `vscode-jsonrpc` on the client. Plain HTTP only for static files and large up/downloads.
- **Why one socket:** ordering, one place for auth, one place for reconnect logic. The same messages run over stdio for a future VS Code sidecar.
- **Language features are LSP-shaped** (`textDocument/completion`, `hover`, `signatureHelp`, `publishDiagnostics`, `semanticTokens`). The SPA registers thin Monaco providers that call them. `monaco-languageclient` is the heavier alternative. Either way, the server half later becomes the VS Code extension's language server.
- **Versioned from day one:** `fry/initialize` negotiates `protocolVersion`. Add fields; never repurpose them.

| Method | Purpose |
|---|---|
| `fry/initialize` | Version, capabilities, workspace root, theme tokens |
| `fry/workspace/listFolder`, `goToFile`, `search` (streamed), `watch` | Explorer, Ctrl+P, Find in Files, external changes |
| `fry/document/open`, `save` (with `baseVersion`), `close` | Files; a save with a stale `baseVersion` is a conflict, never a silent overwrite |
| `fry/run/start`, `stop`, `stdin` | F5 / Ctrl+F5 / Shift+F5 for script files |
| `fry/notebook/open`, `execute`, `interrupt`, `variables`, `share` | Notebooks (one router per notebook) |
| `fry/output` (notification) | `stream` / `display` / `update_display`, with the existing MIME types and render models |
| `fry/input/request` | `Console.ReadLine` / `input()` |
| `fry/visual/event` | Click/select/step on visuals (`docs/visual-protocol.md`) |
| `fry/packages/*` | NuGet / pip / npm with progress |

**Reconnect:** a page refresh or dropped connection **must not kill running work**. Sessions live on the server keyed by document. The client re-attaches and receives buffered output, with a cap and a "trimmed" notice, the same rule as `TerminalTextBuffer`.

Document it in `docs/session-protocol.md`, beside `kernel-protocol.md`.

---

## 6. Phases

### W1 — Host, CLI and security
- [ ] `FrySharp.Web` (ASP.NET Core minimal host) on `FrySharp.Core`.
- [ ] `frysharp` **console** entry with `serve`, using `System.CommandLine`. `Runner/` is a `WinExe` with no console on Windows, so it can't host the verb.
- [ ] Security as in §10: token → cookie, `Host`/`Origin` checks, loopback by default, path confinement.
- [ ] SPA served from embedded resources, so a single-file publish works.
- **Done when:** `frysharp serve --open` opens the browser. A request without the token, or with a foreign `Origin`, is rejected (tests cover HTTP and the WebSocket upgrade).

### W2 — Shell: layout, Explorer, editor
- [ ] 5-zone layout. Single-sidebar rule. Keybindings `Ctrl+B`, `Ctrl+J`, `Ctrl+S`, `Ctrl+P`, `Ctrl+F`, `Ctrl+Shift+E/F`, `F5`/`Ctrl+F5`/`Shift+F5`. Browser-reserved keys (e.g. `Ctrl+W`, `Ctrl+N`) get documented alternatives.
- [ ] **Themes shared with the desktop:** `fry/initialize` sends the active theme's tokens (theme studio palettes) as CSS variables, so themes are built once.
- [ ] Explorer: a flat list of visible rows virtualized with TanStack Virtual (the desktop's `ExplorerRowList` model), folders listed lazily with `listFolder`, Go to File over `WorkspaceFileIndex`.
- [ ] Monaco **bundled locally**. `@monaco-editor/react` loads Monaco from a CDN by default, which breaks offline use and CSP, so configure `loader.config({ monaco })`. Tokenize with the **same TextMate grammar files** the desktop gets from `TextMateSharp.Grammars` (served by the backend, run with `vscode-textmate` + `vscode-oniguruma`), plus Roslyn semantic tokens for C#. Colours then match the desktop.
- [ ] Tabs with dirty markers, breadcrumbs, and save with conflict detection. External edits come through `watch`.
- **Done when:** a 30,000-file workspace's Explorer shows its first level in under 200 ms and stays smooth while scrolling.

### W3 — Run, output, problems
- [ ] Run / stop for every language whose engine has moved to Core.
- [ ] Output in xterm.js with ANSI colours; stdin; output batched (the kernel protocol's rule: flush at newline, 8 KB or 50 ms).
- [ ] Problems panel from run diagnostics (`IDiagnosticParser`), with click-to-navigate.
- [ ] Status bar: language, Ln/Col, compiler status, execution timer.
- **Done when:** a script printing 100,000 lines doesn't freeze the tab, and memory stays capped.

### W4 — Notebooks
- [ ] `.frynb` open/save (byte-identical round trip when nothing changed), plus `.ipynb` import.
- [ ] **Virtualized cell list:** only cells near the viewport mount a Monaco editor. The desktop needed this too: 150 cells went from 15.7 s / 2.6 GB to 0.75 s.
- [ ] Run / add / delete / move, interrupt, `input()`, polyglot cells with `#!share`, Variables panel.
- [ ] Reconnect keeps kernels and outputs (§5).
- **Done when:** a 1,000-cell notebook opens in under 1 s, and the repo's sample notebooks produce the same text outputs as the desktop (an automated comparison).

### W5 — Rich outputs
- [ ] Table renderer for `application/vnd.fry.table+json`: virtualized and sortable, with "showing N of M".
- [ ] Charts, 3D plots and visualizers drawn on Canvas/SVG **from the render models the server builds** (`Visuals/Rendering`), not from a second chart implementation. Events go through `fry/visual/event`.
- [ ] ECharts outputs with the same embedded JS, in sandboxed iframes.
- [ ] Object inspector, images; `text/html` in sandboxed iframes.
- **Done when:** every case in the visual conformance fixtures (`Tests/Visuals/Specs/VisualConformanceFixturesTests.cs`) renders in the browser, compared against the desktop's `UiSnapshots` images.

### W6 — Language features and tools
- [ ] C#: completion, hover, signature help, diagnostics and semantic tokens from the existing Roslyn services.
- [ ] Other languages: the existing completion services. Server-side `LspClient` proxies external language servers (rust-analyzer, etc.).
- [ ] Find in Files (streamed `WorkspaceTextSearch`), NuGet/pip/npm panel, Settings.
- [ ] Per-keystroke work tracks the edit, not the document. The desktop's large-document thresholds (`LargeDocumentLength`, `FoldingLimits`) apply here too.

### W7 — Parity, later
Debugger (breakpoints in the Monaco gutter, call stack and variables over DAP-shaped messages on `ScriptDebugSession`), Git source control, AI composer (key in the secret store, see [DatabaseIntegrationPlan.md](DatabaseIntegrationPlan.md) §6), Fry Server studio, Docs, Blind 75 problems.

---

## 7. Front-end stack

| Package | Purpose |
|---|---|
| `vite`, `react`, `typescript` | Build, UI |
| `monaco-editor` (+ `@monaco-editor/react`, loaded locally) | Editor |
| `vscode-textmate`, `vscode-oniguruma` | Same grammars as the desktop |
| `vscode-jsonrpc` | Session protocol client |
| `xterm` (+ fit addon) | Program output / terminal |
| `zustand` | State |
| `react-resizable-panels` | Splitters for Zones 2 and 4 |
| `@tanstack/react-virtual`, `@tanstack/react-table` | Explorer, results, notebooks, tables |

---

## 8. Performance budgets (the desktop mandate applies to web too)

| Scenario | Budget |
|---|---|
| Cold start to interactive (localhost) | < 1.5 s; Monaco and renderers are code-split and lazy |
| 30,000-file workspace, Explorer first level | < 200 ms |
| 1,000-cell notebook open | < 1 s; memory independent of cell count |
| Typing in a 2 MB file | no long task > 50 ms |
| 100,000-line output flood | tab stays responsive; capped buffer |
| Tab / view switch | views kept alive, never rebuilt, never refetched |

Measure with Playwright traces in a `tools/WebPerf` harness, the web counterpart of `UiSnapshots perf`, and record the numbers in the PR.

---

## 9. What will not port

- **Customizations that build Avalonia UI** (`~/.frysharp/init.csx` using `ExtensibilityUiService`, `DynamicViewFactory`, custom windows) can't run in a browser. Web mode loads only the parts that don't touch UI: commands, hooks, languages. It reports the rest as "desktop only" instead of failing silently. A web contribution API is a separate decision (§14).
- **Displaying Avalonia controls** in output is desktop only. Bitmaps and visuals become MIME bundles and do port.

---

## 10. Security (the server runs arbitrary code by design)

- Bind to `127.0.0.1` by default.
- A random token per start, printed and put in the opened URL, swapped for an `HttpOnly; SameSite=Strict` cookie, and required on **every** HTTP request and WebSocket upgrade.
- **Check `Host` and `Origin`** on every request and upgrade. This blocks DNS rebinding and cross-site WebSocket hijacking, as Jupyter does. `--allow-origin` exists only for the Vite dev server.
- **No `--no-auth` on any non-loopback address.** Remote access means token + TLS (`--certfile`/`--keyfile`) or an SSH tunnel.
- File APIs are confined to the workspace root: resolve symlinks, reject `..`, cap upload size.
- Strict CSP on the SPA. `text/html` and ECharts outputs render in sandboxed iframes with no access to the session.
- Single-user by design, like Jupyter Server. Multi-user hosting is out of scope.

---

## 11. Build, distribution, tests

- **Repo:** `web/` (SPA) and `FrySharp.Web/` (host). In development, Vite proxies `/session` to the backend. In release, CI builds the SPA and embeds it into `FrySharp.Web`, so Node is needed only at build time.
- **Distribution (decision §14):** a .NET global tool (`dotnet tool install -g FrySharp` → `frysharp serve`), and/or self-contained single-file binaries per OS.
- **Tests:**
  - Backend: xUnit + `WebApplicationFactory` with a real WebSocket client (auth, protocol, reconnect).
  - SPA: Vitest.
  - End to end: Playwright against a real `frysharp serve` running the sample notebooks and scripts, plus the conformance fixtures and the perf scenarios above, on the CI OS matrix.

---

## 12. VS Code extension (deferred)

When it's picked up, it is a **second client of the same session protocol**, over stdio instead of a WebSocket:
- `FrySharp.Sidecar` = `FrySharp.Core` + the protocol handlers from `FrySharp.Web`, minus HTTP.
- Notebook serializer for `.frynb`, a controller that calls `fry/notebook/*`, renderers reused from the web SPA's renderer bundle.
- The language server serves `.frycs` and notebook cells only, never plain `.cs`, which the C# extension owns.
- Other languages use VS Code's own debuggers. A DAP server is needed only for C# scripts.
- Ship as platform-specific VSIXs on the Marketplace and Open VSX.

---

## 13. Risks

| Risk | Mitigation |
|---|---|
| Phase 0 delays everything | Slice 0a is small and unblocks W1–W3. Languages move one per PR while web work continues. |
| Two UIs (desktop + web) drift apart | Shared protocol, shared theme tokens, shared TextMate grammars, shared visual render models, shared conformance fixtures. |
| Notebook memory/CPU in the browser | Virtualized cells; Monaco only for visible cells; budgets in §8 checked in CI. |
| A refresh loses running work | Server-side sessions with buffered output (§5). |
| Security mistakes in a code-execution server | §10 rules covered by tests, including a foreign-Origin WebSocket test. |
| Bundle size (Monaco is large) | Code splitting, lazy languages and workers, brotli on the embedded assets. |

## 14. Decisions for the owner

1. Approve **Phase 0 slice 0a** as the first step?
2. **Terminal:** program output only (matches the desktop), or a real interactive shell, which needs a PTY layer (ConPTY on Windows, `forkpty` on Unix) and would then also come to the desktop?
3. **Distribution:** .NET global tool, self-contained binaries, or both?
4. **Several browser tabs on one document:** last-save-wins with conflict detection (simplest), or one editor at a time with "take over"?
5. **Customizations in web:** desktop-only for now (recommended), or design a web contribution API?
