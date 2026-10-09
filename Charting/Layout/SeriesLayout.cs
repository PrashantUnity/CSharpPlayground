using System;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Layout;

/// <summary>A series as a cartesian chart draws it: its kind, its axis, where it stacks and which bar lane it takes.</summary>
internal sealed class SeriesLayout
{
    public required ChartSeries Series { get; init; }

    /// <summary>The series' place in the chart (what <c>fillTo</c> and click events call it).</summary>
    public required int Index { get; init; }

    public required ChartType Kind { get; init; }

    public AxisSide Side => Series.Axis;

    /// <summary>Stacked: where the series starts and ends at each value, in data units (percent stacks in 0 to 100).</summary>
    public double[]? Base { get; set; }

    public double[]? Top { get; set; }

    /// <summary>For a bar: which of a slot's side-by-side bars it is (stacked bars of one pile share one).</summary>
    public int Lane { get; set; }

    public bool IsBar => Kind == ChartType.Bar;
    public bool IsLineLike => Kind is ChartType.Line or ChartType.Area;
    public bool IsStacked => Top != null;

    /// <summary>Where value <paramref name="i"/> is drawn: its stacked top, or its own value.</summary>
    public double ValueAt(int i) => Top is { } top && i < top.Length ? top[i] : Series.Points[i].Y;

    /// <summary>Where value <paramref name="i"/> starts (a bar's foot): the pile under it, its floating start, or <paramref name="baseline"/>.</summary>
    public double BaseAt(int i, double baseline) =>
        Base is { } stacked && i < stacked.Length ? stacked[i] : Series.Points[i].From ?? baseline;

    public bool IsDrawn(int i) => i < Series.Points.Count && double.IsFinite(Series.Points[i].Y) && double.IsFinite(Series.Points[i].X);
}
