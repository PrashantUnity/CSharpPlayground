using System;
using System.Threading.Tasks;

namespace FrySharp.Sdk;

/// <summary>
/// Represents an open document inside the editor tab strip.
/// </summary>
public interface IDocumentContext
{
    /// <summary>Absolute file path if saved to disk, or null if in-memory.</summary>
    string? FilePath { get; }

    /// <summary>Tab display title.</summary>
    string Title { get; }

    /// <summary>Source code contents of the document.</summary>
    string Text { get; set; }

    /// <summary>Language identifier ('csharp', 'python', 'javascript', etc.).</summary>
    string LanguageId { get; }

    /// <summary>Whether the document has unsaved modifications.</summary>
    bool IsDirty { get; }

    /// <summary>Active cursor line number (1-based).</summary>
    int CaretLine { get; }

    /// <summary>Active cursor column number (1-based).</summary>
    int CaretColumn { get; }

    /// <summary>Currently selected text in the code canvas.</summary>
    string SelectedText { get; set; }

    /// <summary>Formats the document according to the active language formatter.</summary>
    void Format();

    /// <summary>Saves the document to its storage backing.</summary>
    void Save();
}

/// <summary>
/// Public API for controlling the editor canvas, tabs, and documents.
/// </summary>
public interface IEditorApi
{
    /// <summary>Gets the currently active document, or null if no editor tab is open.</summary>
    IDocumentContext? ActiveDocument { get; }

    /// <summary>Opens a file into an editor tab.</summary>
    Task OpenFileAsync(string filePath);

    /// <summary>Creates a new untitled document with optional starter code.</summary>
    Task CreateDocumentAsync(string languageId = "csharp", string? initialCode = null);

    /// <summary>Formats the currently active document.</summary>
    void FormatActiveDocument();

    /// <summary>Saves the currently active document.</summary>
    void SaveActiveDocument();
}
