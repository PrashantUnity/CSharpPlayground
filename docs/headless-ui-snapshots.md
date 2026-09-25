# Headless UI snapshots

`tools/UiSnapshots` renders the studio's real views (the actual XAML, view models, styles and theme) into PNG files
without opening a window or needing a display. It exists so an agent can **see** the UI it is changing: render, open
the PNG, fix, render again. `tools/UiSnapshots/images.py` then helps look closely: zoom into a region, read off
coordinates, diff two renders, tile several.

Use it after every UI change. A green build and passing tests say nothing about clipped buttons, text running into the
next column, a style that doesn't apply or a scrollbar covering content; a snapshot shows all of those.

## Quick start

```bash
dotnet build tools/UiSnapshots
dotnet tools/UiSnapshots/bin/Debug/net10.0/UiSnapshots.dll blind75
```

The tool prints one line per image it saved, the full path:

```
/…/CSharpPlayground/tools/UiSnapshots/out/blind75.png
```

Open that path with your image-reading tool (in Claude Code, `Read`). `dotnet run --project tools/UiSnapshots -- blind75`
works too but rebuilds every time; build once and call the DLL when rendering several images. Rebuilding the tool also
rebuilds the plugin, so it always renders your latest XAML and code.

`help` lists every command and option. The project is part of `CSharpEditorPlugin.slnx`, so the normal build keeps it
compiling, and `tools/UiSnapshots/out/` is git-ignored.

## Commands

| Command | What it renders | Default size |
|---|---|---|
| `blind75` | The Blind 75 problem browser | 1400×820 |
| `details <n>` | Problem n's details panel at full height | 410×2600 |
| `markdown <n>` | Problem n's statement and "How to think" through `MarkdownView` | 440×1600 |
| `studio <n>` | Code Studio with problem n's script open | 1400×900 |
| `studio --file <path>` | Code Studio with any script file open | 1400×900 |
| `notebook <n>` | Notebook Studio with problem n's notebook | 1400×900 |
| `visualizer [n ...]` | Runs each script and saves steps of every visualizer it shows as `p{n}_v{k}_s{step}.png`; prints the test output and every step's description. With no numbers: all 76 problems | 900×640 |
| `script <n>` | Prints the generated script and notebook code (no image) | |

Command options:

| Command | Option | Effect |
|---|---|---|
| `blind75` | `--details <n>` | Open the details panel for problem n |
| | `--solved <n,n,...>` / `--saved <n,n,...>` | Mark problems solved / bookmarked (throwaway progress; never yours) |
| | `--category <name>` `--difficulty <All\|Easy\|Med\|Hard>` `--status <All\|Solved\|Unsolved\|Bookmarked>` `--search <text>` | Filters |
| | `--sort <columns>` | `Title`, `Category`, `Acceptance` or `Difficulty`; `Difficulty,Difficulty` sorts descending |
| | `--drag <divider:px,...>` | Drag column dividers with real pointer events; 1 = Title\|Category, 2 = Category\|Acceptance, 3 = Acceptance\|Difficulty. Prints the widths after each drag |
| `studio` | `--sidebar <view>` | `explorer`, `search`, `debug`, `nuget`, `notes` (default) or `problems` |
| | `--edit-notes` | Click Edit in Scratchpad & Notes first |
| | `--run` | Run the script (F5) before capturing |
| | `--panel <tab>` | Bottom panel tab: `results`, `terminal`, `problems`, `tests` or `debug` |
| | `--panel-height <px>` | Bottom panel height (default 280, whatever the window height) |
| | `--generate-tests` / `--run-tests` / `--add-test` | Click Generate, Run All or Add Test Case in the Test Cases panel |
| `notebook` | `--run` | Run all cells first |
| `visualizer` | `--steps <s,s,...>` | Steps to save; negative counts from the end (default: first, 1/3, 2/3, last) |
| | `--quiet` | Don't print each step's description |

Options for every command:

