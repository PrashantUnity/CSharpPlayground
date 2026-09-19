namespace PdfEditorApp.Plugins.CSharpEditor.Models;

public class SearchResultItem
{
    public int LineNumber { get; set; }
    public int Column { get; set; }
    public int Length { get; set; }
    public string LineText { get; set; } = string.Empty;
    public string FormattedLocation => $"Ln {LineNumber}, Col {Column}";
}
