using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

/// <summary>
/// A chart being put together; start one with <see cref="Charts"/>. Each setting is optional: what is left out is the
/// studio's default, the same in every language.
/// </summary>
public sealed class ChartBuilder : VisualBuilder<ChartBuilder, ChartSpec>
{
    internal ChartBuilder(ChartSpec spec) : base(spec) { }

    /// <summary>A name for the axis along the bottom.</summary>
    public ChartBuilder XLabel(string label) => Edit(s => s.XAxis.Title = label);

    /// <summary>A name for the axis up the side.</summary>
    public ChartBuilder YLabel(string label) => Edit(s => s.YAxis.Title = label);

    /// <summary>The part of the x axis to show (worked out from the data when left out).</summary>
    public ChartBuilder XRange(double min, double max) => Edit(s => (s.XAxis.Min, s.XAxis.Max) = (min, max));

    /// <summary>The part of the y axis to show (worked out from the data when left out).</summary>
    public ChartBuilder YRange(double min, double max) => Edit(s => (s.YAxis.Min, s.YAxis.Max) = (min, max));

    /// <summary>The colour of the first series, a CSS colour such as <c>"#4ec9b0"</c>; the others follow the studio's palette.</summary>
    public ChartBuilder Color(string color) => Edit(s => s.Color = color);

    /// <summary>Shows or hides the series legend (it shows by itself when there is more than one series).</summary>
    public ChartBuilder Legend(bool show = true) => Edit(s => s.Legend.Show = show);

    /// <summary>Shows or hides the grid lines behind the data.</summary>
    public ChartBuilder Grid(bool show = true) => Edit(s => s.Grid = show);

    /// <summary>Shows or hides a dot on every value of a line or area.</summary>
    public ChartBuilder Points(bool show = true) => Edit(s => s.ShowPoints = show);

    /// <summary>Shows or hides the min, max, average and count summary.</summary>
    public ChartBuilder Stats(bool show = true) => Edit(s => s.ShowStats = show);

    /// <summary>How many bars a histogram counts its samples into.</summary>
    public ChartBuilder Bins(int bins) => Edit(s => s.Bins = bins);

    /// <summary>Draws the chart as another kind.</summary>
    public ChartBuilder As(ChartType kind) => Edit(s => s.Kind = kind);

    /// <summary>
    /// Adds a series: numbers, [x, y] pairs, a label → number map or records, like the data a chart starts from. A map of
    /// names to sequences adds a series for each.
    /// </summary>
    public ChartBuilder Series(string name, object data, string? color = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        var added = ChartSpecBuilder.From(data, Spec.Kind).Series;
        if (added.Count == 1)
        {
            added[0].Name = name;
            if (color != null) added[0].Color = color;
        }

        return Edit(s => s.Series.AddRange(added));
    }

    /// <summary>Adds a series from records: each at <paramref name="x"/> (a number, or a label) with the value <paramref name="y"/>.</summary>
    public ChartBuilder Series<T>(string name, IEnumerable<T> records, Func<T, object?> x, Func<T, object?> y, string? color = null)
    {
        var added = ChartSpecBuilder.From(records, x, y, Spec.Kind).Series;
        foreach (var series in added)
        {
            series.Name = name;
            if (color != null) series.Color = color;
        }

        return Edit(s => s.Series.AddRange(added));
    }

    /// <summary>A colour for each value of the first series (a bar, a point or a slice); a null keeps the series colour.</summary>
    public ChartBuilder Colors(params string?[] colors) => Edit(s =>
    {
        if (s.Series.Count == 0) throw new InvalidOperationException("There is no series to colour yet: give the chart its data first.");
        s.Series[0].Colors = [.. colors];
    });
}
