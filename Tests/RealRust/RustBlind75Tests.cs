using System.Collections.Concurrent;
using System.Text;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages.Rust;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.Rust;
using PdfEditorApp.Plugins.CSharpEditor.Services.Kernels;
using PdfEditorApp.Plugins.CSharpEditor.Services.Processes;
using PdfEditorApp.Plugins.CSharpEditor.Services.Toolchains;
using PdfEditorApp.Plugins.CSharpEditor.Tests.TestSupport;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests.RealRust;

/// <summary>The Blind 75 problems as Rust scripts, built and run with the real toolchain.</summary>
[Collection(RealRustCollection.Name)]
public class RustBlind75Tests : IClassFixture<RustStudioFixture>, IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan PerScript = TimeSpan.FromMinutes(3);
    private readonly RustStudioFixture _studio;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "FryPDF_RustBlind75_" + Guid.NewGuid().ToString("N"));

    public RustBlind75Tests(RustStudioFixture studio)
    {
        _studio = studio;
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private async Task<(ScriptRunResult Result, string Output)> RunScript(string fileName, string code)
    {
        var path = Path.Combine(_dir, fileName);
        await File.WriteAllTextAsync(path, code.Replace("\r\n", "\n"));
        var rust = TestRust.Require();
        var language = (RustLanguage)_studio.Services.Registry.Get(LanguageIds.Rust)!;
        var plan = await language.ScriptRunner.PlanAsync(new ScriptRunContext(path, _dir, rust));
        var output = new ConcurrentQueue<string>();
        var session = new ScriptRunExecutor(_studio.Services.Processes).Start(plan, path, language.RunDiagnostics, output.Enqueue);
        var result = await session.Completion.WaitAsync(PerScript);
        return (result, string.Concat(output));
    }

    // BLIND75_PROBLEMS=1,3,49 tries only those; unset, every problem.
    private static IEnumerable<BlindProblemItem> Problems()
    {
        var chosen = Environment.GetEnvironmentVariable("BLIND75_PROBLEMS");
        var numbers = string.IsNullOrWhiteSpace(chosen) ? null : chosen.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse).ToHashSet();
        return Blind75CatalogService.GetAllProblems().Where(p => numbers == null || numbers.Contains(p.Number));
    }

    private static string Describe(BlindProblemItem problem, ScriptRunResult result, string output) =>
        $"#{problem.Number} {problem.Title}: exit {result.ExitCode}, failed step {result.FailedBuildStep}\n{Tail(output)}";

    private static string Tail(string text) => text.Length <= 3000 ? text : "…" + text[^3000..];

    [RustFact]
    public async Task EveryProblemsScript_Builds_AndEveryCaseGetsAVerdictLine()
    {
        var problems = Problems().ToList();
        var problemsWithoutLines = new List<string>();
        var checkedCases = 0;

        using var patience = new CancellationTokenSource(Patience);
        foreach (var problem in problems)
        {
            var script = RustProblemLanguageAdapter.Instance.BuildScript(problem);
            var (result, output) = await RunScript($"p{problem.Number}.rs", script.Code).WaitAsync(patience.Token);

            // The starter is empty, so its cases fail (exit 1); a build error or a panic that ends the script (101) is the problem.
            Assert.True(result.FailedBuildStep == null, "The script doesn't build: " + Describe(problem, result, output));
            Assert.True(result.ExitCode is 0 or 1, "The script ended abnormally: " + Describe(problem, result, output));
            Assert.DoesNotContain("⚠️", output);

            foreach (var test in problem.Tests.Concat(problem.ExtraTests))
            {
                checkedCases++;
                if (Judge.Verdict(output, test.Name) == null) problemsWithoutLines.Add($"#{problem.Number} \"{test.Name}\"");
            }
        }

        Assert.True(problemsWithoutLines.Count == 0, "Cases with no ✅/❌ line: " + string.Join(", ", problemsWithoutLines));
        Assert.True(problems.Count < 76 || checkedCases > 400, $"only {checkedCases} cases were checked");
    }

    [RustFact]
    public async Task EveryCuratedSolution_PassesEveryCase_WhichProvesTheHarnessAndTheTranslationAgree()
    {
        var curated = Problems().Where(p => p.LanguageImplementations.ContainsKey(LanguageIds.Rust)).ToList();
        Assert.True(Environment.GetEnvironmentVariable("BLIND75_PROBLEMS") != null || curated.Count == 18, $"expected 18 curated Rust solutions, found {curated.Count}");

        using var patience = new CancellationTokenSource(Patience);
        foreach (var problem in curated)
        {
            var script = RustProblemLanguageAdapter.Instance.BuildScript(problem);
            var (result, output) = await RunScript($"s{problem.Number}.rs", script.Code).WaitAsync(patience.Token);

            Assert.True(result.FailedBuildStep == null, "The solution doesn't build: " + Describe(problem, result, output));
            foreach (var test in problem.Tests.Concat(problem.ExtraTests))
            {
                Assert.True(Judge.Verdict(output, test.Name) == true, $"#{problem.Number} \"{test.Name}\" isn't ✅: {Judge.LineFor(output, test.Name)}\n{Tail(output)}");
            }

            Assert.Equal(0, result.ExitCode);
            Assert.DoesNotContain("❌", output);
        }
    }

    // A solution that is right must read ✅ on every case, whatever the problem takes and returns: this is what proves the
    // harness and the translation for lists that share nodes, matrices changed in place, boards, graphs, design classes, floats,
    // unsigned integers and answers in any order.
    [RustFact]
    public async Task ACorrectSolution_ReadsGreen_OnEveryShapeOfProblem()
    {
        using var patience = new CancellationTokenSource(Patience);
        var fresh = Blind75CatalogService.CreateAllProblems();
        foreach (var (number, solution) in RustBlind75Oracles.Solutions)
        {
            var chosen = Environment.GetEnvironmentVariable("BLIND75_PROBLEMS");
            if (chosen != null && !chosen.Split(',').Contains(number.ToString())) continue;

            var problem = fresh.First(p => p.Number == number);
            problem.LanguageImplementations[LanguageIds.Rust] = new PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Core.ProblemLanguageBundle { LanguageId = LanguageIds.Rust, SolutionCode = solution };
            var script = RustProblemLanguageAdapter.Instance.BuildScript(problem);
            var (result, output) = await RunScript($"o{number}.rs", script.Code).WaitAsync(patience.Token);

            Assert.True(result.FailedBuildStep == null, "The solution doesn't build: " + Describe(problem, result, output));
            foreach (var test in problem.Tests.Concat(problem.ExtraTests))
            {
                Assert.True(Judge.Verdict(output, test.Name) == true, $"#{number} \"{test.Name}\" isn't ✅: {Judge.LineFor(output, test.Name)}\n{Tail(output)}");
            }

            Assert.True(result.ExitCode == 0, Describe(problem, result, output));
        }
    }

    private (BlindProblemItem Problem, string Code) ScriptWith(int number, string solution)
    {
        var problem = Blind75CatalogService.CreateAllProblems().First(p => p.Number == number);
        problem.LanguageImplementations[LanguageIds.Rust] = new PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Core.ProblemLanguageBundle { LanguageId = LanguageIds.Rust, SolutionCode = solution };
        return (problem, RustProblemLanguageAdapter.Instance.BuildScript(problem).Code);
    }

    [RustFact]
    public async Task AWrongAnswer_ReadsRed_WithBothValues_AndTheScriptExitsWithOne()
    {
        var (problem, code) = ScriptWith(1, """
            struct Solution;

            impl Solution {
                pub fn two_sum(nums: Vec<i32>, target: i32) -> Vec<i32> {
                    vec![9, 9]
                }
            }
            """);

        var (result, output) = await RunScript("wrong.rs", code);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("❌ Example 1 → got [9,9] · expected [0,1]", output);
        Assert.All(problem.Tests, t => Assert.False(Judge.Verdict(output, t.Name)));
    }

    [RustFact]
    public async Task ASolutionThatPanics_IsRedForThatCaseOnly_AndTheOthersStillRun()
    {
        var (problem, code) = ScriptWith(1, """
            struct Solution;

            impl Solution {
                pub fn two_sum(nums: Vec<i32>, target: i32) -> Vec<i32> {
                    if target == 6 {
                        panic!("boom on six");
                    }
                    let mut seen: HashMap<i32, i32> = HashMap::new();
                    for (i, &num) in nums.iter().enumerate() {
                        if let Some(&j) = seen.get(&(target - num)) {
                            return vec![j, i as i32];
                        }
                        seen.insert(num, i as i32);
                    }
                    vec![]
                }
            }
            """);

        var (result, output) = await RunScript("panics.rs", code);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("❌ Example 2 → panicked: boom on six", output);
        Assert.True(Judge.Verdict(output, "Example 1"));
        Assert.True(Judge.Verdict(output, "Extreme values"));
        Assert.Equal(problem.Tests.Count + problem.ExtraTests.Count, problem.Tests.Concat(problem.ExtraTests).Count(t => Judge.Verdict(output, t.Name) != null));
    }

    [RustFact]
    public async Task ACaseThatAllowsAnyOrder_AcceptsAnyOrder_AndOnlyThen()
    {
        // Two Sum lets its answer come in either order, so [1,0] is as good as [0,1] ...
        var (_, twoSum) = ScriptWith(1, """
            struct Solution;

            impl Solution {
                pub fn two_sum(nums: Vec<i32>, target: i32) -> Vec<i32> {
                    for i in 0..nums.len() {
                        for j in 0..nums.len() {
                            if i != j && nums[i] + nums[j] == target {
                                return vec![j as i32, i as i32];
                            }
                        }
                    }
                    vec![]
                }
            }
            """);
        var (_, output) = await RunScript("anyorder.rs", twoSum);

        Assert.True(Judge.Verdict(output, "Example 1"));
        Assert.Contains("✅ Example 1 → [1,0]", output);

        // ... but Product of Array Except Self does not: the same numbers in another order are a wrong answer.
        var (product, productCode) = ScriptWith(238, """
            struct Solution;

            impl Solution {
                pub fn product_except_self(nums: Vec<i32>) -> Vec<i32> {
                    let mut answer: Vec<i32> = (0..nums.len()).map(|i| nums.iter().enumerate().filter(|&(j, _)| j != i).map(|(_, v)| *v).product()).collect();
                    answer.reverse();
                    answer
                }
            }
            """);
        var (_, productOutput) = await RunScript("inorder.rs", productCode);

        Assert.False(Judge.Verdict(productOutput, "Example 1"));
        Assert.Contains("❌ Example 1 → got [6,8,12,24] · expected [24,12,8,6]", productOutput);
        Assert.NotEmpty(product.Tests);
    }

    // The notebook a learner opens: run its code cells one after another with the real kernel, as the notebook does.
    [RustFact]
    public async Task TheNotebook_RunsCellByCell_AndItsTestsAllPass()
    {
        var language = _studio.Services.Registry.Get(LanguageIds.Rust)!;
        foreach (var number in new[] { 1, 21, 104, 49 })
        {
            var problem = Blind75CatalogService.GetProblemByNumber(number)!;
            var notebook = RustProblemLanguageAdapter.Instance.BuildNotebook(problem);
            using var kernel = language.NotebookKernels!.Create(new KernelCreationContext(() => _dir));
            using var patience = new CancellationTokenSource(PerScript * 3);

            var lastOutput = string.Empty;
            foreach (var cell in notebook.Cells.Where(c => c.Type == CellType.Code))
            {
                var console = new StringBuilder();
                var result = await kernel.ExecuteAsync(new KernelExecutionRequest { Code = cell.Source, OnConsole = text => console.Append(text) }, patience.Token);
                Assert.True(result.Success, $"#{number} cell failed: {result.ErrorMessage}\n{result.ConsoleOutput}\n--- cell ---\n{cell.Source}");
                lastOutput = console.ToString();
            }

            foreach (var test in problem.Tests.Concat(problem.ExtraTests))
            {
                Assert.True(Judge.Verdict(lastOutput, test.Name) == true, $"#{number} \"{test.Name}\" isn't ✅ in the notebook: {Judge.LineFor(lastOutput, test.Name)}\n{Tail(lastOutput)}");
            }
        }
    }

    [RustFact]
    public async Task TheNotebookOfAProblemWithHelpers_RunsToo_WithTheOracleSolution()
    {
        var language = _studio.Services.Registry.Get(LanguageIds.Rust)!;
        foreach (var number in new[] { 48, 143, 133, 208, 297 })
        {
            var problem = Blind75CatalogService.CreateAllProblems().First(p => p.Number == number);
            problem.LanguageImplementations[LanguageIds.Rust] = new PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Core.ProblemLanguageBundle
            {
                LanguageId = LanguageIds.Rust,
                SolutionCode = RustBlind75Oracles.Solutions[number]
            };
            var notebook = RustProblemLanguageAdapter.Instance.BuildNotebook(problem);
            using var kernel = language.NotebookKernels!.Create(new KernelCreationContext(() => _dir));
            using var patience = new CancellationTokenSource(PerScript * 3);

            var lastOutput = string.Empty;
            foreach (var cell in notebook.Cells.Where(c => c.Type == CellType.Code))
            {
                var console = new StringBuilder();
                var result = await kernel.ExecuteAsync(new KernelExecutionRequest { Code = cell.Source, OnConsole = text => console.Append(text) }, patience.Token);
                Assert.True(result.Success, $"#{number} cell failed: {result.ErrorMessage}\n{result.ConsoleOutput}\n--- cell ---\n{cell.Source}");
                lastOutput = console.ToString();
            }

            foreach (var test in problem.Tests.Concat(problem.ExtraTests))
            {
                Assert.True(Judge.Verdict(lastOutput, test.Name) == true, $"#{number} \"{test.Name}\" isn't ✅ in the notebook: {Judge.LineFor(lastOutput, test.Name)}\n{Tail(lastOutput)}");
            }
        }
    }

    [RustFact]
    public async Task TheStarterTemplates_BuildAndRun()
    {
        var fresh = new RustLanguage(new StudioLanguageServices(Path.Combine(_dir, ".templates")));
        var template = CodeTemplateLibrary.GetTemplates().Single(t => t.Id == "rust_iterators_starter");

        foreach (var (name, code) in new[] { ("new_file.rs", fresh.NewFileTemplate), ("iterators.rs", template.InitialCode) })
        {
            var (result, output) = await RunScript(name, code);

            Assert.True(result.Succeeded, $"{name} didn't run: exit {result.ExitCode}, failed step {result.FailedBuildStep}\n{Tail(output)}");
            Assert.DoesNotContain("warning", output);
        }
    }
}
