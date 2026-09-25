using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using PdfEditorApp.Plugins.CSharpEditor.Views;

namespace PdfEditorApp.Plugins.CSharpEditor.Tools.UiSnapshots;

/// <summary>The Hub (workspace dashboard) and the Docs learning center.</summary>
internal static class HubAndDocsSnapshots
{
    /// <summary>
    /// <c>hub</c>: the dashboard over a throwaway workspace holding a few scripts and notebooks, one of them pinned
    /// (<c>--empty</c> for a first run). <c>--templates</c> shows the template gallery; <c>--create script|notebook</c>
    /// opens the "New Script" / "New Notebook" dialog.
    /// </summary>
    public static void Hub(Options options)
    {
        var storage = new LocalScriptStorageService(Snapshot.TempFolder("hub"));
        if (!options.Flag("empty"))
        {
            foreach (string title in new[] { "Sorting playground", "PDF invoice parser", "LINQ practice" })
            {
                Snapshot.Wait(storage.CreateNewScriptAsync(title));
            }
            foreach (string title in new[] { "Data exploration", "Async walkthrough" })
            {
                Snapshot.Wait(storage.CreateNewNotebookAsync(title));
            }
        }

        var vm = new CSharpManagerViewModel(storage, openScriptAction: _ => { }, openNotebookAction: _ => { });
        Snapshot.Wait(vm.LoadWorkspaceItemsAsync());
        if (vm.AllItems.FirstOrDefault() is { } first) Snapshot.Wait(vm.TogglePinAsync(first));

        if (options.Flag("templates")) vm.ActiveDashboardView = "Templates";
        if (options.Value("create") is { } create)
        {
            vm.PendingCreateKind = create.Equals("notebook", StringComparison.OrdinalIgnoreCase) ? WorkspaceItemKind.Notebook : WorkspaceItemKind.Script;
            vm.NewItemName = vm.PendingCreateKind == WorkspaceItemKind.Notebook ? "New Interactive Notebook" : "New Script";
        }

        var window = Snapshot.Show(new CSharpManagerView { DataContext = vm }, options.Int("width", 1400), options.Int("height", 900));
        string name = options.Flag("templates") ? "hub_templates" : options.Value("create") is { } kind ? $"hub_create_{kind}" : "hub";
        Snapshot.Save(window, options, name);
    }

    /// <summary><c>docs</c>: the learning center, on the first article or the one whose title contains <c>--article</c>.</summary>
    public static void Docs(Options options)
    {
        var vm = new CSharpDocsViewModel();
        if (options.Value("article") is { } wanted)
        {
            var article = vm.Categories.SelectMany(c => c.Articles)
                .FirstOrDefault(a => a.Title.Contains(wanted, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"No docs article's title contains '{wanted}'. Try `docs --list`.");
            vm.SelectArticle(article);
        }
        if (options.Flag("list"))
        {
            foreach (var category in vm.Categories)
            {
                Console.WriteLine(category.Title);
                foreach (var article in category.Articles) Console.WriteLine($"  {article.Title}");
            }
            return;
        }

        var window = Snapshot.Show(new CSharpDocsView { DataContext = vm }, options.Int("width", 1400), options.Int("height", 900));
        Snapshot.Save(window, options, "docs");
    }
}
