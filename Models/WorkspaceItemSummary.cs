using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Material.Icons;

namespace PdfEditorApp.Plugins.CSharpEditor.Models;

public enum WorkspaceItemKind
{
    Script,
    Notebook
}

public class WorkspaceItemSummary : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public WorkspaceItemKind Kind { get; set; } = WorkspaceItemKind.Script;
    public string FolderPath { get; set; } = string.Empty;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public int ExecutionCount { get; set; }
    public int CellCount { get; set; }
    public string ExecutionMode { get; set; } = "Statements";

    private bool _isPinned;
    public bool IsPinned
    {
        get => _isPinned;
        set => SetProperty(ref _isPinned, value);
    }

    public string DisplayLocation => IsExternal
        ? (!string.IsNullOrEmpty(ExternalWorkspaceName) ? $"Workspace: {ExternalWorkspaceName}" : FolderPath)
        : (!string.IsNullOrEmpty(FolderPath) ? $"~/{FolderPath.TrimStart('/', '\\')}/" : "~/library/");

    public string DisplayLocationTooltip => !string.IsNullOrEmpty(FolderPath)
        ? FolderPath
        : "Internal FryPDF Document Library";

    public bool IsNotebook => Kind == WorkspaceItemKind.Notebook;
    public bool IsScript => Kind == WorkspaceItemKind.Script;
    public string KindLabel => IsNotebook ? "Notebook" : "Script";
    public string KindBadgeText => IsNotebook ? "Notebook" : "Script";
    public string KindBadgeColor => KindBadgeForeground;
    public string RuntimeBadgeText => IsNotebook ? ".NET 10" : "Roslyn C# 13";
    public bool HasRuntimeDot => IsScript;

    public bool IsExternal => !string.IsNullOrEmpty(FolderPath) && Path.IsPathRooted(FolderPath);
    public string ExternalWorkspaceName => IsExternal
        ? (Path.GetFileName(FolderPath.TrimEnd('/', '\\')) is { Length: > 0 } name ? name : FolderPath)
        : string.Empty;

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

    private const string NotebookAccentHex = "#D97706";
    private const string ScriptAccentHex = "#58A6FF";
    private string AccentHex => IsNotebook ? NotebookAccentHex : ScriptAccentHex;

    public string IconForeground => AccentHex;
    public string IconBackground => "#33" + AccentHex.TrimStart('#');
    public string IconBorder => "#66" + AccentHex.TrimStart('#');

    public string KindBadgeForeground => AccentHex;
    public string KindBadgeBackground => "#33" + AccentHex.TrimStart('#');
    public string KindBadgeBorder => "#66" + AccentHex.TrimStart('#');

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

    public string ShortDetailsSnippet => IsNotebook
        ? $"{CellCount} cell{(CellCount == 1 ? "" : "s")}"
        : ExecutionMode;
}
