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
    public string FolderPath { get; set; } = string.Empty; // "" = root; e.g. "Reports/Q1" — computed from real storage, never persisted in the document itself
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public int ExecutionCount { get; set; }
    public int CellCount { get; set; } // Only relevant for Notebooks
    public string ExecutionMode { get; set; } = "Statements"; // Only relevant for Scripts

    public bool IsNotebook => Kind == WorkspaceItemKind.Notebook;
    public bool IsScript => Kind == WorkspaceItemKind.Script;
    public string KindLabel => IsNotebook ? "Notebook" : "Script";
    public string KindBadgeText => IsNotebook ? "Notebook" : "Script";
    public string KindBadgeColor => KindBadgeForeground;

    // Category & Domain Pattern Recognition
    private bool IsAlgorithms =>
        Category.Equals("Algorithms", StringComparison.OrdinalIgnoreCase) ||
        Title.Contains("Two Sum", StringComparison.OrdinalIgnoreCase) ||
        Title.Contains("Algorithm", StringComparison.OrdinalIgnoreCase) ||
        Title.Contains("LeetCode", StringComparison.OrdinalIgnoreCase);

    private bool IsScratchpad =>
        Category.Equals("Scratchpad", StringComparison.OrdinalIgnoreCase) ||
        Title.Contains("Scratchpad", StringComparison.OrdinalIgnoreCase) ||
        Title.Contains(".Dump", StringComparison.OrdinalIgnoreCase);

    private bool IsAutomation =>
        Category.Equals("Automation", StringComparison.OrdinalIgnoreCase) ||
        Title.Contains("PDF", StringComparison.OrdinalIgnoreCase) ||
        Title.Contains("Automation", StringComparison.OrdinalIgnoreCase) ||
        Title.Contains("Document", StringComparison.OrdinalIgnoreCase);

    private bool IsGraphics =>
        Category.Equals("Graphics", StringComparison.OrdinalIgnoreCase) ||
        Title.Contains("Graphics", StringComparison.OrdinalIgnoreCase) ||
        Title.Contains("SkiaSharp", StringComparison.OrdinalIgnoreCase) ||
        Title.Contains("Image", StringComparison.OrdinalIgnoreCase);

    public MaterialIconKind IconKind => Kind switch
    {
        WorkspaceItemKind.Notebook => IsGraphics ? MaterialIconKind.ImageOutline : MaterialIconKind.NotebookOutline,
        _ => IsAlgorithms ? MaterialIconKind.CodeBraces
            : IsScratchpad ? MaterialIconKind.LightningBoltOutline
            : IsAutomation ? MaterialIconKind.FilePdfBox
            : IsGraphics ? MaterialIconKind.ImageOutline
            : MaterialIconKind.FileCodeOutline
    };

    // Rows differentiate by icon shape and text, not by a different accent color per category/kind —
    // one consistent, muted palette (this app's own M3Primary/M3SurfaceContainerHigh/M3OutlineVariant
    // dark-theme values) everywhere instead.
    public string IconForeground => "#9BA1AD";
    public string IconBackground => "#252C36";
    public string IconBorder => "#3D4450";

    public string KindBadgeForeground => "#9BA1AD";
    public string KindBadgeBackground => "#252C36";
    public string KindBadgeBorder => "#3D4450";

    public string CategoryForeground => "#9BA1AD";
    public string CategoryBackground => "#252C36";
    public string CategoryBorder => "#3D4450";

    public string AccentColor => IconForeground;

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

    // Compact-row variant of DetailsSnippet: every item in the workspace uses the same Roslyn/
    // Interactive-C# engine, so repeating that suffix on every single row is pure noise once there
    // are more than a couple of items — it's still available via DetailsSnippet for detail-on-demand
    // UI (e.g. an inspector panel) where the extra context is welcome rather than repetitive.
    public string ShortDetailsSnippet => IsNotebook
        ? $"{CellCount} cell{(CellCount == 1 ? "" : "s")}"
        : ExecutionMode;
}
