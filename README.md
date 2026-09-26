# CSharpPlayground (FrySharp)

[![CI](https://github.com/PrashantUnity/CSharpPlayground/actions/workflows/ci.yml/badge.svg)](https://github.com/PrashantUnity/CSharpPlayground/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![Avalonia UI](https://img.shields.io/badge/Avalonia-12.1.2-red.svg)](https://avaloniaui.net/)

**CSharpPlayground** (standalone executable: **FrySharp**) is an interactive multi-language code studio, polyglot Jupyter-style notebook environment, and script automation engine for .NET 10, Python, and extensible language toolchains built with Avalonia UI.

It operates both as:
1. **A Standalone Desktop Application (`FrySharp`)**: Native desktop app for macOS (`.dmg`) and Windows (`.msix` / `.exe` installer) with an authentic VS Code-inspired 5-zone IDE layout.
2. **A FryPDF Ecosystem Plugin (`com.frypdf.plugin.csharpeditor`)**: Edge-to-edge full-viewport workspace studio and document automation plugin for FryPDF.

---

## 🌟 Key Features

- **Authentic VS Code Layout & Ergonomics**:
  - **Activity Bar**: Explorer, Search, Run & Debug, Package & Dependency Manager (NuGet, pip, npm), Scratchpad, and Problems badges.
  - **Primary Side Bar**: Multi-language file tree, project explorer, and script management hub.
  - **Multi-Tab Editor**: Smooth horizontal tab bar with dirty indicators (`●`), quick close, and isolated execution states.
  - **Bottom Tool Deck**: Problems (Roslyn diagnostics and external language tracebacks with quick-fixes), Output, Terminal Console (with interactive stdin), Debug REPL, and Rich Results.
  - **Status Bar**: Execution timer (`⏱ 14ms`), line/column coordinates, spaces, UTF-8, active language & toolchain status (`C# (.NET 10 Roslyn)`, `Python 3.12 (.venv)`, etc.), and compiler status.
- **Interactive Jupyter-Style C# Notebooks**:
  - Stateful cell execution kernel chaining submissions using Roslyn Scripting API.
  - NuGet package resolution directly in scripts (`#r "nuget: ..."`).
  - Rich output display: text, images, charts, and custom Avalonia controls (`Display.Image(...)`, `Display.Control(...)`).
  - Cell input/output collapsing, folding, and export.
- **Python & Polyglot Notebooks**:
  - `.py` files open, run (F5 / Ctrl+F5) and take `input()` in Code Studio, with your installed Python, so numpy, pandas, matplotlib and every other package work. Tracebacks land in Problems with an "Install <package>" fix.
  - Notebooks mix C# and Python cells, as in Polyglot Notebooks. Each language has its own kernel, and `#!share --from csharp nums` copies values between them. `%pip install` works in cells.
  - The studio finds Python as your terminal would (a project `.venv` first) and says what's missing when it can't. See the App Guide's "Languages & Python".
  - Extensible language module architecture with roadmap support for JavaScript, Java, C++, and C: [docs/adding-a-language.md](docs/adding-a-language.md), [docs/kernel-protocol.md](docs/kernel-protocol.md).
- **Tabular Data Analytics**:
  - Native display and profiling for `DataTable`, `DataView`, and Microsoft.Data.Analysis `DataFrame`.
  - Column summaries, data types, row counts, and inline search.
- **Real-Time Roslyn Diagnostics**:
  - Debounced (350ms) background analysis via `Microsoft.CodeAnalysis.CSharp`.
  - "Problems" drawer with error/warning counts, line/column coordinates, and click-to-jump navigation.
- **Interactive Debugging**:
  - Visual gutter breakpoints, line highlighting, and stepping controls (F5 Continue, F10 Step Over, F11 Step Into, Shift+F5 Stop).
  - Variable inspector and immediate REPL evaluation.

---

## 🚀 Quick Start

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Optional: [Python 3.9+](https://www.python.org/downloads/) for `.py` files and Python notebook cells

### Run Standalone Desktop App (FrySharp)
```bash
# Clone the repository
git clone https://github.com/PrashantUnity/CSharpPlayground.git
cd CSharpPlayground

# Run standalone desktop app
dotnet run --project Runner/CSharpEditorPlugin.Runner.csproj
```

### Run Automated Unit Tests
```bash
# Run the unit tests (the real-Python ones run when Python 3.9+ is installed)
dotnet test CSharpEditorPlugin.slnx
```

---

## 📦 Solution Structure

```
CSharpPlayground/
├── CSharpEditorPlugin.slnx         # Modern XML solution (Engine + Runner + Tests)
├── CSharpEditorPlugin.csproj       # Core Studio & Plugin library (.NET 10)
├── CSharpEditorPlugin.cs           # IFryPlugin entrypoint
├── plugin.json                     # FryPDF marketplace manifest
├── Controls/                       # VS Code activity bar, status bar, tabs, editor
├── Models/                         # POCO models for scripts, cells, diagnostics
├── Services/                       # Roslyn compiler, kernel, completion, debugger
│   ├── Languages/                  # Language registry; C# and Python modules (Python/Kernel: the kernel program)
│   ├── Toolchains/ Processes/      # Finding installed toolchains; running programs (stdin, Stop, cleanup)
│   └── Kernels/ Packages/          # Notebook kernels per language, #!share, the kernel protocol; pip
├── ViewModels/                     # Reactive MVVM view models
├── Views/                          # Avalonia XAML views (Studio, Notebook, Manager)
├── Runner/                         # Standalone desktop executable (FrySharp)
│   ├── CSharpEditorPlugin.Runner.csproj
│   ├── Program.cs / App.axaml
│   └── MainWindow.axaml
├── Tests/                          # Comprehensive xUnit test suite (1,100+ tests; RealPython/ needs Python)
│   └── CSharpEditorPlugin.Tests.csproj
├── tools/UiSnapshots/              # Headless renderer: real views to PNG, for checking UI changes
├── docs/                           # Developer guides: headless UI snapshots, adding a language, the kernel protocol
└── packaging/                      # macOS DMG & Windows MSIX/Inno packaging assets
```

---

## 🏗 Architecture: Stateful Notebook Execution

```mermaid
flowchart TD
    subgraph Notebook UI
        C1["Cell 1: var m = 10;"]
        C2["Cell 2: Console.WriteLine(m * 2);"]
        C3["Cell 3: Display.Image(surface.Snapshot());"]
    end

    subgraph "NotebookExecutionKernel (Persistent Session)"
        Init["ScriptOptions\n(System Refs + NuGet Refs + Usings)"]
        S0["Submission #0: ScriptState\n(Creates 'm' field, Output = 10)"]
        S1["Submission #1: ScriptState.ContinueWithAsync\n(References S0, Reads 'm', Output = 20)"]
        S2["Submission #2: ScriptState.ContinueWithAsync\n(References S1, Rich Media Display)"]
        VarExp["Variable Inspector State\n[m : int = 10]"]
    end

    subgraph Rich Cell Output
        Out1["Text Output / Badge"]
        Out2["Console Output: 20"]
        Out3["Avalonia Image Control\n(Zoom, Copy, Save PNG)"]
    end

    C1 -->|"Execute Cell"| S0
    S0 -->|"State Chaining"| S1
    C2 -->|"Execute Cell"| S1
    S1 -->|"State Chaining"| S2
    C3 -->|"Execute Cell"| S2
    
    S0 --> VarExp
    S1 --> VarExp
    S2 --> VarExp

    S0 --> Out1
    S1 --> Out2
    S2 --> Out3
```

---

## 📜 License
MIT License. See [LICENSE](LICENSE) for details.