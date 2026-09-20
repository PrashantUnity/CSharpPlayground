using Avalonia;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Models;

public class ChartHitTestResult
{
    public ChartDataPoint Point { get; }
    public ChartSeries Series { get; }
    public Point CanvasPosition { get; }
    public string DisplayText { get; }

    public ChartHitTestResult(ChartDataPoint point, ChartSeries series, Point canvasPosition, string? displayText = null)
    {
        Point = point;
        Series = series;
        CanvasPosition = canvasPosition;
        DisplayText = displayText ?? (string.IsNullOrEmpty(point.Label)
            ? $"X: {point.X:0.##} • Y: {point.GetFormattedY()}"
            : $"{point.Label}: {point.GetFormattedY()}");
    }
}
