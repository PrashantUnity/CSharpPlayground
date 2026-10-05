# FrySharp Web Mode — Roadmap
> **Concept**: Run FrySharp as a CLI-hosted local server; use a React SPA in the browser as the IDE UI — exactly like JupyterLab.

---

## Overview

```
$ frysharp serve --port 5000
  → Open http://localhost:5000 in your browser
```

The full C# Code Studio experience (5-zone VS Code layout, Monaco Editor, terminal, notebooks) runs in the browser. The backend is an ASP.NET Core host that reuses **all existing services unchanged**.

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  CLI: frysharp serve --port 5000                            │
│                                                             │
│  ASP.NET Core Host                                          │
│  ├── REST API   /api/scripts, /api/packages, /api/files     │
│  ├── WebSocket  /ws/terminal, /ws/diagnostics, /ws/output   │
│  ├── Serves React SPA (wwwroot static files)                │
│  └── Reuses existing Services (zero changes needed):        │
│       ├── RoslynCompilerService                             │
│       ├── IProcessLauncher / ProcessRegistry                │
│       ├── TerminalTextBuffer                                │
│       ├── StudioLanguageServices                            │
│       ├── IToolchainProvider (Python, Node, Java, ...)      │
│       └── IPackageManager (NuGet, pip, npm)                 │
└─────────────────────────────────────────────────────────────┘
                      ↕  HTTP / WebSocket
┌─────────────────────────────────────────────────────────────┐
│  React SPA  (browser @ localhost:5000)                      │
│  ├── Monaco Editor         (@monaco-editor/react)           │
│  ├── xterm.js              (terminal / console)             │
│  ├── Zustand               (global state)                   │
│  └── Same 5-zone VS Code layout (CSS + React components)    │
└─────────────────────────────────────────────────────────────┘
```

---

## Avalonia → Web Mapping

| Avalonia / Native | Web Equivalent |
|---|---|
| `AvaloniaEdit` | Monaco Editor (`@monaco-editor/react`) |
| `TerminalTextBuffer` UI | `xterm.js` + WebSocket stdin/stdout |
| `Dispatcher.UIThread.Post` | WebSocket push → React state update |
| `[ObservableProperty]` | `useState` / `zustand` store |
| Roslyn squiggle diagnostics | `editor.setModelMarkers()` (Monaco API) |
| `GridSplitter` panels | `react-resizable-panels` |
| Activity Bar icons | React SVG / Material Icons |
| Bottom Deck tabs | React tab component |
| File Explorer tree | `react-arborist` or custom tree |
| Breadcrumbs bar | Simple React component |

---

## What Is Reused As-Is (Zero Changes)

- ✅ All `Services/` — Roslyn compiler, script runners, toolchain discovery, process registry
- ✅ All `Models/` — scripts, notebooks, cells, diagnostics, test cases
- ✅ All `ViewModels/` business logic (thin wrapper as API controllers)
- ✅ `IProcessLauncher` + `ProcessRegistry` — process tree management
- ✅ `TerminalTextBuffer` — just pipe its stream over WebSocket

---

## Phased Roadmap

### Phase 1 — Backend Scaffolding
- [ ] New project: `FrySharp.Web` (ASP.NET Core minimal API)
- [ ] `frysharp serve` CLI verb (using `System.CommandLine`)
- [ ] Register existing DI services into ASP.NET Core container
- [ ] REST endpoints: list files, open file, save file, run script, stop
- [ ] Serve React SPA static files from `wwwroot/`
- [ ] Basic auth / single-user token (security for local network use)

### Phase 2 — React SPA Foundation
- [ ] Vite + React + TypeScript project in `FrySharp.Web/ClientApp/`
- [ ] 5-zone layout (Activity Bar, Sidebar, Editor, Bottom Deck, Status Bar)
- [ ] Monaco Editor integration (`@monaco-editor/react`)
- [ ] File Explorer panel (list, open, create, rename, delete files)
- [ ] Tab bar for open files
- [ ] Breadcrumbs bar

### Phase 3 — Real-Time: Terminal, Output, Diagnostics
- [ ] WebSocket hub: `ws/terminal` — stream stdout/stderr, accept stdin
- [ ] `xterm.js` terminal component in Bottom Deck
- [ ] WebSocket: `ws/diagnostics` — push Roslyn errors → Monaco `setModelMarkers()`
- [ ] Output panel streaming (compiler logs, runner logs)
- [ ] Problems panel (list of diagnostics with click-to-navigate)

### Phase 4 — Full Feature Parity
- [ ] Run / Stop / Debug toolbar buttons
- [ ] Status Bar: language selector, line/col, Roslyn status, execution timer
- [ ] NuGet / pip / npm package manager panel
- [ ] Search panel (file content search)
- [ ] Settings panel
- [ ] Keyboard shortcuts (VS Code bindings via Monaco + custom handlers)
- [ ] Dark+ / Light+ theme switching

### Phase 5 — Notebook Support
- [ ] Notebook view (multi-cell layout) — Monaco multi-model
- [ ] Cell run / add / delete / move
- [ ] Rich output rendering: tables, images, HTML (`.DUMP` / Results panel)
- [ ] Polyglot notebook cells (C#, Python, JS mixed)
- [ ] Fry Kernel Protocol over WebSocket

---

## Key Tech Stack (Frontend)

| Package | Purpose |
|---|---|
| `@monaco-editor/react` | Code editor (same engine as VS Code) |
| `xterm.js` | Terminal emulator |
| `zustand` | Lightweight global state |
| `react-resizable-panels` | Resizable splitter panels |
| `react-arborist` | File tree (virtual, performant) |
| `vite` | Fast dev server + bundler |

---

## CLI Design (Sketch)

```bash
# Start web server
frysharp serve

# Custom port
frysharp serve --port 8888

# Serve specific workspace folder
frysharp serve --workspace ~/my-scripts

# Open browser automatically
frysharp serve --open

# No auth (trusted local only)
frysharp serve --no-auth
```

---

## Security Considerations

- Default: single-use token in URL (like JupyterLab)  
- Option: disable auth for trusted LAN use (`--no-auth`)  
- HTTPS via `dotnet dev-certs` for secure local serving  
- No remote access by default — bind to `localhost` only  

---

> [!NOTE]
> The native Avalonia app and web mode share 100% of the backend service layer. The React app is purely a UI skin — the engine is identical.

> [!TIP]
> Start with Phase 1 + Phase 2 to get a working skeleton quickly. The WebSocket work in Phase 3 is where the real interactivity comes alive.
