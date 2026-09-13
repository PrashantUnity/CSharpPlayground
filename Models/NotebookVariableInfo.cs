namespace PdfEditorApp.Plugins.CSharpEditor.Models;

public class NotebookVariableInfo
{
    public string Name { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string ValueDisplay { get; set; } = string.Empty;
    public string Kind { get; set; } = "Variable";
}
