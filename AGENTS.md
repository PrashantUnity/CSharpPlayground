# C# Code Studio (CSharpEditorPlugin) Development & Ergonomics Mandate

This document defines the strict architectural, visual, and ergonomic standards for the **C# Code Studio** (`com.frypdf.plugin.csharpeditor`) in FryPDF. All AI agents and contributors modifying this project must strictly comply with this mandate.

---

## 1. Core Paradigm: Authentic VS Code Ergonomics
C# Code Studio is not a basic overlay widget; it is a full-fledged, high-performance in-app IDE for .NET 10 document scripting, automation, algorithms, and interactive notebooks. Its layout, chrome, navigation, and keybindings must faithfully mirror **Visual Studio Code**.

---

## 2. The 5-Zone Layout Mandate
Every main studio interface (both script code studio and notebook studio) must strictly follow the 5-zone VS Code structure:

### Zone 1: Activity Bar (Leftmost, 48px fixed)
- Vertical rail of tool icons:
  - **Explorer** (`FolderMultipleOutline` / `FileTreeOutline`)
  - **Search** (`Magnify`)
  - **Run & Debug** (`BugPlayOutline` / `PlayBoxOutline`)
  - **Dependencies & NuGet** (`PackageVariantClosed`)
  - **Scratchpad & Notes** (`FileDocumentOutline`)
  - **Problems** (`AlertCircleOutline` with error badge)
- Bottom utilities: **Settings** (`CogOutline`) and **Return to Hub** (`ArrowLeft` / `HomeOutline`).
- Active item displays a 2px high-contrast vertical accent line on the left edge (`#007ACC` / `{DynamicResource M3PrimaryBrush}`).
- Clicking the active icon toggles the Primary Side Bar closed/open (`Ctrl+B`).

### Zone 2: Primary Side Bar (Resizable ~270px, Collapsible)
- **Single Side Bar Rule**: Only ONE primary side bar may exist in the horizontal flow. Never place multiple sidebars side-by-side or insert hardcoded fixed middle columns.
- Hosts the view corresponding to the active Activity Bar icon.
- Explorer header features standard action buttons: **New File** (`FilePlusOutline`), **New Folder** (`FolderPlusOutline`), **Open Project** (`FolderOpenOutline`), **Refresh** (`Refresh`), and **Collapse All** (`ArrowCollapseVertical`).
- Collapsible via `IsSideBarVisible` or keyboard shortcut `Ctrl+B`.

### Zone 3: Editor Area (Dominant Central Canvas)
- **Editor Multi-Tab Bar**: Located at the top, showing all open scripts/notebooks side-by-side in a horizontal scrollable strip with close buttons (`×`), dirty status indicators (`●`), file-type icons, and a new tab (`+`) button.
- **Editor Toolbar**: Located at top-right of the editor header with Run, Debug, Stepping controls (when paused), Format Document, Word Wrap, Find, and Bottom Panel toggle.
- **Breadcrumbs Bar**: Subtle 24px breadcrumb trail below tabs (`workspace > scripts > File.csx > C# (.NET 10)`).
- **Code Canvas**: AvaloniaEdit text editor with Dark+ theme, line numbers, folding markers, breakpoint gutter, debug line highlighter, and debug hover tooltips.

### Zone 4: Bottom Panel / Tool Deck (Resizable, Collapsible via `Ctrl+J`)
- VS Code tabbed panel:
  - `PROBLEMS` (badge: `(×) {ErrorCount}`)
  - `OUTPUT` (Roslyn compiler output)
  - `TERMINAL / CONSOLE` (execution stdout/stderr)
  - `DEBUG CONSOLE (REPL)` (immediate expression evaluation prompt)
  - `RESULTS (.DUMP)` (rich interactive tables, HTML viewer, object inspector)
  - `TEST CASES` (unit/algorithm test cases)
- Right-side actions: Clear Output, Maximize/Restore, Close (`×`).

### Zone 5: Status Bar (Bottom, 22px)
- Left: Remote/Workspace badge (`>< C# Studio`), Problems counter `(× 0 ! 0)`, Roslyn compiler status (`Ready` / `Compiling...`), Run/Pause state.
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
- All background tasks must use `Task.Run` with `CancellationToken`.
- Property updates back to the UI thread must be dispatched via `Dispatcher.UIThread.Post`.
- Visual components must clean up timers, events, and background tasks when unloaded or unmounted.

---

## 5. Architectural MVVM Separation
- Maintain clear boundaries:
  - `Views/`: Pure XAML views with minimal code-behind focused on AvaloniaEdit and keyboard routing.
  - `ViewModels/`: Reactive ViewModels based on `CommunityToolkit.Mvvm` (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`).
  - `Models/`: Immutable or POCO data models for scripts, notebooks, cells, diagnostics, and test cases.
  - `Services/`: Isolated headless engines for compilation, execution, debugging, storage, and NuGet resolution.
  - `Controls/`: Reusable specialized Avalonia controls (margins, hover tips, syntax themes).

---

## 6. Component Architecture & Codebase Health Mandate
All contributors and agents must follow `.agents/rules/component_architecture_and_reuse_mandate.md`:
- **Line budgets**: AXAML views < 400 lines, View code-behind < 150 lines, ViewModels < 400 lines per file (use domain partials e.g. `.Explorer.cs`, `.Debugging.cs`), Services < 500 lines.
- **Mandatory Control Reusability**: Shared UI (Activity Bar, Status Bar, Bottom Tool Deck, Explorer Panel, Search Panel, Breadcrumbs, Tab Bar) must be implemented as reusable controls in `Controls/`.
- **Shared Styles**: Centralize styles in `Controls/SharedStudioStyles.axaml`. Never duplicate hundreds of lines in individual `<UserControl.Styles>`.
- **100% Backward Compatibility**: All 237+ automated unit tests must continue to pass with 0 warnings and 0 errors.

