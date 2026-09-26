using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using PdfEditorApp.Plugins.CSharpEditor.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using PdfEditorApp.Plugins.CSharpEditor.Views;

namespace PdfEditorApp.Plugins.CSharpEditor.Tools.UiSnapshots;

/// <summary>The two studios, with a Blind 75 problem (or any script file) open, optionally after running it.</summary>
internal static class StudioSnapshots
{
    // Activity Bar order: CSharpCodeStudioViewModel.SelectedActivityBarIndex.
    private static readonly string[] SideBarViews = { "explorer", "search", "debug", "nuget", "notes", "problems" };

    // Bottom panel tab order: CSharpCodeStudioViewModel.SelectedBottomTabIndex.
    private static readonly string[] PanelTabs = { "results", "terminal", "problems", "tests", "debug" };

    // Notebook Activity Bar order: CSharpNotebookStudioViewModel.SelectedActivityBarIndex.
    private static readonly string[] NotebookSideBarViews = { "explorer", "outline", "variables", "search" };

    /// <summary><c>studio n</c> or <c>studio --file path</c>: Code Studio with that script open.</summary>
    public static void CodeStudio(Options options)
    {
        string? file = options.Value("file");

        // Languages over a throwaway folder: the toolchains found are the machine's own, but nothing chosen or created
        // here reaches the user's settings. --python picks the interpreter for .py files.
        var languages = new StudioLanguageServices(Snapshot.TempFolder("languages"));
        if (options.Value("python") is { } python) languages.Registry.Get(LanguageIds.Python)?.Toolchain?.Select(python);
        var storage = new LocalScriptStorageService(Snapshot.TempFolder("scripts"), languages.Registry);

        // A source file (main.py) is copied into the throwaway workspace and opened as one; any other file is a C# script.
        var sourceLanguage = languages.Registry.FindSourceFileLanguage(file);
        var script = file != null && sourceLanguage == null
            ? new ScriptDocumentItem { Title = Path.GetFileName(file), Code = File.ReadAllText(file) }
            : file != null
                ? new ScriptDocumentItem { Title = "Notes" }
                : Blind75CatalogService.ConvertToScript(Blind75CatalogService.GetProblemByNumber(options.Problem())!);

        // Throwaway progress too: --run-tests marks a Blind 75 problem solved when all its cases pass.
        var vm = new CSharpCodeStudioViewModel(
            script,
            storage,
            new RoslynCompilerService(),
            new ScriptExecutionEngine(),
            backToHubAction: () => { },
            backToHomeAction: () => { },
            blindProgress: new LocalBlindProgressService(Snapshot.TempFolder("blind75-progress")),
            languages: languages);
        if (file != null && sourceLanguage != null) OpenSourceFile(vm, storage, file);
        vm.SelectedActivityBarIndex = IndexOf(SideBarViews, options.Value("sidebar") ?? (sourceLanguage != null ? "explorer" : "notes"), "--sidebar");
        vm.IsSideBarVisible = true;
        if (options.Flag("edit-notes") && vm.IsNotesPreviewMode) vm.ToggleNotesPreviewCommand.Execute(null);

        var window = Snapshot.Show(new CSharpCodeStudioView { DataContext = vm }, options.Int("width", 1400), options.Int("height", 900));
        ShowQuickOpen(vm.QuickOpen, options);
        Task? stillRunning = null;
        if (options.Flag("run") && options.Flag("while-running"))
        {
            // --while-running: the picture is taken while the program runs (waiting for input, say), then it's stopped.
            stillRunning = vm.RunCodeCommand.ExecuteAsync(null);
            Snapshot.WaitFor(() => vm.IsAcceptingProgramInput || stillRunning.IsCompleted, TimeSpan.FromSeconds(30));
            Snapshot.WaitFor(() => false, TimeSpan.FromMilliseconds(500));
        }
        else if (options.Flag("run"))
        {
            var run = vm.RunCodeCommand.ExecuteAsync(null);
            // --stdin <text>: typed into the Terminal once the program waits for input, as a person would.
            if (options.Value("stdin") is { } input)
            {
                if (!Snapshot.WaitFor(() => vm.IsAcceptingProgramInput || run.IsCompleted, TimeSpan.FromSeconds(60)) || run.IsCompleted)
                {
                    throw new ArgumentException("--stdin: the program never waited for input.");
                }
                Snapshot.WaitFor(() => false, TimeSpan.FromMilliseconds(300)); // let its prompt arrive
                vm.ProgramInputText = input;
                Snapshot.Wait(vm.SendProgramInputCommand.ExecuteAsync(null));
            }
            Snapshot.Wait(run);
            Snapshot.Settle();
        }
        if (options.Value("debug") != null) PauseAt(vm, options.Int("debug", 1));
        // The Test Cases panel: Generate (Blind 75 problems), Run All, and the Add Test Case form.
        if (options.Flag("generate-tests")) vm.GenerateTestCasesCommand.Execute(null);
        if (options.Flag("run-tests"))
        {
            Snapshot.Wait(vm.RunAllTestCasesCommand.ExecuteAsync(null));
            Snapshot.Settle();
        }
        if (options.Flag("add-test")) vm.AddTestCaseCommand.Execute(null);
        if (options.Value("panel") is { } panel)
        {
            vm.IsBottomDeckExpanded = true;
            vm.SelectedBottomTabIndex = IndexOf(PanelTabs, panel, "--panel");
        }
        // The bottom panel keeps its own height (280px) however tall the window is; this is the splitter's position.
        if (options.Value("panel-height") != null)
        {
            vm.IsBottomDeckExpanded = true;
            vm.BottomDeckGridLength = new GridLength(options.Int("panel-height", 280));
        }
        Snapshot.Settle();
        ShowQuickInfo(window, options);

        var name = options.Value("name") ?? (file != null ? $"studio_{Path.GetFileNameWithoutExtension(file)}" : $"studio_{options.Problem()}");
        Snapshot.Save(window, options, name);
        if (options.Value("menu") is { } menu)
        {
            // The toolchain menu lists what's installed, which takes a moment to look for.
            Snapshot.Wait(vm.RefreshToolchainsCommand.ExecuteAsync(null));
            SaveMenu(window, menu, name, selectedCell: null);
        }
        if (vm.IsDebugging) vm.StopDebug();
        if (stillRunning != null)
        {
            vm.StopCommand.Execute(null);
            Snapshot.Wait(stillRunning);
        }
    }

