using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PdfEditorApp.Plugins.CSharpEditor.Models;

public enum ProblemDifficulty
{
    Easy,
    Medium,
    Hard
}

public partial class BlindProblemItem : ObservableObject
{
    public string Id { get; init; } = string.Empty;
    public int Number { get; init; }
    public string Title { get; init; } = string.Empty;
    public string FullTitle => $"{Number}. {Title}";
    public string Category { get; init; } = string.Empty;
    public ProblemDifficulty Difficulty { get; init; }
    public double AcceptanceRate { get; init; } // e.g. 52.9
    public string AcceptanceRateText => $"{AcceptanceRate:F1}%";
    public bool IsPremium { get; init; }

    /// <summary>LeetCode's name for the problem in its URL: "Implement Trie (Prefix Tree)" → implement-trie-prefix-tree.</summary>
    public string LeetCodeSlug => ToSlug(Title);

    /// <summary>The problem's page on LeetCode, e.g. https://leetcode.com/problems/two-sum/.</summary>
    public string LeetCodeUrl => $"https://leetcode.com/problems/{LeetCodeSlug}/";

    public string LeetCodeTooltip => IsPremium
        ? $"Open on LeetCode (needs LeetCode Premium): leetcode.com/problems/{LeetCodeSlug}"
        : $"Open on LeetCode: leetcode.com/problems/{LeetCodeSlug}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SolvedTooltip))]
    private bool _isSolved;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BookmarkTooltip))]
    private bool _isBookmarked;

    /// <summary>True while this problem is the one open in the details panel, so its row stands out.</summary>
    [ObservableProperty]
    private bool _isHighlighted;

    public string SolvedTooltip => IsSolved ? "Solved (click to mark unsolved)" : "Mark as solved";
    public string BookmarkTooltip => IsBookmarked ? "Saved (click to remove)" : "Save for later";

    public List<string> Tags { get; init; } = new();
    public string DescriptionMarkdown { get; init; } = string.Empty;
    public string StarterCode { get; init; } = string.Empty;
    public string SolutionCode { get; init; } = string.Empty;

    /// <summary>Types and helpers every code cell shares (ListNode, TreeNode, builders), defined once before the rest.</summary>
    public string SupportCode { get; init; } = string.Empty;

    /// <summary>The examples from the statement, one <c>Check(...)</c> line each in the script and the notebook.</summary>
    public List<BlindTest> Tests { get; init; } = new();

    /// <summary>Edge cases (empty input, duplicates, negatives...) revealed by "Generate test data" and always checked.</summary>
    public List<BlindTest> ExtraTests { get; init; } = new();

    /// <summary>
    /// Helpers the test calls need that depend on the solution (e.g. replaying LeetCode's ["Trie","insert",...] operation
    /// list). Emitted just before the tests, after the solution, in both the script and the notebook.
    /// </summary>
    public string TestSetupCode { get; init; } = string.Empty;
    public string TimeComplexity { get; init; } = "O(N)";
    public string SpaceComplexity { get; init; } = "O(1)";

    // Thinking Process & Multi-Approach Progression
    public string ThinkingProcessMarkdown { get; init; } = string.Empty;
    public List<ProblemApproachItem> Approaches { get; init; } = new();

    public ProblemApproachItem? NaiveApproach => Approaches.FirstOrDefault(a => a.Kind == ApproachKind.Naive);
    public ProblemApproachItem? GreedyApproach => Approaches.FirstOrDefault(a => a.Kind == ApproachKind.Greedy);
    public ProblemApproachItem? DpApproach => Approaches.FirstOrDefault(a => a.Kind == ApproachKind.DynamicProgramming);
    public ProblemApproachItem? OptimalApproach => Approaches.FirstOrDefault(a => a.Kind == ApproachKind.Optimal);

    public bool HasNaiveSolution => NaiveApproach != null;
    public bool HasGreedySolution => GreedyApproach != null;
    public bool HasDpSolution => DpApproach != null;

    // Test Cases
    private ObservableCollection<TestCaseItem> _testCases = new();
    public IList<TestCaseItem> TestCases
    {
        get => _testCases;
        init
        {
            if (value is ObservableCollection<TestCaseItem> oc)
            {
                _testCases = oc;
            }
            else if (value != null)
            {
                _testCases = new ObservableCollection<TestCaseItem>(value);
            }
        }
    }
    public ObservableCollection<TestCaseItem> ObservableTestCases => _testCases;

    // Interactive Visualizer Metadata
    public bool HasVisualizer { get; init; } = true;
    public string? VisualizerKind { get; init; } = "ArrayPointers"; // ArrayPointers, Tree, LinkedList, Graph, Matrix, Bars, Board
    public string VisualizationCode { get; init; } = string.Empty;
    public string VisualizationDescription { get; init; } = string.Empty;

    public string DifficultyBadgeText => Difficulty switch
    {
        ProblemDifficulty.Easy => "Easy",
        ProblemDifficulty.Medium => "Med",
        ProblemDifficulty.Hard => "Hard",
        _ => "Med"
    };

    public string DifficultyColor => Difficulty switch
    {
        ProblemDifficulty.Easy => "#00B8A3", // LeetCode Easy Teal/Green
        ProblemDifficulty.Medium => "#FFC01E", // LeetCode Medium Amber/Yellow
        ProblemDifficulty.Hard => "#EF4444", // LeetCode Hard Rose/Red
        _ => "#FFC01E"
    };

    public string DifficultyBackground => Difficulty switch
    {
        ProblemDifficulty.Easy => "#1A00B8A3",
        ProblemDifficulty.Medium => "#1AFFC01E",
        ProblemDifficulty.Hard => "#1AEF4444",
        _ => "#1AFFC01E"
    };

    // Lowercase letters and digits, one hyphen for each run of spaces or hyphens, everything else dropped.
    private static string ToSlug(string title)
    {
        var slug = new StringBuilder(title.Length);
        foreach (char c in title.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c)) slug.Append(c);
            else if ((c == ' ' || c == '-') && slug.Length > 0 && slug[^1] != '-') slug.Append('-');
        }
        return slug.ToString().TrimEnd('-');
    }
}
