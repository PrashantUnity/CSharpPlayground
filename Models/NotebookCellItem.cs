using System;

namespace PdfEditorApp.Plugins.CSharpEditor.Models;

public enum CellType
{
    Code,
    Markdown
}

public class NotebookCellItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public CellType Type { get; set; } = CellType.Code;
    public string Source { get; set; } = string.Empty;
    public string OutputText { get; set; } = string.Empty;
    public int? ExecutionCount { get; set; }
    public bool IsExecuting { get; set; }
    public string ExecutionTimeText { get; set; } = string.Empty;
    public bool HasError { get; set; }
    public bool IsMarkdownPreviewMode { get; set; } = false;
    public bool IsInputCollapsed { get; set; } = false;
    public bool IsOutputCollapsed { get; set; } = false;
    public bool IsOutputScrolled { get; set; } = false;

    // Rich Media Outputs
    public byte[]? ImageBytes { get; set; }
    public string? ImageFormat { get; set; }
    public int? ImageWidth { get; set; }
    public int? ImageHeight { get; set; }
    public string? HtmlContent { get; set; }
    public DumpTableSnapshot? TableSnapshot { get; set; }
    public ObjectInspectorSnapshot? InspectorSnapshot { get; set; }

    /// <summary>True if this cell last displayed a live Control (e.g. via Display.Animate/Display.
    /// Control) — the control itself can't be serialized, so this only records the fact that one was
    /// there, letting the UI show a "re-run to restore" placeholder instead of silently showing nothing.</summary>
    public bool HadInteractiveControl { get; set; }

    public bool HasImage => ImageBytes != null && ImageBytes.Length > 0;
    public bool HasHtml => !string.IsNullOrEmpty(HtmlContent);
    public bool HasOutput => !string.IsNullOrEmpty(OutputText) || HasImage || HasHtml || TableSnapshot != null || InspectorSnapshot != null || HadInteractiveControl;
    public bool IsCodeCell => Type == CellType.Code;
    public bool IsMarkdownCell => Type == CellType.Markdown;
}
