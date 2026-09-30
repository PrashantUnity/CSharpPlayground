# Performance & Scale Mandate: zero-lag pages, tabs and files, at any workspace size

This rule applies strictly to `examples/CSharpEditorPlugin` (`com.frypdf.plugin.csharpeditor`). It is the plugin's counterpart of FryPDF's `performance_and_zero_lag_mandate.md`, and it exists because this studio is used on **very large codebases**: anything that is merely slow on a small workspace is unusable on a big one. All AI agents and developers must follow it.

---

## 1. Pages are built once and kept alive
- The studio host shows its pages through `Controls/KeepAlivePageHost`: a page's view is built the first time it is shown and only shown or hidden afterwards. **Never** show a page with a `ContentControl` + `DataTemplate` (that rebuilds the whole view, its styles and its editors on every switch, and the views it drops stay subscribed to the long-lived view models).
- A new page is registered in `CSharpStudioHostView`'s constructor. A page view model that owns timers, polling or animations implements `ViewModels/IPageLifecycle` and starts them in `OnActivated` and stops them in `OnDeactivated`. Hidden pages stay in the visual tree, so animations pause on `IsEffectivelyVisible`, not on detach.

## 2. Never block the UI thread
- No `.Result`, `.Wait()` or `.GetAwaiter().GetResult()` on the UI thread; no file, process or network access there either. Heavy work goes through `Task.Run` with a `CancellationToken`.

## 3. Never scan the workspace on a hot path
- Switching a tab, switching a page, opening a file or typing must **not** walk the workspace or read its documents. Consumers remember `IScriptStorageService.StructureVersion` (tree shape) or `ContentVersion` (titles, modified times) and reload only when it moved (`RefreshExplorerIfStaleAsync`, `ReloadIfStaleAsync`), usually from `IPageLifecycle.OnActivated`.
- Every operation that changes the workspace bumps the versions (`LocalScriptStorageService.MarkChanged`); files changed by other programs are noticed by `WorkspaceWatcher` (`ExternalChangeDetected`). A new mutating storage method must call `MarkChanged`.
- Tests can prove it: `LocalScriptStorageService.WorkspaceScanCount` must not move on a tab switch (`WorkspaceStalenessTests`).

## 4. Virtualize every list that can grow
- A list that can exceed ~100 rows is virtualized (`VirtualizingStackPanel` as the items panel, with the `ScrollViewer` in the `ItemsControl` template, or a `ListBox`). Never nest `ItemsControl`s to draw a tree and never put an `ItemsControl` inside a `ScrollViewer` for a growing collection.
- The Explorer binds to `ExplorerRowList.Rows` (a flat list of the visible rows). Change many items through `Suspend()` and `RangeObservableCollection` (one notification), never with thousands of `Add` calls.
- Known debt: the Hub's workspace list (`HubWorkspacesPanelControl`) and the Blind 75 table are not virtualized.

## 5. Per-keystroke and per-switch work is independent of document size
- Do not copy or scan the whole document on the UI thread per keystroke or per tab switch: no `Document.Text` to compare (use a chunked compare), no whole-text regex (see `DirectiveScanInlineLimit`), at most one folding pass, and documents above `LargeDocumentLength` fold shortly after they are shown.
- Any new feature attached to text changes must be measured on the 2 MB case below.

## 6. Clean up what you hook up
- Every `+=` on a longer-lived object needs its `-=`. Views that hook a view model implement `IDisposable` (`CSharpCodeStudioView.Dispose`), and `KeepAlivePageHost.Dispose` calls it.
- Timers get an explicit `DispatcherPriority` (never `Render` for decoration) and stop while their page is hidden.

## 7. Styles load once
- Do not add another `<StyleInclude>` of a shared style file inside a control that is created many times. Known debt: `SharedStudioStyles.axaml`, `CSharpManagerStyles.axaml` and `CSharpManagerPaletteTokens.axaml` are still included per view and per control.

---

## Verification
Time the real studio, headless, on a generated workspace:

```bash
dotnet build tools/UiSnapshots
dotnet tools/UiSnapshots/bin/Debug/net10.0/UiSnapshots.dll perf --files 200 --rounds 3 --big-kb 2048
```

Budgets (Debug build, this machine class; compare runs against each other, not against a stopwatch):

| Measure | Budget |
| :--- | :--- |
| Warm page switch | ≤ 20 ms |
| Tab switch, small file | ≤ 30 ms |
| File open, small file | ≤ 50 ms |
| Workspace scans during tab and page switches | 0 |
| Explorer refresh at 2,000 files | ≤ 600 ms |
| Keystroke in a 2 MB file | ≤ 8 ms |
| Memory growth per round of page visits | ~0 |

Also run the guard tests: `dotnet test Tests --filter "FullyQualifiedName~KeepAlivePageHostTests|FullyQualifiedName~WorkspaceStalenessTests|FullyQualifiedName~ExplorerRowList|FullyQualifiedName~ExternalChangeTests|FullyQualifiedName~TabSwitchStateTests"`.
