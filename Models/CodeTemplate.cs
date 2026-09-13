using System.Collections.Generic;
using Material.Icons;

namespace PdfEditorApp.Plugins.CSharpEditor.Models;

public class CodeTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string Description { get; set; } = string.Empty;
    public MaterialIconKind IconKind { get; set; } = MaterialIconKind.CodeTags;
    public string InitialCode { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public WorkspaceItemKind Kind { get; set; } = WorkspaceItemKind.Script;
    public List<TestCaseItem> TestCases { get; set; } = new();

    public string AccentColor { get; set; } = "#0EA5E9";
    public string AccentBackground { get; set; } = "#082F49";
    public string AccentBorder { get; set; } = "#0284C7";
    public string CategoryBadge { get; set; } = "Preset";
    public List<string> Tags { get; set; } = new();

    public bool IsNotebook => Kind == WorkspaceItemKind.Notebook;
    public string KindBadgeText => IsNotebook ? "Notebook" : "Script";
}