    // The file goes into the workspace under its own name and is opened from the Explorer, as a person would.
    private static void OpenSourceFile(CSharpCodeStudioViewModel vm, LocalScriptStorageService storage, string file)
    {
        var copy = Path.Combine(storage.LibraryRootPath, Path.GetFileName(file));
        File.Copy(file, copy, overwrite: true);
        Snapshot.Wait(vm.RefreshExplorerAsync());
        var item = vm.ExplorerRootItems.First(i => string.Equals(i.Name, Path.GetFileName(file), StringComparison.OrdinalIgnoreCase));
        Snapshot.Wait(vm.SwitchToScriptAsync(item));
    }

    // --debug <line>: a breakpoint on that line, then Debug (F5), returning once the debugger has paused there. The
    // debug run itself only finishes when it's resumed, so this waits for the pause rather than for the command.
    private static void PauseAt(CSharpCodeStudioViewModel vm, int line)
    {
        vm.ToggleBreakpoint(line);
        _ = vm.DebugCodeCommand.ExecuteAsync(null);
        if (!Snapshot.WaitFor(() => vm.IsPaused, TimeSpan.FromSeconds(60)))
        {
            throw new ArgumentException($"--debug {line}: the debugger never paused there. Is line {line} a statement?");
        }
        Snapshot.Settle();
    }

    // --quick-info <text>: rest the mouse on the first occurrence of <text> in an editor (the script, or the first
    // notebook cell that has it), as a person would, and wait for the hover card: Quick Info, or the debugger's data
    // tip while paused (--debug). The analysis runs on a worker thread, and the first one reads the documentation files.
    private static void ShowQuickInfo(Window window, Options options)
    {
        if (options.Value("quick-info") is not { } target) return;

        var editor = window.GetVisualDescendants().OfType<TextEditor>().FirstOrDefault(e => e.Text?.Contains(target, StringComparison.Ordinal) == true)
            ?? throw new ArgumentException($"--quick-info: '{target}' isn't in any editor.");
        int offset = editor.Text.IndexOf(target, StringComparison.Ordinal);

        // Scroll only when the text is off-screen, so a word near the edge stays there (the card then flips above it).
        var location = editor.Document.GetLocation(offset);
        var textView = editor.TextArea.TextView;
        textView.EnsureVisualLines();
        if (!textView.VisualLines.Any(l => l.FirstDocumentLine.LineNumber <= location.Line && location.Line <= l.LastDocumentLine.LineNumber))
        {
            editor.ScrollTo(location.Line, location.Column);
        }
        editor.BringIntoView();
        Snapshot.Settle();

        // Just inside the word's first character, halfway down its line (text view coordinates are document minus scroll).
        var word = textView.GetVisualPosition(new TextViewPosition(location), VisualYPosition.LineMiddle) - textView.ScrollOffset;
        var point = textView.TranslatePoint(word + new Vector(3, 0), window) ?? throw new InvalidOperationException("The editor isn't in the window.");
        window.MouseMove(point);

        if (!Snapshot.WaitFor(() => window.GetVisualDescendants().Any(c => c is QuickInfoTipControl or DebugHoverDataTipControl && c.IsEffectivelyVisible), TimeSpan.FromSeconds(20)))
        {
            Console.Error.WriteLine($"--quick-info: no hover card appeared for '{target}'.");
        }
        Snapshot.Settle();
    }

