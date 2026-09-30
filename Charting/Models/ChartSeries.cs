using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Models;

public class ChartSeries
{
    public string Name { get; set; } = "Series";
    public string Color { get; set; } = "#4ec9b0";
    public double StrokeThickness { get; set; } = 2.0;
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
