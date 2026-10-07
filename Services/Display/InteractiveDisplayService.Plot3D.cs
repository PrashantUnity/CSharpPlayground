using System.ComponentModel;
using System.Runtime.CompilerServices;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Interaction;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

// 3D plots, z up for every kind.
public static partial class Display
{
    /// <summary>
    /// A 3D plot of <paramref name="data"/>: points ((x, y, z) tuples, three-number arrays, records with x, y and z, a name →
    /// points map), a surface (a function, a grid of heights) or a graph. <paramref name="plotType"/> is scatter,
    /// trajectory, surface, wireframe, graph3D or voxelBar.
    /// </summary>
    public static DisplayHandle<Plot3DSpec> Plot3D(
        object data,
        string? title = null,
        string? color = null,
        string? plotType = null,
        ColorMapPreset? colorMap = null,
        double? width = null,
        double? height = null,
        bool? showAxes = null,
        bool? showGrid = null,
        bool autoRotate = false,
        Action<Plot3DSpec>? configure = null,
        string? xLabel = null,
        string? yLabel = null,
        string? zLabel = null) =>
        Show3D(Plot3DSpecBuilder.From(data, Plot3DKindOf(plotType)), title, color, colorMap, autoRotate, configure, width, height, showAxes, showGrid, xLabel, yLabel, zLabel);

    public static DisplayHandle<Plot3DSpec> Plot3D(
        Func<double, double, double> func,
        (double min, double max)? xRange = null,
        (double min, double max)? yRange = null,
        int resolution = 30,
        string? title = null,
        ColorMapPreset? colorMap = null,
        bool wireframe = false,
        bool autoRotate = false,
        Action<Plot3DSpec>? configure = null,
        string? xLabel = null,
        string? yLabel = null,
        string? zLabel = null,
        double? width = null,
        double? height = null) =>
        Surface3D(func,
            minX: xRange?.min ?? -5.0,
            maxX: xRange?.max ?? 5.0,
            minY: yRange?.min ?? -5.0,
            maxY: yRange?.max ?? 5.0,
            resX: resolution,
            resY: resolution,
            title: title,
            colorMap: colorMap,
            wireframe: wireframe,
            autoRotate: autoRotate,
            configure: configure,
            xLabel: xLabel,
            yLabel: yLabel,
            zLabel: zLabel,
            width: width,
            height: height);

    // The helpers below take the same named settings as Plot3D: a label for each axis and the size.

    /// <summary>Points in 3D: (x, y, z) tuples, three-number arrays, records with x, y and z, or a name → points map (a series each).</summary>
    public static DisplayHandle<Plot3DSpec> Scatter3D(object data, string? title = null, string? color = null, ColorMapPreset? colorMap = null, bool autoRotate = false, Action<Plot3DSpec>? configure = null,
        string? xLabel = null, string? yLabel = null, string? zLabel = null, double? width = null, double? height = null) =>
        Show3D(Plot3DSpecBuilder.From(data, Plot3DType.Scatter), title, color, colorMap, autoRotate, configure, width, height, xLabel: xLabel, yLabel: yLabel, zLabel: zLabel);

    /// <summary>A path through 3D points, drawn as a line coloured along its length.</summary>
    public static DisplayHandle<Plot3DSpec> Trajectory3D(object data, string? title = null, ColorMapPreset? colorMap = null, bool autoRotate = false, Action<Plot3DSpec>? configure = null,
        string? xLabel = null, string? yLabel = null, string? zLabel = null, double? width = null, double? height = null) =>
        Show3D(Plot3DSpecBuilder.From(data, Plot3DType.Trajectory), title, null, colorMap, autoRotate, configure, width, height, xLabel: xLabel, yLabel: yLabel, zLabel: zLabel);

    /// <summary>Bars standing on a floor: each point is a bar at (x, y) as high as z.</summary>
    public static DisplayHandle<Plot3DSpec> VoxelBar3D(object data, string? title = null, string? color = null, ColorMapPreset? colorMap = null, bool autoRotate = false, Action<Plot3DSpec>? configure = null,
        string? xLabel = null, string? yLabel = null, string? zLabel = null, double? width = null, double? height = null) =>
        Show3D(Plot3DSpecBuilder.From(data, Plot3DType.VoxelBar), title, color, colorMap, autoRotate, configure, width, height, xLabel: xLabel, yLabel: yLabel, zLabel: zLabel);

    /// <summary>A graph in 3D: <see cref="Graph3DData"/>, or an adjacency map (node → the nodes it links to).</summary>
    public static DisplayHandle<Plot3DSpec> Graph3D(object data, string? title = null, string? color = null, ColorMapPreset? colorMap = null, bool autoRotate = false, Action<Plot3DSpec>? configure = null,
        double? width = null, double? height = null) =>
        Show3D(Plot3DSpecBuilder.From(data, Plot3DType.Graph3D), title, color, colorMap, autoRotate, configure, width, height);

