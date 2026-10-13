# FrySharp for Visual Studio Code

**FrySharp** brings high-performance .NET 10 Roslyn scripting, polyglot notebooks (C#, Python, JavaScript, Java, SQL, Rust), and rich interactive visualizations directly into Visual Studio Code.

Following Microsoft's deprecation of Polyglot Notebooks (.NET Interactive) and Azure Data Studio, **FrySharp** provides a fast, native replacement powered by a lightweight .NET sidecar process.

---

## Features

- **Polyglot Notebooks (`.frynb` & `.ipynb`)**:
  - Run C# cells using in-process Roslyn compiler (.NET 10).
  - Polyglot support: mix C#, Python, JavaScript, Java, and SQL in the same notebook.
  - Cross-language variable sharing: `#!share --from <language> <variable> [--as <name>]`.
- **Script Runner**:
  - Run standalone C# (`.cs`, `.csx`), Python (`.py`), or JavaScript (`.js`) scripts with streaming stdout/stderr in the output channel.
- **Rich Visuals & Displays**:
  - Full support for interactive tables, ECharts, 3D surface plots, and data structures.
- **Zero Configuration**:
  - Automatically launches the headless `FrySharp.Sidecar` over stdio JSON-RPC. No network ports, no web servers, no auth tokens.

---

## Commands

- `FrySharp: New Polyglot Notebook` — Create a new `.frynb` notebook with C# and markdown cells.
- `FrySharp: Run Script with FrySharp` — Execute the active script file with the FrySharp engine.
- `FrySharp: Restart Engine Sidecar` — Restart the backend .NET execution sidecar.

---

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later installed.
