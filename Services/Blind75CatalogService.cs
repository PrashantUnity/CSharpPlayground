using System;
using System.Collections.Generic;
using System.Linq;
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

        // Sort naturally by problem number or default curriculum order
        var sorted = list.OrderBy(p => p.Number).ToList();
        Blind75CurriculumEnhancer.Enhance(sorted);
        return sorted;
    });

    public static IReadOnlyList<BlindProblemItem> GetAllProblems() => _problems.Value;

    public static BlindProblemItem? GetProblemByNumber(int number) =>
        _problems.Value.FirstOrDefault(p => p.Number == number);

    public static BlindProblemItem? GetProblemById(string id) =>
        _problems.Value.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

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
        var scriptCode = BuildScriptCode(problem);
        var notes = !string.IsNullOrWhiteSpace(problem.DescriptionMarkdown)
            ? $"# {problem.FullTitle}\n\nDifficulty: {problem.DifficultyBadgeText} | Category: {problem.Category}\n\n{problem.DescriptionMarkdown}"
            : $"# {problem.FullTitle}\n\nDifficulty: {problem.DifficultyBadgeText}\nCategory: {problem.Category}";

        if (!string.IsNullOrWhiteSpace(problem.ThinkingProcessMarkdown))
        {
            notes += $"\n\n{problem.ThinkingProcessMarkdown}";
        }

        return new ScriptDocumentItem
        {
            Title = $"{problem.Number}. {problem.Title}",
            Code = scriptCode,
            Notes = notes,
            TestCases = problem.TestCases.Count > 0 ? problem.TestCases.Select(t => new TestCaseItem
            {
                Name = t.Name,
                Input = t.Input,
                ExpectedOutput = t.ExpectedOutput
            }).ToList() : new List<TestCaseItem>()
        };
    }

    public static NotebookDocumentItem ConvertToNotebook(BlindProblemItem problem)
    {
        var notebook = new NotebookDocumentItem
        {
            Title = $"{problem.Number}. {problem.Title} (Notebook)"
        };

        // 1. Problem Statement Header
        notebook.Cells.Add(new NotebookCellItem
        {
            Type = CellType.Markdown,
            Source = $"# {problem.FullTitle}\n\n**Category:** `{problem.Category}` | **Difficulty:** `{problem.DifficultyBadgeText}` | **Acceptance:** `{problem.AcceptanceRateText}`\n\n{problem.DescriptionMarkdown}"
        });

        // 2. How to Think / Mental Model
        if (!string.IsNullOrWhiteSpace(problem.ThinkingProcessMarkdown))
        {
            notebook.Cells.Add(new NotebookCellItem
            {
                Type = CellType.Markdown,
                Source = problem.ThinkingProcessMarkdown
            });
        }

        // 3. Naive / Brute Force Approach
        if (problem.NaiveApproach != null)
        {
            notebook.Cells.Add(new NotebookCellItem
            {
                Type = CellType.Markdown,
                Source = $"### 1. Naive / Brute Force Approach\n\n- **Time Complexity:** `{problem.NaiveApproach.TimeComplexity}`\n- **Space Complexity:** `{problem.NaiveApproach.SpaceComplexity}`\n\n{problem.NaiveApproach.Intuition}\n\n> ⚠️ **Bottleneck:** {problem.NaiveApproach.BottleneckExplanation}"
            });

            notebook.Cells.Add(new NotebookCellItem
            {
                Type = CellType.Code,
                Source = problem.NaiveApproach.Code.Trim()
            });
        }

        // 4. Greedy Approach (if applicable)
        if (problem.GreedyApproach != null)
        {
            notebook.Cells.Add(new NotebookCellItem
            {
                Type = CellType.Markdown,
                Source = $"### 2. Greedy Paradigm\n\n- **Time Complexity:** `{problem.GreedyApproach.TimeComplexity}`\n- **Space Complexity:** `{problem.GreedyApproach.SpaceComplexity}`\n\n**Greedy Choice Property:** {problem.GreedyApproach.GreedyChoiceProperty}\n\n{problem.GreedyApproach.Intuition}"
            });

            notebook.Cells.Add(new NotebookCellItem
            {
                Type = CellType.Code,
                Source = problem.GreedyApproach.Code.Trim()
            });
        }

        // 5. Dynamic Programming Approach (if applicable)
        if (problem.DpApproach != null)
        {
            notebook.Cells.Add(new NotebookCellItem
            {
                Type = CellType.Markdown,
                Source = $"### 3. Dynamic Programming Formulation\n\n- **Time Complexity:** `{problem.DpApproach.TimeComplexity}`\n- **Space Complexity:** `{problem.DpApproach.SpaceComplexity}`\n\n**Recurrence Relation:**\n```text\n{problem.DpApproach.RecurrenceRelation}\n```\n\n{problem.DpApproach.Intuition}"
            });

            notebook.Cells.Add(new NotebookCellItem
            {
                Type = CellType.Code,
                Source = problem.DpApproach.Code.Trim()
            });
        }

        // 6. Optimal Solution
        notebook.Cells.Add(new NotebookCellItem
        {
            Type = CellType.Markdown,
            Source = $"### 4. Optimal Production Solution\n\n- **Time Complexity:** `{problem.TimeComplexity}`\n- **Space Complexity:** `{problem.SpaceComplexity}`"
        });

        notebook.Cells.Add(new NotebookCellItem
        {
            Type = CellType.Code,
            Source = !string.IsNullOrWhiteSpace(problem.SolutionCode) ? problem.SolutionCode : problem.StarterCode
        });

        // 7. Interactive Visualizer
        notebook.Cells.Add(new NotebookCellItem
        {
            Type = CellType.Markdown,
            Source = """
                ### 🎨 5. Interactive Time-Travel Visualizer (Tracer Harness)

                > 💡 **Interview & Coding Tip**: In an interview or submission, write your solution normally as shown above without instrumentation.
                > The code below is an **extra execution tracer** using `VisualizerRecorder` and specialized trackers (`TreeTracker`, `LinkedListTracker`, `MatrixTracker`) to record state transitions for step-by-step visual scrubbing.
                """
        });

        string visCode = !string.IsNullOrWhiteSpace(problem.VisualizationCode)
            ? problem.VisualizationCode.Trim()
            : Blind75CurriculumEnhancer.GenerateDefaultVisualizerCode(problem);

        notebook.Cells.Add(new NotebookCellItem
        {
            Type = CellType.Code,
            Source = $"// --- Extra Tracer / Step Recorder Harness (For Interactive Visualizer) ---\n{visCode}"
        });

        // 8. Test Suite & On-Demand Data Generator
        notebook.Cells.Add(new NotebookCellItem
        {
            Type = CellType.Markdown,
            Source = "### 🧪 6. Test Suite & Dynamic Data Generator\n\nRun this cell to evaluate all test cases and generate dynamic edge-case test data on demand."
        });

        notebook.Cells.Add(new NotebookCellItem
        {
            Type = CellType.Code,
            Source = Blind75CurriculumEnhancer.GenerateDefaultTestSuiteCode(problem)
        });

        return notebook;
    }

    private static string BuildScriptCode(BlindProblemItem problem)
    {
        var cleanSolution = !string.IsNullOrWhiteSpace(problem.StarterCode)
            ? problem.StarterCode.Trim()
            : GetDefaultBoilerplate(problem).Trim();

        string visHarness = !string.IsNullOrWhiteSpace(problem.VisualizationCode)
            ? problem.VisualizationCode.Trim()
            : Blind75CurriculumEnhancer.GenerateDefaultVisualizerCode(problem).Trim();

        return $$"""
            {{cleanSolution}}

            // =========================================================================
            // 🎨 EXTRA TRACER HARNESS (Interactive Time-Travel Step Recorder)
            // =========================================================================
            // Note: In an interview or submission, write your Solution cleanly as above.
            // Below is an extra tracer harness wrapping your algorithm with trackers
            // (VisualizerRecorder, TreeTracker, LinkedListTracker, MatrixTracker).
            // Press F5 to mount the interactive time-travel player in the Bottom Deck!

            {{visHarness}}
            """;
    }

    private static string GetDefaultBoilerplate(BlindProblemItem p) => $$"""
        // LeetCode {{p.Number}}: {{p.Title}} ({{p.DifficultyBadgeText}})
        // Category: {{p.Category}}
        // Time Complexity: {{p.TimeComplexity}} | Space Complexity: {{p.SpaceComplexity}}
        using System;
        using System.Collections.Generic;
        using System.Linq;

        public class Solution 
        {
            // Implement your solution here
        }

        var sol = new Solution();
        Console.WriteLine("Ready to solve: {{p.FullTitle}}");

        // Visualization:
        {{(!string.IsNullOrWhiteSpace(p.VisualizationCode) ? p.VisualizationCode : Blind75CurriculumEnhancer.GenerateDefaultVisualizerCode(p))}}
        """;
}
