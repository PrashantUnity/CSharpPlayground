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
        if (options.Value("dotnet") is { } dotnetPath) languages.ToolchainSettings.SetSelectedPath(LanguageIds.CSharp, dotnetPath);
        if (options.Value("csharp-engine") is { } csharpEngine)
        {
            var settings = languages.StudioSettings.GetSettings();
            settings.CSharpExecutionEngine = csharpEngine;
            languages.StudioSettings.SaveSettings(settings);
        }
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

        if (options.Value("zoom") is { } zoomStr && double.TryParse(zoomStr, System.Globalization.CultureInfo.InvariantCulture, out var zoomSize))
        {
            vm.EditorFontSize = zoomSize;
        }

        var window = Snapshot.Show(new CSharpCodeStudioView { DataContext = vm }, options.Int("width", 1400), options.Int("height", 900));
        if (options.Value("zoom-keys") is { } zoomKeys)
        {
            ApplyZoomKeys(window, zoomKeys);
        }
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
        if (options.Value("panel-dock") == "bottom")
        {
            vm.IsBottomDeckExpanded = true;
            vm.ToggleDeckPosition();
            vm.IsDeckDockedToRight = false;
        }
        else if (options.Value("panel-dock") == "right" || options.Flag("dock-right"))
        {
            vm.IsBottomDeckExpanded = true;
            vm.IsDeckDockedToRight = true;
            if (options.Value("panel-width") != null)
            {
                vm.RightDeckGridLength = new GridLength(options.Int("panel-width", 540));
            }
            else
            {
                vm.UpdateAdaptiveDeckWidth(options.Int("width", 1400));
            }
        }
        else
        {
            vm.UpdateAdaptiveDeckWidth(options.Int("width", 1400));
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

    private static void ApplyZoomKeys(Window window, string zoomKeys)
    {
        var target = window.GetVisualDescendants().OfType<TextEditor>().FirstOrDefault() as InputElement
            ?? window.Content as InputElement
            ?? window;

        foreach (var action in zoomKeys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (action.Equals("in", StringComparison.OrdinalIgnoreCase))
            {
                target.RaiseEvent(new KeyEventArgs
                {
                    RoutedEvent = InputElement.KeyDownEvent,
                    Key = Key.OemPlus,
                    PhysicalKey = PhysicalKey.Equal,
                    KeyModifiers = KeyModifiers.Control,
                    Source = target
                });
            }
            else if (action.Equals("out", StringComparison.OrdinalIgnoreCase))
            {
                target.RaiseEvent(new KeyEventArgs
                {
                    RoutedEvent = InputElement.KeyDownEvent,
                    Key = Key.OemMinus,
                    PhysicalKey = PhysicalKey.Minus,
                    KeyModifiers = KeyModifiers.Control,
                    Source = target
                });
            }
            else if (action.Equals("reset", StringComparison.OrdinalIgnoreCase))
            {
                target.RaiseEvent(new KeyEventArgs
                {
                    RoutedEvent = InputElement.KeyDownEvent,
                    Key = Key.D0,
                    PhysicalKey = PhysicalKey.Digit0,
                    KeyModifiers = KeyModifiers.Control,
                    Source = target
                });
            }
        }
        Snapshot.Settle();
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

        var pyDemo = options.Flag("python-demo");
        var jsDemo = options.Flag("js-demo") || options.Flag("polyglot-demo");
        var javaShare = options.Flag("java-share");
        var javaException = options.Flag("java-exception");
        var javaTable = options.Flag("java-table");
        var cppDemo = options.Flag("cpp-demo");
        var goDemo = options.Flag("go-demo");
        var fsharpDemo = options.Flag("fsharp-demo");
        var sqlDemo = options.Flag("sql-demo");
        int number = (pyDemo || jsDemo || javaShare || javaException || javaTable || cppDemo || goDemo || fsharpDemo || sqlDemo) ? 0 : options.Problem();
        var vm = new CSharpNotebookStudioViewModel(
            sqlDemo ? SqlDemoNotebook() : fsharpDemo ? FSharpDemoNotebook() : goDemo ? GoDemoNotebook() : cppDemo ? CppDemoNotebook() : javaException ? JavaExceptionDemoNotebook() : javaTable ? JavaTableDemoNotebook() : javaShare ? JavaShareDemoNotebook() : jsDemo ? PolyglotDemoNotebook() : pyDemo ? PythonDemoNotebook() : Blind75CatalogService.ConvertToNotebook(Blind75CatalogService.GetProblemByNumber(number)!),
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

        if (options.Value("zoom") is { } nbZoomStr && double.TryParse(nbZoomStr, System.Globalization.CultureInfo.InvariantCulture, out var nbZoomSize))
        {
            vm.EditorFontSize = nbZoomSize;
        }

        var window = Snapshot.Show(new CSharpNotebookStudioView { DataContext = vm }, options.Int("width", 1400), options.Int("height", 900));
        if (options.Value("zoom-keys") is { } nbZoomKeys)
        {
            ApplyZoomKeys(window, nbZoomKeys);
        }
        ShowQuickOpen(vm.QuickOpen, options);
        try
        {
            var name = options.Value("name") ?? (sqlDemo ? "notebook_sql_demo" : fsharpDemo ? "notebook_fsharp_demo" : goDemo ? "notebook_go_demo" : cppDemo ? "notebook_cpp_demo" : javaException ? "notebook_java_exception" : javaTable ? "notebook_java_table" : javaShare ? "notebook_java_share_test" : jsDemo ? "notebook_polyglot_demo" : pyDemo ? "notebook_python_demo" : $"notebook_{number}");
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
            "mode" => "Select C# Execution Mode",
            _ => throw new ArgumentException($"--menu is language, kernel (notebook), toolchain or mode (studio); not '{menu}'.")
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
                Source = """
                    ## C# and Python in one notebook
                    Each cell runs in its language's kernel: pick it from the cell's language menu, or start the cell with `#!python`.
                    """
            },
            new NotebookCellItem
            {
                Type = CellType.Code,
                Source = """
                    var nums = new[] { 3, 1, 4, 1, 5, 9, 2, 6 };
                    nums.Sum()
                    """
            },
            new NotebookCellItem
            {
                Type = CellType.Code,
                Language = LanguageIds.Python,
                Source = """
                    #!share --from csharp nums
                    import sys
                    import numpy as np

                    print("Python", sys.version.split()[0], "with numpy", np.__version__)
                    arr = np.array(nums)
                    arr.mean(), arr.std().round(3)
                    """
            },
            new NotebookCellItem
            {
                Type = CellType.Code,
                Language = LanguageIds.Python,
                Source = """
                    import pandas as pd

                    frame = pd.DataFrame({"n": arr, "square": arr ** 2, "even": arr % 2 == 0})
                    squares = frame["square"]
                    frame
                    """
            },
            new NotebookCellItem
            {
                Type = CellType.Code,
                Source = """
                    #!python
                    import matplotlib.pyplot as plt

                    plt.figure(figsize=(6, 2.4))
                    plt.plot(arr, marker="o")
                    plt.title("nums")
                    plt.show()
                    """
            },
            new NotebookCellItem
            {
                Type = CellType.Code,
                Source = """
                    #!share --from python squares
                    $"C# got {squares.Length} squares from Python; the biggest is {squares.Max()}"
                    """
            },
            new NotebookCellItem
            {
                Type = CellType.Code,
                Language = LanguageIds.Python,
                Source = """
                    name = input("Your name? ") or "there"
                    print(f"Hi {name}: the numbers add up to {arr.sum()}")
                    """
            }
        }
    };

    private static NotebookDocumentItem PolyglotDemoNotebook() => new()
    {
        Title = "Polyglot: C#, Python, JavaScript, Java & C++",
        Cells =
        {
            new NotebookCellItem
            {
                Type = CellType.Markdown,
                Source = """
                    ## Polyglot Notebook (C#, Python, JavaScript, Java & C++)
                    Each cell runs in its native runtime. Data shares seamlessly across all five languages with `#!share`.
                    """
            },
            new NotebookCellItem
            {
                Type = CellType.Code,
                Language = LanguageIds.CSharp,
                Source = """
                    var scores = new[] { 88, 95, 72, 91, 84 };
                    $"C# generated {scores.Length} test scores; average is {scores.Average():F1}"
                    """
            },
            new NotebookCellItem
            {
                Type = CellType.Code,
                Language = LanguageIds.JavaScript,
                Source = """
                    #!share --from csharp scores
                    console.log(`Node.js ${process.version} received ${scores.length} scores from C#!`);
                    const grades = scores.map(s => ({
                        score: s,
                        letter: s >= 90 ? 'A' : s >= 80 ? 'B' : 'C',
                        passed: s >= 80
                    }));
                    display(grades);
                    grades
                    """
            },
            new NotebookCellItem
            {
                Type = CellType.Code,
                Language = LanguageIds.Python,
                Source = """
                    #!share --from js grades
                    passed = sum(1 for g in grades if g['passed'])
                    avg = sum(g['score'] for g in grades) / len(grades)
                    print(f"Python analyzed {len(grades)} grades from JavaScript:")
                    print(f"Passed: {passed}/{len(grades)} students. Mean score: {avg:.1f}")
                    grades
                    """
            },
            new NotebookCellItem
            {
                Type = CellType.Code,
                Language = LanguageIds.Java,
                Source = """
                    #!share --from csharp scores
                    int sum = Arrays.stream(scores).sum();
                    double avg = Arrays.stream(scores).average().orElse(0.0);
                    System.out.println("Java JShell: sum = " + sum + ", avg = " + avg);
                    Map<String, Object> javaStats = Map.of("sum", sum, "avg", avg, "count", scores.length);
                    javaStats;
                    """
            },
            new NotebookCellItem
            {
                Type = CellType.Code,
                Language = LanguageIds.Cpp,
                Source = """
                    #!share --from csharp scores
                    int maxScore = 0;
                    for (int s : scores) {
                        if (s > maxScore) maxScore = s;
                    }
                    std::cout << "C++ calculated maximum score: " << maxScore << std::endl;
                    fry::display::table(scores, "C++ Processed Scores");
                    """
            },
            new NotebookCellItem
            {
                Type = CellType.Code,
                Language = LanguageIds.JavaScript,
                Source = """
                    const stats = { count: scores.length, max: Math.max(...scores), min: Math.min(...scores) };
                    stats
                    """
            },
            new NotebookCellItem
            {
                Type = CellType.Code,
                Language = LanguageIds.CSharp,
                Source = """
                    #!share --from js stats
                    #!share --from java javaStats
                    $"C# received: JS Max={stats["max"]}, Java Sum={javaStats["sum"]}, Java Avg={javaStats["avg"]}"
                    """
            }
        }
    };

    private static NotebookDocumentItem CppDemoNotebook() => new()
    {
        Title = "C++20 Interactive Notebook",
        Cells =
        {
            new NotebookCellItem
            {
                Type = CellType.Markdown,
                Source = """
                    # C++20 Interactive Notebook
                    Interactive C++ cell execution with Clang/GCC/MSVC, `<fry/display.hpp>` visual dumps, and STL algorithms.
                    """
            },
            new NotebookCellItem
            {
                Type = CellType.Code,
                Language = LanguageIds.Cpp,
                Source = """
                    #include <iostream>
                    #include <vector>
                    #include <numeric>

                    std::vector<int> numbers = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                    int total = std::accumulate(numbers.begin(), numbers.end(), 0);
                    std::cout << "Sum of numbers: " << total << std::endl;
                    fry::dump(numbers, "Initial Vector");
                    """
            },
            new NotebookCellItem
            {
                Type = CellType.Code,
                Language = LanguageIds.Cpp,
                Source = """
                    #include <algorithm>

                    std::vector<int> squares;
                    for (int n : numbers) {
                        squares.push_back(n * n);
                    }
                    std::cout << "Computed " << squares.size() << " squares." << std::endl;
                    Display::table(squares, "Squares of Numbers");
                    """
            }
        }
    };

    private static NotebookDocumentItem FSharpDemoNotebook() => new()
    {
        Title = "F# Interactive Polyglot Notebook",
        Cells =
        {
            new NotebookCellItem
            {
                Type = CellType.Markdown,
                Source = """
                    # F# Interactive Polyglot Notebook
                    Interactive F# script cells running via F# Interactive (`dotnet fsi`) with type-safe pipelines, pattern matching, and visual dumps.
                    """
            },
            new NotebookCellItem
            {
                Type = CellType.Code,
                Language = LanguageIds.FSharp,
                Source = """
                    printfn "🚀 Hello from F# in Notebook Studio!"

                    let numbers = [ 1 .. 10 ]
                    let sumOfSquares =
                        numbers
                        |> List.map (fun x -> x * x)
                        |> List.sum

                    printfn "Sum of squares (1..10): %d" sumOfSquares
                    """
            },
            new NotebookCellItem
            {
                Type = CellType.Code,
                Language = LanguageIds.FSharp,
                Source = """
                    type Shape =
                        | Circle of radius: float
                        | Rectangle of width: float * height: float

                    let describe shape =
                        match shape with
                        | Circle r -> sprintf "Circle with radius %.2f" r
                        | Rectangle (w, h) -> sprintf "Rectangle %g x %g" w h

                    let c = Circle 4.5
                    let r = Rectangle (3.0, 7.0)

                    printfn "%s" (describe c)
                    printfn "%s" (describe r)
                    """
            }
        }
    };

    private static NotebookDocumentItem GoDemoNotebook() => new()
    {
        Title = "Go Interactive Polyglot Notebook",
        Cells =
        {
            new NotebookCellItem
            {
                Type = CellType.Markdown,
                Source = """
                    # Go Interactive Polyglot Notebook
                    Interactive Go cell execution with concurrent goroutines, channels, and cross-kernel variable sharing.
                    """
            },
            new NotebookCellItem
            {
                Type = CellType.Code,
                Language = LanguageIds.Go,
                Source = """
                    nums := []int{10, 20, 30, 40, 50}
                    total := 0
                    for _, v := range nums {
                        total += v
                    }
                    fmt.Printf("Go computed sum: %d\n", total)
                    """
            },
            new NotebookCellItem
            {
                Type = CellType.Code,
                Language = LanguageIds.Go,
                Source = """
                    ch := make(chan string)
                    go func() {
                        ch <- "🚀 Hello from concurrent Go goroutine in Notebook Studio!"
                    }()
                    msg := <-ch
                    fmt.Println(msg)
                    """
            }
        }
    };

    private static NotebookDocumentItem SqlDemoNotebook() => new()
    {
        Title = "SQL / SQLite Interactive Notebook",
        Cells =
        {
            new NotebookCellItem
            {
                Type = CellType.Markdown,
                Source = """
                    # SQL / SQLite Interactive Notebook
                    Interactive SQL cells running via `sqlite3` CLI — create tables, query results, and share data with other kernel languages.
                    """
            },
            new NotebookCellItem
            {
                Type = CellType.Code,
                Language = LanguageIds.Sql,
                Source = """
                    -- Create a products table and populate it
                    CREATE TABLE IF NOT EXISTS products (
                        id    INTEGER PRIMARY KEY AUTOINCREMENT,
                        name  TEXT    NOT NULL,
                        price REAL    NOT NULL,
                        stock INTEGER DEFAULT 0
                    );

                    INSERT INTO products (name, price, stock) VALUES
                        ('Avalonia Widget',   29.99, 150),
                        ('Roslyn Compiler',   0.00,  999),
                        ('SQLite Extension',  9.99,  42),
                        ('F# Toolkit',        19.99, 75);

                    SELECT name, price, stock FROM products ORDER BY price DESC;
                    """
            },
            new NotebookCellItem
            {
                Type = CellType.Code,
                Language = LanguageIds.Sql,
                Source = """
                    -- Aggregate queries: total value and low-stock items
                    SELECT
                        COUNT(*)        AS total_products,
                        SUM(price)      AS total_price,
                        AVG(price)      AS avg_price,
                        MAX(stock)      AS max_stock
                    FROM products;

                    -- Low-stock items (fewer than 100 units)
                    SELECT name, stock
                    FROM   products
                    WHERE  stock < 100
                    ORDER  BY stock ASC;
                    """
            },
            new NotebookCellItem
            {
                Type = CellType.Code,
                Language = LanguageIds.Sql,
                Source = """
                    -- Share the products table into Python via #!share
                    #!share products
                    """
            }
        }
    };

    private static NotebookDocumentItem JavaShareDemoNotebook() => new()
    {
        Title = "Polyglot: Testing #!share with Java",
        Cells =
        {
            new NotebookCellItem
            {
                Type = CellType.Markdown,
                Source = """
                    ## Polyglot Share: Java Value Sharing Demo
                    Cross-language bidirectional sharing between C# Roslyn and Java JShell kernel with `#!share`.
                    """
            },
            new NotebookCellItem
            {
                Type = CellType.Code,
                Language = LanguageIds.Java,
                Source = """
                    int[] scores = new int[] { 88, 95, 72, 91, 84 };
                    int sum = Arrays.stream(scores).sum();
                    System.out.println("Java JShell computed sum: " + sum);
                    sum;
                    """
            },
            new NotebookCellItem
            {
                Type = CellType.Code,
                Language = LanguageIds.CSharp,
                Source = """
                    #!share --from java scores
                    #!share --from java sum --as javaSum
                    $"C# received sum {javaSum} and scores [{string.Join(", ", scores)}] from Java JShell!"
                    """
            }
        }
    };

    private static NotebookDocumentItem JavaExceptionDemoNotebook() => new()
    {
        Title = "Java: Exception and Traceback Handling",
        Cells =
        {
            new NotebookCellItem
            {
                Type = CellType.Markdown,
                Source = """
                    ## Java Exception Handling
                    Verifying that runtime exceptions in Java JShell are caught, formatted, and displayed cleanly without bringing down the kernel.
                    """
            },
            new NotebookCellItem
            {
                Type = CellType.Code,
                Language = LanguageIds.Java,
                Source = """
                    int divide(int a, int b) {
                        return a / b;
                    }
                    divide(10, 0);
                    """
            },
            new NotebookCellItem
            {
                Type = CellType.Code,
                Language = LanguageIds.Java,
                Source = """
                    int recovery = divide(10, 2);
                    System.out.println("Kernel survived exception! recovery = " + recovery);
                    recovery;
                    """
            }
        }
    };

    private static NotebookDocumentItem JavaTableDemoNotebook() => new()
    {
        Title = "Java: Interactive Tables and Mime Displays",
        Cells =
        {
            new NotebookCellItem
            {
                Type = CellType.Markdown,
                Source = """
                    ## Java Rich Table Display
                    Calling `display(...)` in Java JShell to render rich interactive tables with column sorting in the notebook UI.
                    """
            },
            new NotebookCellItem
            {
                Type = CellType.Code,
                Language = LanguageIds.Java,
                Source = """
                    var rows = List.of(
                        Map.of("id", 101, "package", "com.frypdf.core", "stars", 450),
                        Map.of("id", 102, "package", "com.frypdf.editor", "stars", 920),
                        Map.of("id", 103, "package", "com.frypdf.csharp", "stars", 780)
                    );
                    display(rows);
                    "Rendered " + rows.size() + " packages in interactive table."
                    """
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