| Option | Effect |
|---|---|
| `--width <px>` `--height <px>` | Window size, which is the image size |
| `--light` | Light theme instead of dark |
| `--hover <x,y>` | Move the mouse there before saving, to show hover states |
| `--name <file>` | File name without `.png` (single-image commands) |
| `--out <folder>` | Where images go (default `tools/UiSnapshots/out`) |

Set `T=tools/UiSnapshots/bin/Debug/net10.0/UiSnapshots.dll` for the recipes below.

## Recipes

**Check a UI change, before and after.** Render before editing, change the code, rebuild, render again, and diff:

```bash
dotnet $T blind75 --name before
# ...edit the XAML / view model...
dotnet build tools/UiSnapshots && dotnet $T blind75 --name after
python3 tools/UiSnapshots/images.py compare tools/UiSnapshots/out/before.png tools/UiSnapshots/out/after.png
```

`compare` prints how many pixels changed and where (x and y ranges, largest area first), and writes `after_diff.png`
with both images and every changed area boxed in red. It stacks wide images one above the other so the pair stays
readable. `identical` means your change doesn't show: wrong view, or it needs a state the command doesn't set up.

**Narrow windows and the light theme.** Layout bugs hide at small widths: `dotnet $T blind75 --width 1000 --details 1`,
and `--light` for the other theme.

**Hover states.** Find the point first; `grid` draws labelled lines every 100 px (`--step` to change):

```bash
python3 tools/UiSnapshots/images.py grid tools/UiSnapshots/out/blind75.png
dotnet $T blind75 --hover 700,70 --name chip_hover
```

Image pixels are window pixels, so a point read off the grid goes straight into `--hover`.

**Drags.** `dotnet $T blind75 --drag 1:-300,3:-40` drags divider 1 left 300 px, then divider 3 left 40 px, and prints
the column widths after each, so you can check the numbers as well as the picture.

**Run code, then look at the output.** `dotnet $T studio 1 --run --panel results --height 1400 --panel-height 800`
shows the Results panel with the whole step-by-step visualizer; the bottom panel stays 280 px tall unless
`--panel-height` says otherwise. `--panel terminal` shows console output. `dotnet $T notebook 1 --run --height 2400`
runs every cell and shows more of the notebook.

**Read small text.** Viewers shrink big images. Crop and enlarge the part you care about:

```bash
python3 tools/UiSnapshots/images.py zoom tools/UiSnapshots/out/studio_1.png 0 0 340 300 --scale 3
```

Add `--pixelated` to judge exact pixels (alignment, 1 px borders) without smoothing.

**Visualizers step by step.**

```bash
dotnet $T visualizer 1 11 --quiet --steps 0,3,-1
python3 tools/UiSnapshots/images.py sheet "tools/UiSnapshots/out/p1_v1_s*.png" --columns 3
```

`sheet` tiles the frames into one labelled image. Without `--quiet`, read the printed step descriptions too; they are
what the user reads under each frame.

## How it works

Read this before extending the tool.

1. **Boot without a screen.** `Snapshot.Start` runs
   `AppBuilder.Configure<SnapshotApp>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting()`.
   Avalonia's headless platform creates windows that never reach a screen. `UseHeadlessDrawing = false` with
   `UseSkia()` makes them draw real pixels; with headless drawing there would be nothing to save.
2. **Same theme as the app.** `SnapshotApp` loads what `Runner/App.axaml` loads: the Material 3 tokens and styles from
   the Runner assembly (`avares://FrySharp/...`), Fluent, Material icons and AvaloniaEdit's styles. Without the tokens
   every `{DynamicResource M3...Brush}` resolves to nothing. If `App.axaml` changes, change `SnapshotApp` too.
3. **Real view, real view model.** A command builds the view as the app does, e.g.
   `new CSharpBlindProblemsView { DataContext = new CSharpBlindProblemsViewModel(...) }`, with storage and progress in
   throwaway folders, and puts it in a `Window` of the requested size.
4. **Pump the UI thread by hand.** `SetupWithoutStarting` starts no message loop, so nothing processes the UI thread's
   queue unless you do. `Snapshot.Settle()` calls `Dispatcher.UIThread.RunJobs()` for a few frames so bindings, layout
   and posted work happen.
