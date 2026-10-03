# Dart Language Support Extension (`dart-support`)

A full-featured C# Extension Package for **FrySharp** and **C# Code Studio** bringing **Dart 3** into the multi-language IDE ecosystem with 100% feature parity.

---

## Features

- **Interactive Polyglot Notebooks (`.csnb` / `.ipynb`)**:
  - `DartNotebookKernel`: Executes Dart code cells interactively.
  - Imports and top-level definitions (classes, functions, enums) accumulate across cells.
  - Real-time stdout/stderr streaming to cell outputs.
  - Cross-language variable sharing (`#!share`).
- **Problems Panel Diagnostics**:
  - `DartDiagnosticParser`: Parses Dart compiler errors, analyzer warnings, and runtime exception traces with line and column accuracy for click-to-jump navigation.
  - Automatic detection of missing packages to trigger one-click quick fixes.
- **Toolchain Discovery & Status Bar**:
  - `DartToolchainProvider`: Detects local Dart SDK and Flutter SDK installs from PATH, Homebrew, and standard SDK locations.
  - Probes `dart --version` and displays the active version in the Status Bar (e.g. `Dart 3.x`).
  - Actionable missing-toolchain guidance for macOS (`brew install dart`), Windows (`choco install dart-sdk`), and Linux (`apt-get install dart`).
- **Dependency & Package Management**:
  - `DartPackageManager`: Integrates `dart pub add <package>` and `dart pub get` with the Dependencies & Packages tool deck.
  - Supports `%pub add <pkg>` notebook directives.
- **VS Code Editor Canvas**:
  - Full syntax highlighting themes for **Dark+** and **Light+**.
  - Automatic brace indentation (`BraceIndentationStrategy`).
  - Interactive stdin support (`TerminalTextBuffer`) for `stdin.readLineSync()` script input.

---

## Installation & Usage

### 1. Workspace-Scoped Installation (Recommended for Projects)
Copy or symlink this folder to `.frysharp/extensions/` in your workspace root:

```bash
mkdir -p .frysharp/extensions
cp -R samples/extensions/dart-support .frysharp/extensions/
```

### 2. User-Global Installation
To make Dart available in all workspaces and standalone FrySharp runs, copy it to your user profile:

```bash
mkdir -p ~/.frysharp/extensions
cp -R samples/extensions/dart-support ~/.frysharp/extensions/
```

### 3. Hot-Reload
FrySharp automatically detects newly added extensions or changes to `.cs` files inside `.frysharp/extensions/`, recompiles them in-memory via Roslyn into collectible `AssemblyLoadContext`s, and refreshes the UI dynamically without restarting the application!
