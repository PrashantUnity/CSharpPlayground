namespace PdfEditorApp.Plugins.CSharpEditor.Models;

public class AssemblyReferenceItem
{
    public string Name { get; set; } = string.Empty;
    public string AssemblyPath { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public bool IsSystem { get; set; } = true;
}
