using System;
using System.Collections.Generic;

namespace PdfEditorApp.Plugins.CSharpEditor.Models;

public class NotebookDocumentItem
{
    public string SchemaVersion { get; set; } = "1.0";
    public string Type { get; set; } = "notebook";
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "Untitled Notebook";
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "Interactive";
    public string Kernel { get; set; } = "csharp";
    public List<NotebookCellItem> Cells { get; set; } = new();
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public int ExecutionCount { get; set; }
}
