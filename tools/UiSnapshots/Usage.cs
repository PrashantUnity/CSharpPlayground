namespace PdfEditorApp.Plugins.CSharpEditor.Tools.UiSnapshots;

internal static class Usage
{
    public const string Text = """
        UiSnapshots: save PNGs of FrySharp's real views without opening a window.

          dotnet run --project tools/UiSnapshots -- <command> [problem numbers] [options]

        Commands
          blind75                The Blind 75 problem browser.
              --details <n>          open the details panel for problem n
              --solved <n,n,...>     mark problems solved (throwaway progress; yours is never touched)
              --saved <n,n,...>      bookmark problems
              --category <name>      e.g. "Two Pointers"
              --difficulty <level>   All, Easy, Med or Hard
              --status <status>      All, Solved, Unsolved or Bookmarked
              --search <text>
              --sort <columns>       Title, Category, Acceptance or Difficulty; "Difficulty,Difficulty" sorts descending
              --drag <d:px,...>      drag column dividers with real pointer events: 1 = Title|Category,
                                     2 = Category|Acceptance, 3 = Acceptance|Difficulty ("1:-200,3:40")
          details <n>            Problem n's details panel at full height.
          markdown <n>           Problem n's statement and "How to think", as the markdown renderer draws them.
          script <n>             Print the script and notebook code generated for problem n (no image).
          studio <n>             Code Studio with problem n's script open.
              --file <path>          open a script file instead; a source file (main.py) opens as one, in a
                                     throwaway workspace
              --python <path>        the Python interpreter .py files run with (default: the one the studio finds)
              --stdin <text>         with --run: type this into the Terminal once the program waits for input
              --while-running        with --run: take the picture while the program runs (e.g. waiting for input),
                                     then stop it
              --menu toolchain       also save the picture with the status bar's toolchain menu open (<name>_menu)
              --sidebar <view>       explorer, search, debug, nuget, notes (default) or problems
              --tree <n>             seed the throwaway workspace with n folders (scripts and a nested folder in each)
                                     and expand them all, so the Explorer shows a real tree (implies --sidebar explorer)
              --file-limit <n>       list at most n files (default 20000); with --tree, shows the notice for a cut-off folder
              --edit-notes           click Edit in Scratchpad & Notes first
              --run                  run the script first (F5)
              --debug <line>         set a breakpoint on that line, debug and wait until it pauses there
              --dap-trace            print every DAP protocol message sent and received
              --panel <tab>          bottom panel tab: results, terminal, problems, tests or debug
              --panel-height <px>    bottom panel height (default 280), e.g. 700 to see a whole visualizer
              --generate-tests       click Generate in the Test Cases panel (Blind 75 problems)
              --run-tests            click Run All in the Test Cases panel
              --add-test             open the Add Test Case form
              --quick-open <mode>    show the Quick Open palette: files or commands
              --quick-open-text <t>  type this into the palette (Go to File searches the whole workspace)
              --search-text <t>      type this into the Search panel (implies --sidebar search)
              --search-all           with --search-text: search the whole workspace (Find in Files), not just the open script
              --quick-info <text>    rest the mouse on the first <text> in the editor and show its hover card
                                     (the debugger's data tip when paused with --debug)
              --zoom <size>          editor font size in px (e.g. 18 for 138%, 10 for 77%)
              --zoom-keys <actions>  simulate zoom keys: in,out,reset (e.g. --zoom-keys in,in)
              --loading              show running work: the editor's progress line and the status-bar entry
                                 (--loading-title, --loading-sub, --loading-progress 0..1, --loading-seconds n)
          notebook <n>           Notebook Studio with problem n's notebook.
              --file <path>          open a notebook file (.csnb / .frynb) directly
              --dart-demo            a notebook of Dart cells (classes, collections, persistency across cells)
              --polyglot-demo        a notebook of C#, Python, JavaScript, Java, and C++ cells sharing data across kernels
              --go-demo              a notebook of Go cells (goroutines, channels, and variables)
              --fsharp-demo          a notebook of F# cells (pipelines, pattern matching, records)
              --sql-demo             a notebook of SQL cells (CREATE TABLE, SELECT, aggregates, #!share)
              --java-share           a test notebook attempting cross-language sharing with Java
              --cpp-demo             a notebook of C++ cells (standard library, vector, display tables)
              --rust-demo            a notebook of Rust cells (items kept between cells, a shown value, a table, #!share)
              --python-demo          a notebook of C# and Python cells (numpy, a pandas table, a matplotlib figure,
                                     input()) instead of a problem's
              --python <path>        the Python interpreter Python cells run with (default: the one the studio finds)
              --run                  run all cells first
              --stdin <text>         with --run: the answer a cell's input() gets (default: end of input)
              --while-running        with --run: take the picture while a cell waits for input, then stop
              --cell <n>             select the n-th cell (from 1), so its toolbar shows
              --menu <which>         also save the picture with a menu open (as <name>_menu): language (the selected
                                     cell's language menu) or kernel (the kernel pill's)
              --sidebar <view>       explorer, outline, variables or search
              --quick-open <mode>    files or commands
              --quick-info <text>    rest the mouse on the first <text> in a cell and show its hover card
              --zoom <size>          notebook font size in px (e.g. 18 for 138%, 10 for 77%)
              --loading              show running work: the notebook's progress line and the status-bar entry
              --zoom-keys <actions>  simulate zoom keys: in,out,reset (e.g. --zoom-keys in,in)
          hub                    The Hub dashboard over a throwaway workspace (3 scripts, 2 notebooks, 1 pinned).
              --empty                no scripts or notebooks (first run)
              --python <path>        the Python its STUDIO ENVIRONMENT card shows (default: the one the studio finds)
              --nothing-installed    as on a machine without Python: the card says what's missing
              --templates            the template gallery instead of recent workspaces
              --create <kind>        the "New Script" / "New Notebook" dialog: script or notebook
          server                 API Server Studio (.fryserver) off-screen rendering.
              --run                  start the server and execute an in-cell test request
              --port <port>          configure listening port (default 5000)
              --conflict             simulate port conflict with automatic suggestion
          docs                   The Docs learning center.
              --article <words>      open the first article whose title contains the words
              --list                 print every category and article (no image)
          settings               The Settings and Environment Setup page.
              --language <id>        select a language setting item (csharp, python, javascript, java, cpp, go, rust)
              --category <name>      select a category (Languages, Editor, Keymap)
              --nothing-installed    simulate environment with no toolchains installed to test guidance
              --engine <name>        palette studio engine: oklch or hct
              --lock <ids>           lock palette sections first (Primary,Error,Syntax.String,Chart.3)
              --generate <n>         press Generate n times
              --apply-palette        apply the palette as the studio theme
              --saved-themes <n>     save n themes to My themes first (Settings → Theme & Colors; --category Themes)
              --theme-card <state>   with --saved-themes: rename or delete shows that state on the first card
          about                  The About FrySharp dialog (AboutWindow).
          update                 The Check for Updates dialog (UpdateDialogWindow).
          app-window             The main application window (MainWindow) with macOS NativeMenu bar.
          templates              Run and verify all code templates in CodeTemplateLibrary.
              --template <name>      run only matching template(s) by id or title
              --language <id>        run only templates for language (csharp, python, cpp, etc.)
              --category <name>      run only templates in that category
              --shots                save UI snapshots of any visualizers or plots produced
              --quiet                don't print output streams
          visualizer [n ...]     Run each problem's script and save steps of every visualizer it shows
                                 (every problem when no numbers are given).
              --steps <s,s,...>      steps to save; negative counts from the end (default: first, 1/3, 2/3, last)
              --quiet                don't print each step's description
          plot3d [kind]          Interactive 3D visualization off-screen rendering (surface, scatter, graph, trajectory, voxel).
              --mode <kind>          surface (default), scatter, graph, trajectory, or voxel
              --width <px>           window width (default 960)
              --height <px>          window height (default 640)
          visuals [word ...]     Draw visual specs through the view every chart, 3D plot and visualizer output uses, as a
                                 program's display reaches it: every spec fixture in Tests/Fixtures/Visuals, or those whose
                                 names contain one of the words (e.g. visuals chart tree).
              --file <path>          a spec file (chart-*.json, plot3d-*.json, visualizer-*.json) or a display bundle
                                 ({"application/vnd.fry.chart.v1+json": {...}}) instead; a mistake in it is printed
              --width <px>           window width (default 900)
              --height <px>          window height (default 600)
          perf                   Time the real studio, no image: first visit and warm switch of every page, tab switches,
                                 file opens, workspace re-scans, memory left behind by repeated visits, and the longest
                                 time input waited on the UI thread during each (what a user feels as a hang).
              --files <n>            scripts in the generated workspace (default 200; try 5000 for a big one)
              --rounds <n>           passes over the six pages (default 6, at least 3)
              --big-kb <n>           also open a source file of about n KB and time opening, switching to it and typing
              --typing-experiments   with --big-kb: switch editor features off one by one to see which one a keystroke waits for
              --open-early           open a script the moment the window is up, while the engine is still starting
              --big-csv-mb <n>       also open a CSV of about n MB (shown as a table) and type in it
              --big-image-mp <n>     also open a PNG of about n megapixels and switch away from it and back
              --theme-switch         also apply theme presets from Settings (every page built) and time each
              --csv-experiments      with --big-csv-mb: split an open into its parts (view model, editor, table, language switch)
              --table-rows <n>       only: show a table of n rows in a window of its own (--table-shot <name> saves it)
              --memory-trace         print the heap after each start-up step and each page's first visit
              --memory-parts         print what each page and part of the studio holds when built on its own
              --memory-children      with --memory-parts: the same for each control inside the Code Studio
              --style-cost           what one include of the studio's style bundle costs (time and memory)
              --shots                also save each page as shown in the studio host (host-<page>.png)
              --cells <n>            also open a notebook of n cells: open time, memory, scrolling, opening the side panels
              --external <n>         also open a folder of n source files: Explorer, Hub, the file index and Go to File search
              --visuals              also time big visuals against their budgets: a chart of 100,000 values and a 30 × 30
                                 grid visualizer of 500 steps (the display call, the UI work before the first frame,
                                 stepping through every step and the memory it leaves)

        Options for every command
          --width <px>  --height <px>   window size, which is the image size
          --light                       light theme instead of dark
          --palette <engine[:seed]>     draw under a theme generated by the palette studio (oklch or hct)
          --hover <x,y>                 move the mouse there before saving, to show hover states
          --name <file>                 file name without .png (single-image commands)
          --unique                      if a file with this name exists, append _1, _2, etc. instead of overwriting
          --timestamp                   append current timestamp to filename (_yyyyMMdd_HHmmss)
          --out <folder>                where images go (default: tools/UiSnapshots/out)

        Every saved file's full path is printed, one per line. To look closer (zoom, coordinate grid, before/after
        diff, contact sheet): python3 tools/UiSnapshots/images.py --help. Guide: docs/headless-ui-snapshots.md
        """;
}
