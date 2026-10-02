using System;
using System.Collections.Generic;
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

    /// <summary>Total number of lines in the document.</summary>
    int LineCount => string.IsNullOrEmpty(Text) ? 1 : Text.Split('\n').Length;

    /// <summary>Gets the text of a specific 1-based line.</summary>
    string GetLineText(int lineNumber)
    {
        if (lineNumber < 1) return string.Empty;
        var lines = Text.Split('\n');
        return lineNumber <= lines.Length ? lines[lineNumber - 1].TrimEnd('\r') : string.Empty;
    }

    /// <summary>Moves the editor cursor to the specified 1-based line and column.</summary>
    void SetCaret(int line, int column) { }

    /// <summary>Selects a text range spanning from start line/col to end line/col (1-based).</summary>
    void SetSelection(int startLine, int startColumn, int endLine, int endColumn) { }

    /// <summary>Inserts text at the current cursor position or replaces active selection.</summary>
    void InsertText(string text)
    {
        SelectedText = text;
    }

    /// <summary>Replaces text within a line and column span.</summary>
    void ReplaceRange(int startLine, int startColumn, int endLine, int endColumn, string replacement) { }

    /// <summary>Scrolls the editor canvas to bring the line into view.</summary>
    void ScrollToLine(int lineNumber) { }

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

    /// <summary>Gets all open documents in the editor tab strip.</summary>
    IReadOnlyList<IDocumentContext> OpenDocuments => ActiveDocument != null ? [ActiveDocument] : Array.Empty<IDocumentContext>();

    /// <summary>Opens a file into an editor tab.</summary>
    Task OpenFileAsync(string filePath);

    /// <summary>Creates a new untitled document with optional starter code.</summary>
    Task CreateDocumentAsync(string languageId = "csharp", string? initialCode = null);

    /// <summary>Closes a specific open document tab.</summary>
    Task CloseDocumentAsync(IDocumentContext document) => Task.CompletedTask;

    /// <summary>Closes the currently active document tab.</summary>
    Task CloseActiveDocumentAsync() => Task.CompletedTask;

    /// <summary>Switches active tab focus to the specified document.</summary>
    void SwitchToDocument(IDocumentContext document) { }

    /// <summary>Formats the currently active document.</summary>
    void FormatActiveDocument();

    /// <summary>Saves the currently active document.</summary>
    void SaveActiveDocument();

    /// <summary>Fired when the focused document tab changes.</summary>
    event Action<IDocumentContext?>? ActiveDocumentChanged;
}
