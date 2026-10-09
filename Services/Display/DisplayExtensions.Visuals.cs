using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

// `data.DisplayLineChart()` is `Display.LineChart(data)` that hands the data back, so it can end a chain or sit inside
// one. Every visual has one, named Display + the helper's name.
public static partial class DisplayExtensions
{
    // Charts

    public static T DisplayChart<T>(this T data, string? title = null, string? color = null, string? chartType = null, Action<ChartSpec>? configure = null, string? xLabel = null, string? yLabel = null, double? width = null, double? height = null, bool? legend = null)
    {
        if (data != null) Display.Chart(data, title, color, chartType, width: width, height: height, configure: configure, xLabel: xLabel, yLabel: yLabel, legend: legend);
        return data;
    }

    /// <summary>Charts the records: each at <paramref name="x"/> (a number, or a label) with the value <paramref name="y"/>.</summary>
    public static IEnumerable<T> DisplayChart<T>(this IEnumerable<T> records, Func<T, object?> x, Func<T, object?> y, string? title = null, string? color = null, string? chartType = null, Action<ChartSpec>? configure = null, string? xLabel = null, string? yLabel = null, double? width = null, double? height = null, bool? legend = null)
    {
        Display.Chart(records, x, y, title, color, chartType, configure, xLabel, yLabel, width, height, legend);
        return records;
    }

    public static T DisplayLineChart<T>(this T data, string? title = null, string? color = null, bool? showPoints = null, Action<ChartSpec>? configure = null, string? xLabel = null, string? yLabel = null, double? width = null, double? height = null, bool? legend = null, IEnumerable<string>? labels = null, ChartStack? stack = null)
    {
        if (data != null) Display.LineChart(data, title, color, showPoints, configure, xLabel, yLabel, width, height, legend, labels, stack);
        return data;
    }

    public static T DisplayAreaChart<T>(this T data, string? title = null, string? color = null, Action<ChartSpec>? configure = null, string? xLabel = null, string? yLabel = null, double? width = null, double? height = null, bool? legend = null, IEnumerable<string>? labels = null, ChartStack? stack = null)
    {
        if (data != null) Display.AreaChart(data, title, color, configure, xLabel, yLabel, width, height, legend, labels, stack);
        return data;
    }

    public static T DisplayBarChart<T>(this T data, string? title = null, string? color = null, Action<ChartSpec>? configure = null, string? xLabel = null, string? yLabel = null, double? width = null, double? height = null, bool? legend = null, IEnumerable<string>? labels = null, ChartStack? stack = null, bool horizontal = false)
    {
        if (data != null) Display.BarChart(data, title, color, configure, xLabel, yLabel, width, height, legend, labels, stack, horizontal);
        return data;
    }

    public static T DisplayScatterChart<T>(this T data, string? title = null, string? color = null, Action<ChartSpec>? configure = null, string? xLabel = null, string? yLabel = null, double? width = null, double? height = null, bool? legend = null)
    {
        if (data != null) Display.ScatterChart(data, title, color, configure, xLabel, yLabel, width, height, legend);
        return data;
    }

    public static T DisplayStackedBarChart<T>(this T data, string? title = null, string? color = null, Action<ChartSpec>? configure = null,
        string? xLabel = null, string? yLabel = null, double? width = null, double? height = null, bool? legend = null, IEnumerable<string>? labels = null,
        bool percent = false, bool horizontal = false)
    {
        if (data != null) Display.StackedBarChart(data, title, color, configure, xLabel, yLabel, width, height, legend, labels, percent, horizontal);
        return data;
    }

    public static T DisplayHorizontalBarChart<T>(this T data, string? title = null, string? color = null, Action<ChartSpec>? configure = null,
        string? xLabel = null, string? yLabel = null, double? width = null, double? height = null, bool? legend = null, IEnumerable<string>? labels = null,
        ChartStack? stack = null)
    {
        if (data != null) Display.HorizontalBarChart(data, title, color, configure, xLabel, yLabel, width, height, legend, labels, stack);
        return data;
    }

