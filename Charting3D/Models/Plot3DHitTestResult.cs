using Avalonia;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;

public class Plot3DHitTestResult
{
    public Point3D? Point { get; set; }
    public Graph3DNode? Node { get; set; }
    public Series3D? Series { get; set; }
    public Point CanvasPosition { get; set; }
    public string DisplayText { get; set; } = string.Empty;
    public double DistanceSquared { get; set; }
}
