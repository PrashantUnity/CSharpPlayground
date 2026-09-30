using System;
using System.Collections.Generic;
using System.ComponentModel;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

// The names these had before every visual's extension was Display + its helper's name. Scripts that use them keep
// working; completion doesn't offer them, so new code finds only the one name.
public static partial class DisplayExtensions
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static T Chart<T>(this T data, string? title = null, string? color = null, string? chartType = null) => data.DisplayChart(title, color, chartType);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static T LineChart<T>(this T data, string? title = null, string? color = null) => data.DisplayLineChart(title, color);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static T AreaChart<T>(this T data, string? title = null, string? color = null) => data.DisplayAreaChart(title, color);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static T BarChart<T>(this T data, string? title = null, string? color = null) => data.DisplayBarChart(title, color);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static T ScatterChart<T>(this T data, string? title = null, string? color = null) => data.DisplayScatterChart(title, color);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static T PieChart<T>(this T data, string? title = null) => data.DisplayPieChart(title);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static T DonutChart<T>(this T data, string? title = null) => data.DisplayDonutChart(title);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static T Plot3D<T>(this T data, string? title = null, string? color = null, string? plotType = null, ColorMapPreset? colorMap = null) =>
        data.DisplayPlot3D(title, color, plotType, colorMap);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static T Scatter3D<T>(this T data, string? title = null, string? color = null) => data.DisplayScatter3D(title, color);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static T Surface3D<T>(this T data, string? title = null, ColorMapPreset? colorMap = null, bool wireframe = false) => data.DisplaySurface3D(title, colorMap, wireframe);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static T Graph3D<T>(this T data, string? title = null, string? color = null) => data.DisplayGraph3D(title, color);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static T Trajectory3D<T>(this T data, string? title = null, ColorMapPreset? colorMap = null) => data.DisplayTrajectory3D(title, colorMap);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static T Dump3D<T>(this T data, string? title = null, string? color = null) => data.DisplayPlot3D(title, color);

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IEnumerable<T> Dump3D<T>(this IEnumerable<T> source, Func<T, double> xSelector, Func<T, double> ySelector, Func<T, double> zSelector,
        Func<T, string>? labelSelector = null, string? title = null, string? color = null) =>
        source.DisplayScatter3D(xSelector, ySelector, zSelector, labelSelector, title, color);
}
