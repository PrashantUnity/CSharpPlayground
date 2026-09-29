namespace PdfEditorApp.Plugins.CSharpEditor.Tools.UiSnapshots;

internal static class Usage
{
    public const string Text = """
        UiSnapshots: save PNGs of C# Code Studio's real views without opening a window.

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
              --edit-notes           click Edit in Scratchpad & Notes first
              --run                  run the script first (F5)
              --debug <line>         set a breakpoint on that line, debug and wait until it pauses there
              --panel <tab>          bottom panel tab: results, terminal, problems, tests or debug
              --panel-height <px>    bottom panel height (default 280), e.g. 700 to see a whole visualizer
              --generate-tests       click Generate in the Test Cases panel (Blind 75 problems)
              --run-tests            click Run All in the Test Cases panel
              --add-test             open the Add Test Case form
              --quick-open <mode>    show the Quick Open palette: files or commands
              --quick-info <text>    rest the mouse on the first <text> in the editor and show its hover card
                                     (the debugger's data tip when paused with --debug)
              --zoom <size>          editor font size in px (e.g. 18 for 138%, 10 for 77%)
              --zoom-keys <actions>  simulate zoom keys: in,out,reset (e.g. --zoom-keys in,in)
          notebook <n>           Notebook Studio with problem n's notebook.
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
              --zoom-keys <actions>  simulate zoom keys: in,out,reset (e.g. --zoom-keys in,in)
          hub                    The Hub dashboard over a throwaway workspace (3 scripts, 2 notebooks, 1 pinned).
              --empty                no scripts or notebooks (first run)
              --python <path>        the Python its STUDIO ENVIRONMENT card shows (default: the one the studio finds)
              --nothing-installed    as on a machine without Python: the card says what's missing
              --templates            the template gallery instead of recent workspaces
              --create <kind>        the "New Script" / "New Notebook" dialog: script or notebook
          docs                   The Docs learning center.
              --article <words>      open the first article whose title contains the words
              --list                 print every category and article (no image)
          settings               The Settings and Environment Setup page.
              --language <id>        select a language setting item (csharp, python, javascript, java, cpp, go, rust)
              --category <name>      select a category (Languages, Editor, Keymap)
              --nothing-installed    simulate environment with no toolchains installed to test guidance
          visualizer [n ...]     Run each problem's script and save steps of every visualizer it shows
                                 (every problem when no numbers are given).
              --steps <s,s,...>      steps to save; negative counts from the end (default: first, 1/3, 2/3, last)
              --quiet                don't print each step's description
          plot3d [kind]          Interactive 3D visualization off-screen rendering (surface, scatter, graph, trajectory, voxel).
              --mode <kind>          surface (default), scatter, graph, trajectory, or voxel
              --width <px>           window width (default 960)
              --height <px>          window height (default 640)

        Options for every command
          --width <px>  --height <px>   window size, which is the image size
          --light                       light theme instead of dark
          --hover <x,y>                 move the mouse there before saving, to show hover states
          --name <file>                 file name without .png (single-image commands)
          --unique                      if a file with this name exists, append _1, _2, etc. instead of overwriting
          --timestamp                   append current timestamp to filename (_yyyyMMdd_HHmmss)
          --out <folder>                where images go (default: tools/UiSnapshots/out)

        Every saved file's full path is printed, one per line. To look closer (zoom, coordinate grid, before/after
        diff, contact sheet): python3 tools/UiSnapshots/images.py --help. Guide: docs/headless-ui-snapshots.md
        """;
}
