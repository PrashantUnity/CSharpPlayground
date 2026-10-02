using PdfEditorApp.Plugins.CSharpEditor.Services.Execution;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Catalogs.Blind75;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio;
using Xunit;
using CSharpCodeStudioViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio.CSharpCodeStudioViewModel;
using ExplorerItemViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio.Explorer.ExplorerItemViewModel;

namespace CSharpEditorPlugin.Tests;

/// <summary>The Dependencies side panel for Rust (and the Go directive it used to miss).</summary>
public class RustPackagesPanelTests : IDisposable
{
    private readonly string _baseDir = Path.Combine(Path.GetTempPath(), "FryPDF_RustPanel_" + Guid.NewGuid().ToString("N"));
    private readonly StudioLanguageServices _languages;
    private readonly LocalScriptStorageService _storage;

    public RustPackagesPanelTests()
    {
        _languages = new StudioLanguageServices(Path.Combine(_baseDir, "services"));
        _storage = new LocalScriptStorageService(Path.Combine(_baseDir, "storage"), _languages.Registry);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_baseDir)) Directory.Delete(_baseDir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private async Task<CSharpCodeStudioViewModel> StudioWith(string fileName, string code)
    {
        File.WriteAllText(Path.Combine(_storage.LibraryRootPath, fileName), code);
        var studio = new CSharpCodeStudioViewModel(
            await _storage.CreateNewScriptAsync("Notes"), _storage, new RoslynCompilerService(), new ScriptExecutionEngine(),
            backToHubAction: () => { },
            blindProgress: new LocalBlindProgressService(Path.Combine(_baseDir, "progress")),
            languages: _languages);
        await studio.SwitchToScriptAsync(Flatten(studio.ExplorerRootItems).Single(i => i.Name == fileName));
        return studio;
    }

    private static IEnumerable<ExplorerItemViewModel> Flatten(IEnumerable<ExplorerItemViewModel> items) =>
        items.SelectMany(i => new[] { i }.Concat(Flatten(i.Children)));

    [Fact]
    public async Task ARustFile_ShowsCargosCrates()
    {
        var studio = await StudioWith("main.rs", "fn main() {}\n");
        studio.InitializeNuGetPackages();

        Assert.Equal("cargo", studio.ActivePackageManagerName);
        Assert.Equal("CARGO PACKAGES", studio.ActivePackageManagerTitle);
        Assert.Contains(studio.NuGetSearchResults, p => p.Id == "serde");
        Assert.Contains(studio.NuGetSearchResults, p => p.Id == "rand");
        Assert.DoesNotContain(studio.NuGetSearchResults, p => p.Id == "Newtonsoft.Json");
    }

    [Fact]
    public async Task SearchingCrates_FiltersTheCatalog()
    {
        var studio = await StudioWith("main.rs", "fn main() {}\n");
        studio.NuGetSearchQuery = "json";

        await studio.SearchNuGetPackagesCommand.ExecuteAsync(null);

        Assert.Equal(["serde_json"], studio.NuGetSearchResults.Select(p => p.Id));
        studio.NuGetSearchQuery = "no-such-crate-anywhere";
        await studio.SearchNuGetPackagesCommand.ExecuteAsync(null);
        Assert.Empty(studio.NuGetSearchResults);
        Assert.Equal((string?)"No crates found in catalog", (string?)studio.NuGetStatusMessage);
    }

    [Fact]
    public async Task AddingACrate_WritesItsCommentAtTheTop_PinnedToTheCatalogVersion()
    {
        var studio = await StudioWith("main.rs", "fn main() {}\n");

        studio.AddNuGetPackage(new NuGetPackageItem("rand", "0.9", "d", "a", 1));

        Assert.StartsWith("// #crate: rand = \"0.9\"" + Environment.NewLine + "fn main()", (string?)studio.Code);
        Assert.Equal(["// #crate: rand = \"0.9\""], studio.DocumentNuGetPackages);
    }

    [Fact]
    public async Task AddingSerde_EnablesItsDeriveFeature()
    {
        var studio = await StudioWith("main.rs", "fn main() {}\n");

        studio.AddNuGetPackage(new NuGetPackageItem("serde", "1", "d", "a", 1));

        Assert.StartsWith((string?)"// #crate: serde = { version = \"1\", features = [\"derive\"] }", (string?)studio.Code);
    }

    [Fact]
    public async Task AddingACrateTwice_SaysItIsAlreadyThere()
    {
        var studio = await StudioWith("main.rs", "fn main() {}\n");
        var rand = new NuGetPackageItem("rand", "0.9", "d", "a", 1);

        studio.AddNuGetPackage(rand);
        studio.AddNuGetPackage(rand);

        Assert.Equal(1, studio.Code.Split("// #crate: rand").Length - 1);
        Assert.Contains((string)"already referenced", (string?)studio.NuGetStatusMessage);
    }

    [Fact]
    public async Task TheCratesTheFileNames_AreListed_AndCanBeRemoved()
    {
        var studio = await StudioWith("main.rs", "// #crate: rand = \"0.9\"\n%cargo add itertools\n// #crate: serde = { version = \"1\", features = [\"derive\"] }\nfn main() {}\n");

        studio.RefreshDocumentNuGetPackages();

        Assert.Equal(
            ["// #crate: rand = \"0.9\"", "%cargo add itertools", "// #crate: serde = { version = \"1\", features = [\"derive\"] }"],
            studio.DocumentNuGetPackages);

        studio.RemoveNuGetPackage("// #crate: rand = \"0.9\"");

        Assert.DoesNotContain((string)"rand", (string?)studio.Code);
        Assert.Contains((string)"serde", (string?)studio.Code);
    }

    [Fact]
    public async Task AGoFile_ListsItsPackageComments_WhichThePanelUsedToMiss()
    {
        var studio = await StudioWith("main.go", "// #go: github.com/google/uuid\npackage main\nfunc main() {}\n");

        studio.RefreshDocumentNuGetPackages();

        Assert.Equal(["// #go: github.com/google/uuid"], studio.DocumentNuGetPackages);
    }
}
