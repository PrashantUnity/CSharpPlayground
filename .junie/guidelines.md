# C# Code Studio (CSharpEditorPlugin) Project Guidelines

## VS Code Architecture Mandate
- Always preserve the authentic 5-zone VS Code layout:
  1. Activity Bar (48px)
  2. Primary Side Bar (~270px)
  3. Editor Area (tabs, breadcrumbs, action toolbar, AvaloniaEdit canvas)
  4. Bottom Panel / Dock (Problems, Output, Terminal, REPL, Dump Results, Test Cases)
  5. Status Bar (22px)
- Never create fixed multi-column sidebars side-by-side.
- Ensure all keyboard shortcuts (Ctrl+B, Ctrl+J, F5, Ctrl+Shift+E/F/D/M) are fully operational.
- Keep UI thread free of compilation/execution bottlenecks.
