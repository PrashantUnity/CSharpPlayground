using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>
/// The Code Studio runs a file of any language that brings a runner, using only what the language declares. FakeLang's
/// "processes" are pretend ones, so every path (output, errors, fixes, input, Stop) is checked without real programs.
/// </summary>
public class CodeStudioExternalLanguageTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private readonly string _baseDir = Path.Combine(Path.GetTempPath(), "FryPDF_ExternalLanguage_" + Guid.NewGuid().ToString("N"));
    private readonly FakeProcessLauncher _launcher = new();
    private readonly StudioLanguageServices _languages;
    private readonly LocalScriptStorageService _storage;
    private readonly FakeLanguage _fake;

    public CodeStudioExternalLanguageTests()
    {
        FakeLanguage? fake = null;
        _languages = new StudioLanguageServices(Path.Combine(_baseDir, "services"), processLauncher: _launcher,
            configure: (_, registry) => fake = FakeLanguage.RegisterIn(registry));
        _fake = fake!;
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
        var item = Flatten(studio.ExplorerRootItems).Single(i => i.Name == fileName);
        await studio.SwitchToScriptAsync(item);
        return studio;
    }

    private static IEnumerable<ExplorerItemViewModel> Flatten(IEnumerable<ExplorerItemViewModel> items) =>
        items.SelectMany(i => new[] { i }.Concat(Flatten(i.Children)));

    [Fact]
    public async Task Run_StartsTheLanguagesRunner_AndShowsItsOutput()
    {
        _launcher.Behavior = (_, p) =>
        {
            p.Write("hello from fakelang\n");
            p.Exit(0);
            return Task.CompletedTask;
        };
        var studio = await StudioWith("hello.fake", "print hello from fakelang\n");

        await studio.RunCodeCommand.ExecuteAsync(null);

        var run = Assert.Single(_launcher.Started);
        Assert.Equal("/fake/bin/fakec", run.FileName);
        Assert.Equal(["run", studio.Script.SourceFilePath!], run.Arguments);
        Assert.Equal(Path.GetDirectoryName(studio.Script.SourceFilePath), run.WorkingDirectory);
        Assert.Contains("▶ FakeLang 1.2.3 (Test) · hello.fake", studio.ConsoleOutput);
        Assert.Contains("hello from fakelang", studio.ConsoleOutput);
        Assert.Contains("exited with code 0", studio.ConsoleOutput);
        Assert.Equal("Completed", studio.CompilerStatusText);
        Assert.False(studio.IsExecuting);
        Assert.Equal("FakeLang 1.2.3 (Test)", studio.RuntimeLabel);
    }

    [Fact]
    public async Task Run_SavesTheEditorsTextFirst()
    {
        string? ranWith = null;
        _launcher.Behavior = (spec, p) =>
        {
            ranWith = File.ReadAllText(spec.Arguments[1]);
            p.Exit(0);
            return Task.CompletedTask;
        };
        var studio = await StudioWith("hello.fake", "print old\n");
        studio.Code = "print new\n";

        await studio.RunCodeCommand.ExecuteAsync(null);

        Assert.Equal("print new\n", ranWith);
    }

    [Fact]
    public async Task AFailedRun_PutsItsErrorsInProblems_WithAFixForAMissingDependency()
    {
        _launcher.Behavior = (_, p) =>
        {
            p.WriteError("Line 2: numpy is missing\nmissing module numpy\n");
            p.Exit(1);
            return Task.CompletedTask;
        };
        var studio = await StudioWith("hello.fake", "print a\nimport numpy\n");

        await studio.RunCodeCommand.ExecuteAsync(null);

        var problem = Assert.Single(studio.Diagnostics);
        Assert.Equal(2, problem.Line);
        Assert.Equal(2, studio.SelectedBottomTabIndex); // Problems
        Assert.Equal("Exited with code 1", studio.CompilerStatusText);
        Assert.Equal("Install fake-numpy", problem.QuickFixLabel);

        await problem.QuickFixCommand!.ExecuteAsync(null);

        Assert.Equal("install fake-numpy", Assert.Single(_fake.FakePackages.Ran));
        Assert.Contains("Installed fake-numpy", studio.CompilerStatusText);
    }

    [Fact]
    public async Task AMissingToolchain_SaysWhatToInstall_AndStartsNothing()
    {
        _fake.FakeToolchain.Installed = false;
        var studio = await StudioWith("hello.fake", "print hi\n");

        await studio.RunCodeCommand.ExecuteAsync(null);

        Assert.Empty(_launcher.Started);
        Assert.Contains("FakeLang isn't installed", studio.ConsoleOutput);
        Assert.Contains("fakepkg install fakelang", studio.ConsoleOutput);
        Assert.Equal("FakeLang not found", studio.CompilerStatusText);
        Assert.True(studio.IsToolchainMissing);
    }

    [Fact]
    public async Task WhatsTypedInTheTerminal_ReachesTheProgram()
    {
        _launcher.Behavior = async (_, p) =>
        {
            p.Write("Name? ");
            var name = await p.ReadLineAsync();
            p.Write($"Hi {name}\n");
            p.Exit(0);
        };
        var studio = await StudioWith("hello.fake", "ask Name?\n");

        var run = studio.RunCodeCommand.ExecuteAsync(null);
        await WaitUntil(() => studio.IsAcceptingProgramInput);
        studio.ProgramInputText = "Ada";
        await studio.SendProgramInputCommand.ExecuteAsync(null);
        await run.WaitAsync(Patience);

        Assert.Contains("Name? Ada\nHi Ada", studio.ConsoleOutput);
        Assert.Equal(string.Empty, studio.ProgramInputText);
        Assert.False(studio.IsAcceptingProgramInput);
    }

    [Fact]
    public async Task Stop_EndsTheProgram()
    {
        FakeProcess? running = null;
        _launcher.Behavior = async (_, p) =>
        {
            running = p;
            await Task.Delay(Timeout.Infinite, p.KilledToken);
        };
        var studio = await StudioWith("hello.fake", "loop\n");

        var run = studio.RunCodeCommand.ExecuteAsync(null);
        await WaitUntil(() => running != null);
        studio.StopCommand.Execute(null);
        await run.WaitAsync(Patience);

        Assert.True(running!.WasKilled);
        Assert.Equal("🛑 Cancelled", studio.CompilerStatusText);
        Assert.Contains("Stopped.", studio.ConsoleOutput);
    }

    [Fact]
    public async Task ClosingTheTab_EndsItsRun()
    {
        FakeProcess? running = null;
        _launcher.Behavior = async (_, p) =>
        {
            running = p;
            await Task.Delay(Timeout.Infinite, p.KilledToken);
        };
        var studio = await StudioWith("hello.fake", "loop\n");
        var tab = studio.OpenTabs.Single(t => t.IsActive);

        var run = studio.RunCodeCommand.ExecuteAsync(null);
        await WaitUntil(() => running != null);
        await studio.CloseTabAsync(tab);
        await run.WaitAsync(Patience);

        Assert.True(running!.WasKilled);
    }

    [Fact]
    public async Task F5_RunsALanguageThatHasNoDebugger()
    {
        var studio = await StudioWith("hello.fake", "print hi\n");

        await studio.DebugCodeCommand.ExecuteAsync(null);

        Assert.Single(_launcher.Started);
        Assert.False(studio.IsDebugging);
        Assert.Contains("no FakeLang debugger yet", studio.ConsoleOutput);
    }

    [Fact]
    public async Task CSharpOnlyFeatures_LeaveAnotherLanguagesCodeAlone()
    {
        var studio = await StudioWith("hello.fake", "x =    1\nprint x\n");

        Assert.False(studio.SupportsDebugging);
        Assert.False(studio.ShowDebugButton);
        Assert.False(studio.SupportsFormatting);
        Assert.False(studio.SupportsTestCases);
        Assert.False(studio.SupportsExecutionModes);
        Assert.True(studio.HasToolchain);

        studio.FormatCode();
        Assert.Equal("x =    1\nprint x\n", studio.Code);

        studio.AddTestCaseCommand.Execute(null);
        Assert.False(studio.IsAddingTestCase);
        studio.NewTestCaseName = "Case 1";
        studio.NewTestCaseCall = "Solve()";
        studio.NewTestCaseExpected = "1";
        studio.ConfirmAddTestCaseCommand.Execute(null);
        Assert.Equal("x =    1\nprint x\n", studio.Code);

        studio.InsertTemplate(CodeTemplateLibrary.GetTemplates()[0]);
        Assert.Equal("x =    1\nprint x\n", studio.Code);

        studio.ToggleBreakpoint(1);
        Assert.Empty(studio.Breakpoints);
    }

    [Fact]
    public async Task RoslynNeverChecksAnotherLanguagesCode()
    {
        var studio = await StudioWith("hello.fake", "print hi\n");

        studio.Code = "this is not C# at all {{{";
        await Task.Delay(700); // longer than the live check's 350 ms debounce

        Assert.Empty(studio.Diagnostics);
        Assert.Equal(0, studio.ErrorCount);
    }

    [Fact]
    public async Task SwitchingBackToCSharp_BringsBackItsFeatures()
    {
        var studio = await StudioWith("hello.fake", "print hi\n");

        await studio.SwitchToScriptAsync(Flatten(studio.ExplorerRootItems).Single(i => i.Name == "Notes.frycs"));

        Assert.Equal(LanguageIds.CSharp, studio.ActiveLanguage.Id);
        Assert.True(studio.SupportsDebugging);
        Assert.True(studio.ShowDebugButton);
        Assert.False(studio.HasToolchain);
        Assert.Equal("C# (.NET 10 Roslyn)", studio.RuntimeLabel);
    }

    [Fact]
    public async Task TheToolchainPicker_ListsWhatsFound_AndItsActions()
    {
        var studio = await StudioWith("hello.fake", "print hi\n");

        await studio.RefreshToolchainsCommand.ExecuteAsync(null);

        Assert.Equal(["Automatic", "FakeLang 1.2.3 (Test)"], studio.ToolchainChoices.Select(c => c.Label));
        Assert.True(studio.ToolchainChoices[0].IsSelected);
        Assert.Equal("Set up FakeLang", Assert.Single(studio.ToolchainActions).Label);

        studio.ToolchainChoices[1].Command.Execute(null);
        Assert.Equal("/fake/bin/fakec", _fake.FakeToolchain.SelectedPath);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Patience;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition never became true.");
            await Task.Delay(10);
        }
    }
}
