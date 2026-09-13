# FryPDF C# Code Studio Plugin (`com.frypdf.plugin.csharpeditor`)

An interactive in-app C# development and script automation studio for **FryPDF** (.NET 10 cross-platform document studio).

Inspired by [arklumpus/CSharpEditor](https://github.com/arklumpus/CSharpEditor), this plugin provides a dual-page workflow:
1. **Script Management Hub**: Organize, create, search, and duplicate scripts with a starter template gallery.
2. **Dedicated C# Code Studio**: Rich code editor powered by `Avalonia.AvaloniaEdit`, real-time Roslyn diagnostics, and in-memory execution with live console output redirection.

---

## 🌟 Key Features

- **Two-Page Architecture**:
  - **Management Hub**: Browse saved scripts, view LOC statistics, search by tags, and spawn scripts from starter templates.
  - **Code Editor Studio**: Full-screen coding workspace with run/stop controls, error navigation, and console output.
- **Hardware-Accelerated Code Editor**:
  - Powered by `Avalonia.AvaloniaEdit 12.0.0` with full C# syntax highlighting, line numbers, word wrap, and dark code palette.
- **Real-Time Roslyn Diagnostics**:
  - Debounced (350ms) background analysis via `Microsoft.CodeAnalysis.CSharp`.
  - "Problems" drawer with error/warning counts, line/column coordinates, and click-to-jump navigation.
- **In-Memory Script Execution**:
  - Dynamic compilation into memory stream (`CSharpCompilation.Emit`).
  - Isolated loading via collectible `AssemblyLoadContext`.
  - Captures `Console.Out` and `Console.Error` to a live terminal viewer with execution duration timing.
  - User cancellation and timeout protection.
- **Starter Template Library**:
  - Hello World Console
  - PDF Document Automation & Inspection
  - LINQ & Allocation High-Performance Benchmark
  - JSON Serialization with `System.Text.Json`
- **Zero Overlay / Full-Viewport Workspace**:
  - Mounts directly into FryPDF's left sidebar navigation as a dedicated full-viewport workspace studio page.
  - Deep integration with Command Palette (`Ctrl+Alt+E`), Status Bar (`{ } C# Studio`), and Ribbon Plugins Tab.

---

## 🚀 Instant F5 Standalone Testing

You can develop, test, and debug the plugin without running the main FryPDF application:

1. Open `examples/CSharpEditorPlugin/CSharpEditorPlugin.slnx` in Rider or Visual Studio.
2. Set `CSharpEditorPlugin.Runner` as the startup project.
3. Hit **Run (F5)**!

Or run from terminal:
```bash
dotnet run --project examples/CSharpEditorPlugin/Runner/CSharpEditorPlugin.Runner.csproj
```

---

## 📦 Building & Packaging

To compile and package the release `.fryplugin` distribution archive:

```bash
# Automated packaging CLI
python3 tools/package_plugin.py CSharpEditor
```

The output `CSharpEditor.fryplugin` will be generated and staged in `plugins/com.frypdf.plugin.csharpeditor/`.

## Architecture: How Jupyter / .NET Interactive Stateful Execution Works

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