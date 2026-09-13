using System;
using System.Collections.Generic;

namespace PdfEditorApp.Plugins.CSharpEditor.Models;

public class ScriptDocumentItem
{
    public string SchemaVersion { get; set; } = "1.0";
    public string Type { get; set; } = "script";
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "Untitled Script";
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string ExecutionMode { get; set; } = "Statements"; // Statements, Program, Expression
    public string Code { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public List<string> References { get; set; } = new();
    public List<TestCaseItem> TestCases { get; set; } = new();
    public List<int> Breakpoints { get; set; } = new();
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public int ExecutionCount { get; set; }
}
