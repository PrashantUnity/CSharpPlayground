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
    public LegendPosition LegendPosition { get; set; } = LegendPosition.Bottom;
    public double Width { get; set; } = 560;
    public double Height { get; set; } = 280;
    public List<ChartSeries> Series { get; set; } = new();

    public ChartOrientation Orientation { get; set; } = ChartOrientation.Vertical;
    public ChartStack Stack { get; set; } = ChartStack.None;

    /// <summary>Where a pie, donut or polar area starts, in degrees clockwise from the top.</summary>
    public double StartAngle { get; set; }

    /// <summary>How far round it goes, in degrees (180 is a half circle).</summary>
    public double Sweep { get; set; } = 360;

    /// <summary>A donut's empty middle, as a share of its radius.</summary>
    public double Cutout { get; set; } = 0.55;

    public ChartAxisOptions XAxis { get; set; } = new();
    public ChartAxisOptions YAxis { get; set; } = new();

    /// <summary>The value axis up the right side, for the series measured on it; null when none is.</summary>
    public ChartAxisOptions? Y2Axis { get; set; }

    public string? XAxisTitle { get => XAxis.Title; set => XAxis.Title = value; }
    public string? YAxisTitle { get => YAxis.Title; set => YAxis.Title = value; }

    /// <summary>A fixed range for an axis; worked out from the data when null.</summary>
    public double? XMin { get => XAxis.Min; set => XAxis.Min = value; }
    public double? XMax { get => XAxis.Max; set => XAxis.Max = value; }
    public double? YMin { get => YAxis.Min; set => YAxis.Min = value; }
    public double? YMax { get => YAxis.Max; set => YAxis.Max = value; }

    /// <summary>Indexes of series the viewer has hidden (a view setting, never part of the spec); null: none. They are not drawn and don't count towards the axes.</summary>
    public ISet<int>? HiddenSeries { get; set; }

    /// <summary>A copy that draws the same series (shared, not copied) with the view's own choices of kind, grid and hidden series.</summary>
    public ChartOptions WithView(ChartType type, bool showGrid, ISet<int>? hiddenSeries, ChartStack stack)
    {
        var copy = (ChartOptions)MemberwiseClone();
        copy.Type = type;
        copy.ShowGrid = showGrid;
        copy.Stack = stack;
        copy.HiddenSeries = hiddenSeries is { Count: > 0 } ? hiddenSeries : null;
        return copy;
    }

    /// <summary>A line for the header when not everything is drawn, e.g. "showing the first 200,000 of 350,000 values".</summary>
    public string? Notice { get; set; }
}
