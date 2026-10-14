using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting3D.Services;

/// <summary>An opaque colour as three bytes.</summary>
public readonly record struct RgbColor(byte R, byte G, byte B)
{
    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

    public static RgbColor FromHex(string hex)
    {
        var s = hex.AsSpan().TrimStart('#');
        if (s.Length != 6) throw new FormatException($"'{hex}' is not a #rrggbb colour.");
        return new RgbColor(
            byte.Parse(s[..2], System.Globalization.NumberStyles.HexNumber),
            byte.Parse(s[2..4], System.Globalization.NumberStyles.HexNumber),
            byte.Parse(s[4..], System.Globalization.NumberStyles.HexNumber));
    }
}

/// <summary>The colour maps of 3D plots and grids: a value from 0 to 1 gives a colour, the same in every host.</summary>
public static class ColorMaps
{
    public static RgbColor Sample(ColorMapPreset preset, double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        return preset switch
        {
            ColorMapPreset.Plasma => Between(PlasmaStops, t),
            ColorMapPreset.CoolWarm => Between(CoolWarmStops, t),
            ColorMapPreset.Turbo => Between(TurboStops, t),
            ColorMapPreset.Rainbow => SampleRainbow(t),
            ColorMapPreset.Ocean => SampleOcean(t),
            ColorMapPreset.Fire => SampleFire(t),
            _ => Between(ViridisStops, t)
        };
    }

    public static string GetHexColor(ColorMapPreset preset, double t) => Sample(preset, t).ToHex();

    // The published palettes, as evenly spaced stops (matplotlib's viridis and plasma, Google's Turbo, Moreland's
    // cool-warm), read between them in a straight line: close to the originals, which are tables of 256.
    private static readonly RgbColor[] ViridisStops = Stops("#440154", "#482878", "#3E4A89", "#31688E", "#26828E", "#1F9E89", "#35B779", "#6DCD59", "#B4DE2C", "#FDE725");
    private static readonly RgbColor[] PlasmaStops = Stops("#0D0887", "#46039F", "#7201A8", "#9C179E", "#BD3786", "#D8576B", "#ED7953", "#FB9F3A", "#FDCA26", "#F0F921");
    private static readonly RgbColor[] TurboStops = Stops("#30123B", "#4662D7", "#36AAF9", "#1AE4B6", "#72FE5E", "#C8EF34", "#FABA39", "#F66B19", "#CA2A04", "#7A0403");
    private static readonly RgbColor[] CoolWarmStops = Stops("#3B4CC0", "#6788EE", "#9ABBFF", "#C9D7F0", "#EDD1C2", "#F7A889", "#E26952", "#B40426");

    private static RgbColor[] Stops(params string[] hex) => hex.Select(RgbColor.FromHex).ToArray();

    private static RgbColor Between(RgbColor[] stops, double t)
    {
        var at = t * (stops.Length - 1);
        var i = Math.Min((int)at, stops.Length - 2);
        var f = at - i;
        var (a, b) = (stops[i], stops[i + 1]);
        return new RgbColor(
            (byte)Math.Round(a.R + (b.R - a.R) * f),
            (byte)Math.Round(a.G + (b.G - a.G) * f),
            (byte)Math.Round(a.B + (b.B - a.B) * f));
    }

    // Rainbow: blue to red.
    private static RgbColor SampleRainbow(double t) => HsvToRgb((1.0 - t) * 240.0, 0.9, 0.95);

    // Ocean: deep navy -> turquoise -> pale cyan.
    private static RgbColor SampleOcean(double t) => new(
        (byte)Math.Clamp(10 + 120 * t, 0, 255),
        (byte)Math.Clamp(40 + 200 * t, 0, 255),
        (byte)Math.Clamp(90 + 165 * t, 0, 255));

    // Fire: black -> red -> orange -> yellow -> white.
    private static RgbColor SampleFire(double t) => new(
        (byte)Math.Clamp(t * 300, 0, 255),
        (byte)Math.Clamp(t > 0.35 ? (t - 0.35) * 350 : 0, 0, 255),
        (byte)Math.Clamp(t > 0.75 ? (t - 0.75) * 800 : 0, 0, 255));

    public static RgbColor HsvToRgb(double hue, double saturation, double value)
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
            0 => new RgbColor(v, t, p),
            1 => new RgbColor(q, v, p),
            2 => new RgbColor(p, v, t),
            3 => new RgbColor(p, q, v),
            4 => new RgbColor(t, p, v),
            _ => new RgbColor(v, p, q)
        };
    }
}
