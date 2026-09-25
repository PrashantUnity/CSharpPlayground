using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

/// <summary>
/// Every Blind 75 problem, run the way a learner runs it: the Code Studio script and each notebook cell go through the
/// real kernel, every example and edge case must print ✅, and the visualizer must actually animate the algorithm.
/// </summary>
public class Blind75ProblemsTests
{
    private const int MinSteps = 3;
    private const int MaxSteps = 600;

    // BLIND75_PROBLEMS=1,217 limits a run to those problems while working on them.
    public static IEnumerable<object[]> ProblemNumbers()
    {
        var numbers = Blind75CatalogService.GetAllProblems().Select(p => p.Number);
        var only = Environment.GetEnvironmentVariable("BLIND75_PROBLEMS");
        if (!string.IsNullOrWhiteSpace(only))
        {
            var chosen = only.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse).ToHashSet();
            numbers = numbers.Where(chosen.Contains);
        }
        return numbers.Select(n => new object[] { n });
    }

    private sealed record Run(bool Success, string Error, string Console, List<RichCellOutput> Outputs);

    private static BlindProblemItem Problem(int number) =>
        Blind75CatalogService.GetProblemByNumber(number) ?? throw new InvalidOperationException($"No problem {number}");

    private static async Task<Run> RunAsync(NotebookExecutionKernel kernel, string code)
    {
        var outputs = new List<RichCellOutput>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await kernel.ExecuteCellAsync(code, onRichOutput: outputs.Add, ct: cts.Token);
        string error = result.Success ? string.Empty
            : result.WasCancelled ? "timed out after 30s"
            : result.Diagnostics.Count > 0
                ? string.Join("\n", result.Diagnostics.Take(4).Select(d => $"line {d.Line}: {d.Message}"))
                : result.ErrorMessage;
        return new Run(result.Success, error, result.ConsoleOutput, outputs);
    }

    [Theory]
    [MemberData(nameof(ProblemNumbers))]
    public async Task Script_PassesEveryCaseAndAnimatesTheAlgorithm(int number)
    {
        var problem = Problem(number);
        var script = Blind75CatalogService.ConvertToScript(problem);

        var run = await RunAsync(new NotebookExecutionKernel(), script.Code);

        Assert.True(run.Success, $"{problem.FullTitle}: the script failed:\n{run.Error}\n--- console ---\n{Tail(run.Console)}");
        AssertEveryCasePassed(problem, run.Console);
        AssertAnimates(problem, run);

        // The Test Cases panel reads each case's own ✅ line.
        foreach (var testCase in script.TestCases)
        {
            Assert.True(Judge.Verdict(run.Console, testCase.Name) == true, $"{problem.FullTitle}: no ✅ line for panel case '{testCase.Name}'");
        }
    }

    [Theory]
    [MemberData(nameof(ProblemNumbers))]
    public void Script_ShowsNothingInTheProblemsPanel(int number)
    {
        // The editor's live check (not the kernel) is what paints red and yellow squiggles in Code Studio.
        var problem = Problem(number);
        var diagnostics = new RoslynCompilerService().CheckDiagnostics(Blind75CatalogService.ConvertToScript(problem).Code, ExecutionLanguageMode.Statements);
        Assert.True(diagnostics.Count == 0, $"{problem.FullTitle}: the Problems panel would show:\n" +
            string.Join("\n", diagnostics.Take(6).Select(d => $"{d.Severity} {d.Id} line {d.Line}: {d.Message}")));
    }

    [Theory]
    [MemberData(nameof(ProblemNumbers))]
    public async Task Notebook_EveryCellRunsInOrder(int number)
    {
        var problem = Problem(number);
        var notebook = Blind75CatalogService.ConvertToNotebook(problem);
        var kernel = new NotebookExecutionKernel();
        var approachCode = problem.Approaches.Where(a => a.Kind != ApproachKind.Optimal).Select(a => a.Code.Trim()).ToHashSet();

        Run? visual = null, tests = null;
        foreach (var cell in notebook.Cells.Where(c => c.Type == CellType.Code))
        {
            var run = await RunAsync(kernel, cell.Source);
            Assert.True(run.Success, $"{problem.FullTitle}: notebook cell failed:\n{FirstLines(cell.Source)}\n→ {run.Error}\n--- console ---\n{Tail(run.Console)}");

            if (approachCode.Contains(cell.Source.Trim()))
            {
                Assert.True(run.Console.Trim().Length > 0 || run.Outputs.Count > 0,
                    $"{problem.FullTitle}: an approach cell should show its answer on an example, but printed nothing:\n{FirstLines(cell.Source)}");
            }
            if (cell.Source.Trim() == problem.VisualizationCode.Trim()) visual = run;
            if (cell.Source.Contains("var judge = new Judge();", StringComparison.Ordinal)) tests = run;
        }

        Assert.NotNull(visual);
        AssertAnimates(problem, visual!);
        Assert.NotNull(tests);
        AssertEveryCasePassed(problem, tests!.Console);
    }

    [Theory]
    [MemberData(nameof(ProblemNumbers))]
    public void Content_ExplainsTheProblemAndHowToThink(int number)
    {
        var p = Problem(number);

        Assert.Contains("Example", p.DescriptionMarkdown);
        Assert.Contains("Constraints", p.DescriptionMarkdown);
        Assert.True(p.ThinkingProcessMarkdown.Trim().Length >= 300, $"{p.FullTitle}: 'How to think' is too thin to teach the idea");
        Assert.False(string.IsNullOrWhiteSpace(p.SolutionCode), $"{p.FullTitle}: no solution");
        Assert.False(string.IsNullOrWhiteSpace(p.VisualizationDescription), $"{p.FullTitle}: say what the visualizer shows");
        Assert.NotNull(p.OptimalApproach);
        Assert.True(p.Approaches.Count >= 2, $"{p.FullTitle}: show at least the direct approach and the optimal one");
        Assert.True(p.Tests.Count >= 2, $"{p.FullTitle}: needs the statement's examples as tests");
        Assert.True(p.ExtraTests.Count >= 2, $"{p.FullTitle}: needs edge cases for 'Generate test data'");

        var names = p.Tests.Concat(p.ExtraTests).Select(t => t.Name).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.All(p.Tests.Concat(p.ExtraTests), t =>
        {
            Assert.False(string.IsNullOrWhiteSpace(t.Input), $"{p.FullTitle}: '{t.Name}' has no readable input");
            Assert.False(string.IsNullOrWhiteSpace(t.Call), $"{p.FullTitle}: '{t.Name}' has no call");
        });
    }

    private static void AssertEveryCasePassed(BlindProblemItem problem, string console)
    {
        Assert.DoesNotContain(Judge.FailMark, console);
        foreach (var test in problem.Tests.Concat(problem.ExtraTests))
        {
            Assert.True(Judge.Verdict(console, test.Name) == true, $"{problem.FullTitle}: '{test.Name}' did not print ✅\n--- console ---\n{Tail(console)}");
        }
    }

    private static void AssertAnimates(BlindProblemItem problem, Run run)
    {
        var visualizers = run.Outputs.Where(o => o.Kind == CellOutputKind.Visualizer && o.VisualizerOptions?.Sequence != null).ToList();
        Assert.True(visualizers.Count > 0, $"{problem.FullTitle}: no visualizer was displayed");

        var primary = visualizers.FirstOrDefault(v => v.VisualizerOptions!.Kind.ToString() == problem.VisualizerKind);
        Assert.True(primary != null, $"{problem.FullTitle}: expected a {problem.VisualizerKind} visualizer, got {string.Join(", ", visualizers.Select(v => v.VisualizerOptions!.Kind))}");

        foreach (var output in visualizers)
        {
            var sequence = output.VisualizerOptions!.Sequence!;
            Assert.InRange(sequence.TotalSteps, MinSteps, MaxSteps);
            Assert.All(sequence.Steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Description), $"{problem.FullTitle}: a step has no description"));

            int linked = sequence.Steps.Count(s => s.SourceLine > 0);
            Assert.True(linked >= sequence.TotalSteps * 0.7, $"{problem.FullTitle}: only {linked}/{sequence.TotalSteps} steps point at a code line");

            foreach (var step in sequence.Steps)
            {
                if (step.Snapshot is ArrayPointerData array && step.CustomData is List<PointerMarkerData> pointers)
                {
                    foreach (var pointer in pointers)
                    {
                        Assert.True(pointer.Index >= -1 && pointer.Index <= array.Items.Count,
                            $"{problem.FullTitle}: step '{step.Description}' puts pointer '{pointer.Name}' at {pointer.Index}, outside the {array.Items.Count} cells (values belong in Watch, not pointers)");
                    }
                }
            }
        }
    }

    private static string FirstLines(string code) => string.Join("\n", code.Split('\n').Take(3));

    private static string Tail(string text) => text.Length <= 1500 ? text : "…" + text[^1500..];
}