    public static T DisplayBubbleChart<T>(this T data, string? title = null, string? color = null, Action<ChartSpec>? configure = null,
        string? xLabel = null, string? yLabel = null, double? width = null, double? height = null, bool? legend = null)
    {
        if (data != null) Display.BubbleChart(data, title, color, configure, xLabel, yLabel, width, height, legend);
        return data;
    }

    public static T DisplayRadarChart<T>(this T data, string? title = null, string? color = null, Action<ChartSpec>? configure = null,
        double? width = null, double? height = null, bool? legend = null, IEnumerable<string>? labels = null, LegendPosition? legendPosition = null)
    {
        if (data != null) Display.RadarChart(data, title, color, configure, width, height, legend, labels, legendPosition);
        return data;
    }

    public static T DisplayPolarAreaChart<T>(this T data, string? title = null, Action<ChartSpec>? configure = null,
        double? width = null, double? height = null, IEnumerable<string>? labels = null)
    {
        if (data != null) Display.PolarAreaChart(data, title, configure, width, height, labels);
        return data;
    }

    public static T DisplayPieChart<T>(this T data, string? title = null, Action<ChartSpec>? configure = null, double? width = null, double? height = null, bool? legend = null, IEnumerable<string>? labels = null)
    {
        if (data != null) Display.PieChart(data, title, configure, width, height, legend, labels);
        return data;
    }

    public static T DisplayDonutChart<T>(this T data, string? title = null, Action<ChartSpec>? configure = null, double? width = null, double? height = null, bool? legend = null, IEnumerable<string>? labels = null, bool gauge = false)
    {
        if (data != null) Display.DonutChart(data, title, configure, width, height, legend, labels, gauge);
        return data;
    }

    public static T DisplayHistogram<T>(this T samples, string? title = null, int? bins = null, string? color = null, Action<ChartSpec>? configure = null, string? xLabel = null, string? yLabel = null, double? width = null, double? height = null, bool? legend = null)
    {
        if (samples != null) Display.Histogram(samples, title, bins, color, configure, xLabel, yLabel, width, height, legend);
        return samples;
    }

    // 3D plots

    public static T DisplayPlot3D<T>(this T data, string? title = null, string? color = null, string? plotType = null, ColorMapPreset? colorMap = null, Action<Plot3DSpec>? configure = null, string? xLabel = null, string? yLabel = null, string? zLabel = null, double? width = null, double? height = null)
    {
        if (data != null) Display.Plot3D(data, title, color, plotType, colorMap, width: width, height: height, configure: configure, xLabel: xLabel, yLabel: yLabel, zLabel: zLabel);
        return data;
    }

    public static T DisplayScatter3D<T>(this T data, string? title = null, string? color = null, ColorMapPreset? colorMap = null, Action<Plot3DSpec>? configure = null, string? xLabel = null, string? yLabel = null, string? zLabel = null, double? width = null, double? height = null)
    {
        if (data != null) Display.Scatter3D(data, title, color, colorMap, configure: configure, xLabel: xLabel, yLabel: yLabel, zLabel: zLabel, width: width, height: height);
        return data;
    }

    /// <summary>Plots the records as points at (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>), labelled by <paramref name="label"/>.</summary>
    public static IEnumerable<T> DisplayScatter3D<T>(this IEnumerable<T> records, Func<T, double> x, Func<T, double> y, Func<T, double> z, Func<T, string?>? label = null, string? title = null, string? color = null, Action<Plot3DSpec>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(records);
        var points = records.Where(r => r != null).Select(r => new Point3D(x(r), y(r), z(r), label?.Invoke(r))).ToList();
        Display.Scatter3D(points, title, color, configure: configure);
        return records;
    }

    public static T DisplayTrajectory3D<T>(this T data, string? title = null, ColorMapPreset? colorMap = null, Action<Plot3DSpec>? configure = null, string? xLabel = null, string? yLabel = null, string? zLabel = null, double? width = null, double? height = null)
    {
        if (data != null) Display.Trajectory3D(data, title, colorMap, configure: configure, xLabel: xLabel, yLabel: yLabel, zLabel: zLabel, width: width, height: height);
        return data;
    }

