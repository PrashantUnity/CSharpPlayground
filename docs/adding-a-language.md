# Adding a language

The studio doesn't know languages by name. A language is one module (a class deriving from `LanguageDefinition`, and the services it hands out) registered in one place. Everything else is generic and adapts by what the module says it supports:

- the Explorer's New File menu, file pickers and Hub badges;
- Code Studio's run, stop, stdin, Problems and toolchain picker;
- notebook cells, their kernels, `#!share` and package lines;
- the Hub's STUDIO ENVIRONMENT row.

Python (`Services/Languages/Python/`) is the example to copy for an interpreted language. Rust (`Services/Languages/Rust/`) is the most complete compiled one: a Cargo package per file, crates from comments, static completion and hover, a `lldb-dap` debugger, an in-process notebook kernel and a Blind 75 adapter. `Tests/TestSupport/FakeLanguage.cs` is the smallest complete module: a language that exists only in tests, which prove the studio works with a language it has never heard of.

## 1. The definition

Create `Services/Languages/<Name>/<Name>Language.cs` deriving from `LanguageDefinition` (`Services/Languages/ILanguageDefinition.cs`).

| Member | What it's for | Python |
|---|---|---|
| `Id`, `DisplayName`, `ShortName` | The id in notebooks (`#!python`, `NotebookCellItem.Language`); names in menus; the cell chip | `python`, `Python`, `PY` |
| `Aliases` | Other names for `#!alias` and `#!share --from alias` | `py`, `python3` |
| `FileExtensions`, `Storage` | Which files are this language. `SourceFile` keeps them as plain files in the workspace; `FryDocument` is only for C#'s `.frycs` | `.py`, `SourceFile` |
| `Capabilities` | The features to switch on (see below) | `StandardInput \| NotebookCells \| ValueSharing \| Packages` |
| `IconKind`, `AccentHex` | Material icon name and color for tabs, the Explorer and the Hub | `LanguagePython`, `#4B8BBE` |
| `LineCommentPrefix` | Toggle comment; comments in a cell's directive block; the new-cell template | `#` |
| `NewFileTemplate` | A new file's content | prints the Python version |
| `RuntimeDescription` | The breadcrumbs' runtime label when no toolchain is known | `Python` |
| `GetHighlighting(isDark)` | An AvaloniaEdit `IHighlightingDefinition`: an inline XSHD with Dark+ and Light+ colors, as in `PythonSyntaxHighlighting.cs` | |
| `CreateIndentationStrategy`, `Folding` | Enter and folding behaviour (folding is optional) | indent after `:` |
| `EditorAssistants` | Where completion, hover and live diagnostics attach, e.g. an LSP client later. Attached when a file of the language is shown and disposed when it isn't | none yet |

Capabilities, and what each turns on:

| Flag | Turns on |
|---|---|
| `Completion`, `QuickInfo`, `LiveDiagnostics`, `Formatting` | Say what the language's editor helpers provide. A language that supplies its own `EditorAssistants` (completion and hover controllers) makes the Roslyn ones stand aside for its files (`LanguageDefinitionExtensions.UsesRoslynHelper`), so a `.rs` file never gets a C# popup on top of Rust's. Without `EditorAssistants` the flags turn on the C# helpers, which is right only for C# |
| `Debugging`, `Breakpoints` | F5 debugging, the breakpoint gutter and F9. Without them F5 runs the file |
| `TestCases`, `Templates`, `ExecutionModes` | The Test Cases panel, the template gallery and C#'s run modes |
| `StandardInput` | The Terminal's input row while a file runs |
| `NotebookCells` | The language can be picked for notebook cells (needs `NotebookKernels`) |
| `ValueSharing` | `#!share` in and out of its kernel |
| `Packages` | Package lines in cells and "Install …" offers (needs `Packages`) |

## 2. The toolchain (`IToolchainProvider`)

A language that runs with something installed needs a way to find it, say what it found, and say what's missing.

- **`ResolveAsync(ToolchainQuery)`:** return the toolchain to use for a file or notebook folder. If there isn't one, return `ToolchainResolution.NotFound(new MissingToolchainGuidance(...))` with per-OS install steps; the Terminal, notebook cells and the Hub all show them.
- **Look the way a terminal would:** read the machine through `IHostEnvironment` (`Services/Toolchains/HostEnvironment.cs`), never through `File` or `Process` directly, so tests can fake a machine (`Tests/TestSupport/FakeHostEnvironment.cs`). `GetLoginShellPathAsync()` gives the login shell's PATH, which an app started from the Finder doesn't have. `ExecutableSearch` walks it.
- **Probe before trusting a candidate:** run it with a short timeout and parse its answer. Cache the results; `Refresh()` forgets them.
- **The user's choice:** save it with `ToolchainSettingsStore` (per language, in `toolchains.json`), through `Select`. The status bar lists `ListAsync` and your `Actions` (e.g. "Create studio environment"), which run through `RunActionAsync`.
- **Order:** the saved choice first, then the project's own environment, then what's installed. That way a project's pinned toolchain wins.

