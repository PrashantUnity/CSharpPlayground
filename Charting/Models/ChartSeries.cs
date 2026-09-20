using System.Collections.Generic;
using System.Linq;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Models;

public class ChartSeries
{
    public string Name { get; set; } = "Series";
    public string Color { get; set; } = "#4ec9b0";
    public double StrokeThickness { get; set; } = 2.0;
    public List<ChartDataPoint> Points { get; set; } = new();

    public double MinY => Points.Count > 0 ? Points.Min(p => p.Y) : 0;
    public double MaxY => Points.Count > 0 ? Points.Max(p => p.Y) : 0;
    public double MinX => Points.Count > 0 ? Points.Min(p => p.X) : 0;
    public double MaxX => Points.Count > 0 ? Points.Max(p => p.X) : 0;
    public double AverageY => Points.Count > 0 ? Points.Average(p => p.Y) : 0;
    public double AvgY => AverageY;
    public double SumY => Points.Count > 0 ? Points.Sum(p => p.Y) : 0;
}
