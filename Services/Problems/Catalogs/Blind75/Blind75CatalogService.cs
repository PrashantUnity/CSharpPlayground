using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Catalogs.Blind75.MultiLanguage;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Core;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.CSharp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.Cpp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.FSharp;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.Go;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.Java;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.JavaScript;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.Python;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.Rust;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Languages.Sql;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static partial class Blind75CatalogService
{
    static Blind75CatalogService()
    {
        ProblemLanguageRegistry.Register(CSharpProblemLanguageAdapter.Instance);
        ProblemLanguageRegistry.Register(PythonProblemLanguageAdapter.Instance);
        ProblemLanguageRegistry.Register(JavaScriptProblemLanguageAdapter.Instance);
        ProblemLanguageRegistry.Register(JavaProblemLanguageAdapter.Instance);
        ProblemLanguageRegistry.Register(CppProblemLanguageAdapter.Instance);
        ProblemLanguageRegistry.Register(GoProblemLanguageAdapter.Instance);
        ProblemLanguageRegistry.Register(FSharpProblemLanguageAdapter.Instance);
        ProblemLanguageRegistry.Register(SqlProblemLanguageAdapter.Instance);
        ProblemLanguageRegistry.Register(RustProblemLanguageAdapter.Instance);
    }

    private static readonly Lazy<IReadOnlyList<BlindProblemItem>> _problems = new(CreateAllProblems);

    /// <summary>
    /// The catalog as new objects. A view that keeps state on the items (solved and saved flags, the highlighted row,
    /// generated test cases) builds its own copy, so views and tests never share it; <see cref="GetAllProblems"/> is
    /// the process-wide copy, for reading the problems.
    /// </summary>
    public static IReadOnlyList<BlindProblemItem> CreateAllProblems()
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
            Blind75MultiLanguageSolutions.Enrich(problem);
            if (problem.TestCases.Count == 0)
            {
                foreach (var test in problem.Tests)
                {
                    problem.TestCases.Add(test.ToTestCase());
                }
            }
        }
        return sorted;
    }

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

    public static ScriptDocumentItem ConvertToScript(BlindProblemItem problem, string languageId = "csharp") =>
        ProblemLanguageRegistry.GetAdapter(languageId).BuildScript(problem);

    public static NotebookDocumentItem ConvertToNotebook(BlindProblemItem problem, string languageId = "csharp") =>
        ProblemLanguageRegistry.GetAdapter(languageId).BuildNotebook(problem);

    /// <summary>Generates verification test assertions for this problem in the specified language.</summary>
    public static string BuildTestCode(BlindProblemItem problem, string languageId = "csharp") =>
        ProblemLanguageRegistry.GetAdapter(languageId).BuildTestCode(problem);
}
