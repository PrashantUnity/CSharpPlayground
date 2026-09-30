using System.Collections.Generic;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Models;

public class ChartOptions
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public ChartType Type { get; set; } = ChartType.Line;
    public string PrimaryColor { get; set; } = "#4ec9b0";
    public bool ShowGrid { get; set; } = true;
    public bool ShowPoints { get; set; } = true;
    public bool ShowStats { get; set; } = true;
    public bool ShowLegend { get; set; } = false;
    public double Width { get; set; } = 560;
    public double Height { get; set; } = 280;
    public List<ChartSeries> Series { get; set; } = new();

    public string? XAxisTitle { get; set; }
    public string? YAxisTitle { get; set; }

    /// <summary>A fixed range for an axis; worked out from the data when null.</summary>
    public double? XMin { get; set; }
    public double? XMax { get; set; }
    public double? YMin { get; set; }
    public double? YMax { get; set; }

    /// <summary>A line for the header when not everything is drawn, e.g. "showing the first 200,000 of 350,000 values".</summary>
    public string? Notice { get; set; }
}
