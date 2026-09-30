using System;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Rendering;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting3D.Services;

/// <summary>
/// The older way to turn data into a 3D plot's model. It reads data exactly as <see cref="Plot3DSpecBuilder"/> does
/// (points of any numbers, records, surfaces, graphs) and gives the model the 3D control draws from that spec.
/// </summary>
public static class Plot3DDataParser
{
    public static Plot3DOptions Parse(
        object? data,
        string? title = null,
        string? color = null,
        string? plotType = null,
        ColorMapPreset? colorMap = null)
    {
        // A model given is changed in place, as it always was.
        if (data is Plot3DOptions existing)
        {
            if (title != null) existing.Title = title;
            if (color != null) existing.PrimaryColor = color;
            if (colorMap.HasValue) existing.ColorMap = colorMap.Value;
            return existing;
        }

        Plot3DType? kind = !string.IsNullOrEmpty(plotType) && Enum.TryParse<Plot3DType>(plotType, true, out var parsed) ? parsed : null;
        var spec = Plot3DSpecBuilder.From(data, kind);
        spec.Title = title ?? spec.Title;
        if (color != null) spec.Color = color;
        if (colorMap.HasValue) spec.ColorMap = colorMap.Value;
        return Plot3DRenderModelBuilder.Build(spec);
    }
}
