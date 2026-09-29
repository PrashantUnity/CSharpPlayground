using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Layouts;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting3D.Services;

public static class Plot3DDataParser
{
    public static Plot3DOptions Parse(
        object? data,
        string? title = null,
        string? color = null,
        string? plotType = null,
        ColorMapPreset? colorMap = null)
    {
        var options = new Plot3DOptions
        {
            Title = title ?? "3D Visualization",
            PrimaryColor = color ?? "#4ec9b0"
        };

        if (colorMap.HasValue) options.ColorMap = colorMap.Value;

        if (!string.IsNullOrEmpty(plotType) && Enum.TryParse<Plot3DType>(plotType, true, out var pt))
        {
            options.Type = pt;
        }

        if (data == null) return options;

        if (data is Plot3DOptions existing)
        {
            if (title != null) existing.Title = title;
            if (color != null) existing.PrimaryColor = color;
            if (colorMap.HasValue) existing.ColorMap = colorMap.Value;
            return existing;
        }

        if (data is Surface3DData surface)
        {
            options.Surface = surface;
            options.Type = options.Type == Plot3DType.Wireframe ? Plot3DType.Wireframe : Plot3DType.Surface;
            options.Wireframe = options.Type == Plot3DType.Wireframe;
            options.RecalculateBounds();
            return options;
        }

        if (data is Graph3DData graph)
        {
            options.Graph = graph;
            options.Type = Plot3DType.Graph3D;
            ForceDirected3DLayout.ComputeLayout(graph);
            options.RecalculateBounds();
            return options;
        }

        if (data is Func<double, double, double> func)
        {
            options.Surface = Surface3DData.FromFunction(func);
            options.Type = options.Type == Plot3DType.Wireframe ? Plot3DType.Wireframe : Plot3DType.Surface;
            options.Wireframe = options.Type == Plot3DType.Wireframe;
            options.RecalculateBounds();
            return options;
        }

        if (data is double[,] matrix)
        {
            options.Surface = Surface3DData.FromGrid(matrix);
            options.Type = options.Type == Plot3DType.Wireframe ? Plot3DType.Wireframe : Plot3DType.Surface;
            options.Wireframe = options.Type == Plot3DType.Wireframe;
            options.RecalculateBounds();
            return options;
        }

        // Sequences of points
        var series = new Series3D { Name = title ?? "Series 1", Color = options.PrimaryColor };

        if (data is IEnumerable<(double x, double y, double z)> tupleList)
        {
            foreach (var (x, y, z) in tupleList)
            {
                series.Points.Add(new Point3D(x, y, z));
            }
        }
        else if (data is IEnumerable<(double x, double y, double z, string label)> labeledTupleList)
        {
            foreach (var (x, y, z, lbl) in labeledTupleList)
            {
                series.Points.Add(new Point3D(x, y, z, lbl));
            }
        }
        else if (data is IEnumerable<Point3D> ptList)
        {
            series.Points.AddRange(ptList);
        }
        else if (data is IEnumerable enumerable)
        {
            ParseGenericEnumerable(enumerable, series);
        }

        if (series.Points.Count > 0)
        {
            options.Series.Add(series);
            options.RecalculateBounds();
        }

        return options;
    }

    private static void ParseGenericEnumerable(IEnumerable enumerable, Series3D series)
    {
        PropertyInfo? propX = null, propY = null, propZ = null, propLabel = null;
        bool inspected = false;

        foreach (var item in enumerable)
        {
            if (item == null) continue;

            if (!inspected)
            {
                inspected = true;
                var t = item.GetType();
                propX = t.GetProperty("X", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                propY = t.GetProperty("Y", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                propZ = t.GetProperty("Z", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                propLabel = t.GetProperty("Label", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                            ?? t.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            }

            if (propX != null && propY != null && propZ != null)
            {
                double x = Convert.ToDouble(propX.GetValue(item) ?? 0);
                double y = Convert.ToDouble(propY.GetValue(item) ?? 0);
                double z = Convert.ToDouble(propZ.GetValue(item) ?? 0);
                string? label = propLabel?.GetValue(item)?.ToString();

                series.Points.Add(new Point3D(x, y, z, label));
            }
        }
    }
}
