namespace PdfEditorApp.Plugins.CSharpEditor.Models;

public class NotebookVariableInfo
{
    public string Name { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string ValueDisplay { get; set; } = string.Empty;
    public string Kind { get; set; } = "Variable";

    /// <summary>The language whose kernel holds it (e.g. "C#", "Python"), set when a notebook lists every kernel's variables.</summary>
    public string Kernel { get; set; } = string.Empty;
}
