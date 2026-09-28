using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>
/// A notebook of C# and another language, as in Polyglot Notebooks: each cell runs in its language's kernel, with the
/// directives at its top (#!language, #!share, package lines) run first. The other language is <see cref="FakeLanguage"/>,
/// so nothing here needs a real toolchain and nothing in the notebook code names a language.
/// </summary>
public class NotebookPolyglotTabTests : IDisposable
{
    private readonly string _baseDir = Path.Combine(Path.GetTempPath(), "FryPDF_Polyglot_" + Guid.NewGuid().ToString("N"));

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

    // A notebook tab over throwaway folders, with C#, Python and FakeLang registered.
    private (NotebookTabViewModel Tab, FakeLanguage Fake) Tab(params NotebookCellItem[] cells)
    {
        FakeLanguage? fake = null;
        var services = new StudioLanguageServices(Path.Combine(_baseDir, "services"), configure: (_, registry) => fake = FakeLanguage.RegisterIn(registry));
        var notebook = new NotebookDocumentItem { Title = "Mixed" };
        notebook.Cells.AddRange(cells);
        var tab = new NotebookTabViewModel(notebook, languages: services, workspaceRoot: () => _baseDir);
        return (tab, fake!);
    }

    private static NotebookCellItem CSharp(string source) => new() { Type = CellType.Code, Source = source };

    private static NotebookCellItem Fake(string source) => new() { Type = CellType.Code, Source = source, Language = FakeLanguage.LanguageId };

    private static FakeKernel OnlyKernel(FakeLanguage fake) => Assert.Single(fake.Kernels.Created);

    [Fact]
    public async Task EachCell_RunsInItsLanguagesKernel()
    {
        var (tab, fake) = Tab(CSharp("var x = 40 + 2;\nx"), Fake("print hello"));

        await tab.RunAllCellsAsync();

        Assert.Contains("42", tab.Cells[0].OutputText);
        Assert.Equal("hello\n", tab.Cells[1].OutputText);
        Assert.Equal(new[] { "print hello" }, OnlyKernel(fake).Executed);
        Assert.Equal(".NET (C#) · FakeLang 1.2.3", tab.KernelName);
        Assert.Equal(_baseDir, OnlyKernel(fake).Context.WorkingDirectory());
    }

    [Fact]
    public async Task AKernel_StartsOnlyWhenACellOfItsLanguageRuns()
    {
        var (tab, fake) = Tab(CSharp("1 + 1"), Fake("print later"));

        await tab.RunSingleCellAsync(tab.Cells[0]);

        Assert.Empty(fake.Kernels.Created);
        Assert.Equal(".NET (C#) · FakeLang", tab.KernelName); // the language's name until its kernel says more
    }

    [Fact]
    public async Task AFirstLineDirective_PicksTheLanguage_AndIsBlankedFromTheCode()
    {
        var (tab, fake) = Tab(CSharp("#!fk\nprint hi"));
        var cell = tab.Cells[0];

        await tab.RunSingleCellAsync(cell);

        Assert.Equal(FakeLanguage.LanguageId, cell.EffectiveLanguage);
        Assert.Equal("FK", cell.LanguageTag);
        Assert.Equal(new[] { "\nprint hi" }, OnlyKernel(fake).Executed); // line numbers still match the editor
        Assert.Equal("hi\n", cell.OutputText);
    }

    [Fact]
    public async Task PackageLines_RunBeforeTheCode_AndShowWhatTheyDid()
    {
        var (tab, fake) = Tab(Fake("%fakepkg install left-pad\nprint ok"));
        var cell = tab.Cells[0];

        await tab.RunSingleCellAsync(cell);

        Assert.Equal(new[] { "install left-pad" }, fake.FakePackages.Ran);
        Assert.Contains("fakepkg install left-pad: done", cell.OutputText);
        Assert.EndsWith("ok\n", cell.OutputText);
        Assert.False(cell.HasError);
    }

    [Fact]
    public async Task AnUnknownDirective_StopsTheCell_AndSaysWhy()
    {
        var (tab, fake) = Tab(Fake("#!wat\nprint 1"));
        var cell = tab.Cells[0];

        await tab.RunSingleCellAsync(cell);

        Assert.True(cell.HasError);
        Assert.Contains("\"#!wat\" isn't a directive", cell.OutputText);
        Assert.Empty(fake.Kernels.Created.SelectMany(k => k.Executed));
    }

    [Fact]
    public async Task ASharedValue_ComesFromTheKernelThatHasIt_AndProblemsAreExplained()
    {
        var (tab, _) = Tab(
            CSharp("#!share --from fk nums\nnums"),
            Fake("nums = [1,2,3]"),
            CSharp("#!share --from cobol x\nx"),
            Fake("#!share --from fk nums"));

        await tab.RunSingleCellAsync(tab.Cells[0]);
        var beforeFakeRan = tab.Cells[0].OutputText;
        await tab.RunSingleCellAsync(tab.Cells[1]);
        await tab.RunSingleCellAsync(tab.Cells[2]);
        await tab.RunSingleCellAsync(tab.Cells[3]);

        Assert.Contains("No FakeLang cell has run yet", beforeFakeRan);
        Assert.Contains("no notebook language called \"cobol\"", tab.Cells[2].OutputText);
        Assert.Contains("already a FakeLang value", tab.Cells[3].OutputText);
        Assert.All(new[] { tab.Cells[0], tab.Cells[2], tab.Cells[3] }, c => Assert.True(c.HasError));
    }

