using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    [ObservableProperty]
    private bool _isSelected;

    public string AccentBgColor
    {
        get
        {
            if (string.IsNullOrWhiteSpace(AccentColor)) return "#1A38BDF8";
            var hex = AccentColor.TrimStart('#');
            if (hex.Length == 6) return "#1A" + hex;
            if (hex.Length == 8) return "#1A" + hex.Substring(2);
            return "#1A38BDF8";
        }
    }

    public int ArticleCount => Articles.Count;
}

public partial class DocArticle : ObservableObject
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

    [ObservableProperty]
    private bool _isSelected;
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

public partial class DocCodeLanguageVariant : ObservableObject
{
    public string Language { get; set; } = "csharp";
    public string DisplayLabel { get; set; } = "C#";
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    [ObservableProperty]
    private bool _hasSeparator;

    [ObservableProperty]
    private bool _isSelected;
}

public partial class DocCodeSnippet : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    [ObservableProperty]
    private string _code = string.Empty;

    [ObservableProperty]
    private string _language = "csharp";

    public WorkspaceItemKind TargetKind { get; set; } = WorkspaceItemKind.Script;
    public string Category { get; set; } = "General";

    /// <summary>True for a sample that is only for reading or for a studio (it starts a server, needs a package, or needs the user): it gets no Run button.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRunner))]
    private bool _notRunnable;

    /// <summary>The Run button and output under the sample, set by the docs page when the sample's language can run here.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRunner))]
    private PdfEditorApp.Plugins.CSharpEditor.ViewModels.Docs.SnippetRunViewModel? _run;

    /// <summary>True when the sample, in the language tab showing, can be run on the page (it gets a Run button).</summary>
    public bool HasRunner => Run?.CanRun == true;

    partial void OnLanguageChanged(string value) => OnPropertyChanged(nameof(HasRunner));

    public ObservableCollection<DocCodeLanguageVariant> Variants { get; set; } = new();

    public bool HasVariants => Variants.Count > 1;

    [ObservableProperty]
    private DocCodeLanguageVariant? _selectedVariant;

    [RelayCommand]
    public void SelectVariant(DocCodeLanguageVariant? variant)
    {
        if (variant == null) return;
        foreach (var v in Variants)
        {
            v.IsSelected = (v == variant);
        }
        SelectedVariant = variant;
        Code = variant.Code;
        Language = variant.Language;
    }

    public DocCodeSnippet AddVariant(string language, string displayLabel, string code, string description = "")
    {
        if (Variants.Count > 0)
        {
            Variants[^1].HasSeparator = true;
        }

        var variant = new DocCodeLanguageVariant
        {
            Language = language,
            DisplayLabel = displayLabel,
            Code = code.Trim(),
            Description = description,
            HasSeparator = false
        };
        Variants.Add(variant);
        if (Variants.Count == 1 || SelectedVariant == null)
        {
            SelectVariant(variant);
        }
        OnPropertyChanged(nameof(HasVariants));
        return this;
    }

    public void EnsureDefaultSelection()
    {
        if (Variants.Count > 0 && (SelectedVariant == null || string.IsNullOrEmpty(Code)))
        {
            SelectVariant(Variants[0]);
        }
    }
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
