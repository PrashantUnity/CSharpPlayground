using System.Collections.Generic;

namespace PdfEditorApp.Plugins.CSharpEditor.Models;

public class CodeTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string Description { get; set; } = string.Empty;
    public string IconKind { get; set; } = "CodeTags";
    public string InitialCode { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public WorkspaceItemKind Kind { get; set; } = WorkspaceItemKind.Script;
    public List<TestCaseItem> TestCases { get; set; } = new();
}
