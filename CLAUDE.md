# C# Code Studio (CSharpEditorPlugin) Project Rule

This project (`examples/CSharpEditorPlugin`) implements an authentic **VS Code-inspired layout and structure** for document scripting and interactive C# execution in FryPDF.

## Core Mandates
1. **5-Zone Layout**:
   - Zone 1: Activity Bar (48px fixed, leftmost rail with Explorer, Search, Debug, NuGet, Scratchpad, Problems)
   - Zone 2: Primary Side Bar (~270px, resizable, collapsible via `Ctrl+B`, renders view corresponding to active Activity Bar tool with New File/Folder explorer toolbar)
   - Zone 3: Editor Area (Multi-tab bar with close buttons & dirty indicators, breadcrumbs, action toolbar, AvaloniaEdit canvas)
   - Zone 4: Bottom Panel / Tool Deck (Problems, Output, Terminal, REPL, Results .Dump, Test Cases; dynamic resizable splitter, collapsible via `Ctrl+J`)
   - Zone 5: Status Bar (22px bottom strip with remote badge, diagnostics counter, compiler status, Ln/Col, Spaces: 4, UTF-8, C#)
2. **Single Sidebar Rule**: Only ONE primary side bar may exist in the horizontal flow. Never introduce multi-column sidebar clutter.
3. **Keyboard Ergonomics**: Honor `Ctrl+B` (SideBar), `Ctrl+J` (Bottom Panel), `Ctrl+S` (Save), `F5` / `Ctrl+F5` / `Shift+F5`, `F10` / `F11`, `Ctrl+F` (Find), `Ctrl+Shift+E/F/D/M`.
4. **Performance**: Zero heavy operations on the Avalonia UI thread. Roslyn compilation and evaluation must run on background threads with cancellation support.
5. **Quality**: Maintain 0 warnings, 0 errors, and ensure all tests pass.
