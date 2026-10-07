using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

/// <summary>
/// A 3D plot being put together; start one with <see cref="Charts"/>. Z points up in every kind. Each setting is optional:
/// what is left out is the studio's default, the same in every language.
/// </summary>
public sealed class Plot3DBuilder : VisualBuilder<Plot3DBuilder, Plot3DSpec>
{
    internal Plot3DBuilder(Plot3DSpec spec) : base(spec) { }

    /// <summary>A name for the x axis.</summary>
    public Plot3DBuilder XLabel(string label) => Edit(s => s.XAxis.Title = label);

    /// <summary>A name for the y axis.</summary>
    public Plot3DBuilder YLabel(string label) => Edit(s => s.YAxis.Title = label);

    /// <summary>A name for the upward axis.</summary>
    public Plot3DBuilder ZLabel(string label) => Edit(s => s.ZAxis.Title = label);

    /// <summary>The colour of the first series, a CSS colour such as <c>"#4ec9b0"</c>.</summary>
    public Plot3DBuilder Color(string color) => Edit(s => s.Color = color);

    /// <summary>The colour scale for heights and values.</summary>
    public Plot3DBuilder ColorMap(ColorMapPreset colorMap) => Edit(s => s.ColorMap = colorMap);

    /// <summary>Turns the camera slowly around the plot.</summary>
    public Plot3DBuilder AutoRotate(bool on = true) => Edit(s => s.AutoRotate = on);

    /// <summary>Shows or hides the x, y and z axes.</summary>
    public Plot3DBuilder Axes(bool show = true) => Edit(s => s.ShowAxes = show);

    /// <summary>Shows or hides the floor grid and the box around the data.</summary>
    public Plot3DBuilder Grid(bool show = true) => Edit(s => s.ShowGrid = show);

    /// <summary>Draws the surface as a wireframe, or as a solid again.</summary>
    public Plot3DBuilder Wireframe(bool on = true) => Edit(s =>
    {
        if (s.Surface == null) throw new InvalidOperationException("Only a surface has a wireframe: start with Charts.Surface(...).");
        s.Kind = on ? Plot3DType.Wireframe : Plot3DType.Surface;
    });

    /// <summary>Adds a series of points: (x, y, z) tuples, three-number arrays or records with x, y and z.</summary>
    public Plot3DBuilder Series(string name, object points, string? color = null)
    {
        ArgumentNullException.ThrowIfNull(points);
        var added = Plot3DSpecBuilder.From(points, Spec.Kind).Series;
        if (added.Count == 1)
        {
            added[0].Name = name;
            if (color != null) added[0].Color = color;
        }

        return Edit(s => s.Series.AddRange(added));
    }

    /// <summary>A label for each point of the last series, shown when the point is hovered.</summary>
    public Plot3DBuilder Labels(IEnumerable<string?> labels) => Edit(s =>
    {
        if (s.Series.Count == 0) throw new InvalidOperationException("There are no points to label yet: give the plot its points first.");
        s.Series[^1].Labels = [.. labels];
    });
}
