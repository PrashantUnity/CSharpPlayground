using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Material.Icons;

namespace PdfEditorApp.Plugins.CSharpEditor.Models;

public enum DocCalloutType
{
    None,
    Info,
    Tip,
    Warning
}

public partial class DocCategory : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public MaterialIconKind IconKind { get; set; } = MaterialIconKind.BookOpenPageVariantOutline;
    public string AccentColor { get; set; } = "#38BDF8";
    public string Badge { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<DocArticle> Articles { get; set; } = new();

    [ObservableProperty]
    private bool _isExpanded;
}

public class DocArticle
{
    public string Id { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string ReadingTime { get; set; } = "3 min read";
    public string Summary { get; set; } = string.Empty;
    public List<DocSection> Sections { get; set; } = new();
    public List<DocCodeSnippet> CodeSnippets { get; set; } = new();
    public List<DocApiSignature> ApiSignatures { get; set; } = new();
    public List<DocShortcutItem> Shortcuts { get; set; } = new();
    public List<string> Keywords { get; set; } = new();

    public bool HasCodeSnippets => CodeSnippets.Count > 0;
    public bool HasApiSignatures => ApiSignatures.Count > 0;
    public bool HasShortcuts => Shortcuts.Count > 0;
}

public class DocSection
{
    public string Heading { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DocCalloutType CalloutType { get; set; } = DocCalloutType.None;
    public string? CalloutText { get; set; }
    public List<string> BulletPoints { get; set; } = new();

    public bool HasCallout => CalloutType != DocCalloutType.None && !string.IsNullOrEmpty(CalloutText);
    public bool HasBullets => BulletPoints.Count > 0;
}

public class DocCodeSnippet
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Language { get; set; } = "csharp";
    public WorkspaceItemKind TargetKind { get; set; } = WorkspaceItemKind.Script;
    public string Category { get; set; } = "General";
}

public class DocApiSignature
{
    public string MethodName { get; set; } = string.Empty;
    public string ReturnType { get; set; } = string.Empty;
    public string Parameters { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ReturnDescription { get; set; }
    public string? ExampleCode { get; set; }

    public string FullSignature => $"{ReturnType} {MethodName}({Parameters})";
}

public class DocShortcutItem
{
    public string Action { get; set; } = string.Empty;
    public string MacKey { get; set; } = string.Empty;
    public string WinKey { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
}
