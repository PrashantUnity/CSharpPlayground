using System;
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

    /// <summary>The script's language id; C# unless it's a plain source file of another language.</summary>
    public string LanguageId { get; set; } = Services.Languages.LanguageIds.CSharp;

    /// <summary>E.g. "Python", shown on badges; empty for C#.</summary>
    public string LanguageName { get; set; } = string.Empty;

    /// <summary>The file's extension with the dot. Set by storage; the .frycs/.frynb default fits documents.</summary>
    public string? FileExtension { get; set; }

    /// <summary>True for a plain source file (main.py) rather than a .frycs/.frynb document.</summary>
    public bool IsSourceFile { get; set; }

    public string DisplayExtension => FileExtension ?? (IsNotebook ? ".frynb" : ".frycs");

    private bool _isPinned;
    public bool IsPinned
    {
        get => _isPinned;
        set => SetProperty(ref _isPinned, value);
    }

    /// <summary>Set by the storage service: true when this item's active workspace root is an opened external folder rather than the internal library.</summary>
    public bool IsExternalRoot { get; set; }
    /// <summary>Set by the storage service: the opened folder's name, when <see cref="IsExternalRoot"/> is true.</summary>
    public string? WorkspaceRootName { get; set; }

    public string DisplayLocation => IsExternal
        ? (!string.IsNullOrEmpty(ExternalWorkspaceName) ? $"Workspace: {ExternalWorkspaceName}{(!string.IsNullOrEmpty(FolderPath) ? $"/{FolderPath}" : string.Empty)}" : FolderPath)
        : (!string.IsNullOrEmpty(FolderPath) ? $"~/{FolderPath.TrimStart('/', '\\')}/" : "~/library/");

    public string DisplayLocationTooltip => DisplayLocation;

    public bool IsNotebook => Kind == WorkspaceItemKind.Notebook;
    public bool IsScript => Kind == WorkspaceItemKind.Script;
    public string KindLabel => IsNotebook ? "Notebook" : "Script";

    /// <summary>
    /// Language-qualified badge text shown in the TYPE column.
    /// e.g. "Python Script", "Java Script", "JavaScript Script", "Notebook", "C# Script".
    /// </summary>
    public string KindBadgeText => IsNotebook
        ? (!string.IsNullOrEmpty(LanguageName) ? $"{LanguageName} Notebook" : "Notebook")
        : (!string.IsNullOrEmpty(LanguageName) ? $"{LanguageName} Script" : "C# Script");

    public string KindBadgeColor => KindBadgeForeground;
    public string RuntimeBadgeText => IsNotebook ? ".NET 10" : IsSourceFile ? LanguageName : "Roslyn C# 13";
    public bool HasRuntimeDot => IsScript;

    public bool IsExternal => IsExternalRoot;
    public string ExternalWorkspaceName => IsExternalRoot ? (WorkspaceRootName ?? string.Empty) : string.Empty;

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

    /// <summary>Language-specific icon. Non-C# source files use their own language icon; C# uses category heuristics.</summary>
    public MaterialIconKind IconKind => LanguageId switch
    {
        Services.Languages.LanguageIds.Python => IsNotebook ? MaterialIconKind.NotebookOutline : MaterialIconKind.LanguagePython,
        Services.Languages.LanguageIds.Java => IsNotebook ? MaterialIconKind.NotebookOutline : MaterialIconKind.LanguageJava,
        Services.Languages.LanguageIds.JavaScript => IsNotebook ? MaterialIconKind.NotebookOutline : MaterialIconKind.LanguageJavascript,
        Services.Languages.LanguageIds.Cpp => IsNotebook ? MaterialIconKind.NotebookOutline : MaterialIconKind.LanguageCpp,
        Services.Languages.LanguageIds.Go => IsNotebook ? MaterialIconKind.NotebookOutline : MaterialIconKind.LanguageGo,
        Services.Languages.LanguageIds.FSharp => IsNotebook ? MaterialIconKind.NotebookOutline : MaterialIconKind.FunctionVariant,
        Services.Languages.LanguageIds.Sql => IsNotebook ? MaterialIconKind.NotebookOutline : MaterialIconKind.Database,
        Services.Languages.LanguageIds.Rust => IsNotebook ? MaterialIconKind.NotebookOutline : MaterialIconKind.LanguageRust,
        _ => Kind switch
        {
            WorkspaceItemKind.Notebook => IsGraphics ? MaterialIconKind.ImageOutline : MaterialIconKind.NotebookOutline,
            _ => IsAlgorithms ? MaterialIconKind.CodeBraces
                : IsScratchpad ? MaterialIconKind.LightningBoltOutline
                : IsAutomation ? MaterialIconKind.FilePdfBox
                : IsGraphics ? MaterialIconKind.ImageOutline
                : MaterialIconKind.CodeBraces
        }
    };

    // Language-specific accent palettes
    private const string NotebookAccentHex = "#D97706";   // amber  — notebooks
    private const string CSharpAccentHex   = "#58A6FF";   // blue   — C#
    private const string PythonAccentHex   = "#3AC97E";   // green  — Python
    private const string JavaAccentHex     = "#F89820";   // orange — Java
    private const string JsAccentHex       = "#F1D04B";   // yellow — JavaScript
    private const string CppAccentHex      = "#659AD2";   // blue   — C++
    private const string GoAccentHex       = "#00ADD8";   // cyan   — Go
    private const string FSharpAccentHex   = "#30B9DB";   // cyan/blue — F#
    private const string SqlAccentHex      = "#F29111";   // orange/gold — SQL
    private const string RustAccentHex     = "#DEA584";   // tan — Rust

    private string AccentHex => LanguageId switch
    {
        Services.Languages.LanguageIds.Python     => IsNotebook ? NotebookAccentHex : PythonAccentHex,
        Services.Languages.LanguageIds.Java       => IsNotebook ? NotebookAccentHex : JavaAccentHex,
        Services.Languages.LanguageIds.JavaScript => IsNotebook ? NotebookAccentHex : JsAccentHex,
        Services.Languages.LanguageIds.Cpp        => IsNotebook ? NotebookAccentHex : CppAccentHex,
        Services.Languages.LanguageIds.Go         => IsNotebook ? NotebookAccentHex : GoAccentHex,
        Services.Languages.LanguageIds.FSharp     => IsNotebook ? NotebookAccentHex : FSharpAccentHex,
        Services.Languages.LanguageIds.Sql        => IsNotebook ? NotebookAccentHex : SqlAccentHex,
        Services.Languages.LanguageIds.Rust       => IsNotebook ? NotebookAccentHex : RustAccentHex,
        _ => IsNotebook ? NotebookAccentHex : CSharpAccentHex
    };

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
        : IsSourceFile ? $"{LanguageName} script" : $"{ExecutionMode} mode • Roslyn C# 13";

    public string ShortDetailsSnippet => IsNotebook
        ? $"{CellCount} cell{(CellCount == 1 ? "" : "s")}"
        : IsSourceFile ? LanguageName : ExecutionMode;
}
