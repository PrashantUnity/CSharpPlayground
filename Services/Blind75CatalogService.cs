using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static partial class Blind75CatalogService
{
    private static readonly Lazy<IReadOnlyList<BlindProblemItem>> _problems = new(() =>
    {
        var list = new List<BlindProblemItem>();
        list.AddRange(GetArraysAndHashingProblems());
        list.AddRange(GetTwoPointersAndSlidingWindowProblems());
        list.AddRange(GetLinkedListProblems());
        list.AddRange(GetTreeProblems());
        list.AddRange(GetGraphAndTrieProblems());
        list.AddRange(GetDynamicProgrammingProblems());
        list.AddRange(GetIntervalsAndMathProblems());

        var sorted = list.OrderBy(p => p.Number).ToList();

        // The panel starts with the statement's examples ("Generate test data" adds the edge cases); the script and
        // notebook always check both and print a ✅/❌ line per case.
        foreach (var problem in sorted)
        {
            if (problem.TestCases.Count == 0)
            {
                foreach (var test in problem.Tests)
                {
                    problem.TestCases.Add(test.ToTestCase());
                }
            }
        }
        return sorted;
    });

    public static IReadOnlyList<BlindProblemItem> GetAllProblems() => _problems.Value;

    public static BlindProblemItem? GetProblemByNumber(int number) =>
        _problems.Value.FirstOrDefault(p => p.Number == number);

    public static BlindProblemItem? GetProblemById(string id) =>
        _problems.Value.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The problem a script was opened from: its title is the problem's full title ("11. Container With Most Water"),
    /// as <see cref="ConvertToScript"/> names it. Null for any other script, including one merely starting with a number.
    /// </summary>
    public static BlindProblemItem? FindForScript(ScriptDocumentItem? script)
    {
        var match = System.Text.RegularExpressions.Regex.Match(script?.Title ?? string.Empty, @"^(\d+)\.\s+(.+)$");
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out int number)) return null;
        var problem = GetProblemByNumber(number);
        return problem != null && string.Equals(problem.Title, match.Groups[2].Value.Trim(), StringComparison.OrdinalIgnoreCase)
            ? problem
            : null;
    }

    public static IReadOnlyList<string> GetAllCategories() => new[]
    {
        "All",
        "Arrays & Hashing",
        "Two Pointers",
        "Sliding Window",
        "Stack",
        "Binary Search",
        "Linked List",
        "Trees",
        "Tries",
        "Heap / Priority Queue",
        "Backtracking",
        "Graphs",
        "Advanced Graphs",
        "1-D DP",
        "2-D DP",
        "Greedy",
        "Intervals",
        "Math & Geometry",
        "Bit Manipulation"
    };

    public static MaterialIconKind GetCategoryIcon(string category) => category switch
    {
        "Arrays & Hashing" => MaterialIconKind.FormatListNumbered,
        "Two Pointers" => MaterialIconKind.RayEndArrow,
        "Sliding Window" => MaterialIconKind.ArrowLeftRight,
        "Stack" => MaterialIconKind.LayersOutline,
        "Binary Search" => MaterialIconKind.FileSearchOutline,
        "Linked List" => MaterialIconKind.LinkVariant,
        "Trees" => MaterialIconKind.FamilyTree,
        "Tries" => MaterialIconKind.GraphOutline,
        "Heap / Priority Queue" => MaterialIconKind.SortAscending,
        "Backtracking" => MaterialIconKind.UndoVariant,
        "Graphs" => MaterialIconKind.Graph,
        "Advanced Graphs" => MaterialIconKind.ShareVariantOutline,
        "1-D DP" => MaterialIconKind.ChartLine,
        "2-D DP" => MaterialIconKind.Grid,
        "Greedy" => MaterialIconKind.TrendingUp,
        "Intervals" => MaterialIconKind.TimelineOutline,
        "Math & Geometry" => MaterialIconKind.CompassOutline,
        "Bit Manipulation" => MaterialIconKind.Memory,
        _ => MaterialIconKind.CodeBraces
    };

    public static ScriptDocumentItem ConvertToScript(BlindProblemItem problem)
    {
        var notes = new StringBuilder();
        notes.Append($"# {problem.FullTitle}\n\n**{problem.DifficultyBadgeText}** · {problem.Category}\n\n{problem.DescriptionMarkdown.Trim()}");
        if (!string.IsNullOrWhiteSpace(problem.ThinkingProcessMarkdown))
        {
            notes.Append($"\n\n## How to think\n\n{problem.ThinkingProcessMarkdown.Trim()}");
        }

        return new ScriptDocumentItem
        {
            Title = $"{problem.Number}. {problem.Title}",
            Code = BuildScriptCode(problem),
            Notes = notes.ToString(),
            TestCases = problem.TestCases.Select(t => new TestCaseItem
            {
                Name = t.Name,
                Input = t.Input,
                ExpectedOutput = t.ExpectedOutput
            }).ToList()
        };
    }

    public static NotebookDocumentItem ConvertToNotebook(BlindProblemItem problem)
    {
        var notebook = new NotebookDocumentItem
        {
            Title = $"{problem.Number}. {problem.Title} (Notebook)"
        };

        // The notes open rendered; "MD" on a cell still shows its markdown source for editing.
        void Markdown(string source) => notebook.Cells.Add(new NotebookCellItem { Type = CellType.Markdown, Source = source.Trim(), IsMarkdownPreviewMode = true });
        void Code(string source) => notebook.Cells.Add(new NotebookCellItem { Type = CellType.Code, Source = source.Trim() });

        // 1. The problem, then how to think about it
        Markdown($"# {problem.FullTitle}\n\n**Category:** `{problem.Category}` · **Difficulty:** `{problem.DifficultyBadgeText}` · **Acceptance:** `{problem.AcceptanceRateText}`\n\n{problem.DescriptionMarkdown.Trim()}");
        if (!string.IsNullOrWhiteSpace(problem.ThinkingProcessMarkdown))
        {
            Markdown($"## 🧠 How to think\n\n{problem.ThinkingProcessMarkdown.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(problem.SupportCode))
        {
            Markdown("### 🧰 Setup\n\nRun this first: the node types and helpers every later cell uses.");
            Code(problem.SupportCode);
        }

        // 2. Approaches from the most direct to the best, each one runnable
        int number = 0;
        foreach (var approach in problem.Approaches.Where(a => a.Kind != ApproachKind.Optimal))
        {
            Markdown(DescribeApproach(++number, approach));
            if (!string.IsNullOrWhiteSpace(approach.Code)) Code(approach.Code);
        }

        var optimal = problem.OptimalApproach;
        Markdown(DescribeApproach(++number, optimal ?? new ProblemApproachItem
        {
            Name = "Optimal Solution",
            Kind = ApproachKind.Optimal,
            TimeComplexity = problem.TimeComplexity,
            SpaceComplexity = problem.SpaceComplexity
        }));
        Code(problem.SolutionCode);

        // 3. Watch it run
        Markdown($"""
            ### 🎬 Watch it run

            {(string.IsNullOrWhiteSpace(problem.VisualizationDescription) ? "The same algorithm with tracker calls that record each step." : problem.VisualizationDescription.Trim())}

            > **Using the player:** ◀ ▶ (or the arrow keys) step through, **Space** plays, the **Ln** badge jumps to the line that recorded the step, **Fit** shows the whole drawing and **⛶** opens it full screen. Values that changed since the previous step are outlined in lime.
            >
            > In an interview you write only the solution above; the tracker calls exist only to draw it.
            """);
        Code(problem.VisualizationCode);

        // 4. Check it
        Markdown("### 🧪 Tests\n\nThe examples from the statement plus a few edge cases. `Check(name, answer, expected)` prints ✅ when the answer matches and ❌ with both values when it doesn't.");
        Code(BuildTestCode(problem));

        return notebook;
    }

    private static string DescribeApproach(int number, ProblemApproachItem approach)
    {
        var text = new StringBuilder();
        string badge = approach.Kind switch
        {
            ApproachKind.Naive => "🐢",
            ApproachKind.Greedy => "🎯",
            ApproachKind.DynamicProgramming => "🧮",
            ApproachKind.Optimal => "✅",
            _ => "💡"
        };
        text.Append($"### {badge} Approach {number}: {approach.Name}\n\n");
        text.Append($"**Time:** `{approach.TimeComplexity}` · **Space:** `{approach.SpaceComplexity}`\n\n");
        if (!string.IsNullOrWhiteSpace(approach.Intuition)) text.Append(approach.Intuition.Trim()).Append("\n\n");
        if (!string.IsNullOrWhiteSpace(approach.RecurrenceRelation)) text.Append($"**Recurrence:**\n\n```text\n{approach.RecurrenceRelation.Trim()}\n```\n\n");
        if (!string.IsNullOrWhiteSpace(approach.GreedyChoiceProperty)) text.Append($"**Greedy choice:** {approach.GreedyChoiceProperty.Trim()}\n\n");
        if (!string.IsNullOrWhiteSpace(approach.BottleneckExplanation)) text.Append($"> ⚠️ **Why it's not enough:** {approach.BottleneckExplanation.Trim()}\n");
        return text.ToString();
    }

    private static string BuildScriptCode(BlindProblemItem problem)
    {
        var code = new StringBuilder();
        code.AppendLine($"// {problem.FullTitle} · {problem.DifficultyBadgeText} · {problem.Category}");
        code.AppendLine("// Run (F5): the tests print ✅/❌, then the step-by-step visualizer opens in the Results panel.");
        code.AppendLine("// The full statement and hints are in the Notes panel.");
        code.AppendLine();
        if (!string.IsNullOrWhiteSpace(problem.SupportCode))
        {
            code.AppendLine(problem.SupportCode.Trim());
            code.AppendLine();
        }
        code.AppendLine(problem.SolutionCode.Trim());
        code.AppendLine();
        code.AppendLine("// ── Tests: each Check prints ✅ when the answer matches, ❌ when it doesn't ──");
        code.AppendLine(BuildTestCode(problem));
        code.AppendLine();
        code.AppendLine("// ── Visualizer: the same algorithm with tracker calls that record every step ──");
        code.AppendLine("// (Interviews only need the solution above; this part exists to draw it.)");
        code.AppendLine(problem.VisualizationCode.Trim());
        return code.ToString();
    }

    /// <summary>One Check(name, answer, expected) line per example and edge case, after any helpers the calls need.</summary>
    public static string BuildTestCode(BlindProblemItem problem)
    {
        var code = new StringBuilder();
        if (problem.SolutionCode.Contains("class Solution", StringComparison.Ordinal))
        {
            code.AppendLine("var sol = new Solution();");
        }
        if (!string.IsNullOrWhiteSpace(problem.TestSetupCode))
        {
            code.AppendLine(problem.TestSetupCode.Trim());
        }
        foreach (var test in problem.Tests.Concat(problem.ExtraTests))
        {
            string order = test.AnyOrder ? ", anyOrder: true" : string.Empty;
            code.AppendLine($"Check({Literal(test.Name)}, {test.Call.Trim()}, {Literal(test.Expected)}{order});");
        }
        return code.ToString().TrimEnd();
    }

    private static string Literal(string text) =>
        Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(text, quote: true);
}