## 3. Running files (`IScriptRunner`, `IDiagnosticParser`)

- `PlanAsync(ScriptRunContext)` returns a `ScriptRunPlan` of `ProcessStep`s:
  - **Interpreted scripts (Python, JS):** single run step (e.g. `python -u file.py` or `node file.js`).
  - **Compiled languages (Java, C++, C):** two-phase execution plan:
    1. *Build step* (`IsBuildStep = true`): compiler invocation (e.g., `javac -d <temp> file.java`, `clang++ -std=c++20 -o <bin> file.cpp`, or `clang -o <bin> file.c`). If compilation fails with non-zero exit code, `ScriptRunExecutor` stops immediately, parses errors via `IDiagnosticParser`, and does not run the executable.
    2. *Run step* (`IsBuildStep = false`): execution of the generated artifact (e.g., `java -cp <temp> Main` or `<bin>`).
- Start programs only through `IProcessLauncher` (`StudioLanguageServices.Processes`). It registers them with `ProcessRegistry`, which kills them when the plugin unloads or the app exits, and Stop kills the whole process tree.
- `IDiagnosticParser.Parse(output, file)` turns a failed run or build's output into Problems (line, column, message). It understands compiler formats (`javac`, `clang`/`gcc`, or Python tracebacks). It can also name a `MissingDependency`, which Problems offers to install.

## 4. Notebook cells (`INotebookKernelFactory`)

Write a kernel program that speaks the [Fry kernel protocol](kernel-protocol.md), embed it, and return `new ProtocolKernel(id, name, launcher, services.Processes, context)` from the factory.

