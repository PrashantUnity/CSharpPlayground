using System.Collections;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

/// <summary>
/// Charts and 3D plots, put together one setting at a time and then shown:
/// <code>
/// Charts.Line(sales).Title("Sales").XLabel("Month").YLabel("USD").Show();
/// Charts.Surface((x, y) => Math.Sin(x) * Math.Cos(y)).ColorMap(ColorMapPreset.Plasma).Show();
/// </code>
/// It draws what the same call on <see cref="Display"/> draws; use whichever reads better. Returned as a cell's last
/// value, a chart shows without <c>.Show()</c>.
/// </summary>
public static class Charts
{
    // 2D

    /// <summary>A line chart of numbers, [x, y] pairs, a label → number map or a name → sequence map (a series each).</summary>
    public static ChartBuilder Line(object data, string? name = null) => Chart(data, ChartType.Line, name);

    /// <summary>An area chart: a line with the space under it filled.</summary>
    public static ChartBuilder Area(object data, string? name = null) => Chart(data, ChartType.Area, name);

    /// <summary>A bar chart.</summary>
    public static ChartBuilder Bar(object data, string? name = null) => Chart(data, ChartType.Bar, name);

    /// <summary>A scatter chart of [x, y] pairs.</summary>
    public static ChartBuilder Scatter(object data, string? name = null) => Chart(data, ChartType.Scatter, name);

    /// <summary>A pie chart: each label → number is a slice.</summary>
    public static ChartBuilder Pie(object data) => Chart(data, ChartType.Pie, null);

    /// <summary>A donut chart: a pie with a hole.</summary>
    public static ChartBuilder Donut(object data) => Chart(data, ChartType.Donut, null);

    /// <summary>A histogram of the samples (or of each sequence in a name → sequence map); <c>.Bins(n)</c> sets how many bars.</summary>
    public static ChartBuilder Histogram(object samples, string? name = null) => Named(new ChartBuilder(ChartSpecBuilder.Histogram(samples)), name);

    /// <summary>A line chart of records: each at <paramref name="x"/> (a number, or a label) with the value <paramref name="y"/>.</summary>
    public static ChartBuilder Line<T>(IEnumerable<T> records, Func<T, object?> x, Func<T, object?> y, string? name = null) => Chart(records, x, y, ChartType.Line, name);

    /// <summary>An area chart of records.</summary>
    public static ChartBuilder Area<T>(IEnumerable<T> records, Func<T, object?> x, Func<T, object?> y, string? name = null) => Chart(records, x, y, ChartType.Area, name);

    /// <summary>A bar chart of records.</summary>
    public static ChartBuilder Bar<T>(IEnumerable<T> records, Func<T, object?> x, Func<T, object?> y, string? name = null) => Chart(records, x, y, ChartType.Bar, name);

    /// <summary>A scatter chart of records.</summary>
    public static ChartBuilder Scatter<T>(IEnumerable<T> records, Func<T, object?> x, Func<T, object?> y, string? name = null) => Chart(records, x, y, ChartType.Scatter, name);

    /// <summary>A pie chart of records: each is a slice labelled by <paramref name="x"/> as big as <paramref name="y"/>.</summary>
    public static ChartBuilder Pie<T>(IEnumerable<T> records, Func<T, object?> x, Func<T, object?> y) => Chart(records, x, y, ChartType.Pie, null);

    /// <summary>A donut chart of records.</summary>
    public static ChartBuilder Donut<T>(IEnumerable<T> records, Func<T, object?> x, Func<T, object?> y) => Chart(records, x, y, ChartType.Donut, null);

    /// <summary>A line chart of several named series: <c>Charts.Line(("Sales", sales), ("Costs", costs))</c>.</summary>
    public static ChartBuilder Line(params (string Name, IEnumerable<double> Values)[] series) => Several(ChartType.Line, series);

    /// <summary>An area chart of several named series.</summary>
    public static ChartBuilder Area(params (string Name, IEnumerable<double> Values)[] series) => Several(ChartType.Area, series);

    /// <summary>A bar chart of several named series, side by side (or piled up with <c>.Stacked()</c>).</summary>
    public static ChartBuilder Bar(params (string Name, IEnumerable<double> Values)[] series) => Several(ChartType.Bar, series);

    /// <summary>A bubble chart of (x, y, size) items: tuples, three-number arrays or records with x, y and a size. The size is the circle's radius in pixels.</summary>
    public static ChartBuilder Bubble(object data, string? name = null) => Chart(data, ChartType.Bubble, name);

    /// <summary>A radar chart: a polygon for each series over spokes named by <paramref name="labels"/> (or by the labels of the data).</summary>
    public static ChartBuilder Radar(object data, IEnumerable<string>? labels = null)
    {
        var builder = Chart(data, ChartType.Radar, null);
        return labels == null ? builder : builder.Labels(labels);
    }

