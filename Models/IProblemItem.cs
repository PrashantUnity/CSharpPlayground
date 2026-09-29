using System.Collections.Generic;
using System.Collections.ObjectModel;
using PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Core;

namespace PdfEditorApp.Plugins.CSharpEditor.Models;

/// <summary>
/// General abstraction for any coding interview or curriculum problem
/// (e.g. Blind 75, Top Interview 150, NeetCode 150, or custom practice sets).
/// Enables C# script and notebook generation across any problem catalog.
/// </summary>
public interface IProblemItem
{
    string Id { get; }
    int Number { get; }
    string Title { get; }
    string FullTitle { get; }
    string Category { get; }
    ProblemDifficulty Difficulty { get; }
    double AcceptanceRate { get; }
    string AcceptanceRateText { get; }
    bool IsPremium { get; }
    string LeetCodeSlug { get; }
    string LeetCodeUrl { get; }
    bool IsSolved { get; set; }
    bool IsBookmarked { get; set; }
    List<string> Tags { get; }

    // Problem Statement & Thinking
    string DescriptionMarkdown { get; }
    string ThinkingProcessMarkdown { get; }
    string TimeComplexity { get; }
    string SpaceComplexity { get; }
    List<ProblemApproachItem> Approaches { get; }
    ProblemApproachItem? OptimalApproach { get; }

    // Code & Support (Default / C#)
    string StarterCode { get; }
    string SolutionCode { get; }
    string SupportCode { get; }
    string TestSetupCode { get; }
    List<BlindTest> Tests { get; }
    List<BlindTest> ExtraTests { get; }
    IList<TestCaseItem> TestCases { get; }

    // Visualizer Metadata
    bool HasVisualizer { get; }
    string? VisualizerKind { get; }
    string VisualizationCode { get; }
    string VisualizationDescription { get; }

    // UI Badges & Formatting
    string DifficultyBadgeText { get; }
    string DifficultyColor { get; }
    string DifficultyBackground { get; }
}