    /// <summary>
    /// <c>notebook n</c>: Notebook Studio with the problem's notebook; <c>notebook --python-demo</c>: a notebook of C# and
    /// Python cells. <c>--run</c> runs every cell first.
    /// </summary>
    public static void Notebook(Options options)
    {
        // Languages over a throwaway folder, as for Code Studio: --python picks the interpreter Python cells run with.
        var languages = new StudioLanguageServices(Snapshot.TempFolder("languages"));
        if (options.Value("python") is { } python) languages.Registry.Get(LanguageIds.Python)?.Toolchain?.Select(python);

        var demo = options.Flag("python-demo");
        int number = demo ? 0 : options.Problem();
        var vm = new CSharpNotebookStudioViewModel(
            demo ? PythonDemoNotebook() : Blind75CatalogService.ConvertToNotebook(Blind75CatalogService.GetProblemByNumber(number)!),
            new LocalScriptStorageService(Snapshot.TempFolder("notebooks"), languages.Registry),
            new RoslynCompilerService(),
            new ScriptExecutionEngine(),
            backToHubAction: () => { },
            backToHomeAction: () => { },
            languages: languages);

        if (options.Value("sidebar") is { } sidebar)
        {
            vm.SelectedActivityBarIndex = IndexOf(NotebookSideBarViews, sidebar, "--sidebar");
            vm.IsSideBarVisible = true;
        }

        var window = Snapshot.Show(new CSharpNotebookStudioView { DataContext = vm }, options.Int("width", 1400), options.Int("height", 900));
        ShowQuickOpen(vm.QuickOpen, options);
        try
        {
            var name = options.Value("name") ?? (demo ? "notebook_python_demo" : $"notebook_{number}");
            if (options.Flag("run") && RunAll(vm, window, options, name)) return;

            // --cell <n>: the n-th cell (from 1) is selected, as a click would, so its toolbar shows.
            if (options.Int("cell", 0) is > 0 and var cell && vm.ActiveTab is { } tab)
            {
                tab.SelectCell(tab.Cells[Math.Min(cell, tab.Cells.Count) - 1]);
                Snapshot.Settle();
            }

            ShowQuickInfo(window, options);
            Snapshot.Save(window, options, name);
            if (options.Value("menu") is { } menu) SaveMenu(window, menu, name, vm.ActiveCell);
        }
        finally
        {
            // The Python kernel runs as a program of its own.
            foreach (var tab in vm.Tabs) tab.ShutdownKernels();
        }
    }

    // Runs every cell. A cell that asks for input (Python's input()) gets --stdin's text, or end of input; with
    // --while-running the picture is taken while it waits instead (true: it's saved, and the rest is stopped).
    private static bool RunAll(CSharpNotebookStudioViewModel vm, Window window, Options options, string name)
    {
        var run = vm.RunAllCellsAsync();
        while (Snapshot.WaitFor(() => run.IsCompleted || vm.Cells.Any(c => c.IsAwaitingInput), TimeSpan.FromSeconds(120)) && !run.IsCompleted)
        {
            var asking = vm.Cells.First(c => c.IsAwaitingInput);
            if (options.Flag("while-running"))
            {
                vm.ActiveTab?.SelectCell(asking);
                Snapshot.Settle();
                Snapshot.Save(window, options, name);
                vm.ActiveTab?.InterruptExecution();
                Snapshot.Wait(run);
                return true;
            }

            if (options.Value("stdin") is not { } answer)
            {
                asking.EndInput();
                continue;
            }

            // Typed into the cell's input box and sent with Enter, as a person would (the box takes the focus itself).
            Snapshot.Settle();
            window.KeyTextInput(answer);
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Snapshot.Settle();
            if (asking.IsAwaitingInput) throw new InvalidOperationException("--stdin: typing into the cell's input box didn't send the answer.");
        }

        Snapshot.Wait(run);
        Snapshot.Settle();
        return false;
    }