    /// <summary>A polar area chart: a pie whose slices all take the same angle and reach as far out as their value.</summary>
    public static ChartBuilder PolarArea(object data) => Chart(data, ChartType.PolarArea, null);

    /// <summary>A half circle gauge showing <paramref name="value"/> out of <paramref name="max"/>.</summary>
    public static ChartBuilder Gauge(double value, double max = 100, string? title = null)
    {
        var shown = Math.Clamp(value, 0, max);
        var spec = new ChartSpec { Kind = ChartType.Donut, StartAngle = -90, Sweep = 180, Cutout = 0.7, Title = title };
        spec.Series.Add(new ChartSeriesSpec { Y = [shown, max - shown], Labels = ["Value", "Remaining"], Colors = ["#4ec9b0", "#2a3342"] });
        return new ChartBuilder(spec);
    }

    private static ChartBuilder Several(ChartType kind, (string Name, IEnumerable<double> Values)[] series) =>
        new(ChartSpecBuilder.FromSeries(kind, series.Select(s => (s.Name, (IEnumerable)s.Values))));

    // 3D, z up

    /// <summary>Points in 3D: (x, y, z) tuples, three-number arrays, records with x, y and z, or a name → points map (a series each).</summary>
    public static Plot3DBuilder Scatter3D(object data) => new(Plot3DSpecBuilder.From(data, Plot3DType.Scatter));

    /// <summary>Points in 3D, from records: each is at (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>), labelled by <paramref name="label"/>.</summary>
    public static Plot3DBuilder Scatter3D<T>(IEnumerable<T> records, Func<T, double> x, Func<T, double> y, Func<T, double> z, Func<T, string?>? label = null)
    {
        ArgumentNullException.ThrowIfNull(records);
        return Scatter3D(records.Where(r => r != null).Select(r => new Point3D(x(r), y(r), z(r), label?.Invoke(r))).ToList());
    }

    /// <summary>A path through 3D points, drawn as a line coloured along its length.</summary>
    public static Plot3DBuilder Trajectory3D(object data) => new(Plot3DSpecBuilder.From(data, Plot3DType.Trajectory));

    /// <summary>Bars standing on a floor: each point is a bar at (x, y) as high as z, or a grid of heights.</summary>
    public static Plot3DBuilder VoxelBar3D(object data) => new(Plot3DSpecBuilder.From(data, Plot3DType.VoxelBar));

    /// <summary>A graph in 3D: <see cref="Graph3DData"/>, or an adjacency map (node → the nodes it links to).</summary>
    public static Plot3DBuilder Graph3D(object data) => new(Plot3DSpecBuilder.From(data, Plot3DType.Graph3D));

    /// <summary>The surface z = <paramref name="func"/>(x, y) over the ranges (each −5 to 5 when left out).</summary>
    public static Plot3DBuilder Surface(Func<double, double, double> func, (double min, double max)? xRange = null, (double min, double max)? yRange = null, int resolution = 30) =>
        new(Plot3DSpecBuilder.Surface(func, xRange ?? (-5.0, 5.0), yRange ?? (-5.0, 5.0), resolution, Plot3DType.Surface));

    /// <summary>A surface of heights: a grid (a row per y, a column per x) or <see cref="Surface3DData"/>.</summary>
    public static Plot3DBuilder Surface(object heights) => new(Plot3DSpecBuilder.From(heights, Plot3DType.Surface));

    /// <summary>The surface z = <paramref name="func"/>(x, y) drawn as a wireframe.</summary>
    public static Plot3DBuilder Wireframe(Func<double, double, double> func, (double min, double max)? xRange = null, (double min, double max)? yRange = null, int resolution = 30) =>
        new(Plot3DSpecBuilder.Surface(func, xRange ?? (-5.0, 5.0), yRange ?? (-5.0, 5.0), resolution, Plot3DType.Wireframe));

    /// <summary>A grid of heights drawn as a wireframe.</summary>
    public static Plot3DBuilder Wireframe(object heights) => new(Plot3DSpecBuilder.From(heights, Plot3DType.Wireframe));

    private static ChartBuilder Chart(object data, ChartType kind, string? name) =>
        Named(new ChartBuilder(ChartSpecBuilder.From(data, kind)), name);

    private static ChartBuilder Chart<T>(IEnumerable<T> records, Func<T, object?> x, Func<T, object?> y, ChartType kind, string? name) =>
        Named(new ChartBuilder(ChartSpecBuilder.From(records, x, y, kind)), name);

    // Names the data's series when it is the one.
    private static ChartBuilder Named(ChartBuilder builder, string? name)
    {
        if (name != null && builder.Spec.Series.Count == 1) builder.Spec.Series[0].Name = name;
        return builder;
    }
}
