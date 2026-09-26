# VS Code Layout & Ergonomics Mandate (C# Code Studio)

This rule applies exclusively to `examples/CSharpEditorPlugin` (`com.frypdf.plugin.csharpeditor`) and standalone executable (`FrySharp`).

## 1. Five-Zone Layout Architecture
The studio interface MUST strictly adhere to the 5-zone Visual Studio Code design:
1. **Activity Bar** (48px fixed width):
   - Vertical rail on the left.
   - Icons: Explorer, Search, Run & Debug, Dependencies & Packages (NuGet/pip/npm), Scratchpad/Notes, Problems (with error badge).
   - Bottom icons: Settings and Return to Hub.
   - Active icon has high-contrast left accent bar.
   - Clicking active icon toggles the Primary Side Bar.
2. **Primary Side Bar** (~270px, resizable, collapsible):
   - ONLY ONE sidebar in horizontal flow. No multi-column sidebar clutter.
   - Dynamically displays the view selected in the Activity Bar.
   - Explorer toolbar features: New File (`FilePlusOutline`), New Folder (`FolderPlusOutline`), Open Project (`FolderOpenOutline`), Refresh (`Refresh`), and Collapse All (`ArrowCollapseVertical`).
   - Supports multi-language file types (`.cs`, `.csx`, `.frycs`, `.py`, `.js`, `.ipynb`, `.csnb`) with distinct language file icons.
   - Can be collapsed/expanded via `Ctrl+B` or clicking the active Activity Bar icon.
3. **Editor Area**:
   - Multi-tab document bar at top showing all open scripts/notebooks side-by-side with close button (`×`), dirty status indicator (`●`), language icon, and new tab (`+`) button.
   - Editor action toolbar on the right: Run, Debug (when supported), Stepping controls, Format Document, Word Wrap, Find, Bottom Panel toggle.
   - Dedicated 24px breadcrumbs bar below tabs displaying file path and active language/toolchain runtime (`workspace > scripts > {FileName} > {Language} ({Toolchain/Version})`).
   - AvaloniaEdit code canvas with language syntax highlighting, language indentation strategies, breakpoints, folding, line numbers, and hover tooltips.
4. **Bottom Panel / Dock** (dynamic resizable, collapsible):
   - Tabbed deck: Problems (with one-click "Install <package>" fixes for missing dependencies), Output, Terminal/Console (with interactive stdin input support), Debug Console (REPL), Results (.Dump), Test Cases.
   - Dynamic vertical resizing via `GridSplitter` and `BottomDeckGridLength` without clipping results.
   - Collapsible with `Ctrl+J`.
5. **Status Bar** (22px high, bottom):
   - Remote badge (`>< C# Studio`), Problems counter `(× 0 ! 0)`, compiler/language runner state, execution timer, Ln/Col, Spaces: 4, UTF-8, Active Language & Toolchain selector (`C# (.NET 10 Roslyn)`, `Python 3.12 (.venv)`, etc.), Bottom Panel toggle.

## 2. Keyboard Ergonomics
- `Ctrl+B` (Mac: `Cmd+B`): Toggle Primary Side Bar.
- `Ctrl+J` (Mac: `Cmd+J`): Toggle Bottom Panel.
- `Ctrl+S` (Mac: `Cmd+S`): Save Active Script.
- `F5`: Debug / Continue (or Run when debugger not supported by language).
- `Ctrl+F5`: Run Script without Debugging.
- `Shift+F5`: Stop Execution / Stop Debugging.
- `F10`: Step Over.
- `F11`: Step Into.
- `Ctrl+K Ctrl+D` or `Shift+Alt+F`: Format Document.
- `Ctrl+K Ctrl+I`: Show Hover (Quick Info) for the symbol at the caret.
- `Ctrl+F`: Find & Replace.
- `Ctrl+Shift+E`: Focus Explorer.
- `Ctrl+Shift+F`: Focus Search.
- `Ctrl+Shift+D`: Focus Run & Debug.
- `Ctrl+Shift+M`: Focus Problems.

## 3. Performance & Zero UI-Thread Freeze
- Heavy initialization (Roslyn compiler, assembly reflection, NuGet/pip package operations, toolchain resolution, script execution) must NEVER execute on the UI thread.
- Always use `Task.Run` for compilation, toolchain probing, and script evaluation with `CancellationToken`.
- Return updates to UI thread via `Dispatcher.UIThread.Post`.
- Spawn all external processes through `IProcessLauncher` registered with `ProcessRegistry` to ensure complete process tree termination.
