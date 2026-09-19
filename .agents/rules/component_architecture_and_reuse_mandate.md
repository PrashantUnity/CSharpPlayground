# Component Architecture, Control Reusability & Codebase Health Mandate

This rule applies strictly to `examples/CSharpEditorPlugin` (`com.frypdf.plugin.csharpeditor`). All AI agents and developers modifying this codebase must adhere to the following architecture, reusability, and file budget constraints.

---

## 1. File Size Budgets & Decomposition Limits

To keep files maintainable, human-reviewable, and resilient against merge conflicts:

| Layer | Maximum Line Budget | Action Required When Exceeded |
| :--- | :--- | :--- |
| **AXAML Views (`.axaml`)** | **~400 lines** | Extract UI zones (Activity Bar, SideBar panels, Tool Deck, Status Bar, Breadcrumbs, Tab Bar, Dialogs) into reusable `UserControl`s under `Controls/` or `Views/Shared/`. |
| **View Code-Behind (`.axaml.cs`)** | **~150 lines** | Restrict code-behind to view-only plumbing (editor margins, text folding hooks, drag-and-drop, tunnel keyboard routing). Business and orchestration logic belongs in ViewModels or Services. |
| **ViewModels (`.cs`)** | **~400 lines per file** | Partition multi-domain ViewModels into domain partials (e.g. `.Explorer.cs`, `.Debugging.cs`, `.Search.cs`, `.Tabs.cs`, `.BottomDeck.cs`, `.Testing.cs`) or decompose into specialized child ViewModels. |
| **Services (`.cs`)** | **~500 lines** | Extract large hardcoded templates, data seeders, or specialized helper engines into separate focused service classes. |

---

## 2. Mandatory Control Reusability

1. **Single Source of Truth for Shared UI**:
   - Any UI element, tool deck, panel, or bar used across more than one studio surface (e.g., across `CSharpCodeStudioView` and `CSharpNotebookStudioView`) **MUST** be implemented as a reusable `UserControl` in `Controls/`.
   - Never copy-paste layout definitions or controls across views.

2. **Standard Reusable Studio Controls**:
   - `StudioActivityBarControl`: Reusable 48px rail for Zone 1.
   - `StudioStatusBarControl`: Reusable 22px bar for Zone 5.
   - `StudioBottomDeckControl`: Reusable 6-tab tool deck for Zone 4.
   - `StudioExplorerPanelControl`: Reusable file tree and context menus for Zone 2.
   - `StudioSearchPanelControl`: Reusable search & replace panel for Zone 2.
   - `StudioBreadcrumbsControl`: Reusable navigation breadcrumbs strip for Zone 3.
   - `StudioTabBarControl`: Reusable multi-document tab strip for Zone 3.
   - `QuickOpenOverlayControl`: Reusable file/symbol quick open palette.

3. **Centralized Style Dictionaries**:
   - Common styling classes (`activity-bar-btn`, `vscode-tab`, `ide-tab-btn`, `ide-tab-badge`, `explorer-rename-box`, `sidebar-search-box`, `cs-toolbar-*`) must reside in shared resource dictionaries (`Controls/SharedStudioStyles.axaml`).
   - Do **NOT** duplicate 200+ lines of identical styles in individual `<UserControl.Styles>` sections.

---

## 3. ViewModel Domain Partial Pattern

When a ViewModel manages multiple complex domains (e.g., Roslyn script studio with tabs, debugger, compiler, explorer, search, nuget):
- Maintain the class as a `public partial class [Name]ViewModel : ObservableObject`.
- Separate concerns into clean feature files following the naming pattern:
  - `[Name]ViewModel.cs` — Primary constructor, dependencies, core observable properties, and lifecycle.
  - `[Name]ViewModel.Tabs.cs` — Tab collection, switching, document tracking, dirty states.
  - `[Name]ViewModel.Explorer.cs` — Workspace file tree, directory creation, rename, duplicate, delete.
  - `[Name]ViewModel.Search.cs` — Search in files, regex/word matching, results collection.
  - `[Name]ViewModel.Debugging.cs` — Breakpoints, stepping, pause/resume, call stack, variables.
  - `[Name]ViewModel.NuGet.cs` — Package search, metadata fetching, installation, dependency tracking.
  - `[Name]ViewModel.BottomDeck.cs` — Output routing, terminal streaming, REPL command evaluation.
  - `[Name]ViewModel.Testing.cs` — Test case discovery and test execution engine.
- This pattern preserves **100% binary and API backward compatibility** with all existing unit tests and XAML bindings while slashing individual file sizes.

---

## 4. Zero UI-Thread Freezes & Performance Rules

- Heavy initialization (Roslyn compiler, assembly reflection, NuGet resolution, file I/O) must **NEVER** run synchronously on the Avalonia UI thread.
- Always execute async background work via `Task.Run(..., cancellationToken)`.
- Push UI updates back to the UI thread via `Dispatcher.UIThread.Post(...)`.
- Visual components must clean up event subscriptions, timers, and background tasks when unloaded or unmounted.

---

## 5. Verification & Test Integrity

- Every refactoring or control extraction must maintain **0 warnings and 0 errors** during compilation:
  ```bash
  dotnet build examples/CSharpEditorPlugin/CSharpEditorPlugin.slnx
  ```
- All automated unit tests must continue to pass with 100% success:
  ```bash
  dotnet test examples/CSharpEditorPlugin/Tests/CSharpEditorPlugin.Tests.csproj
  ```
