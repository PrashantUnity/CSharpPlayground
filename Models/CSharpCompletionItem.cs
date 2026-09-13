using System;

namespace PdfEditorApp.Plugins.CSharpEditor.Models;

public enum CompletionItemKind
{
    Method,
    ExtensionMethod,
    Property,
    Field,
    Class,
    Struct,
    Record,
    Interface,
    Enum,
    Keyword,
    Snippet,
    Variable,
    Namespace
}

public class CSharpCompletionItem
{
    public required string DisplayText { get; init; }
    public required string InsertionText { get; init; }
    public CompletionItemKind Kind { get; init; } = CompletionItemKind.Method;
    public string? ReturnType { get; init; }
    public string? Signature { get; init; }
    public string? Documentation { get; init; }
    public double Priority { get; init; } = 0.0;
    public int CaretOffsetDelta { get; init; } = 0; // Negative offset from end of insertion text to place caret
}