    /// <summary>A surface of heights: a grid (a row per y, a column per x), <see cref="Surface3DData"/> or a function.</summary>
    public static DisplayHandle<Plot3DSpec> Surface3D(object data, string? title = null, ColorMapPreset? colorMap = null, bool wireframe = false, bool autoRotate = false, Action<Plot3DSpec>? configure = null,
        string? xLabel = null, string? yLabel = null, string? zLabel = null, double? width = null, double? height = null) =>
        Show3D(Plot3DSpecBuilder.From(data, wireframe ? Plot3DType.Wireframe : Plot3DType.Surface), title, null, colorMap, autoRotate, configure, width, height, xLabel: xLabel, yLabel: yLabel, zLabel: zLabel);

    /// <summary>
    /// The surface z = <paramref name="func"/>(x, y) over the ranges (each −5 to 5 when left out), sampled
    /// <paramref name="resolution"/> times along each axis. <c>Display.Surface3D((x, y) => Math.Sin(x) * Math.Cos(y), (-5, 5), (-5, 5));</c>
    /// </summary>
    public static DisplayHandle<Plot3DSpec> Surface3D(
        Func<double, double, double> func,
        (double min, double max)? xRange = null,
        (double min, double max)? yRange = null,
        int resolution = 30,
        string? title = null,
        ColorMapPreset? colorMap = null,
        bool wireframe = false,
        bool autoRotate = false,
        Action<Plot3DSpec>? configure = null,
        string? xLabel = null,
        string? yLabel = null,
        string? zLabel = null,
        double? width = null,
        double? height = null) =>
        Show3D(Plot3DSpecBuilder.Surface(func, xRange ?? (-5.0, 5.0), yRange ?? (-5.0, 5.0), resolution, wireframe ? Plot3DType.Wireframe : Plot3DType.Surface),
            title, null, colorMap, autoRotate, configure, width, height, xLabel: xLabel, yLabel: yLabel, zLabel: zLabel);

    /// <summary>The surface z = <paramref name="func"/>(x, y) over the ranges, sampled <paramref name="resX"/> by <paramref name="resY"/> times.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [OverloadResolutionPriority(-1)]
    public static DisplayHandle<Plot3DSpec> Surface3D(
        Func<double, double, double> func,
        double minX = -5.0,
        double maxX = 5.0,
        double minY = -5.0,
        double maxY = 5.0,
        int resX = 30,
        int resY = 30,
        string? title = null,
        ColorMapPreset? colorMap = null,
        bool wireframe = false,
        bool autoRotate = false,
        int? resolution = null,
        Action<Plot3DSpec>? configure = null,
        string? xLabel = null,
        string? yLabel = null,
        string? zLabel = null,
        double? width = null,
        double? height = null) =>
        Show3D(Plot3DSpecBuilder.Surface(func, (minX, maxX), (minY, maxY), resolution ?? resX, resolution ?? resY, wireframe ? Plot3DType.Wireframe : Plot3DType.Surface),
            title, null, colorMap, autoRotate, configure, width, height, xLabel: xLabel, yLabel: yLabel, zLabel: zLabel);

    private static Plot3DType? Plot3DKindOf(string? plotType)
    {
        if (string.IsNullOrWhiteSpace(plotType)) return null;
        var name = plotType.Replace("3D", string.Empty, StringComparison.OrdinalIgnoreCase).Replace(" ", string.Empty);
        return name.ToLowerInvariant() switch
        {
            "graph" => Plot3DType.Graph3D,
            "voxel" or "bar" or "bars" => Plot3DType.VoxelBar,
            _ when Enum.TryParse<Plot3DType>(name, true, out var kind) => kind,
            _ => throw new ArgumentException($"There is no \"{plotType}\" 3D plot: use scatter, trajectory, surface, wireframe, graph or voxelBar.", nameof(plotType))
        };
    }

    private static DisplayHandle<Plot3DSpec> Show3D(
        Plot3DSpec spec,
        string? title,
        string? color,
        ColorMapPreset? colorMap,
        bool autoRotate,
        Action<Plot3DSpec>? configure,
        double? width = null,
        double? height = null,
        bool? showAxes = null,
        bool? showGrid = null,
        string? xLabel = null,
        string? yLabel = null,
        string? zLabel = null)
    {
        Set(spec, title, color, width, height);
        Label(spec, xLabel, yLabel, null, zLabel);
        spec.ColorMap = colorMap ?? spec.ColorMap;
        if (autoRotate) spec.AutoRotate = true;
        spec.ShowAxes = showAxes ?? spec.ShowAxes;
        spec.ShowGrid = showGrid ?? spec.ShowGrid;
        return Show(spec, configure);
    }
}
