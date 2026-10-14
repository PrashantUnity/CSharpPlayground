using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting3D.Services;

/// <summary>The colour maps of <see cref="ColorMaps"/> as Avalonia colours, for the desktop's 3D renderers.</summary>
public static class ColorMapService
{
    public static Color GetColor(ColorMapPreset preset, double t) => ToColor(ColorMaps.Sample(preset, t));

    public static string GetHexColor(ColorMapPreset preset, double t) => ColorMaps.GetHexColor(preset, t);

    public static Color HsvToRgb(double hue, double saturation, double value) => ToColor(ColorMaps.HsvToRgb(hue, saturation, value));

    private static Color ToColor(RgbColor c) => Color.FromRgb(c.R, c.G, c.B);
}