    [Fact]
    public async Task Values_GoBetweenCSharpAndAnotherLanguage_BothWays()
    {
        var (tab, _) = Tab(
            CSharp("var nums = new[] { 3, 1, 4 };"),
            Fake("#!share --from csharp nums\nshow nums"),
            Fake("total = 8"),
            CSharp("#!share --from fk total --as sum\nsum * 2"));

        await tab.RunAllCellsAsync();

        Assert.All(tab.Cells, c => Assert.False(c.HasError, c.OutputText));
        Assert.Equal("[3,1,4]\n", tab.Cells[1].OutputText);
        Assert.Contains("16", tab.Cells[3].OutputText);
    }

    [Fact]
    public async Task AValueCSharpCantShare_FailsTheCellThatAskedForIt()
    {
        var (tab, _) = Tab(CSharp("Func<int, int> twice = n => n * 2;"), Fake("#!share --from csharp twice\nprint never"));

        await tab.RunAllCellsAsync();

        Assert.True(tab.Cells[1].HasError);
        Assert.Contains("'twice' is a function", tab.Cells[1].OutputText);
        Assert.DoesNotContain("never", tab.Cells[1].OutputText);
    }

    [Fact]
    public async Task AnUndefinedName_OffersToRunTheCellsAbove()
    {
        var (tab, _) = Tab(Fake("show total"));
        var cell = tab.Cells[0];

        await tab.RunSingleCellAsync(cell);

        Assert.True(cell.HasMissingVariableError);
        Assert.Equal("total", cell.MissingVariableName);
    }

