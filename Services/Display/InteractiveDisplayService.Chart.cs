using PdfEditorApp.Plugins.CSharpEditor.Charting.Services;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Interaction;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Output;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

// Charts. Every helper takes the data first, then the title, then what is particular to it; settings left out are the
// studio's defaults, the same in every language, and `configure` can change anything in the spec before it is shown.
public static partial class Display
{
    /// <summary>
    /// A chart of <paramref name="data"/>: numbers, [x, y] pairs, a label → number map, a name → sequence map (a series
    /// each) or records (their y, value, amount… against their x, key, label…). <paramref name="chartType"/> is line,
    /// area, bar, scatter, pie, donut or histogram.
    /// </summary>
    public static DisplayHandle<ChartSpec> Chart(
        object data,
        string? title = null,
        string? color = null,
        string? chartType = null,
        ChartType? type = null,
        double? width = null,
        double? height = null,
        bool? showGrid = null,
        bool? showPoints = null,
        bool? showStats = null,
        Action<ChartSpec>? configure = null,
        string? xLabel = null,
        string? yLabel = null,
        bool? legend = null)
    {
        var resolvedType = type ?? KindOf(chartType);
        var spec = ChartSpecBuilder.From(data, resolvedType);
        Set(spec, title, color, width, height);
        spec.Grid = showGrid ?? spec.Grid;
        spec.ShowPoints = showPoints ?? spec.ShowPoints;
        spec.ShowStats = showStats ?? spec.ShowStats;
        Label(spec, xLabel, yLabel, legend);
        return Show(spec, configure);
    }

    /// <summary>A chart of records: each is drawn at <paramref name="x"/> (a number, or a label) with the value <paramref name="y"/>.</summary>
    public static DisplayHandle<ChartSpec> Chart<T>(
        IEnumerable<T> records,
        Func<T, object?> x,
        Func<T, object?> y,
        string? title = null,
        string? color = null,
        string? chartType = null,
        Action<ChartSpec>? configure = null,
        string? xLabel = null,
        string? yLabel = null,
        double? width = null,
        double? height = null,
        bool? legend = null)
    {
        var spec = ChartSpecBuilder.From(records, x, y, KindOf(chartType));
        Set(spec, title, color, width, height);
        Label(spec, xLabel, yLabel, legend);
        return Show(spec, configure);
    }

    // The kind helpers take the same named settings as Chart: a label for each axis, the size, the legend.

    /// <summary>A line chart. <c>Display.LineChart(sales, "Sales", xLabel: "Month", yLabel: "USD");</c></summary>
    public static DisplayHandle<ChartSpec> LineChart(object data, string? title = null, string? color = null, bool? showPoints = null, Action<ChartSpec>? configure = null,
        string? xLabel = null, string? yLabel = null, double? width = null, double? height = null, bool? legend = null) =>
        Chart(data, title, color, nameof(ChartType.Line), showPoints: showPoints, width: width, height: height, configure: configure, xLabel: xLabel, yLabel: yLabel, legend: legend);

    /// <summary>An area chart: a line with the space under it filled.</summary>
    public static DisplayHandle<ChartSpec> AreaChart(object data, string? title = null, string? color = null, Action<ChartSpec>? configure = null,
        string? xLabel = null, string? yLabel = null, double? width = null, double? height = null, bool? legend = null) =>
        Chart(data, title, color, nameof(ChartType.Area), width: width, height: height, configure: configure, xLabel: xLabel, yLabel: yLabel, legend: legend);

    /// <summary>A bar chart. <c>Display.BarChart(new Dictionary&lt;string, int&gt; { ["Mon"] = 4, ["Tue"] = 7 });</c></summary>
    public static DisplayHandle<ChartSpec> BarChart(object data, string? title = null, string? color = null, Action<ChartSpec>? configure = null,
        string? xLabel = null, string? yLabel = null, double? width = null, double? height = null, bool? legend = null) =>
        Chart(data, title, color, nameof(ChartType.Bar), width: width, height: height, configure: configure, xLabel: xLabel, yLabel: yLabel, legend: legend);

