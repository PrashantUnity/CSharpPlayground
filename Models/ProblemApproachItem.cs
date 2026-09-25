using System;

namespace PdfEditorApp.Plugins.CSharpEditor.Models;

public enum ApproachKind
{
    Naive,
    Greedy,
    DynamicProgramming,
    Optimal,
    Visualization,

    /// <summary>Another sound way worth knowing (sort first, a heap, a different key), between brute force and optimal.</summary>
    Alternative
}

public class ProblemApproachItem
{
    public string Name { get; init; } = string.Empty;
    public ApproachKind Kind { get; init; }
    public string Intuition { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string TimeComplexity { get; init; } = string.Empty;
    public string SpaceComplexity { get; init; } = string.Empty;
    public string? RecurrenceRelation { get; init; }
    public string? GreedyChoiceProperty { get; init; }
    public string? BottleneckExplanation { get; init; }
}
