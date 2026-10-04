namespace PdfEditorApp.Plugins.CSharpEditor.Models.AI;

/// <summary>
/// Represents an autocomplete mention suggestion in the AI Composer prompt input (e.g. @Files, @Problems, @Docs, @UI).
/// </summary>
public class MentionSuggestionItem
{
    public string Prefix { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconKind { get; set; } = "TagOutline";
    public string Category { get; set; } = "Context";
}
