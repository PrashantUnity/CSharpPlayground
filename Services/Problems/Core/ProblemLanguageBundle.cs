using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Core;

/// <summary>
/// Encapsulates language-specific source code and test definitions for a problem.
/// </summary>
public sealed class ProblemLanguageBundle
{
    public string LanguageId { get; init; } = "csharp";

    /// <summary>The starter function / class template learners see when starting.</summary>
    public string StarterCode { get; init; } = string.Empty;

    /// <summary>Reference solution code in this language.</summary>
    public string SolutionCode { get; init; } = string.Empty;

    /// <summary>Supporting types, data structures or helper utilities (e.g. ListNode, TreeNode).</summary>
    public string SupportCode { get; init; } = string.Empty;

    /// <summary>Test setup code emitted before tests are executed.</summary>
    public string TestSetupCode { get; init; } = string.Empty;

    /// <summary>Language-specific tests with concrete calls.</summary>
    public List<BlindTest> Tests { get; init; } = new();

    /// <summary>Edge case tests.</summary>
    public List<BlindTest> ExtraTests { get; init; } = new();

    /// <summary>Visualizer recording script or visual output logic.</summary>
    public string VisualizationCode { get; init; } = string.Empty;
}
