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
              --file <path>          open a script file instead
              --sidebar <view>       explorer, search, debug, nuget, notes (default) or problems
              --edit-notes           click Edit in Scratchpad & Notes first
              --run                  run the script first (F5)
              --panel <tab>          bottom panel tab: results, terminal, problems, tests or debug
              --panel-height <px>    bottom panel height (default 280), e.g. 700 to see a whole visualizer
              --generate-tests       click Generate in the Test Cases panel (Blind 75 problems)
              --run-tests            click Run All in the Test Cases panel
              --add-test             open the Add Test Case form
              --quick-open <mode>    show the Quick Open palette: files or commands
          notebook <n>           Notebook Studio with problem n's notebook.
              --run                  run all cells first
              --sidebar <view>       explorer, outline, variables or search
              --quick-open <mode>    files or commands
          hub                    The Hub dashboard over a throwaway workspace (3 scripts, 2 notebooks, 1 pinned).
              --empty                no scripts or notebooks (first run)
              --templates            the template gallery instead of recent workspaces
              --create <kind>        the "New Script" / "New Notebook" dialog: script or notebook
          docs                   The Docs learning center.
              --article <words>      open the first article whose title contains the words
              --list                 print every category and article (no image)
          visualizer [n ...]     Run each problem's script and save steps of every visualizer it shows
                                 (every problem when no numbers are given).
              --steps <s,s,...>      steps to save; negative counts from the end (default: first, 1/3, 2/3, last)
              --quiet                don't print each step's description

        Options for every command
          --width <px>  --height <px>   window size, which is the image size
          --light                       light theme instead of dark
          --hover <x,y>                 move the mouse there before saving, to show hover states
          --name <file>                 file name without .png (single-image commands)
          --out <folder>                where images go (default: tools/UiSnapshots/out)

        Every saved file's full path is printed, one per line. To look closer (zoom, coordinate grid, before/after
        diff, contact sheet): python3 tools/UiSnapshots/images.py --help. Guide: docs/headless-ui-snapshots.md
        """;
}
