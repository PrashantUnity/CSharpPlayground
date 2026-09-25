using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>
/// The Test Cases panel: adding a case writes its Check line into the script, Generate brings in a Blind 75 problem's
/// verified cases, every run shows each case's result, and deleting a case the panel added takes its line back out.
/// </summary>
public class TestCasesPanelTests : IDisposable
{
    private readonly string _baseDir = Path.Combine(Path.GetTempPath(), "FryPDF_TestCasesPanel_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_baseDir)) Directory.Delete(_baseDir, recursive: true);
        }
        catch { }
    }

    // Throwaway progress: a Blind 75 script whose cases all pass is marked solved, never in your real progress.
    private CSharpCodeStudioViewModel Studio(ScriptDocumentItem script) =>
        new(script, new LocalScriptStorageService(Path.Combine(_baseDir, "scripts")), new RoslynCompilerService(), new ScriptExecutionEngine(),
            backToHubAction: () => { }, blindProgress: new LocalBlindProgressService(Path.Combine(_baseDir, "progress")));

    // Starts from the statement's examples: the catalog's problems are shared, and the Blind 75 page's "Generate test
    // data" can have replaced a problem's list, so the script's own list isn't relied on.
    private CSharpCodeStudioViewModel Blind75(int number)
    {
        var problem = Blind75CatalogService.GetProblemByNumber(number)!;
        var script = Blind75CatalogService.ConvertToScript(problem);
        script.TestCases = problem.Tests.Select(t => t.ToTestCase()).ToList();
        return Studio(script);
    }

    private static void Add(CSharpCodeStudioViewModel studio, string name, string call, string expected)
    {
        studio.AddTestCaseCommand.Execute(null);
        studio.NewTestCaseName = name;
        studio.NewTestCaseCall = call;
        studio.NewTestCaseExpected = expected;
        studio.ConfirmAddTestCaseCommand.Execute(null);
    }

    [Fact]
    public void AddingACase_StartsFromTheProblemsOwnCall()
    {
        var studio = Blind75(11);

        studio.AddTestCaseCommand.Execute(null);

        Assert.True(studio.IsAddingTestCase);
        Assert.Equal(Blind75CatalogService.GetProblemByNumber(11)!.Tests[0].Call, studio.NewTestCaseCall);
        Assert.StartsWith("Case ", studio.NewTestCaseName);
    }

    [Fact]
    public async Task AnAddedCase_IsACheckLineInTheScript_AndRunningShowsItsResult()
    {
        var studio = Blind75(11);
        Add(studio, "Two walls", "sol.MaxArea(new[] { 1, 1 })", "1");
        Add(studio, "Wrong on purpose", "sol.MaxArea(new[] { 1, 1 })", "2");

        Assert.False(studio.IsAddingTestCase);
        Assert.Contains("Check(\"Two walls\", sol.MaxArea(new[] { 1, 1 }), \"1\");", studio.Code);
        Assert.True(studio.Code.IndexOf("Check(\"Two walls\"", StringComparison.Ordinal) > studio.Code.IndexOf("var sol = new Solution();", StringComparison.Ordinal));

        await studio.RunAllTestCasesCommand.ExecuteAsync(null);

        var right = studio.TestCases.Single(t => t.Name == "Two walls");
        var wrong = studio.TestCases.Single(t => t.Name == "Wrong on purpose");
        Assert.True(right.IsPassed);
        Assert.Equal("1", right.ActualOutput);
        Assert.True(wrong.IsFailed);
        Assert.Equal("got 1 · expected 2", wrong.ActualOutput);
        Assert.StartsWith($"{studio.TestCases.Count - 1} of {studio.TestCases.Count} passed", studio.TestCasesSummary);
    }

    [Fact]
    public void TheFormSaysWhatIsMissing()
    {
        var studio = Blind75(11);
        int before = studio.TestCases.Count;

        Add(studio, "Example 1", "sol.MaxArea(new[] { 1, 1 })", "1");
        Assert.Contains("already a case", studio.TestCaseFormError);

        studio.NewTestCaseName = "Fresh";
        studio.NewTestCaseCall = "  ";
        studio.ConfirmAddTestCaseCommand.Execute(null);
        Assert.Contains("C# expression", studio.TestCaseFormError);

        studio.NewTestCaseCall = "sol.MaxArea(new[] { 1, 1 })";
        studio.NewTestCaseExpected = "";
        studio.ConfirmAddTestCaseCommand.Execute(null);
        Assert.Contains("answer you expect", studio.TestCaseFormError);

        Assert.True(studio.IsAddingTestCase);
        Assert.Equal(before, studio.TestCases.Count);
    }

    [Fact]
    public async Task Generate_BringsInTheProblemsEdgeCases_AndTheyAllPass()
    {
        var studio = Blind75(1);
        var problem = Blind75CatalogService.GetProblemByNumber(1)!;
        Assert.True(studio.CanGenerateTestCases);
        string codeBefore = studio.Code;

        studio.GenerateTestCasesCommand.Execute(null);

        var expected = problem.Tests.Concat(problem.ExtraTests).Select(t => t.Name).ToList();
        Assert.Equal(expected, studio.TestCases.Select(t => t.Name).ToList());
        Assert.Equal(codeBefore, studio.Code); // the script already checks all of them
        Assert.Contains("Added", studio.TestCasesStatus);

        await studio.RunAllTestCasesCommand.ExecuteAsync(null);
        Assert.All(studio.TestCases, t => Assert.True(t.IsPassed, $"{t.Name}: {t.ActualOutput}"));

        studio.GenerateTestCasesCommand.Execute(null);
        Assert.Equal(expected.Count, studio.TestCases.Count);
        Assert.Contains("already here", studio.TestCasesStatus);
    }

    [Fact]
    public void Generate_WritesCheckLinesBackForCasesTheScriptNoLongerHas()
    {
        var studio = Blind75(1);
        studio.Code = TestCaseCode.RemoveCheck(studio.Code, "Zeros");

        studio.GenerateTestCasesCommand.Execute(null);

        Assert.True(TestCaseCode.HasCheck(studio.Code, "Zeros"));
        Assert.True(studio.TestCases.Single(t => t.Name == "Zeros").IsFromPanel);
    }

    [Fact]
    public void DeletingACaseThePanelAdded_TakesItsCheckLineOut_ButScriptCasesKeepTheirs()
    {
        var studio = Blind75(11);
        Add(studio, "Mine", "sol.MaxArea(new[] { 2, 2 })", "2");

        studio.DeleteTestCaseCommand.Execute(studio.TestCases.Single(t => t.Name == "Mine"));
        Assert.False(TestCaseCode.HasCheck(studio.Code, "Mine"));
        Assert.DoesNotContain(studio.TestCases, t => t.Name == "Mine");

        studio.DeleteTestCaseCommand.Execute(studio.TestCases.Single(t => t.Name == "Example 1"));
        Assert.True(TestCaseCode.HasCheck(studio.Code, "Example 1"));
    }

    [Fact]
    public async Task AnyScript_CanCheckAValue_AndF5ShowsTheResult()
    {
        var studio = Studio(new ScriptDocumentItem { Title = "Scratch", Code = "int Square(int n) => n * n;" });
        Assert.False(studio.CanGenerateTestCases);
        Assert.Empty(studio.TestCases); // no placeholder case that could only fail

        Add(studio, "Square of 7", "Square(7)", "49");
        Assert.Contains("// ── Test cases", studio.Code);

        await studio.RunCodeCommand.ExecuteAsync(null);

        var testCase = studio.TestCases.Single();
        Assert.True(testCase.IsPassed);
        Assert.Equal("49", testCase.ActualOutput);
    }

    [Fact]
    public async Task ACaseTheRunNeverReached_ShowsNotRun_NotAPassFromSomeOtherLine()
    {
        var studio = Studio(new ScriptDocumentItem
        {
            Title = "Scratch",
            Code = "int Square(int n) => n * n;\nint Boom() => throw new InvalidOperationException(\"boom\");"
        });
        Add(studio, "Reached", "Square(1)", "1");
        Add(studio, "Throws", "Boom()", "1");
        Add(studio, "Never reached", "Square(1)", "1");

        await studio.RunAllTestCasesCommand.ExecuteAsync(null);

        // The output does contain "1" ("✅ Reached → 1"), but only a case's own line decides it.
        Assert.True(studio.TestCases.Single(t => t.Name == "Reached").IsPassed);
        Assert.All(studio.TestCases.Where(t => t.Name != "Reached"), t => Assert.True(t.IsNotRun, t.Name));
        Assert.Equal("1 of 3 passed · 2 not run", studio.TestCasesSummary);
        Assert.Equal(3, studio.SelectedBottomTabIndex); // Run All stays on the Test Cases tab
    }

    [Fact]
    public async Task WhenNoCaseRuns_ThePanelSaysWhy()
    {
        var studio = Studio(new ScriptDocumentItem { Title = "Scratch", Code = "int Square(int n) => n * n" }); // no semicolon
        Add(studio, "Square of 1", "Square(1)", "1");

        await studio.RunAllTestCasesCommand.ExecuteAsync(null);

        Assert.True(studio.TestCases.Single().IsNotRun);
        Assert.Contains("No case ran", studio.TestCasesStatus);
        Assert.Contains("Problems", studio.TestCasesStatus);
    }

    [Fact]
    public async Task AnOlderCaseWithoutACheckLine_PassesWhenTheRunPrintsItsExpectedText()
    {
        var studio = Studio(new ScriptDocumentItem
        {
            Title = "Scratch",
            Code = "Console.WriteLine(\"Hello, world\");",
            TestCases = new() { new TestCaseItem { Name = "Greets", ExpectedOutput = "Hello" }, new TestCaseItem { Name = "Counts", ExpectedOutput = "42" } }
        });

        await studio.RunCodeCommand.ExecuteAsync(null);

        Assert.True(studio.TestCases[0].IsPassed);
        Assert.True(studio.TestCases[1].IsFailed);
        Assert.Equal("not found in the output", studio.TestCases[1].ActualOutput);
    }

    [Fact]
    public async Task ARunsResults_LandOnItsOwnCases_EvenAfterSwitchingToAnotherTab()
    {
        // The script waits for this file, so the tab switch happens while it runs.
        Directory.CreateDirectory(_baseDir);
        string go = Path.Combine(_baseDir, "go");
        var waiting = new TestCaseItem { Name = "Answer", ExpectedOutput = "42" };
        var studio = Studio(new ScriptDocumentItem
        {
            Title = "Waits",
            Code = $"var until = DateTime.Now.AddSeconds(20);\nwhile (!System.IO.File.Exists(@\"{go}\") && DateTime.Now < until) System.Threading.Thread.Sleep(10);\nCheck(\"Answer\", 6 * 7, \"42\");",
            TestCases = new() { waiting }
        });
        var other = new TestCaseItem { Name = "Answer", ExpectedOutput = "42" };

        var run = studio.RunAllTestCasesCommand.ExecuteAsync(null);
        Assert.True(waiting.IsRunning);
        await studio.UpdateActiveScriptAsync(new ScriptDocumentItem { Title = "Another tab", Code = "Console.WriteLine(1);", TestCases = new() { other } });
        File.WriteAllText(go, string.Empty);
        await run;

        Assert.True(waiting.IsPassed);
        Assert.Equal("42", waiting.ActualOutput);
        Assert.False(waiting.IsRunning);
        Assert.True(other.IsNotRun); // the tab now showing has a case with the same name: it wasn't run
        Assert.Equal("Another tab", studio.Script.Title);
    }

    // ── The Check lines themselves ────────────────────────────────────────────

    [Fact]
    public void ACheckLine_GoesAfterTheLastOne_IndentedLikeIt()
    {
        const string code = "var sol = new Solution();\n    Check(\"A\", 1, \"1\");\n    Check(\"B\", 2, \"2\");\nDisplay.Visualizer(t);";

        string updated = TestCaseCode.AddCheck(code, TestCaseCode.CheckLine("C", "3", "3"));

        Assert.Equal("var sol = new Solution();\n    Check(\"A\", 1, \"1\");\n    Check(\"B\", 2, \"2\");\n    Check(\"C\", 3, \"3\");\nDisplay.Visualizer(t);", updated);
    }

    [Fact]
    public void CheckLines_KeepWindowsLineEndings_AndNamesWithQuotesStillMatch()
    {
        string code = "Check(\"A\", 1, \"1\");\r\nConsole.WriteLine();";
        string name = "Say \"hi\"";

        string added = TestCaseCode.AddCheck(code, TestCaseCode.CheckLine(name, "\"hi\"", "\"hi\"", anyOrder: true));
        Assert.Contains("\r\nCheck(\"Say \\\"hi\\\"\", \"hi\", \"\\\"hi\\\"\", anyOrder: true);\r\n", added);
        Assert.True(TestCaseCode.HasCheck(added, name));

        Assert.Equal(code, TestCaseCode.RemoveCheck(added, name));
    }

    [Theory]
    [InlineData("11. Container With Most Water", 11)]
    [InlineData("1. Two Sum", 1)]
    [InlineData("1. My own sorting script", null)]
    [InlineData("Two Sum", null)]
    public void OnlyScriptsOpenedFromBlind75_CountAsItsProblems(string title, int? number)
    {
        Assert.Equal(number, Blind75CatalogService.FindForScript(new ScriptDocumentItem { Title = title })?.Number);
    }
}
