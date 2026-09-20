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
}
