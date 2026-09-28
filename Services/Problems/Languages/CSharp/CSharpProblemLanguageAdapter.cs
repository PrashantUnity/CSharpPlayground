using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Core;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.CSharp;

/// <summary>
/// Language adapter that formats algorithm and curriculum problems into runnable C# Roslyn scripts,
/// interactive notebooks, and Check() assertion test runners.
/// </summary>
public class CSharpProblemLanguageAdapter : IProblemLanguageAdapter
{
    public static CSharpProblemLanguageAdapter Instance { get; } = new();

    public string LanguageId => "csharp";
    public string DisplayName => "C# (.NET 10 Roslyn)";
    public string DefaultFileExtension => ".csx";

    public ScriptDocumentItem BuildScript(IProblemItem problem)
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

    public NotebookDocumentItem BuildNotebook(IProblemItem problem)
    {
        var notebook = new NotebookDocumentItem
        {
            Title = $"{problem.Number}. {problem.Title} (Notebook)"
        };

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

    public string BuildTestCode(IProblemItem problem)
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

    private string BuildScriptCode(IProblemItem problem)
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

    private static string Literal(string text) =>
        Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(text, quote: true);
}

/// <summary>Backward-compatible alias for CSharpProblemLanguageAdapter.</summary>
public sealed class CSharpBlindAdapter : CSharpProblemLanguageAdapter
{
    public static new CSharpBlindAdapter Instance { get; } = new();
}
