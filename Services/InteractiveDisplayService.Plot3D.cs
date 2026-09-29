using System;
using System.Collections;
using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static partial class Display
{
    public static Plot3DOptions Plot3D(
        object data,
        string? title = null,
        string? color = null,
        string? plotType = null,
        ColorMapPreset? colorMap = null,
        double width = 640,
        double height = 340,
        bool showAxes = true,
        bool showGrid = true,
        bool autoRotate = false)
    {
        Plot3DOptions options = Plot3DDataParser.Parse(data, title, color, plotType, colorMap);

        if (!string.IsNullOrEmpty(title)) options.Title = title;
        if (!string.IsNullOrEmpty(color)) options.PrimaryColor = color;
        if (colorMap.HasValue) options.ColorMap = colorMap.Value;

        options.Width = width;
        options.Height = height;
        options.ShowAxes = showAxes;
        options.ShowFloorGrid = showGrid;
        options.ShowBoundingBox = showGrid;
        options.AutoRotate = autoRotate;

        InteractiveDisplayContext.Emit(new RichCellOutput
        {
            Kind = CellOutputKind.Plot3D,
            Plot3DOptions = options
        });

        return options;
    }

    public static Plot3DOptions Scatter3D(
        object data,
        string? title = null,
        string? color = null,
        ColorMapPreset? colorMap = null,
        bool autoRotate = false)
    {
        var opts = Plot3D(data, title, color, "Scatter", colorMap);
        opts.AutoRotate = autoRotate;
        return opts;
    }

    public static Plot3DOptions Surface3D(
        object data,
        string? title = null,
        ColorMapPreset? colorMap = null,
        bool wireframe = false,
        bool autoRotate = false)
    {
        var opts = Plot3D(data, title, null, wireframe ? "Wireframe" : "Surface", colorMap);
        opts.Wireframe = wireframe;
        opts.AutoRotate = autoRotate;
        return opts;
    }

    public static Plot3DOptions Surface3D(
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
        int? resolution = null)
    {
        int rx = resolution ?? resX;
        int ry = resolution ?? resY;
        var surface = Surface3DData.FromFunction(func, minX, maxX, minY, maxY, rx, ry);
        return Surface3D(surface, title, colorMap, wireframe, autoRotate);
    }

    public static Plot3DOptions Surface3D(
        Func<double, double, double> func,
        (double min, double max) xRange,
        (double min, double max) yRange,
        int resolution = 30,
        string? title = null,
        ColorMapPreset? colorMap = null,
        bool wireframe = false,
        bool autoRotate = false)
    {
        return Surface3D(func, xRange.min, xRange.max, yRange.min, yRange.max, resolution, resolution, title, colorMap, wireframe, autoRotate);
    }

    public static Plot3DOptions Graph3D(
        object data,
        string? title = null,
        string? color = null,
        ColorMapPreset? colorMap = null,
        bool autoRotate = false)
    {
        var opts = Plot3D(data, title, color, "Graph3D", colorMap);
        opts.AutoRotate = autoRotate;
        return opts;
    }

    public static Plot3DOptions Trajectory3D(
        object data,
        string? title = null,
        ColorMapPreset? colorMap = null,
        bool autoRotate = false)
    {
        var opts = Plot3D(data, title, null, "Trajectory", colorMap);
        opts.AutoRotate = autoRotate;
        return opts;
    }

    public static Plot3DOptions VoxelBar3D(
        object data,
        string? title = null,
        string? color = null,
        ColorMapPreset? colorMap = null,
        bool autoRotate = false)
    {
        var opts = Plot3D(data, title, color, "VoxelBar", colorMap);
        opts.AutoRotate = autoRotate;
        return opts;
    }
}

public static partial class DisplayExtensions
{
    public static T Plot3D<T>(
        this T data,
        string? title = null,
        string? color = null,
        string? plotType = null,
        ColorMapPreset? colorMap = null)
    {
        if (data != null)
        {
            Display.Plot3D(data, title, color, plotType, colorMap);
        }
        return data;
    }

    public static T Scatter3D<T>(
        this T data,
        string? title = null,
        string? color = null)
    {
        if (data != null)
        {
            Display.Scatter3D(data, title, color);
        }
        return data;
    }

    public static T Surface3D<T>(
        this T data,
        string? title = null,
        ColorMapPreset? colorMap = null,
        bool wireframe = false)
    {
        if (data != null)
        {
            Display.Surface3D(data, title, colorMap, wireframe);
        }
        return data;
    }

    public static T Graph3D<T>(
        this T data,
        string? title = null,
        string? color = null)
    {
        if (data != null)
        {
            Display.Graph3D(data, title, color);
        }
        return data;
    }

    public static T Trajectory3D<T>(
        this T data,
        string? title = null,
        ColorMapPreset? colorMap = null)
    {
        if (data != null)
        {
            Display.Trajectory3D(data, title, colorMap);
        }
        return data;
    }

    public static T Dump3D<T>(
        this T data,
        string? title = null,
        string? color = null)
    {
        if (data != null)
        {
            Display.Plot3D(data, title, color);
        }
        return data;
    }

    public static IEnumerable<T> Dump3D<T>(
        this IEnumerable<T> source,
        Func<T, double> xSelector,
        Func<T, double> ySelector,
        Func<T, double> zSelector,
        Func<T, string>? labelSelector = null,
        string? title = null,
        string? color = null)
    {
        if (source != null)
        {
            var points = new List<Point3D>();
            foreach (var item in source)
            {
                if (item == null) continue;
                double x = xSelector(item);
                double y = ySelector(item);
                double z = zSelector(item);
                string? label = labelSelector?.Invoke(item);
                points.Add(new Point3D(x, y, z, label));
            }
            Display.Scatter3D(points, title, color);
        }
        return source!;
    }
}