    /// <summary>A scatter chart of [x, y] pairs.</summary>
    public static DisplayHandle<ChartSpec> ScatterChart(object data, string? title = null, string? color = null, Action<ChartSpec>? configure = null,
        string? xLabel = null, string? yLabel = null, double? width = null, double? height = null, bool? legend = null) =>
        Chart(data, title, color, nameof(ChartType.Scatter), width: width, height: height, configure: configure, xLabel: xLabel, yLabel: yLabel, legend: legend);

    /// <summary>A pie chart: each label → number is a slice.</summary>
    public static DisplayHandle<ChartSpec> PieChart(object data, string? title = null, Action<ChartSpec>? configure = null,
        double? width = null, double? height = null, bool? legend = null) =>
        Chart(data, title, null, nameof(ChartType.Pie), width: width, height: height, configure: configure, legend: legend);

    /// <summary>A donut chart: a pie with a hole.</summary>
    public static DisplayHandle<ChartSpec> DonutChart(object data, string? title = null, Action<ChartSpec>? configure = null,
        double? width = null, double? height = null, bool? legend = null) =>
        Chart(data, title, null, nameof(ChartType.Donut), width: width, height: height, configure: configure, legend: legend);

    /// <summary>A histogram of the samples (or of each sequence in a name → sequence map), counted into <paramref name="bins"/> bars.</summary>
    public static DisplayHandle<ChartSpec> Histogram(object samples, string? title = null, int? bins = null, string? color = null, Action<ChartSpec>? configure = null,
        string? xLabel = null, string? yLabel = null, double? width = null, double? height = null, bool? legend = null)
    {
        var spec = ChartSpecBuilder.Histogram(samples, bins);
        Set(spec, title, color, width, height);
        Label(spec, xLabel, yLabel, legend);
        return Show(spec, configure);
    }

    /// <summary>Shows a visual described by its spec: a chart, 3D plot or visualizer, drawn the same from any language.</summary>
    public static DisplayHandle<TSpec> Show<TSpec>(TSpec spec) where TSpec : VisualSpec => Show(spec, configure: null);

    /// <summary>Shows anything that describes itself as a visual: a tracker, a recorder.</summary>
    public static DisplayHandle<VisualSpec> Show(IVisualSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Show(source.ToVisualSpec(), configure: null, origin: source);
    }

    private static void Label(VisualSpec spec, string? xLabel, string? yLabel, bool? legend, string? zLabel = null)
    {
        switch (spec)
        {
            case ChartSpec chart:
                if (xLabel != null) chart.XAxis.Title = xLabel;
                if (yLabel != null) chart.YAxis.Title = yLabel;
                if (legend != null) chart.Legend.Show = legend;
                break;
            case Plot3DSpec plot:
                if (xLabel != null) plot.XAxis.Title = xLabel;
                if (yLabel != null) plot.YAxis.Title = yLabel;
                if (zLabel != null) plot.ZAxis.Title = zLabel;
                break;
        }
    }

    private static ChartType? KindOf(string? chartType) =>
        string.IsNullOrWhiteSpace(chartType) ? null : ChartDataParser.ParseChartType(chartType);

    private static void Set(VisualSpec spec, string? title, string? color, double? width, double? height)
    {
        if (!string.IsNullOrEmpty(title)) spec.Title = title;
        spec.Width = width ?? spec.Width;
        spec.Height = height ?? spec.Height;
        if (string.IsNullOrEmpty(color)) return;
        switch (spec)
        {
            case ChartSpec chart: chart.Color = color; break;
            case Plot3DSpec plot: plot.Color = color; break;
        }
    }

    // Every visual goes out this way: as a spec, changed by `configure` last, and checked, so one that can't be drawn is
    // an error that says why.
    private static DisplayHandle<TSpec> Show<TSpec>(TSpec spec, Action<TSpec>? configure, object? origin = null) where TSpec : VisualSpec
    {
        ArgumentNullException.ThrowIfNull(spec);
        configure?.Invoke(spec);
        var output = VisualOutputs.FromSpec(spec, origin: origin);
        InteractiveDisplayContext.Emit(output);
        return new DisplayHandle<TSpec>(output.Visual);
    }
}
