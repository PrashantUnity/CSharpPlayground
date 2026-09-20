using Avalonia;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

public class VisualizerHitTestResult
{
    public string Title { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public int? Row { get; set; }
    public int? Col { get; set; }
    public string? NodeId { get; set; }
    public Point CanvasPoint { get; set; }
    public string? ColorHex { get; set; }
}
