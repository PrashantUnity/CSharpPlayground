# C# Code Studio (CSharpEditorPlugin) Development & Ergonomics Mandate

This document defines the strict architectural, visual, and ergonomic standards for the **C# Code Studio** (`com.frypdf.plugin.csharpeditor`) in FryPDF and standalone executable (**FrySharp**). All AI agents and contributors modifying this project must strictly comply with this mandate.

---

## 1. Core Paradigm: Authentic VS Code Ergonomics
C# Code Studio is not a basic overlay widget; it is a full-fledged, high-performance in-app IDE for .NET 10 document scripting, automation, algorithms, and polyglot interactive notebooks. While engineered with first-class Roslyn compilation for C#, the studio natively supports multiple languages (C#, Python, and extensible external language modules such as JavaScript/Node) through an extensible toolchain and kernel architecture, with roadmap support planned for compiled languages: **Java**, **C++**, and **C**. Its layout, chrome, navigation, and keybindings must faithfully mirror **Visual Studio Code**.

---

## 2. The 5-Zone Layout Mandate
Every main studio interface (both script code studio and notebook studio) must strictly follow the 5-zone VS Code structure:

### Zone 1: Activity Bar (Leftmost, 48px fixed)
- Vertical rail of tool icons:
  - **Explorer** (`FolderMultipleOutline` / `FileTreeOutline`)
  - **Search** (`Magnify`)
  - **Run & Debug** (`BugPlayOutline` / `PlayBoxOutline`)
  - **Dependencies & Packages** (`PackageVariantClosed` — NuGet for C#, pip for Python, npm for JS/Node)
  - **Scratchpad & Notes** (`FileDocumentOutline`)
  - **Problems** (`AlertCircleOutline` with error badge)
- Bottom utilities: **Settings** (`CogOutline`) and **Return to Hub** (`ArrowLeft` / `HomeOutline`).
- Active item displays a 2px high-contrast vertical accent line on the left edge (`#007ACC` / `{DynamicResource M3PrimaryBrush}`).
- Clicking the active icon toggles the Primary Side Bar closed/open (`Ctrl+B`).

### Zone 2: Primary Side Bar (Resizable ~270px, Collapsible)
- **Single Side Bar Rule**: Only ONE primary side bar may exist in the horizontal flow. Never place multiple sidebars side-by-side or insert hardcoded fixed middle columns.
- Hosts the view corresponding to the active Activity Bar icon.
- Explorer header features standard action buttons: **New File** (`FilePlusOutline`), **New Folder** (`FolderPlusOutline`), **Open Project** (`FolderOpenOutline`), **Refresh** (`Refresh`), and **Collapse All** (`ArrowCollapseVertical`).
- Multi-language file tree supporting `.cs`, `.csx`, `.frycs`, `.py`, `.js`, `.java`, `.cpp`, `.cc`, `.c`, `.h`, `.hpp`, `.ipynb`, `.csnb`, displaying language-specific file icons and color accents.
- Collapsible via `IsSideBarVisible` or keyboard shortcut `Ctrl+B`.

### Zone 3: Editor Area (Dominant Central Canvas)
- **Editor Multi-Tab Bar**: Located at the top, showing all open scripts/notebooks side-by-side in a horizontal scrollable strip with close buttons (`×`), dirty status indicators (`●`), file-type icons, and a new tab (`+`) button.
- **Editor Toolbar**: Located at top-right of the editor header with Run, Debug (when supported by active language capabilities), Stepping controls (when paused), Format Document, Word Wrap, Find, and Bottom Panel toggle.
- **Breadcrumbs Bar**: Subtle 24px breadcrumb trail below tabs showing path and active language runtime (`workspace > scripts > {FileName} > {Language} ({Toolchain/Version})`, e.g. `scripts > main.py > Python (3.12)` or `scripts > File.csx > C# (.NET 10 Roslyn)`).
- **Code Canvas**: AvaloniaEdit text editor with Dark+ and Light+ themes, language-specific syntax highlighting definitions (`IHighlightingDefinition`), language-aware indentation strategies (`IIndentationStrategy`), line numbers, folding markers, breakpoint gutter, debug line highlighter, hover Quick Info (signature, container and XML docs of the symbol under the pointer) and debug hover tooltips.

### Zone 4: Bottom Panel / Tool Deck (Resizable, Collapsible via `Ctrl+J`)
- VS Code tabbed panel:
  - `PROBLEMS` (badge: `(×) {ErrorCount}`): Live Roslyn diagnostics for C#, and run-time traceback/diagnostic parsers (`IDiagnosticParser`) for external languages with "Install <package>" quick fixes.
  - `OUTPUT`: Roslyn compiler output, language runner process logs, and package installation streams.
  - `TERMINAL / CONSOLE`: Execution stdout/stderr with interactive stdin support via `TerminalTextBuffer` (supports user `input()` during script execution).
  - `DEBUG CONSOLE (REPL)`: Immediate expression evaluation prompt.
  - `RESULTS (.DUMP)`: Rich interactive tables (`DataTable`, `DataFrame`), HTML viewer, images (`Display.Image`), and object inspector.
  - `TEST CASES`: Unit/algorithm test cases and verification assertions.
- Dynamic vertical expansion via `GridSplitter` and `BottomDeckGridLength` without clipping results.
- Right-side actions: Clear Output, Maximize/Restore, Close (`×`).
- Collapsible with `Ctrl+J`.

### Zone 5: Status Bar (Bottom, 22px)
- Left: Remote/Workspace badge (`>< C# Studio`), Problems counter `(× 0 ! 0)`, Roslyn/Language compiler/runner status (`Ready` / `Compiling...` / `Running...`), Run/Pause state.
- Right: Execution timer (`⏱ 14ms`), `Ln X, Col Y`, `Spaces: 4`, `UTF-8`, Active Language & Toolchain selector/status (`C# (.NET 10 Roslyn)`, `Python 3.12 (.venv)`, etc.), Bottom Deck toggle button.

---

## 3. Keyboard Shortcuts Mandate
Every studio view must register and honor standard VS Code shortcuts:
- `Ctrl+B` (Mac: `Cmd+B`): Toggle Primary Side Bar.
- `Ctrl+J` (Mac: `Cmd+J`): Toggle Bottom Panel / Terminal.
- `Ctrl+S` (Mac: `Cmd+S`): Save Active Script.
- `F5`: Start Debugging (C#) / Run Script (when debugger not supported by language).
- `Ctrl+F5`: Run Script without Debugging.
- `Shift+F5`: Stop Execution / Stop Debugging.
- `F10`: Step Over.
- `F11`: Step Into.
- `Ctrl+K Ctrl+D` or `Shift+Alt+F`: Format Document.
- `Ctrl+K Ctrl+I`: Show Hover (Quick Info) for the symbol at the caret.
- `Ctrl+F` (Mac: `Cmd+F`): Find & Replace.
- `Ctrl+Shift+E`: Focus Explorer in SideBar.
- `Ctrl+Shift+F`: Focus Search in SideBar.
- `Ctrl+Shift+D`: Focus Run & Debug in SideBar.
- `Ctrl+Shift+M`: Focus Problems in Bottom Deck.

---

## 4. Performance & Zero UI-Thread Freeze
- **NEVER** construct `RoslynCompilerService`, resolve NuGet/pip/npm packages, probe external toolchains (`IToolchainProvider`), run external processes, perform heavy reflection, or run scripts on the Avalonia UI thread.
- All background tasks must use `Task.Run` with `CancellationToken`.
- Property updates back to the UI thread must be dispatched via `Dispatcher.UIThread.Post`.
- Visual components must clean up timers, events, and background tasks when unloaded or unmounted.
- All external child processes must be spawned through `IProcessLauncher` and registered with `ProcessRegistry` (using Job Objects on Windows and process groups on Unix) so all process trees are terminated cleanly on Stop, tab close, or plugin unload.

---

## 5. Architectural MVVM Separation
- Maintain clear boundaries:
  - `Views/`: Pure XAML views with minimal code-behind focused on AvaloniaEdit and keyboard routing.
  - `ViewModels/`: Reactive ViewModels based on `CommunityToolkit.Mvvm` (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`). Partition complex ViewModels into domain partials (e.g. `.Explorer.cs`, `.Languages.cs`, `.ExternalRun.cs`, `.Debugging.cs`, `.Tabs.cs`).
  - `Models/`: Immutable or POCO data models for scripts, notebooks, cells, diagnostics, and test cases.
  - `Services/`: Isolated headless engines for compilation, execution, debugging, storage, toolchain discovery, and package management.
  - `Controls/`: Reusable specialized Avalonia controls (margins, hover tips, syntax themes, tab bars, tool decks).

---

## 6. Component Architecture & Codebase Health Mandate
All contributors and agents must follow `.agents/rules/component_architecture_and_reuse_mandate.md`:
- **Line budgets**: AXAML views < 400 lines, View code-behind < 150 lines, ViewModels < 400 lines per file (use domain partials e.g. `.Explorer.cs`, `.Languages.cs`, `.ExternalRun.cs`), Services < 500 lines.
- **Mandatory Control Reusability**: Shared UI (Activity Bar, Status Bar, Bottom Tool Deck, Explorer Panel, Search Panel, Breadcrumbs, Tab Bar) must be implemented as reusable controls in `Controls/`.
- **Shared Styles**: Centralize styles in `Controls/SharedStudioStyles.axaml`. Never duplicate hundreds of lines in individual `<UserControl.Styles>`.
- **100% Backward Compatibility**: All 1,100+ automated unit tests must continue to pass with 0 warnings and 0 errors.

---

## 7. Multi-Language & Polyglot Architecture Mandate
The studio runs C# in-process (Roslyn) and other languages (Python today, JavaScript/Node and others via module plugins) with the user's installed toolchains. A language is an isolated module registered in `StudioLanguageServices`; views and view models switch features strictly on its `LanguageCapabilities`, never on a language's string id.
- **One Module Per Language**: Every language derives from `LanguageDefinition` in `Services/Languages/<Name>/`. Core IDE features adapt generically to what the definition exposes.
- **Toolchain Discovery**: Extensible languages implement `IToolchainProvider` to find local interpreters (project `.venv`/`env`, studio-managed environments, PATH via login shell `IHostEnvironment.GetLoginShellPathAsync`). Provide `MissingToolchainGuidance` with per-platform installation commands when missing.
- **Process & Script Execution**: Start programs exclusively through `IProcessLauncher` registered with `ProcessRegistry` (so they are reliably killed on Stop, tab close, or plugin unload). Read machine state only through `IHostEnvironment` (so tests can fake it). Interactive stdin streams via `TerminalTextBuffer`.
- **Polyglot Notebooks & Fry Kernel Protocol**: Polyglot notebook cells communicate via JSON lines over stdin/stdout pipes (`ProtocolKernel`). Cross-language variable sharing is supported via `#!share --from <language> <variable> [--as <name>]`.
- **Document Storage Kinds (`LanguageStorageKind`)**:
  Languages specify their persistence model: `FryDocument` (JSON metadata, notes, and breakpoints for C# `.frycs`) or `SourceFile` (plain text source files like `.py`, `.js`, `.csx`, `.java`, `.cpp`, `.c` readable by external editors and git).
- **Compiled Languages Architecture (Java, C++, C Roadmap)**:
  Compiled languages execute through a two-phase `ScriptRunPlan` in `ScriptRunExecutor`:
  1. *Build Step* (`ProcessStep` with `IsBuildStep = true`): Calls the language compiler (`javac` for Java, `clang++`/`g++` for C++, `clang`/`gcc` for C). If compilation fails, the executor stops immediately, parses compiler error streams into structured diagnostics via `IDiagnosticParser` (e.g. `JavaCompilerDiagnosticParser`, `ClangDiagnosticParser` for line/column errors), and populates the Problems panel without executing.
  2. *Run Step* (`ProcessStep` with `IsBuildStep = false`): Executes the generated byte code (`java ClassName`) or native binary (`./a.out` / `.exe`), with full interactive stdin streaming via `TerminalTextBuffer`.
  3. *Toolchain Discovery*: `IToolchainProvider` discovers local compilers and JDKs (`JAVA_HOME`, PATH, Xcode Command Line Tools, Visual Studio C++ Build Tools) with automated version probing and actionable missing-toolchain guidance (`MissingToolchainGuidance`).
  4. *Polyglot Notebook Integration*: Kernels for compiled languages speak the Fry Kernel Protocol (`ProtocolKernel`) via dedicated interactive runtime wrappers (e.g., JShell for Java, Cling/custom REPL for C/C++), supporting cell evaluation and cross-language value sharing (`#!share`).
- **Testing Mandate**: Tests build `new StudioLanguageServices(tempFolder)` and never touch user toolchain settings or live environments. Real-toolchain tests use `[<Language>Fact]` (skipped when runtime not installed, e.g. `[PythonFact]` skipped without Python 3.9+, required in CI via `FRY_REQUIRE_PYTHON=1`).
- To add or modify a language, follow [`docs/adding-a-language.md`](docs/adding-a-language.md) and [`docs/kernel-protocol.md`](docs/kernel-protocol.md).

---

## 8. Seeing the UI: Headless Snapshots
To check a UI change without launching the app, render the real views to PNG with `tools/UiSnapshots` (`dotnet build tools/UiSnapshots`, then `dotnet tools/UiSnapshots/bin/Debug/net10.0/UiSnapshots.dll help`) and open the image it prints. `tools/UiSnapshots/images.py` zooms, overlays coordinates and diffs before/after renders. Guide: [`docs/headless-ui-snapshots.md`](docs/headless-ui-snapshots.md).
