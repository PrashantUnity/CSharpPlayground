using System;

namespace PdfEditorApp.Plugins.CSharpEditor.Models;

public enum WorkspaceItemKind
{
    Script,
    Notebook
}

public class WorkspaceItemSummary
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public WorkspaceItemKind Kind { get; set; } = WorkspaceItemKind.Script;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public int ExecutionCount { get; set; }
    public int CellCount { get; set; } // Only relevant for Notebooks
    public string ExecutionMode { get; set; } = "Statements"; // Only relevant for Scripts

    public bool IsNotebook => Kind == WorkspaceItemKind.Notebook;
    public bool IsScript => Kind == WorkspaceItemKind.Script;
    public string KindBadgeText => IsNotebook ? "📓 Notebook" : "📜 Script";
    public string KindBadgeColor => IsNotebook ? "#8B5CF6" : "#10B981"; // Purple for Notebook, Emerald for Script
}
