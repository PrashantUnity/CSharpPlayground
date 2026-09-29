using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using PdfEditorApp.Plugins.CSharpEditor.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using PdfEditorApp.Plugins.CSharpEditor.Views;

namespace PdfEditorApp.Plugins.CSharpEditor.Tools.UiSnapshots;

/// <summary>The Blind 75 page: the problem browser, one problem's details panel, its markdown and its generated code.</summary>
internal static class Blind75Snapshots
{
    /// <summary><c>blind75</c>: the browser, after the filters, marks, open details and divider drags the options ask for.</summary>
    public static void Browser(Options options)
    {
        var vm = NewViewModel();
        foreach (int number in options.Numbers("solved")) Snapshot.Wait(vm.ToggleSolvedAsync(Find(vm, number)));
        foreach (int number in options.Numbers("saved")) Snapshot.Wait(vm.ToggleBookmarkAsync(Find(vm, number)));
        if (options.Value("category") is { } category) vm.SetCategory(category);
        if (options.Value("difficulty") is { } difficulty) vm.SetDifficulty(difficulty);
        if (options.Value("status") is { } status) vm.SetStatusFilter(status);
        if (options.Value("search") is { } search) vm.SearchQuery = search;
        foreach (string column in options.List("sort")) vm.SortBy(column);
        foreach (int number in options.Numbers("details")) vm.OpenDetailFlyout(Find(vm, number));

        var view = new CSharpBlindProblemsView { DataContext = vm };
        var window = Snapshot.Show(view, options.Int("width", 1400), options.Int("height", 820));

        // "1:-200" drags divider 1 (Title|Category) 200px left, with real pointer events on the header's divider.
        foreach (string drag in options.List("drag"))
        {
            var parts = drag.Split(':');
            if (parts.Length != 2 || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double dx))
            {
                throw new ArgumentException($"--drag wants divider:pixels, like 1:-200; not '{drag}'.");
            }
            var divider = view.GetVisualDescendants().OfType<Border>()
                .FirstOrDefault(b => b.Classes.Contains("column-divider") && b.Tag as string == parts[0] && b.IsEffectivelyVisible)
                ?? throw new ArgumentException($"There is no visible column divider {parts[0]} (1, 2 or 3; 1 hides with Category on narrow tables).");
            var center = divider.TranslatePoint(new Point(divider.Bounds.Width / 2, divider.Bounds.Height / 2), window)!.Value;
            Snapshot.Drag(window, center, new Vector(dx, 0));
            Console.WriteLine($"dragged divider {parts[0]} {dx:+0;-0}px: Title, Category, Acceptance, Difficulty = " +
                              string.Join(", ", vm.Columns.Widths().Select(w => w.ToString("0", CultureInfo.InvariantCulture))));
        }

        Snapshot.Save(window, options, "blind75");
    }

    /// <summary><c>details n</c>: the details panel on its own, in a window tall enough to show all of it.</summary>
    public static void Details(Options options)
    {
        int number = options.Problem();
        var vm = NewViewModel();
        vm.OpenDetailFlyout(Find(vm, number));

        // On the page the panel gets these styles from CSharpBlindProblemsView; shown alone it has to be given them.
        var panel = new BlindProblemDetailFlyoutControl { DataContext = vm };
        Snapshot.AddPluginStyles(panel, "Controls/SharedStudioStyles.axaml", "Controls/BlindProblemsStyles.axaml");

        var window = Snapshot.Show(panel, options.Int("width", 410), options.Int("height", 2600));
        Snapshot.Save(window, options, $"details_{number}");
    }

    /// <summary><c>markdown n</c>: the statement and "How to think" through MarkdownView, the renderer the app uses.</summary>
    public static void Markdown(Options options)
    {
        var problem = Blind75CatalogService.GetProblemByNumber(options.Problem())!;
        var view = new MarkdownView
        {
            Markdown = problem.DescriptionMarkdown + "\n\n## How to think\n\n" + problem.ThinkingProcessMarkdown,
            FontSize = 12.5
        };
        view.Bind(MarkdownView.ForegroundProperty, view.GetResourceObservable("M3OnSurfaceBrush"));
        var page = new Border { Padding = new Thickness(16), Child = view };
        page.Bind(Border.BackgroundProperty, page.GetResourceObservable("M3SurfaceContainerLowestBrush"));

        var window = Snapshot.Show(page, options.Int("width", 440), options.Int("height", 1600));
        Snapshot.Save(window, options, $"markdown_{problem.Number}");
    }

    /// <summary><c>script n</c>: prints the code Code Studio and the notebook get for the problem; no image.</summary>
    public static void Script(Options options)
    {
        var problem = Blind75CatalogService.GetProblemByNumber(options.Problem())!;
        Console.WriteLine(Blind75CatalogService.ConvertToScript(problem).Code);
        foreach (var cell in Blind75CatalogService.ConvertToNotebook(problem).Cells.Where(c => c.Type == CellType.Code))
        {
            Console.WriteLine("\n// ───── notebook code cell ─────");
            Console.WriteLine(cell.Source);
        }
    }

    // Throwaway progress, so marking problems solved here never changes your real Blind 75 progress; no browser opens.
    private static CSharpBlindProblemsViewModel NewViewModel() =>
        new(new LocalBlindProgressService(Snapshot.TempFolder("blind75-progress")), openUrlAction: url => Console.WriteLine($"(would open {url})"));

    private static BlindProblemItem Find(CSharpBlindProblemsViewModel vm, int number) => vm.AllProblems.First(p => p.Number == number);
}
