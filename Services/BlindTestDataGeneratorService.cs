using System.Collections.Generic;
using System.Linq;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

/// <summary>
/// "Generate test data" for a Blind 75 problem: the statement's examples plus its edge cases (empty input, duplicates,
/// negatives, the tricky ones). Every expected answer is checked against the reference solution by the test suite,
/// so nothing here is a placeholder.
/// </summary>
public static class BlindTestDataGeneratorService
{
    /// <summary>Every verified case; <paramref name="count"/> is ignored and kept only so existing callers compile.</summary>
    public static List<TestCaseItem> GenerateTestCases(BlindProblemItem problem, int count = 3)
    {
        if (problem == null) return new List<TestCaseItem>();

        var cases = problem.Tests.Concat(problem.ExtraTests).Select(t => t.ToTestCase()).ToList();
        return cases.Count > 0 ? cases : problem.TestCases.ToList();
    }
}
