using System.Text.Json.Serialization;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Spec;

/// <summary>
/// A visual as data: what a chart, 3D plot or data-structure visualizer shows, in a form every language can write as
/// JSON and the studio can save, send and redraw. It says what to draw, not how: the studio works out bounds, layout,
/// colours and everything left out.
/// </summary>
public abstract class VisualSpec
{
    protected VisualSpec(VisualFamily family) => Family = family;

    /// <summary>Which of the three kinds of visual this is (it decides the MIME type, so it isn't written in the JSON).</summary>
    [JsonIgnore]
    public VisualFamily Family { get; }

    public string? Title { get; set; }

    /// <summary>A line under the title.</summary>
    public string? Subtitle { get; set; }

    /// <summary>The widest the visual is drawn, in pixels; it still fits narrower space. Left out, it takes the width there is.</summary>
    public double? Width { get; set; }

    /// <summary>The drawing's height in pixels, under the header. Left out, the studio picks one for the kind.</summary>
    public double? Height { get; set; }
}

/// <summary>Anything that can describe itself as a visual: the trackers and recorders, and the older chart, 3D and visualizer models.</summary>
public interface IVisualSource
{
    VisualSpec ToVisualSpec();
}

/// <summary>A group of optional settings; one with nothing set is left out of the JSON.</summary>
public interface IOptionalSpec
{
    [JsonIgnore]
    bool IsEmpty { get; }
}

/// <summary>An axis: its title, and the range to show, kept exactly (worked out from the data when left out).</summary>
public sealed class AxisSpec : IOptionalSpec
{
    public string? Title { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }

    [JsonIgnore]
    public bool IsEmpty => Title == null && Min == null && Max == null;
}

/// <summary>The series legend. Left out, it shows when there is more than one series (a pie or donut always lists its slices).</summary>
public sealed class LegendSpec : IOptionalSpec
{
    public bool? Show { get; set; }

    [JsonIgnore]
    public bool IsEmpty => Show == null;
}

/// <summary>A span of values, such as the x range a surface covers.</summary>
public sealed class RangeSpec
{
    public RangeSpec() { }

    public RangeSpec(double min, double max)
    {
        Min = min;
        Max = max;
    }

    [JsonRequired]
    public double Min { get; set; }
    [JsonRequired]
    public double Max { get; set; }
}
