using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using PdfEditorApp.Plugins.CSharpEditor.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
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
        var script = file != null
            ? new ScriptDocumentItem { Title = Path.GetFileName(file), Code = File.ReadAllText(file) }
            : Blind75CatalogService.ConvertToScript(Blind75CatalogService.GetProblemByNumber(options.Problem())!);

        // Throwaway progress too: --run-tests marks a Blind 75 problem solved when all its cases pass.
        var vm = new CSharpCodeStudioViewModel(
            script,
            new LocalScriptStorageService(Snapshot.TempFolder("scripts")),
            new RoslynCompilerService(),
            new ScriptExecutionEngine(),
            backToHubAction: () => { },
            backToHomeAction: () => { },
            blindProgress: new LocalBlindProgressService(Snapshot.TempFolder("blind75-progress")));
        vm.SelectedActivityBarIndex = IndexOf(SideBarViews, options.Value("sidebar") ?? "notes", "--sidebar");
        vm.IsSideBarVisible = true;
        if (options.Flag("edit-notes") && vm.IsNotesPreviewMode) vm.ToggleNotesPreviewCommand.Execute(null);

        var window = Snapshot.Show(new CSharpCodeStudioView { DataContext = vm }, options.Int("width", 1400), options.Int("height", 900));
        ShowQuickOpen(vm.QuickOpen, options);
        if (options.Flag("run"))
        {
            Snapshot.Wait(vm.RunCodeCommand.ExecuteAsync(null));
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

        Snapshot.Save(window, options, file != null ? $"studio_{Path.GetFileNameWithoutExtension(file)}" : $"studio_{options.Problem()}");
        if (vm.IsDebugging) vm.StopDebug();
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

    /// <summary><c>notebook n</c>: Notebook Studio with the problem's notebook; <c>--run</c> runs every cell first.</summary>
    public static void Notebook(Options options)
    {
        int number = options.Problem();
        var vm = new CSharpNotebookStudioViewModel(
            Blind75CatalogService.ConvertToNotebook(Blind75CatalogService.GetProblemByNumber(number)!),
            new LocalScriptStorageService(Snapshot.TempFolder("notebooks")),
            new RoslynCompilerService(),
            new ScriptExecutionEngine(),
            backToHubAction: () => { },
            backToHomeAction: () => { });

        if (options.Value("sidebar") is { } sidebar)
        {
            vm.SelectedActivityBarIndex = IndexOf(NotebookSideBarViews, sidebar, "--sidebar");
            vm.IsSideBarVisible = true;
        }

        var window = Snapshot.Show(new CSharpNotebookStudioView { DataContext = vm }, options.Int("width", 1400), options.Int("height", 900));
        ShowQuickOpen(vm.QuickOpen, options);
        if (options.Flag("run"))
        {
            Snapshot.Wait(vm.RunAllCellsAsync());
            Snapshot.Settle();
        }

        ShowQuickInfo(window, options);
        Snapshot.Save(window, options, $"notebook_{number}");
    }

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
