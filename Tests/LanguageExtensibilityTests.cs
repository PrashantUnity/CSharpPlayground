using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>
/// A language the studio has never heard of works once it's registered: nothing else in the studio names languages.
/// Uses <see cref="FakeLanguage"/>, which exists only in these tests.
/// </summary>
public class LanguageExtensibilityTests : IDisposable
{
    private readonly string _baseDir = Path.Combine(Path.GetTempPath(), "FryPDF_Extensibility_" + Guid.NewGuid().ToString("N"));

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

    // The studio with the built-in languages plus FakeLang, over throwaway folders.
    private (StudioLanguageServices Services, LocalScriptStorageService Storage, FakeLanguage Fake) Workspace()
    {
        FakeLanguage? fake = null;
        var services = new StudioLanguageServices(Path.Combine(_baseDir, "services"), configure: (_, registry) => fake = FakeLanguage.RegisterIn(registry));
        return (services, new LocalScriptStorageService(Path.Combine(_baseDir, "storage"), services.Registry), fake!);
    }

    private CSharpCodeStudioViewModel Studio(StudioLanguageServices services, LocalScriptStorageService storage, ScriptDocumentItem script) =>
        new(script, storage, new RoslynCompilerService(), new ScriptExecutionEngine(),
            backToHubAction: () => { },
            blindProgress: new LocalBlindProgressService(Path.Combine(_baseDir, "progress")),
            languages: services);

    [Fact]
    public async Task Storage_ListsAndOpensARegisteredLanguagesFiles()
    {
        var (_, storage, _) = Workspace();
        File.WriteAllText(Path.Combine(storage.LibraryRootPath, "hello.fake"), "print hi\n");

        var summary = Assert.Single(await storage.LoadWorkspaceSummariesAsync(), s => s.IsSourceFile);
        var document = await storage.LoadScriptAsync(summary.Id);

        Assert.Equal(FakeLanguage.LanguageId, summary.LanguageId);
        Assert.Equal("FakeLang", summary.RuntimeBadgeText);
        Assert.Equal("print hi\n", document!.Code);
        Assert.Equal(FakeLanguage.LanguageId, document.LanguageId);
    }

    [Fact]
    public async Task CodeStudio_OffersANewFileOfTheLanguage_WithItsTemplate()
    {
        var (services, storage, _) = Workspace();
        var studio = Studio(services, storage, await storage.CreateNewScriptAsync("Notes"));

        var option = Assert.Single(studio.NewFileOptions, o => o.LanguageId == FakeLanguage.LanguageId);
        await option.Command.ExecuteAsync(null);

        Assert.Equal("New FakeLang File", option.Label);
        Assert.EndsWith(".fake", studio.Script.SourceFilePath);
        Assert.Equal("print hello from fakelang\n", studio.Code);
        Assert.Equal(FakeLanguage.LanguageId, studio.ActiveLanguage.Id);
    }

    private static (NotebookKernelRouter Router, FakeLanguage Fake, NotebookExecutionKernel CSharp) Router()
    {
        var registry = new LanguageRegistry();
        registry.Register(new CSharpLanguage());
        var fake = FakeLanguage.RegisterIn(registry);
        var csharp = new NotebookExecutionKernel();
        return (new NotebookKernelRouter(registry, new KernelCreationContext(() => "/work"), csharp), fake, csharp);
    }

    [Fact]
    public void Router_StartsALanguagesKernel_TheFirstTimeOneOfItsCellsRuns()
    {
        var (router, fake, csharp) = Router();

        Assert.Empty(fake.Kernels.Created);
        var kernel = router.GetOrCreate("fk");

        Assert.IsType<FakeKernel>(kernel);
        Assert.Same(kernel, router.GetOrCreate(FakeLanguage.LanguageId));
        Assert.Single(fake.Kernels.Created);
        Assert.Equal("/work", ((FakeKernel)kernel!).Context.WorkingDirectory());
        Assert.Same(csharp, router.GetOrCreate(LanguageIds.CSharp));
    }

    [Fact]
    public void Router_HasNoKernel_ForALanguageWithoutNotebookCells()
    {
        var registry = new LanguageRegistry();
        registry.Register(new PlainTextLanguage());
        var router = new NotebookKernelRouter(registry, new KernelCreationContext(() => null));

        Assert.Null(router.GetOrCreate("plain"));
        Assert.Null(router.GetOrCreate("no-such-language"));
    }

    [Fact]
    public void ResetAll_ClearsEveryKernel()
    {
        var (router, _, _) = Router();
        var fake = (FakeKernel)router.GetOrCreate(FakeLanguage.LanguageId)!;

        router.ResetAll();

        Assert.Equal(1, fake.ResetCount);
    }

    [Fact]
    public void ShutdownOthers_EndsTheOtherKernels_AndTheyComeBackFresh()
    {
        var (router, fake, csharp) = Router();
        var first = (FakeKernel)router.GetOrCreate(FakeLanguage.LanguageId)!;

        router.ShutdownOthers();

        Assert.True(first.Disposed);
        Assert.Same(csharp, router.Find(LanguageIds.CSharp));
        Assert.Null(router.Find(FakeLanguage.LanguageId));
        Assert.NotSame(first, router.GetOrCreate(FakeLanguage.LanguageId));
        Assert.Equal(2, fake.Kernels.Created.Count);
    }

    [Fact]
    public async Task Variables_FromEveryKernel_AreTaggedWithTheirLanguage()
    {
        var (router, _, csharp) = Router();
        await csharp.ExecuteCellAsync("var total = 42;");
        var fake = router.GetOrCreate(FakeLanguage.LanguageId)!;
        await fake.ExecuteAsync(new KernelExecutionRequest { Code = "greeting = \"hi\"" }, CancellationToken.None);

        var variables = await router.GetVariablesAsync();

        Assert.Contains(variables, v => v is { Name: "total", Kernel: "C#" });
        Assert.Contains(variables, v => v is { Name: "greeting", Kernel: "FakeLang" });
    }

    [Fact]
    public async Task CSharpVariables_RedeclaredInALaterCell_AreListedOnce()
    {
        var kernel = new NotebookExecutionKernel();
        await kernel.ExecuteCellAsync("var x = 1;");
        await kernel.ExecuteCellAsync("var x = 2;");

        var x = Assert.Single(kernel.GetActiveVariables(), v => v.Name == "x");
        Assert.Equal("2", x.ValueDisplay);
    }

    private sealed class PlainTextLanguage : LanguageDefinition
    {
        private static readonly string[] Extensions = [".txt"];
        public override string Id => "plain";
        public override string DisplayName => "Plain text";
        public override IReadOnlyList<string> FileExtensions => Extensions;
    }
}
