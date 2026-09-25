using Avalonia.Controls;
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

        var vm = new CSharpCodeStudioViewModel(
            script,
            new LocalScriptStorageService(Snapshot.TempFolder("scripts")),
            new RoslynCompilerService(),
            new ScriptExecutionEngine(),
            backToHubAction: () => { },
            backToHomeAction: () => { });
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

        Snapshot.Save(window, options, file != null ? $"studio_{Path.GetFileNameWithoutExtension(file)}" : $"studio_{options.Problem()}");
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
