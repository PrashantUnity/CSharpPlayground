using System;
using System.Collections.Generic;

namespace PdfEditorApp.Plugins.CSharpEditor.Models;

public class ScriptProjectItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "Untitled Script";
    public string Description { get; set; } = "C# script automation";
    public string Code { get; set; } = string.Empty;
    public string ExecutionMode { get; set; } = "Statements"; // "Statements", "Program", "Expression"
    public bool IsNotebook { get; set; } = false;
    public List<NotebookCellItem> Cells { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public int ExecutionCount { get; set; }
    public string Category { get; set; } = "Custom";
    public string Tag { get; set; } = "Draft";

    public int LinesOfCode => string.IsNullOrEmpty(Code) ? 0 : Code.Split('\n').Length;
}
