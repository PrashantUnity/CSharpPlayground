# FrySharp for Visual Studio Code

Polyglot notebooks and script runs for C#, F#, Java, Python, JavaScript and SQL, with the same engine as the FrySharp desktop
app: Roslyn for C#, your own .NET SDK, JDK, Python, Node.js and SQLite for the others, and rich output (tables, objects, charts, 3D
plots, images).

FrySharp is a replacement for the Polyglot Notebooks extension, whose support ended in March 2026.

## Notebooks

Open a `.frynb` file (or run **FrySharp: New Polyglot Notebook**). Cells run on the FrySharp kernel, which is picked for you.

- **Languages:** C#, F#, Java, Rust, Go, C++, Python, JavaScript and SQL, mixed in one notebook. A cell can start with `#!python`,
  `#!javascript`, `#!csharp`, `#!fsharp`, `#!java`, `#!rust`, `#!go`, `#!cpp` or `#!sql`. Variables move between languages with `#!share --from csharp name [--as other]`.
- **F#:** all the cells of a notebook are one F# Interactive session, as in `dotnet fsi`: a `let`, a function or a type
  one cell defines the next one uses. The value a cell ends with is shown (a list of records as a table, a record as the
  Object Explorer), errors are underlined where the compiler found them, and `Display.table`, `Display.lineChart`,
  `Display.markdown` ... show output. `#r "nuget: Package"` loads a package. Stop ends a cell that waits (`Thread.Sleep`,
  `Console.ReadLine`); one that only computes ends the session, and its values are gone.
- **Java:** the cells of a notebook are one JShell session: a variable, method, class or record one cell declares the next
  one uses. The value of an expression is shown (a list of records or maps as a table, an object as the Object Explorer),
  compile errors are underlined with their column, and a runtime error is on the line of the cell it went through.
  `//DEPS group:artifact:version` downloads a library from Maven Central and puts it on the classpath, and
  `Display.table`, `Display.lineChart`, `Display.markdown` ... show output. Stop ends any cell, even one that only computes,
  and keeps what the notebook declared.
- **Rust:** each cell is built with Cargo as a small program (about a second once the first has built): the functions,
  types and imports one cell defines the next one uses, and so does a `let` of numbers, text, lists, options, tuples, maps
  and sets. A variable of a type you defined can't be handed on (the cell says so; `fry::share!(name)` keeps it as data), and a
  plain `stdin` read sees the end of input: use `fry::input("Prompt: ")`, which asks in an input box. The value a cell
  ends with is shown (a list of structs as a table, a struct, tuple or enum as the Object Explorer, needing `Debug`).
  `// #crate: name = "1"` or `%cargo add name` fetches a crate, errors are underlined and named on the cell's own lines
  (a panic too), and `fry::line_chart`, `fry::table`, `fry::markdown` ... show output. Stop ends the program.
- **Go:** each cell is built with the Go toolchain as a small program (a fraction of a second once the first has built):
  the functions, types, constants and imports one cell declares the next one uses, and so does a variable (`x := ...`, or
  `var x` at the top) whose value can be saved (numbers, text, slices, maps, structs with capitalised fields). A variable
  of a type that can't be handed on (a func, a struct with lowercase fields) isn't kept: the cell says so, and
  `fry.Share("name", value)` shares it as data. A plain read of `os.Stdin` sees the end of input; `fry.Input("Prompt: ")` asks in an
  input box. The value a cell ends with is shown (a slice of structs as a table, a struct as the Object Explorer), errors are
  underlined and named on the cell's own lines (a panic too), `%go get module` adds a module, and `fry.LineChart`,
  `fry.Table`, `fry.Markdown` ... show output. Stop ends the program.
- **C++:** each cell is built with your C++ compiler (clang++, g++ or MSVC; C++20) as a small program (a second or two a cell):
  the includes, functions, types, aliases and `static` or `constexpr` variables one cell declares the next one uses, and so does a
  variable (`int x = 5;`, `std::vector<std::string> names;`) whose value can be saved (numbers, text, vectors, maps, optionals,
  pairs and structs of those; the notebook reads a struct's public data members from its definition). A variable of a type that
  can't be handed on (a pointer, an array, a class with private members) isn't kept: the cell says so, and
  `fry::share("name", value)` shares what can be sent as data. A plain `std::cin` read sees the end of input; `fry::input("Prompt: ")`
  asks in an input box. The value a cell ends with, written without a `;`, is shown (a vector of structs as a table, a struct as the
  Object Explorer), errors are underlined and named on the cell's own lines, an uncaught exception names its cause (C++ has no line to give),
  and `fry::line_chart`, `fry::display::table`, `fry::markdown` ... show output. Stop ends the program. `%vcpkg install name` adds a package.
- **SQL:** a cell runs on SQLite, in a database that lasts as long as the notebook's kernel: what one cell creates the next
  one queries. Each statement's rows are a table you can sort and filter, the notebook's tables are its variables, and
  `#!share --from csharp rows` turns a C# or Python list into a table to query.
- **Output as tabs:** a cell that makes more than one kind of output (printed text and a table, a chart and text ...) shows them as
  tabs in one output, as the desktop app does: **Table** (with its row count), **Console** (with its line count), **Inspector**,
  **Chart**, **Visualizer**, **3D**, **Image**, **HTML**, **Markdown**, and **All**. The console keeps its ANSI colours and redrawn
  progress bars; the tab you chose stays while the cell runs and more output arrives; Copy takes the console's text or the table
  as CSV. A cell with one kind of output keeps VS Code's own output. The file saved is the same either way (it holds the outputs, not
  the tabs). Turn it off with `frysharp.notebook.outputTabs` to have the outputs one after another.
