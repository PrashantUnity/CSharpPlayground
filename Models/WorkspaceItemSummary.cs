using System;
using Material.Icons;

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
    public string KindLabel => IsNotebook ? "Notebook" : "Script";
    public string KindBadgeText => IsNotebook ? "Notebook" : "Script";
    public string KindBadgeColor => IsNotebook ? "#0284C7" : "#10B981";

    public MaterialIconKind IconKind => Kind switch
    {
        WorkspaceItemKind.Notebook => MaterialIconKind.NotebookOutline,
        _ => Category switch
        {
            "Algorithms" => MaterialIconKind.CodeBraces,
            "LINQPad" => MaterialIconKind.LightningBoltOutline,
            "Automation" => MaterialIconKind.FilePdfBox,
            _ => MaterialIconKind.FileCodeOutline
        }
    };

    public string IconForeground => Kind switch
    {
        WorkspaceItemKind.Notebook => "#38BDF8",
        _ => Category switch
        {
            "Algorithms" => "#FBBF24",
            "LINQPad" => "#2DD4BF",
            "Automation" => "#F87171",
            _ => "#34D399"
        }
    };

    public string IconBackground => Kind switch
    {
        WorkspaceItemKind.Notebook => "#082F49",
        _ => Category switch
        {
            "Algorithms" => "#301B05",
            "LINQPad" => "#042F2E",
            "Automation" => "#350E0E",
            _ => "#062E22"
        }
    };

    public string IconBorder => Kind switch
    {
        WorkspaceItemKind.Notebook => "#0284C7",
        _ => Category switch
        {
            "Algorithms" => "#92400E",
            "LINQPad" => "#0D9488",
            "Automation" => "#991B1B",
            _ => "#065F46"
        }
    };

    public string FormattedLastModified
    {
        get
        {
            var diff = DateTime.UtcNow - LastModified;
            if (diff.TotalMinutes < 1) return "Just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
            return LastModified.ToString("MMM dd, yyyy");
        }
    }

    public string DetailsSnippet => IsNotebook
        ? $"{CellCount} cell{(CellCount == 1 ? "" : "s")} • Interactive C#"
        : $"{ExecutionMode} mode • Roslyn C# 13";
}
