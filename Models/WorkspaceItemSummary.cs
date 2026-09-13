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

    // 1. Distinct Leading Icon Container Colors
    public string IconForeground => Kind switch
    {
        WorkspaceItemKind.Notebook => IsGraphics ? "#38BDF8" : "#C084FC",
        _ => IsAlgorithms ? "#34D399"
            : IsScratchpad ? "#FBBF24"
            : IsAutomation ? "#F87171"
            : IsGraphics ? "#38BDF8"
            : "#60A5FA"
    };

    public string IconBackground => Kind switch
    {
        WorkspaceItemKind.Notebook => IsGraphics ? "#082F49" : "#261447",
        _ => IsAlgorithms ? "#064E3B"
            : IsScratchpad ? "#451A03"
            : IsAutomation ? "#4C0519"
            : IsGraphics ? "#082F49"
            : "#0F2850"
    };

    public string IconBorder => Kind switch
    {
        WorkspaceItemKind.Notebook => IsGraphics ? "#0284C7" : "#6B21A8",
        _ => IsAlgorithms ? "#059669"
            : IsScratchpad ? "#D97706"
            : IsAutomation ? "#E11D48"
            : IsGraphics ? "#0284C7"
            : "#2563EB"
    };

    // 2. Kind Badge (Script vs Notebook)
    public string KindBadgeForeground => IsNotebook ? "#E9D5FF" : "#BFDBFE";
    public string KindBadgeBackground => IsNotebook ? "#261447" : "#0F244A";
    public string KindBadgeBorder => IsNotebook ? "#6B21A8" : "#2563EB";

    // 3. Category Chip Styling
    public string CategoryForeground => Kind switch
    {
        WorkspaceItemKind.Notebook => IsGraphics ? "#BAE6FD" : "#E9D5FF",
        _ => IsAlgorithms ? "#A7F3D0"
            : IsScratchpad ? "#FDE68A"
            : IsAutomation ? "#FECDD3"
            : IsGraphics ? "#BAE6FD"
            : "#BFDBFE"
    };

    public string CategoryBackground => Kind switch
    {
        WorkspaceItemKind.Notebook => IsGraphics ? "#082F49" : "#261447",
        _ => IsAlgorithms ? "#064E3B"
            : IsScratchpad ? "#451A03"
            : IsAutomation ? "#4C0519"
            : IsGraphics ? "#082F49"
            : "#0F2850"
    };

    public string CategoryBorder => Kind switch
    {
        WorkspaceItemKind.Notebook => IsGraphics ? "#0284C7" : "#6B21A8",
        _ => IsAlgorithms ? "#059669"
            : IsScratchpad ? "#D97706"
            : IsAutomation ? "#E11D48"
            : IsGraphics ? "#0284C7"
            : "#2563EB"
    };

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
}
