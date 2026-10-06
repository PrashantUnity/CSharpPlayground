using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Docs;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Hub;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings;
using PdfEditorApp.Plugins.CSharpEditor.Views;

namespace PdfEditorApp.Plugins.CSharpEditor.Tools.UiSnapshots.Commands;

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
        Snapshot.EnsureExtensionsLoaded(languages, options);
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

            // Add demo source files for all supported languages so the TYPE badge colour-coding is visible.
            var libRoot = storage.LibraryRootPath;
            File.WriteAllText(Path.Combine(libRoot, "data_pipeline.py"), "# Python demo\nfor x in range(10):\n    print(x)\n");
            File.WriteAllText(Path.Combine(libRoot, "HelloWorld.java"),  "// Java demo\npublic class HelloWorld { }\n");
            File.WriteAllText(Path.Combine(libRoot, "fetch_api.js"),     "// JS demo\nfetch('/api').then(r => r.json());\n");
            File.WriteAllText(Path.Combine(libRoot, "chart_demo.dart"),  "// Dart demo\nvoid main() {\n    print('Dart demo');\n}\n");

            var projectsDir = Snapshot.TempFolder("projects");
            Directory.CreateDirectory(projectsDir);
            var proj1 = Path.Combine(projectsDir, "CSharpPlayground");
            var proj2 = Path.Combine(projectsDir, "FrysharpAlgorithms");
            var nbFile = Path.Combine(projectsDir, "DeepLearningExploration.ipynb");
            Directory.CreateDirectory(proj1);
            Directory.CreateDirectory(proj2);
            File.WriteAllText(nbFile, "{}");
            var git1 = Path.Combine(proj1, ".git");
            Directory.CreateDirectory(git1);
            File.WriteAllText(Path.Combine(git1, "HEAD"), "ref: refs/heads/main\n");

            Snapshot.Wait(storage.RecentWorkspaces.RecordWorkspaceOpenedAsync(proj1));
            Snapshot.Wait(storage.RecentWorkspaces.RecordWorkspaceOpenedAsync(proj2));
            Snapshot.Wait(storage.RecentWorkspaces.RecordWorkspaceOpenedAsync(nbFile));
        }

        var vm = new CSharpManagerViewModel(storage, openScriptAction: _ => { }, openNotebookAction: _ => { }, languages: languages);
        Snapshot.Wait(vm.LoadWorkspaceItemsAsync());
        if (vm.RecentWorkspaces.FirstOrDefault() is { } firstWs) Snapshot.Wait(vm.TogglePinRecentWorkspaceAsync(firstWs));
        if (vm.AllItems.FirstOrDefault() is { } first) Snapshot.Wait(vm.TogglePinAsync(first));

        if (options.Flag("templates") || options.Value("template") != null || options.Value("category") != null || string.Equals(options.Value("tab"), "templates", StringComparison.OrdinalIgnoreCase))
        {
            vm.ActiveDashboardView = "Templates";
        }
        if (options.Value("category") is { } category) vm.SelectedTemplateCategory = category;
        if (options.Value("search") is { } search) vm.SearchQuery = search;
        if (options.Value("template") is { } templateQuery)
        {
            if (vm.StarterTemplates.FirstOrDefault(t => t.Id.Contains(templateQuery, StringComparison.OrdinalIgnoreCase) || t.Title.Contains(templateQuery, StringComparison.OrdinalIgnoreCase)) is { } template)
            {
                vm.SelectTemplateCommand.Execute(template);
            }
        }
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
        var languages = new StudioLanguageServices(Snapshot.TempFolder("languages"));
        Snapshot.EnsureExtensionsLoaded(languages, options);
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

    /// <summary><c>settings</c>: the Settings and Environment Setup page.</summary>
    public static void Settings(Options options)
    {
        var languages = new StudioLanguageServices(Snapshot.TempFolder("languages"), host: options.Flag("nothing-installed") ? new NothingInstalledHost() : null);
        Snapshot.EnsureExtensionsLoaded(languages, options);
        var vm = new CSharpSettingsViewModel(languages);
        if (options.Value("category") is { } cat) vm.SelectCategory(cat);
        if (options.Value("theme-preset") is { } themePreset)
        {
            vm.ApplyThemePresetCommand.Execute(themePreset);
        }
        else if (options.Flag("random-harmony"))
        {
            vm.RandomizeHarmonicWheelCommand.Execute(null);
        }
        else if (options.Value("hue") is { } hueStr && float.TryParse(hueStr, out var hueVal))
        {
            vm.SelectedHueDegrees = hueVal;
            vm.ApplyHarmonicConfigurationCommand.Execute(null);
        }
        if (options.Flag("import-package") || options.Value("package-url") is not null)
        {
            vm.IsImportPackageFormVisible = true;
            if (options.Value("package-url") is { } url)
            {
                vm.PackageGitUrl = url;
            }
        }
        if (options.Value("language") is { } lang)
        {
            var target = vm.Languages.FirstOrDefault(l => l.Language.Id.Equals(lang, StringComparison.OrdinalIgnoreCase) || l.DisplayName.Equals(lang, StringComparison.OrdinalIgnoreCase));
            if (target != null)
            {
                vm.SelectLanguageItem(target);
                if (target.IsToolchainLanguage) Snapshot.Wait(vm.RefreshLanguageToolchainAsync(target));
            }
        }
        if (options.Flag("test-hello-world") && vm.SelectedLanguage is { } selected)
        {
            Snapshot.Wait(selected.TestHelloWorldAsync());
        }
        var window = Snapshot.Show(new CSharpSettingsView { DataContext = vm }, options.Int("width", 1400), options.Int("height", 900));
        string defaultName = options.Value("name") switch
        {
            { } custom => custom,
            _ when options.Flag("nothing-installed") && options.Value("language") is { } nl => $"settings_toolchain_{nl.ToLowerInvariant().Replace(' ', '_').Replace('#', 's')}_missing",
            _ when options.Flag("nothing-installed") => "settings_toolchain_nothing_installed",
            _ when options.Value("language") is { } l => $"settings_toolchain_{l.ToLowerInvariant().Replace(' ', '_').Replace('#', 's')}",
            _ when options.Value("category") is { } c => $"settings_category_{c.ToLowerInvariant().Replace(' ', '_')}",
            _ => "settings_toolchain_csharp"
        };

        Snapshot.Save(window, options, defaultName);
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