    public static T DisplaySurface3D<T>(this T data, string? title = null, ColorMapPreset? colorMap = null, bool wireframe = false, Action<Plot3DSpec>? configure = null, string? xLabel = null, string? yLabel = null, string? zLabel = null, double? width = null, double? height = null)
    {
        if (data != null) Display.Surface3D(data, title, colorMap, wireframe, configure: configure, xLabel: xLabel, yLabel: yLabel, zLabel: zLabel, width: width, height: height);
        return data;
    }

    public static T DisplayGraph3D<T>(this T data, string? title = null, string? color = null, Action<Plot3DSpec>? configure = null, double? width = null, double? height = null)
    {
        if (data != null) Display.Graph3D(data, title, color, configure: configure, width: width, height: height);
        return data;
    }

    public static T DisplayVoxelBar3D<T>(this T data, string? title = null, string? color = null, Action<Plot3DSpec>? configure = null, string? xLabel = null, string? yLabel = null, string? zLabel = null, double? width = null, double? height = null)
    {
        if (data != null) Display.VoxelBar3D(data, title, color, configure: configure, xLabel: xLabel, yLabel: yLabel, zLabel: zLabel, width: width, height: height);
        return data;
    }

    // Visualizers

    /// <summary>Visualizes the data as whatever it is (a grid, tree, list, graph or array); see <see cref="Display.Visualize"/>.</summary>
    public static T DisplayVisualizer<T>(this T data, string? title = null, Action<VisualizerSpec>? configure = null)
    {
        if (data != null) Display.Visualize(data, title, configure);
        return data;
    }

    public static VisualizerRecorder? DisplayVisualizer(this VisualizerRecorder? recorder)
    {
        if (recorder != null) Display.Visualizer(recorder);
        return recorder;
    }

    public static MatrixTracker? DisplayVisualizer(this MatrixTracker? tracker)
    {
        if (tracker != null) Display.Visualizer(tracker);
        return tracker;
    }

    public static T DisplayIslands<T>(this T grid, string? title = null, bool recordSteps = true, bool fourDirectional = true, Action<VisualizerSpec>? configure = null)
    {
        if (grid != null) Display.Islands(grid, title, recordSteps, fourDirectional, configure);
        return grid;
    }

    public static T DisplayMatrix<T>(this T grid, string? title = null, bool? showCoordinates = null, bool? showValues = null, Action<VisualizerSpec>? configure = null)
    {
        if (grid != null) Display.Matrix(grid, title, showCoordinates, showValues, configure: configure);
        return grid;
    }

    public static T DisplayTree<T>(this T root, string? title = null, string? recordTraversal = null, Action<VisualizerSpec>? configure = null)
    {
        if (root != null) Display.Tree(root, title, recordTraversal, configure);
        return root;
    }

    public static T DisplayGraph<T>(this T graph, string? title = null, bool isDirected = true, Action<VisualizerSpec>? configure = null)
    {
        if (graph != null) Display.Graph(graph, title, isDirected, configure);
        return graph;
    }

    public static T DisplayLinkedList<T>(this T head, string? title = null, bool recordCycleSteps = false, Action<VisualizerSpec>? configure = null)
    {
        if (head != null) Display.LinkedList(head, title, recordCycleSteps, configure);
        return head;
    }

    public static T DisplayArray<T>(this T array, object? pointers = null, string? title = null, Action<VisualizerSpec>? configure = null)
    {
        if (array != null) Display.Array(array, pointers, title, configure);
        return array;
    }

    public static T DisplayBars<T>(this T values, string? title = null, Action<VisualizerSpec>? configure = null)
    {
        if (values != null) Display.Bars(values, title, configure);
        return values;
    }

    public static BoardVisualizerData DisplayBoard(this BoardVisualizerData boardData, string? title = null, Action<VisualizerSpec>? configure = null)
    {
        Display.Board(boardData, title, configure);
        return boardData;
    }

    public static VisualizerScene DisplayCanvas(this VisualizerScene scene, string? title = null, Action<VisualizerSpec>? configure = null)
    {
        Display.Canvas(scene, title, configure);
        return scene;
    }
}
