# C# Code Studio (CSharpEditorPlugin) Project Rule & Mandate

This document defines the project-specific architecture, ergonomics, and implementation rules for **C# Code Studio** (`com.frypdf.plugin.csharpeditor`). This rule applies strictly to this project.

---

## 1. Core Paradigm: Authentic VS Code Ergonomics
C# Code Studio is a premier, in-app IDE for .NET 10 document automation, Roslyn scripting, algorithms, and interactive notebooks within FryPDF.
Its layout, structure, chrome, navigation, and keybindings must faithfully adhere to **Visual Studio Code**.

---

## 2. The 5-Zone VS Code Layout Mandate
Every main studio interface must strictly follow the 5-zone VS Code structure:

```
┌────┬──────────────────────┬──────────────────────────────────────────────────┐
│ A  │   PRIMARY SIDE BAR   │                   EDITOR AREA                    │
│ C  │                      │                                                  │
│ T  │  (Dynamic view based │  [Tab: Script.csx ×] [Breadcrumbs: frypdf > ...] │
│ I  │   on selected tool:  ├──────────────────────────────────────────────────┤
│ V  │   Explorer, Search,  │  AvaloniaEdit Code Canvas                        │
│ I  │   Debug, NuGet,      │  (Breakpoints, Syntax, Folding, Hover Tooltip)   │
│ T  │   Templates, Notes,  │                                                  │
│ Y  │   Problems)          ├──────────────────────────────────────────────────┤
│    │                      │                BOTTOM PANEL / DOCK               │
│ B  │                      │  [PROBLEMS] [OUTPUT] [CONSOLE] [REPL] [.DUMP]    │
│ A  │                      │  (Resizable splitter, collapsible with Ctrl+J)   │
│ R  │                      │                                                  │
├────┴──────────────────────┴──────────────────────────────────────────────────┤
│                          STATUS BAR (22px fixed)                             │
│ >< C# Studio  (× 0 ! 0)  Ready   Ln 12, Col 4   Spaces: 4   UTF-8   C#       │
└──────────────────────────────────────────────────────────────────────────────┘
```

### Zone 1: Activity Bar (Leftmost, 48px fixed)
- Fixed 48px vertical rail on the left edge.
- Standard tool icons:
  - **Explorer** (`FolderMultipleOutline`)
  - **Search** (`Magnify`)
  - **Run & Debug** (`BugPlayOutline`)
  - **Dependencies & NuGet** (`PackageVariantClosed`)
  - **Scratchpad & Notes** (`FileDocumentOutline`)
  - **Problems** (`AlertCircleOutline` with error badge)
- Bottom utilities: **Settings** (`CogOutline`) and **Return to Hub** (`ArrowLeft`).
- Active item displays a 2px vertical high-contrast accent indicator on the left edge (`#007ACC` / `{DynamicResource M3PrimaryBrush}`).
- Clicking the active item toggles the Primary Side Bar closed/open (`Ctrl+B`).

### Zone 2: Primary Side Bar (Resizable ~270px, Collapsible)
- **Single Side Bar Rule**: Only ONE primary side bar may exist in the horizontal flow. Never place multiple sidebars side-by-side (no redundant 420px middle columns).
- Dynamically renders the view corresponding to the active Activity Bar icon.
- Explorer header features standard action buttons: **New File** (`FilePlusOutline`), **New Folder** (`FolderPlusOutline`), **Open Project** (`FolderOpenOutline`), **Refresh** (`Refresh`), and **Collapse All** (`ArrowCollapseVertical`).
- Features standard all-caps header with section title and contextual action buttons.
- Collapsible via `IsSideBarVisible` or keyboard shortcut `Ctrl+B`.

