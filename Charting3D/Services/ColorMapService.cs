using System;
using System.Linq;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting3D.Services;

public static class ColorMapService
{
    public static Color GetColor(ColorMapPreset preset, double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        return preset switch
        {
            ColorMapPreset.Plasma => SamplePlasma(t),
            ColorMapPreset.CoolWarm => SampleCoolWarm(t),
            ColorMapPreset.Turbo => SampleTurbo(t),
            ColorMapPreset.Rainbow => SampleRainbow(t),
            ColorMapPreset.Ocean => SampleOcean(t),
            ColorMapPreset.Fire => SampleFire(t),
            _ => SampleViridis(t)
        };
    }

    public static string GetHexColor(ColorMapPreset preset, double t)
    {
        var c = GetColor(preset, t);
        return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    }

    // The published palettes, as evenly spaced stops (matplotlib's viridis and plasma, Google's Turbo, Moreland's
    // cool-warm), read between them in a straight line: close to the originals, which are tables of 256.
    private static readonly Color[] ViridisStops = Stops("#440154", "#482878", "#3E4A89", "#31688E", "#26828E", "#1F9E89", "#35B779", "#6DCD59", "#B4DE2C", "#FDE725");
    private static readonly Color[] PlasmaStops = Stops("#0D0887", "#46039F", "#7201A8", "#9C179E", "#BD3786", "#D8576B", "#ED7953", "#FB9F3A", "#FDCA26", "#F0F921");
    private static readonly Color[] TurboStops = Stops("#30123B", "#4662D7", "#36AAF9", "#1AE4B6", "#72FE5E", "#C8EF34", "#FABA39", "#F66B19", "#CA2A04", "#7A0403");
    private static readonly Color[] CoolWarmStops = Stops("#3B4CC0", "#6788EE", "#9ABBFF", "#C9D7F0", "#EDD1C2", "#F7A889", "#E26952", "#B40426");

    private static Color SampleViridis(double t) => Between(ViridisStops, t);

    private static Color SamplePlasma(double t) => Between(PlasmaStops, t);

    private static Color SampleCoolWarm(double t) => Between(CoolWarmStops, t);

    private static Color SampleTurbo(double t) => Between(TurboStops, t);

    private static Color[] Stops(params string[] hex) => hex.Select(Color.Parse).ToArray();

    private static Color Between(Color[] stops, double t)
    {
        var at = t * (stops.Length - 1);
        var i = Math.Min((int)at, stops.Length - 2);
        var f = at - i;
        var (a, b) = (stops[i], stops[i + 1]);
        return Color.FromRgb(
            (byte)Math.Round(a.R + (b.R - a.R) * f),
            (byte)Math.Round(a.G + (b.G - a.G) * f),
            (byte)Math.Round(a.B + (b.B - a.B) * f));
    }

    // Rainbow
    private static Color SampleRainbow(double t)
    {
        double h = (1.0 - t) * 240.0; // Blue to Red
        return HsvToRgb(h, 0.9, 0.95);
    }

    // Ocean: Deep Navy -> Turquoise -> Pale Cyan
    private static Color SampleOcean(double t)
    {
        byte r = (byte)Math.Clamp(10 + 120 * t, 0, 255);
        byte g = (byte)Math.Clamp(40 + 200 * t, 0, 255);
        byte b = (byte)Math.Clamp(90 + 165 * t, 0, 255);
        return Color.FromRgb(r, g, b);
    }

    // Fire: Black -> Red -> Orange -> Yellow -> White
    private static Color SampleFire(double t)
    {
        byte r = (byte)Math.Clamp(t * 300, 0, 255);
        byte g = (byte)Math.Clamp(t > 0.35 ? (t - 0.35) * 350 : 0, 0, 255);
        byte b = (byte)Math.Clamp(t > 0.75 ? (t - 0.75) * 800 : 0, 0, 255);
        return Color.FromRgb(r, g, b);
    }

    public static Color HsvToRgb(double hue, double saturation, double value)
    {
        int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
        double f = hue / 60 - Math.Floor(hue / 60);

        value *= 255;
        byte v = (byte)Math.Clamp(value, 0, 255);
        byte p = (byte)Math.Clamp(value * (1 - saturation), 0, 255);
        byte q = (byte)Math.Clamp(value * (1 - f * saturation), 0, 255);
        byte t = (byte)Math.Clamp(value * (1 - (1 - f) * saturation), 0, 255);

        return hi switch
        {
            0 => Color.FromRgb(v, t, p),
            1 => Color.FromRgb(q, v, p),
            2 => Color.FromRgb(p, v, t),
            3 => Color.FromRgb(p, q, v),
            4 => Color.FromRgb(t, p, v),
            _ => Color.FromRgb(v, p, q)
        };
    }
}
