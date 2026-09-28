namespace PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Core;

/// <summary>
/// A language-agnostic test case for an algorithm problem:
/// input arguments, expected return value, and comparison semantics.
/// </summary>
public sealed class UniversalTestCase
{
    public string Name { get; init; } = string.Empty;

    /// <summary>Raw input as presented in problem statement (e.g. nums = [2,7,11,15], target = 9).</summary>
    public string Input { get; init; } = string.Empty;

    /// <summary>Expected return representation (e.g. [0,1], true, "bab").</summary>
    public string ExpectedOutput { get; init; } = string.Empty;

    /// <summary>True if result elements can be returned in any order (e.g. 3Sum, Group Anagrams).</summary>
    public bool AnyOrder { get; init; }
}