5. **Capture.** `window.CaptureRenderedFrame()` renders a frame and returns it as a bitmap, saved with
   `Save(path, PngBitmapEncoderOptions.Default)`.
6. **Input.** `window.MouseMove`, `MouseDown` and `MouseUp` (Avalonia.Headless) send real pointer events through hit
   testing, so hover styles, pointer capture and drag handlers run exactly as with a mouse. To change state, prefer
   calling the view model's commands and properties: it's faster and doesn't depend on where things are drawn.

## Adding a snapshot

1. Add a method to a class in `tools/UiSnapshots/Commands/` (or a new one):

   ```csharp
   /// <summary><c>mything n</c>: what it shows.</summary>
   public static void MyThing(Options options)
   {
       var vm = new MyViewModel(new LocalScriptStorageService(Snapshot.TempFolder("scripts")));
       vm.DoSomething();                                   // put the view in the state you want to see
       var window = Snapshot.Show(new MyView { DataContext = vm }, options.Int("width", 1200), options.Int("height", 800));
       Snapshot.Wait(vm.LoadAsync());                      // async work: Wait, never await
       Snapshot.Settle();                                  // let bindings and layout catch up
       Snapshot.Save(window, options, "mything");          // applies --hover and --name, prints the path
   }
   ```

2. Add a `case` for it in `Program.cs` and a line in `Usage.cs`.
3. Build with `dotnet build tools/UiSnapshots`, run it, and look at the image.

## Pitfalls

- **Never `await` on the main thread.** The continuation is queued on the UI thread and nothing runs that queue while
  the program waits: the process hangs at 0% CPU. Use `Snapshot.Wait(task)`, which pumps while it waits.
- **Script execution runs on a worker thread.** `NotebookExecutionKernel` builds visualizer controls through
  `Dispatcher.UIThread.Invoke`. Run it with `Task.Run` and `Snapshot.Wait` (see `VisualizerSnapshots`); on the main
  thread it would block on itself.
- **Settle before saving.** After changing anything, call `Snapshot.Settle()`; `Save` settles two more frames but not
  more. Slow content (images, first Roslyn compile) may need `Settle(30)`.
- **The window is the image.** Content scrolled out of view isn't drawn. Make the window taller (`--height 2600`) to
  capture a whole scrolling panel; the ScrollViewer then shows everything.
- **Styles come from ancestors.** A control shown on its own misses the style sheets its parent view includes (for
  example `SharedStudioStyles.axaml`). Add them with `Snapshot.AddPluginStyles(control, "Controls/SharedStudioStyles.axaml", ...)`
  as `details` does, or render the whole view.
- **Never touch real data.** Point storage and progress at `Snapshot.TempFolder(...)`, which is deleted on exit. The
  Blind 75 commands never write your real progress, and they print a URL instead of opening a browser.
- **Fonts are the machine's.** Text can differ a little from the app on another OS. Compare renders made on the same
  machine.
- **The studios start slowly.** `studio` and `notebook` create a `RoslynCompilerService`, which takes a few seconds.
- **"The process cannot access the file ... .pdb".** Something else (usually a `dotnet watch`) is building the plugin at
  the same time. Wait a moment and build again.

## Files

| File | Purpose |
|---|---|
| `tools/UiSnapshots/Program.cs` | Picks the command |
| `tools/UiSnapshots/Usage.cs` | The `help` text |
| `tools/UiSnapshots/Options.cs` | Command-line parsing |
| `tools/UiSnapshots/Snapshot.cs` | The headless session: start, show, settle, wait, mouse, save, temp folders |
| `tools/UiSnapshots/SnapshotApp.cs` | The app's theme |
| `tools/UiSnapshots/Commands/*.cs` | One method per command |
| `tools/UiSnapshots/images.py` | `grid`, `zoom`, `compare`, `sheet`, `info` for looking at PNGs (needs Pillow: `pip install pillow`) |
