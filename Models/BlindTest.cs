namespace PdfEditorApp.Plugins.CSharpEditor.Models;

/// <summary>One checked case of a Blind 75 problem: what the learner reads, and the C# that produces the answer.</summary>
public sealed class BlindTest
{
    public string Name { get; init; } = string.Empty;

    /// <summary>The input as LeetCode shows it, e.g. <c>nums = [2,7,11,15], target = 9</c>.</summary>
    public string Input { get; init; } = string.Empty;

    /// <summary>The answer as LeetCode prints it, e.g. <c>[0,1]</c>, <c>true</c>, <c>"bab"</c>.</summary>
    public string Expected { get; init; } = string.Empty;

    /// <summary>A C# expression that computes the answer, usually on <c>sol</c>: <c>sol.TwoSum(new[] { 2, 7, 11, 15 }, 9)</c>.</summary>
    public string Call { get; init; } = string.Empty;

    /// <summary>True when any order of the answer's elements is accepted (Group Anagrams, 3Sum, Top K...).</summary>
    public bool AnyOrder { get; init; }

    public TestCaseItem ToTestCase() => new() { Name = Name, Input = Input, ExpectedOutput = Expected };
}