    // --menu language|kernel: opens the selected cell's language menu or the kernel pill's and saves the picture again
    // as <name>_menu (and the menu on its own, when it opens in a window of its own).
    private static void SaveMenu(Window window, string menu, string name, NotebookCellViewModel? selectedCell)
    {
        menu = menu.ToLowerInvariant();
        var tip = menu switch
        {
            "language" => "The cell's language",
            "kernel" => "The notebook's kernels",
            "toolchain" => "Choose which installed toolchain",
            _ => throw new ArgumentException($"--menu is language or kernel (notebook), or toolchain (studio); not '{menu}'.")
        };
        // Every cell has a language menu (only the selected cell's toolbar is opaque), so it's the selected cell's.
        var button = window.GetVisualDescendants().OfType<Button>().FirstOrDefault(b =>
                         b.IsEffectivelyVisible && b.Flyout != null && ToolTip.GetTip(b) is string text && text.StartsWith(tip, StringComparison.Ordinal) &&
                         (menu != "language" || ReferenceEquals(b.DataContext, selectedCell)))
                     ?? throw new ArgumentException($"--menu {menu}: that menu's button isn't showing (for language, select a code cell with --cell).");

        button.Flyout!.ShowAt(button);
        Snapshot.Settle();
        Snapshot.Save(window, name + "_menu");
        if (button.Flyout is Flyout { Content: Visual content } && TopLevel.GetTopLevel(content) is { } popup && !ReferenceEquals(popup, window))
        {
            using var frame = popup.CaptureRenderedFrame();
            if (frame != null)
            {
                var path = Path.Combine(Snapshot.OutputFolder, name + "_menu_popup.png");
                frame.Save(path, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
                Console.WriteLine(path);
            }
        }
        button.Flyout.Hide();
    }

    // C# and Python cells side by side, sharing values both ways (#!share): numpy, a pandas table, a matplotlib figure
    // and input() (what each needs installed shows as an "Install" offer in the cell when it's missing).
    private static NotebookDocumentItem PythonDemoNotebook() => new()
    {
        Title = "CSharp and Python",
        Cells =
        {
            new NotebookCellItem
            {
                Type = CellType.Markdown,
                Source = "## C# and Python in one notebook\nEach cell runs in its language's kernel: pick it from the cell's language menu, or start the cell with `#!python`."
            },
            new NotebookCellItem { Type = CellType.Code, Source = "var nums = new[] { 3, 1, 4, 1, 5, 9, 2, 6 };\nnums.Sum()" },
            new NotebookCellItem
            {
                Type = CellType.Code,
                Language = LanguageIds.Python,
                Source = "#!share --from csharp nums\nimport sys\nimport numpy as np\n\nprint(\"Python\", sys.version.split()[0], \"with numpy\", np.__version__)\narr = np.array(nums)\narr.mean(), arr.std().round(3)"
            },
            new NotebookCellItem
            {
                Type = CellType.Code,
                Language = LanguageIds.Python,
                Source = "import pandas as pd\n\nframe = pd.DataFrame({\"n\": arr, \"square\": arr ** 2, \"even\": arr % 2 == 0})\nsquares = frame[\"square\"]\nframe"
            },
            new NotebookCellItem
            {
                Type = CellType.Code,
                Source = "#!python\nimport matplotlib.pyplot as plt\n\nplt.figure(figsize=(6, 2.4))\nplt.plot(arr, marker=\"o\")\nplt.title(\"nums\")\nplt.show()"
            },
            new NotebookCellItem
            {
                Type = CellType.Code,
                Source = "#!share --from python squares\n$\"C# got {squares.Length} squares from Python; the biggest is {squares.Max()}\""
            },
            new NotebookCellItem
            {
                Type = CellType.Code,
                Language = LanguageIds.Python,
                Source = "name = input(\"Your name? \") or \"there\"\nprint(f\"Hi {name}: the numbers add up to {arr.sum()}\")"
            }
        }
    };

    // --quick-open files|commands: the Ctrl+P / Ctrl+Shift+P palette over the studio.
    private static void ShowQuickOpen(QuickOpenViewModel quickOpen, Options options)
    {
        if (options.Value("quick-open") is not { } mode) return;
        quickOpen.Show(mode.Equals("commands", StringComparison.OrdinalIgnoreCase) ? QuickOpenMode.Commands : QuickOpenMode.Files);
        Snapshot.Settle();
    }

    private static int IndexOf(string[] names, string name, string option)
    {
        int index = Array.FindIndex(names, n => n.Equals(name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index : throw new ArgumentException($"{option} is one of {string.Join(", ", names)}; not '{name}'.");
    }
}