- **Output:** the text a cell prints is one output, colours included. Objects open in the **Object Explorer** (a tree you
  expand and collapse; it lists four levels of members and shows a fifth as a title, an object that points back at itself included,
  as the desktop app does), tables sort and filter and copy as CSV or JSON, and charts, 3D plots, visualizers, ECharts (offline),
  images, HTML and Markdown are drawn in your theme (light, dark or high contrast).
- **Input:** `Console.ReadLine()` and `input()` open an input box; Escape is the end of input.
- **Errors:** a compile error or a traceback line is underlined in the cell and listed in Problems.
- **Run All** stops at the first cell that fails (setting `frysharp.notebook.stopOnError`). **Stop** interrupts the cell;
  Python, JavaScript and Java keep their variables. A C# call that is blocked (`Thread.Sleep`) cannot be interrupted: FrySharp
  gives up waiting after 3 seconds and the kernel stays busy until the call returns, so restart the kernel to free it.
- **Editing:** C# cells have completion, hover and live error marks (as you type) that know the cells above: what an earlier
  cell defined is in scope, and defining a name again in a later cell is not an error. SQL cells have completion (keywords,
  functions, SQLite's dot-commands, the tables and views the cells above create) and hover. `#!` completes the languages and
  `share`; `#!share --from csharp ` completes the variables that kernel holds; `%pip` / `%npm` / `%nuget` lines are
  completed. A directive that is not one (`#!pyton`) is underlined. Directives are coloured, and `fry` + Tab inserts a
  snippet for `Display.Table`, a chart, Markdown or `#!share` in C#, F#, Java, Python and JavaScript (`#!share` in SQL).
  JavaScript cells get VS Code's own completion. Python and the other languages get the help of the extensions you have
  installed for them (for example Pylance), when those serve notebook cells; FrySharp adds nothing to them but the directives.
- **Variables** (Panel, FrySharp): the variables of the notebook in front, by language, refreshed after every cell.
- **Files:** `.frynb` is FrySharp's own format and loses nothing the desktop app saves. `.ipynb` Jupyter notebooks open with
  **Reopen Editor With... > FrySharp Polyglot Notebook (Jupyter file)** and are saved back as Jupyter notebooks.

## Scripts

**FrySharp: Run with FrySharp** (the play button in the editor title, the Explorer and editor menus, `Ctrl+Alt+R`) runs a
`.cs`, `.csx`, `.fsx`, `.java`, `.rs`, `.go`, `.cpp`, `.py`, `.js` or `.sql` file in a terminal:

- the program's text and colours, as it prints them;
- type to answer a program that reads input; `Ctrl+C` stops it, any key runs it again;
- errors in the output are links to the file, and are listed in Problems;
- tables, charts and images a program shows appear in the **Results** panel.

**Scratchpad:** **FrySharp: New Scratchpad** opens a notebook that keeps its kernels. **Send Selection to Scratchpad** runs
the selected code in it.

## Requirements

- **.NET 10** (the runtime is enough). FrySharp uses the `dotnet` on your PATH; without one it offers to install the
  .NET Install Tool, which downloads it. Set `frysharp.dotnetPath` to use a particular one.
- The **.NET SDK** (not only the runtime) for F# cells and scripts: FrySharp runs `dotnet fsi`.
- A **JDK** (11 or newer, with `javac` and JShell; 17 for records) for Java cells and files.
- The **Rust toolchain** (`cargo`; install it from https://rustup.rs) for Rust cells and files.
- The **Go toolchain** (`go`, version 1.18 or newer; https://go.dev/dl) for Go cells and files.
- A **C++20 compiler** (clang++, g++ or MSVC) for C++ cells and files.
- **Python 3**, **Node.js** and **SQLite** (`sqlite3`) for cells and scripts in those languages (FrySharp finds the ones
  installed). macOS and most Linux have `sqlite3`; on Windows run `winget install SQLite.SQLite`. A SQL cell without it says
  how to get it.
- A **trusted workspace**: FrySharp runs code, so until you trust the folder notebooks open but nothing runs.

## Settings

| Setting | |
|---|---|
| `frysharp.dotnetPath` | The `dotnet` that runs the engine. |
| `frysharp.sidecarPath` | A `frysharp-sidecar.dll` to run instead of the packaged engine (for developing it). |
| `frysharp.notebook.stopOnError` | Run All stops at the first cell that fails (default on). |
| `frysharp.notebook.liveDiagnostics` | Mark errors in C# cells (and directives that are not one) as you type (default on). |
| `frysharp.run.arguments`, `frysharp.run.saveBeforeRun`, `frysharp.run.showEditorButton` | Script runs. |
| `frysharp.scratchpad.shiftEnter` | Shift+Enter sends the selection to the scratchpad. |

## Getting started

**Welcome: Open Walkthrough... > Get started with FrySharp** walks through a first cell, mixing languages, tables and charts, and
scripts.

## Troubleshooting

**FrySharp: Show Engine Log** shows what the engine wrote. The status bar item (bottom left, while a FrySharp notebook is
open) says whether the engine is ready, starting, busy or stopped, and its menu restarts a kernel or the engine. If the
engine stops, every notebook's variables are gone: run the cells again.

## Not yet in VS Code

Rust, Go and C++ cells run in the desktop app. Their engines are being moved so VS Code can run them too.