### Zone 3: Editor Area (Central Canvas)
- **Editor Multi-Tab Bar**: Located at the top of the editor canvas with all open script/notebook tabs displayed side-by-side in a horizontal scrollable strip, close buttons (`×`), dirty status dots (`●`), file-type icons, and a new tab (`+`) button.
- **Editor Action Toolbar**: Located at the top-right of the editor header with Run, Debug, Stepping controls, Format Document, Word Wrap, Find, and Bottom Panel toggle.
- **Breadcrumbs Bar**: Dedicated 24px navigation trail below tabs (`scripts > {Script.Title} > C# (.NET 10 Roslyn)`).
- **Code Canvas**: AvaloniaEdit text editor with Dark+ theme, line numbers, folding markers, breakpoint gutter, debug line highlighter, and debug hover tooltips.

### Zone 4: Bottom Panel / Tool Deck (Dynamic Resizable, Collapsible)
- Tabbed deck:
  - `PROBLEMS` (badge: `(×) {ErrorCount}`)
  - `OUTPUT` (Roslyn compiler output)
  - `TERMINAL / CONSOLE` (execution stdout/stderr)
  - `DEBUG CONSOLE (REPL)` (immediate expression evaluation prompt)
  - `RESULTS (.DUMP)` (rich interactive tables, HTML viewer, object inspector)
  - `TEST CASES` (unit/algorithm test cases)
- Dynamic vertical expansion via `GridSplitter` and `BottomDeckGridLength` (expands smoothly without clipping results).
- Right-side actions: Clear Output, Maximize/Restore, Close (`×`).
- Collapsible with `Ctrl+J`.

### Zone 5: Status Bar (Bottom, 22px fixed)
- Left: Remote/Workspace pill (`>< C# Studio`), Problems counter `(× 0 ! 0)`, Roslyn compiler status (`Ready` / `Compiling...`), Run/Pause state.
- Right: Execution timer (`⏱ 14ms`), `Ln X, Col Y`, `Spaces: 4`, `UTF-8`, `C# (.NET 10 Roslyn)`, Bottom Deck toggle button.

---

## 3. Keyboard Shortcuts Mandate
Every studio view must register and honor standard VS Code shortcuts:
- `Ctrl+B` (Mac: `Cmd+B`): Toggle Primary Side Bar.
- `Ctrl+J` (Mac: `Cmd+J`): Toggle Bottom Panel / Terminal.
- `Ctrl+S` (Mac: `Cmd+S`): Save Active Script.
- `F5`: Start Debugging / Continue Execution.
- `Ctrl+F5`: Run Script without Debugging.
- `Shift+F5`: Stop Execution / Stop Debugging.
- `F10`: Step Over.
- `F11`: Step Into.
- `Ctrl+K Ctrl+D` or `Shift+Alt+F`: Format Document.
- `Ctrl+F` (Mac: `Cmd+F`): Find & Replace.
- `Ctrl+Shift+E`: Focus Explorer in SideBar.
- `Ctrl+Shift+F`: Focus Search in SideBar.
- `Ctrl+Shift+D`: Focus Run & Debug in SideBar.
- `Ctrl+Shift+M`: Focus Problems in Bottom Deck.

---

## 4. Performance & Zero UI-Thread Freeze
- **NEVER** construct `RoslynCompilerService`, resolve NuGet packages, perform heavy reflection, or run scripts on the Avalonia UI thread.
- All background operations must run in `Task.Run` with a `CancellationToken`.
- UI updates must be dispatched via `Dispatcher.UIThread.Post`.
- Visual components must clean up timers, events, and background tasks when unloaded or unmounted.

---

## 5. Architectural MVVM Separation
- `Views/`: Pure XAML views with minimal code-behind focused on AvaloniaEdit, focus management, and keyboard routing.
- `ViewModels/`: Reactive ViewModels based on `CommunityToolkit.Mvvm` (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`).
- `Models/`: Immutable or POCO data models for scripts, notebooks, cells, diagnostics, and test cases.
- `Services/`: Isolated headless engines for compilation, execution, debugging, storage, and NuGet resolution.
- `Controls/`: Reusable specialized Avalonia controls (margins, hover tips, syntax themes).