- **Embedding:** add the program's files to `CSharpEditorPlugin.csproj` as `EmbeddedResource`s with a `LogicalName` prefix, as `PythonKernel.%(Filename)%(Extension)` does. Your `IKernelLauncher` extracts them with `EmbeddedKernelFiles.Extract` (once per content hash).
- **Launching:** the launcher resolves the toolchain for `context.WorkingDirectory()` (the notebook's folder) and returns the command. It throws `KernelUnavailableException` with the guidance when the toolchain is missing.
- **What the notebook does with it:** each notebook tab's `NotebookKernelRouter` creates one kernel per language the first time a cell of it runs, and ends it when the tab closes. A `ProtocolKernel` restarts its program after a crash, with a note that the variables are gone. Cells route to it by their language or a `#!<id>` first line. Output, rich output (MIME bundles), `input()`, Stop and Variables need nothing more.
- **In-process kernels:** a kernel can live in the studio instead, as C#'s does. Implement `INotebookKernel` directly. The compiled languages (Go, C++, F#, SQL, Rust) do it by building each cell into a small program and running it, replaying the items earlier cells defined; Rust's (`RustNotebookKernel`) is the model:
  - A cell's process is waited for with `IManagedProcess.WaitForExitOrKillAsync(ct)`, which kills it where the wait is cancelled. Don't register `Kill` on the token and then `WaitAsync(ct)`: the cancelled wait's continuation can dispose the registration before it runs, and the process keeps going.
  - No time limit of the kernel's own: how long a cell may take is the studio's `ExecutionTimeoutSeconds` setting, which arrives as the cancellation token.
  - Close the program's standard input (`CloseInput()`), so a cell that reads input sees the end of it instead of waiting for good.
  - Report a build failure once, after the last attempt (a kernel may try a cell two ways); put compiler messages on the cell's own lines, not the generated program's.
  - Text the program prints as a `text/plain` display (`__FRY_DISPLAY__`) is console text; `ExternalOutputProcessor` also reads `__FRY_SHARE__` lines, a value a cell offers to other kernels.

## 5. Packages (`IPackageManager`)

- **`TryParseDirective(line)`:** recognize a cell's package lines (`%pip install x`, say `%npm install x`).
- **`RunAsync(command, toolchain, output)`:** run the command, streaming its output into the cell. It returns `PackageCommandResult`:
  - `SwitchedToolchain` when it created a new environment to install into (as pip does for an externally managed Python);
  - `AddedSearchPath` when a running kernel should look there (`ISearchPathKernel`).
- **`InstallCommand(package)` and `PackageForMissingDependency(name)`:** power the "Install …" buttons. Python's `cv2` maps to `opencv-python`, for example.
- **A dependency that belongs to a file:** when there is no global install (Cargo's crates, Java's `//DEPS`), return `PackageCommandResult.DirectiveToInsert` (e.g. `// #crate: rand = "0.8"`); the studio adds that line to the open file and says so.

## 6. Debugging (`IDebuggerProvider`)

Most debuggers speak the Debug Adapter Protocol; `DapAdapterManager` starts one and drives it (`Services/Debugging/Dap/`). Check the adapter's real behaviour with the real adapter before trusting its documentation or your memory of it; each of these was learned that way:

- **How it is reached.** `LaunchStdioAdapterAsync` for adapters that talk over their standard streams (`lldb-dap`, `netcoredbg`); `LaunchSocketAdapterAsync` for TCP servers (`debugpy`, and Delve, whose `dlv dap` has no stdio mode at all: it prints its port and waits, so start it with `--listen=127.0.0.1:<port>`). `LaunchBridgeAdapterAsync` is for an adapter the studio implements itself (the Java bridge over `jdb`).
- **The order.** Pass `DapHandshake.Standard` unless the adapter was written for the old order: `initialize`, then `launch` (not awaited: `lldb-dap` and `debugpy` answer it only after `configurationDone`), then the adapter's `initialized` event, `setBreakpoints`, `configurationDone`. Delve refuses `setBreakpoints` and `configurationDone` until it has been asked to launch.
- **Check every answer.** Throw `DapException` when `launch` or `attach` says `success: false`. The handshake already reports a refused `configurationDone`, a `setBreakpoints` the adapter refused (shown in the Output) and an adapter that never answers `initialize` (a timeout) or dies while the session starts (its exit code).
- **Requests carry no `null`.** An optional field is left out; `netcoredbg` refuses `"condition": null` and then sets no breakpoint at all, so the program runs straight through.
- **Name the source as the debug info does.** Breakpoints match the document name in the debug info (`script.cs` for a C# script, from its `#line`), not the editor's title; `DapDebugSession.SourcePathOverride` says so.
- **Where the program's output goes.** Some adapters send it as `output` events (`netcoredbg`, `lldb-dap`), some through their own standard output (Delve), which arrives as live output. Some report exit code 0 whatever it was (`netcoredbg` 3.2).
- **Find the adapter for the user.** Search the folders it is installed in (`LldbDapLocator`, `~/.netcoredbg`), and when it is missing say where to get it; don't recommend a package manager that doesn't have it.
- **See the conversation.** Set `DapClient.Trace` (internal; tests can) to see every message that crosses a connection when a session misbehaves.

## 7. Register it

Add one line to the `StudioLanguageServices` constructor (`Services/Languages/StudioLanguageServices.cs`):

```csharp
Registry.Register(new PythonLanguage(this));
```

`LanguageRegistry.Register` refuses an id, alias or extension that's already taken.

## 8. Test it

- **Fakes first:** unit tests with fakes, as `PythonToolchainProviderTests` (fake machine), `PipPackageManagerTests` and `PythonTracebackParserTests` do.
- **Real toolchain:** tests in the `RealPython` collection style. Write a `[<Name>Fact]` attribute that skips when the toolchain is missing unless a `FRY_REQUIRE_<NAME>=1` variable says it must be there, and set that toolchain up in CI (`.github/workflows/release.yml`, test job). A debugger test does the same with its adapter (`FRY_REQUIRE_DELVE`, `FRY_REQUIRE_NETCOREDBG`), and a stand-in adapter (`MockDapServer`, `FakeDapAdapter`) pins the order of the requests without one.
- **Prove a fix with the real tool.** A stand-in only says what you believe the tool does. Run the real one, and keep the test that failed before the fix.
- **Temp folders only:** every test builds `new StudioLanguageServices(tempFolder)`, so it never reads or changes the user's choices or environments.
- **Snapshots:** render the UI with `tools/UiSnapshots` (see [headless-ui-snapshots.md](headless-ui-snapshots.md)). `studio --file hello.<ext> --run` shows a run, and `notebook --python-demo` shows how a demo notebook is built.

## Rules

- Never branch on a language's id outside its own module. Check a capability, or ask the language for the piece you need.
- Public types go in the `Services.Languages.<Name>` namespace. The plain `Services` namespace is imported into every C# script, so anything put there shows up in users' completion.
- Nothing slow runs on the UI thread: probing, starting programs and installing packages all run in the background, with cancellation.
- Messages are for the person using the studio. Say what happened and what to do, e.g. "Python 3.9 or newer is needed; brew install python".
