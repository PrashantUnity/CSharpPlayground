namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Models;

/// <summary>What a chart's axis shows: its name, its range (fixed, or only suggested), how values map to positions, and which way it runs.</summary>
public class ChartAxisOptions
{
    public string? Title { get; set; }

    /// <summary>A fixed end of the axis; worked out from the data when null.</summary>
    public double? Min { get; set; }

    public double? Max { get; set; }

    /// <summary>How far the axis goes at least, when the data doesn't reach it (a fixed <see cref="Min"/> or <see cref="Max"/> wins).</summary>
    public double? SuggestedMin { get; set; }

    public double? SuggestedMax { get; set; }

    public AxisScale Scale { get; set; } = AxisScale.Linear;

    /// <summary>The axis runs from its largest end (x right to left, y top to bottom).</summary>
    public bool Reverse { get; set; }
}
