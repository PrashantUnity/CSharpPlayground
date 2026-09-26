using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
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
        // Languages over a throwaway folder: the STUDIO ENVIRONMENT rows show the machine's own Python (or --python's),
        // or with --nothing-installed what the Hub says when there's none.
        var languages = new StudioLanguageServices(Snapshot.TempFolder("languages"), host: options.Flag("nothing-installed") ? new NothingInstalledHost() : null);
        if (options.Value("python") is { } python) languages.Registry.Get(LanguageIds.Python)?.Toolchain?.Select(python);
        var storage = new LocalScriptStorageService(Snapshot.TempFolder("hub"), languages.Registry);
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

        var vm = new CSharpManagerViewModel(storage, openScriptAction: _ => { }, openNotebookAction: _ => { }, languages: languages);
        Snapshot.Wait(vm.LoadWorkspaceItemsAsync());
        if (vm.AllItems.FirstOrDefault() is { } first) Snapshot.Wait(vm.TogglePinAsync(first));

        if (options.Flag("templates")) vm.ActiveDashboardView = "Templates";
        if (options.Value("create") is { } create)
        {
            vm.PendingCreateKind = create.Equals("notebook", StringComparison.OrdinalIgnoreCase) ? WorkspaceItemKind.Notebook : WorkspaceItemKind.Script;
            vm.NewItemName = vm.PendingCreateKind == WorkspaceItemKind.Notebook ? "New Interactive Notebook" : "New Script";
        }

        var window = Snapshot.Show(new CSharpManagerView { DataContext = vm }, options.Int("width", 1400), options.Int("height", 900));
        Snapshot.Wait(vm.RefreshToolchainStatusesAsync()); // the panel started it when it was shown
        Snapshot.Settle();
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

/// <summary>A machine with nothing installed (for --nothing-installed): no files, no PATH, and no programs run.</summary>
internal sealed class NothingInstalledHost : IHostEnvironment
{
    private readonly HostEnvironment _real = new();

    public bool IsWindows => _real.IsWindows;
    public bool IsMacOS => _real.IsMacOS;
    public bool IsLinux => _real.IsLinux;
    public string HomeDirectory => _real.HomeDirectory;

    public string? GetEnvironmentVariable(string name) => name.Equals("PATH", StringComparison.OrdinalIgnoreCase) ? string.Empty : _real.GetEnvironmentVariable(name);
    public bool FileExists(string path) => false;
    public bool DirectoryExists(string path) => false;
    public IReadOnlyList<string> GetDirectories(string path) => Array.Empty<string>();
    public Task<string?> GetLoginShellPathAsync(CancellationToken ct = default) => Task.FromResult<string?>(string.Empty);

    public Task<CommandResult> RunAsync(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken ct = default) =>
        Task.FromResult(new CommandResult(-1, string.Empty, string.Empty, TimedOut: false));
}
