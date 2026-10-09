using System.Collections.Generic;
using Avalonia;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Models;

/// <summary>One line of a tooltip that lists every series at a place: its text, its colour and where its value is drawn.</summary>
public sealed record ChartTooltipRow(string Text, string Color, Point Position);

public class ChartHitTestResult
{
    public ChartDataPoint Point { get; }
    public ChartSeries Series { get; }
    public Point CanvasPosition { get; }
    public string DisplayText { get; }

    /// <summary>The place all <see cref="Rows"/> are at (a category, a date, a number), as the tooltip's title.</summary>
    public string? Header { get; init; }

    /// <summary>When several series have a value where the pointer is: each one's, in series order. Null for a single value.</summary>
    public IReadOnlyList<ChartTooltipRow>? Rows { get; init; }

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
