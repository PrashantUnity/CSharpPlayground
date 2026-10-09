using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Models;

public class ChartSeries
{
    public string Name { get; set; } = "Series";
    public string Color { get; set; } = "#4ec9b0";
    public double StrokeThickness { get; set; } = 2.0;

    /// <summary>How this series is drawn when the chart mixes kinds; null: the chart's kind.</summary>
    public ChartType? Kind { get; set; }

    public AxisSide Axis { get; set; } = AxisSide.Left;

    /// <summary>The pile this series stacks in (series with the same name stack together); null: the default pile.</summary>
    public string? StackGroup { get; set; }

    public LineDash Dash { get; set; } = LineDash.Solid;
    public LineInterpolation Interpolation { get; set; } = LineInterpolation.Linear;

    /// <summary>How round a smooth line's curve is, from 0 to 1.</summary>
    public double Tension { get; set; } = 0.4;

    public LineStep Step { get; set; } = LineStep.None;

    /// <summary>Fill down to the baseline; null: filled for an area, not for a line.</summary>
    public bool? Fill { get; set; }

    /// <summary>Fill the space between this series and the series at this index.</summary>
    public int? FillTo { get; set; }

    public PointShape PointStyle { get; set; } = PointShape.Circle;

    /// <summary>The marker radius in pixels; null: the kind's usual size.</summary>
    public double? PointRadius { get; set; }

    /// <summary>The values' own colours colour the line segment that ends at each, not only its marker.</summary>
    public bool ColorSegments { get; set; }

    /// <summary>A bar's rounded outer corners, in pixels; null: the usual.</summary>
    public double? CornerRadius { get; set; }
    public List<ChartDataPoint> Points { get; set; } = new();

    // A missing value (NaN) is a gap in the series, not a value, so none of these count it.
    public double MinY => Values(p => p.Y).DefaultIfEmpty(0).Min();
    public double MaxY => Values(p => p.Y).DefaultIfEmpty(0).Max();
    public double MinX => Values(p => p.X).DefaultIfEmpty(0).Min();
    public double MaxX => Values(p => p.X).DefaultIfEmpty(0).Max();
    public double AverageY => Values(p => p.Y).DefaultIfEmpty(0).Average();
    public double AvgY => AverageY;
    public double SumY => Values(p => p.Y).Sum();

    private IEnumerable<double> Values(Func<ChartDataPoint, double> value) => Points.Select(value).Where(double.IsFinite);
}
