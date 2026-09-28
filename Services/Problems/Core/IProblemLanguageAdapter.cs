using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Problems.Core;

/// <summary>
/// Strategy interface for generating runnable scripts, notebooks, and test runners
/// for a specific programming language from a Blind 75 problem definition.
/// </summary>
public interface IProblemLanguageAdapter
{
    /// <summary>Lower-case language identifier matching Studio Language IDs (e.g. "csharp", "python", "javascript", "java").</summary>
    string LanguageId { get; }

    /// <summary>Display name for UI selection (e.g. "C# (.NET 10 Roslyn)", "Python 3.12").</summary>
    string DisplayName { get; }

    /// <summary>Primary file extension (e.g. ".csx", ".py", ".js", ".java").</summary>
    string DefaultFileExtension { get; }

    /// <summary>Generates a complete, runnable script document item for Code Studio.</summary>
    ScriptDocumentItem BuildScript(IProblemItem problem);

    /// <summary>Generates an interactive, cell-by-cell notebook document item.</summary>
    NotebookDocumentItem BuildNotebook(IProblemItem problem);

    /// <summary>Generates the test assertions / validation harness for this problem in this language.</summary>
    string BuildTestCode(IProblemItem problem);
}