    [Fact]
    public async Task AMissingPackage_IsOffered_AndInstallingItRunsTheCellAgain()
    {
        var (tab, fake) = Tab(Fake("import widgets\nprint ready"));
        var cell = tab.Cells[0];

        await tab.RunSingleCellAsync(cell);
        Assert.True(cell.HasMissingDependency);
        Assert.Equal("fake-widgets", cell.MissingDependency);
        Assert.Equal("Install fake-widgets", cell.InstallMissingDependencyLabel);

        await cell.InstallMissingDependencyCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "install fake-widgets" }, fake.FakePackages.Ran);
        Assert.False(cell.HasError);
        Assert.False(cell.HasMissingDependency);
        Assert.Equal("ready\n", cell.OutputText);
    }

    [Fact]
    public async Task Input_IsAskedForInTheCell()
    {
        var (tab, _) = Tab(Fake("ask Name?"));
        var cell = tab.Cells[0];

        var run = tab.RunSingleCellAsync(cell);
        Assert.True(cell.IsAwaitingInput);
        Assert.Equal("Name?", cell.InputPrompt);
        cell.InputText = "Ada";
        cell.SubmitInput();
        await run.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.False(cell.IsAwaitingInput);
        Assert.Equal("answer: Ada\n", cell.OutputText);
    }

    [Fact]
    public async Task StoppingACellThatWaitsForInput_EndsTheInput()
    {
        var (tab, _) = Tab(Fake("ask Name?"));
        var cell = tab.Cells[0];

        var run = tab.RunSingleCellAsync(cell);
        tab.InterruptExecution();
        await run.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.False(cell.IsAwaitingInput);
        Assert.Contains("answer: <eof>", cell.OutputText);
    }

    [Fact]
    public async Task Variables_ComeFromEveryKernel_TaggedWithTheirLanguage()
    {
        var (tab, _) = Tab(CSharp("var answer = 42;"), Fake("total = 7"));

        await tab.RunAllCellsAsync();

        Assert.Contains(tab.Variables, v => v is { Name: "answer", Kernel: "C#" });
        Assert.Contains(tab.Variables, v => v is { Name: "total", Kernel: "FakeLang", ValueDisplay: "7" });
    }

    [Fact]
    public async Task RunAllAndRestart_ResetEveryKernel()
    {
        var (tab, fake) = Tab(Fake("total = 7"));
        await tab.RunSingleCellAsync(tab.Cells[0]);
        var kernel = OnlyKernel(fake);

        await tab.RunAllCellsAsync();
        var resetsAfterRunAll = kernel.ResetCount;
        tab.RestartKernel();

        Assert.Equal(1, resetsAfterRunAll); // Run All starts from a clean slate in every kernel...
        Assert.Equal(2, kernel.ResetCount); // ...and so does Restart
        Assert.Same(kernel, OnlyKernel(fake));
    }

    [Fact]
    public async Task ClosingTheTab_EndsItsOtherKernels()
    {
        FakeLanguage? fake = null;
        var services = new StudioLanguageServices(Path.Combine(_baseDir, "services"), configure: (_, registry) => fake = FakeLanguage.RegisterIn(registry));
        var notebook = new NotebookDocumentItem { Title = "Mixed" };
        notebook.Cells.Add(Fake("print hi"));
        var studio = new CSharpNotebookStudioViewModel(
            notebook, new LocalScriptStorageService(Path.Combine(_baseDir, "storage"), services.Registry), new RoslynCompilerService(), new ScriptExecutionEngine(),
            backToHubAction: () => { }, languages: services);
        var tab = studio.ActiveTab!;
        await tab.RunSingleCellAsync(tab.Cells[0]);

        studio.CloseTab(tab);

        Assert.True(OnlyKernel(fake!).Disposed);
    }

    [Fact]
    public void ACell_OffersTheNotebookLanguages_AndSwitchingItChangesItsKernel()
    {
        var (tab, _) = Tab(CSharp("1"));
        var cell = tab.Cells[0];

        Assert.True(cell.HasLanguageChoices);
        Assert.Contains(cell.LanguageChoices, c => c is { LanguageId: LanguageIds.CSharp, IsSelected: true });
        Assert.Contains(cell.LanguageChoices, c => c is { LanguageId: FakeLanguage.LanguageId, Label: "FakeLang", IsSelected: false });

        cell.LanguageChoices.Single(c => c.LanguageId == FakeLanguage.LanguageId).Command.Execute(null);
        Assert.Equal(FakeLanguage.LanguageId, cell.Language);
        Assert.Equal("FK", cell.LanguageTag);
        Assert.True(tab.IsModified);

        cell.SetLanguage(LanguageIds.CSharp);
        Assert.Null(cell.Language); // the notebook's default needs no language of its own
    }

    [Fact]
    public void NewCells_AreWrittenInTheLanguageOfTheCellTheyreAddedNextTo()
    {
        var (tab, _) = Tab(CSharp("1"), Fake("print 1"));

        tab.AddCellBelow(tab.Cells[1], CellType.Code);
        var fakeCell = tab.Cells[2];
        tab.AddCellBelow(tab.Cells[0], CellType.Code);
        var csharpCell = tab.Cells[1];

        Assert.Equal(FakeLanguage.LanguageId, fakeCell.Language);
        Assert.Equal("-- FakeLang Code Block\n", fakeCell.Source);
        Assert.Null(csharpCell.Language);
        Assert.Equal("// C# Code Block\n", csharpCell.Source);
    }

    [Fact]
    public void ChangingTheNotebooksLanguage_KeepsEachCellsLanguage()
    {
        var (tab, _) = Tab(CSharp("1"), Fake("print 1"));

        tab.SetDefaultLanguage(FakeLanguage.LanguageId);

        Assert.Equal(FakeLanguage.LanguageId, tab.Notebook.Kernel);
        Assert.Equal(LanguageIds.CSharp, tab.Cells[0].Language);
        Assert.Equal(LanguageIds.CSharp, tab.Cells[0].EffectiveLanguage);
        Assert.Null(tab.Cells[1].Language);
        Assert.Equal(FakeLanguage.LanguageId, tab.Cells[1].EffectiveLanguage);
        Assert.Contains(tab.DefaultLanguageChoices, c => c is { LanguageId: FakeLanguage.LanguageId, IsSelected: true });
        Assert.StartsWith("FakeLang", tab.KernelName);
    }

    [Fact]
    public void Completion_SeesOnlyTheCellsAboveInTheSameLanguage_WithoutTheirDirectives()
    {
        var (tab, _) = Tab(CSharp("var a = 1;"), Fake("b = 2"), CSharp("#!share --from fk b\nvar c = b;"), CSharp("c"));

        var context = tab.Cells[3].GetPrecedingContext();

        Assert.Contains("var a = 1;", context);
        Assert.Contains("var c = b;", context);
        Assert.DoesNotContain("b = 2", context);
        Assert.DoesNotContain("#!share", context);
    }

    [Fact]
    public async Task ACellInALanguageTheStudioDoesntKnow_SaysSo()
    {
        var (tab, _) = Tab(new NotebookCellItem { Type = CellType.Code, Source = "DISPLAY 'HI'.", Language = "cobol" });
        var cell = tab.Cells[0];

        await tab.RunSingleCellAsync(cell);

        Assert.True(cell.HasError);
        Assert.Contains("written in \"cobol\"", cell.OutputText);
    }

    [Fact]
    public void ACellsLanguage_IsSavedOnlyWhenItDiffersFromTheNotebooks()
    {
        var csharp = JsonSerializer.Serialize(CSharp("1"));
        var fake = JsonSerializer.Serialize(Fake("print 1"));

        Assert.DoesNotContain("\"Language\"", csharp); // C#-only notebooks are saved as they always were
        Assert.Contains("\"Language\":\"fakelang\"", fake);
    }
}
