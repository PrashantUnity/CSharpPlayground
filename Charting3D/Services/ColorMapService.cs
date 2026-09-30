using System;
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

    // Viridis: Purple -> Blue -> Teal -> Green -> Yellow
    private static Color SampleViridis(double t)
    {
        double r = Math.Sin(t * Math.PI * 0.85) * 230 + 35 * (1 - t) + 253 * Math.Pow(t, 4);
        double g = Math.Sin(t * Math.PI) * 200 + 231 * Math.Pow(t, 2);
        double b = Math.Cos(t * Math.PI * 0.8) * 120 + 130 * (1 - t) + 36 * t;
        return Color.FromRgb(
            (byte)Math.Clamp(r, 0, 255),
            (byte)Math.Clamp(g, 0, 255),
            (byte)Math.Clamp(b, 0, 255));
    }

    // Plasma: Dark Blue -> Violet -> Magenta -> Orange -> Yellow
    private static Color SamplePlasma(double t)
    {
        byte r = (byte)Math.Clamp(13 + 240 * Math.Pow(t, 0.8), 0, 255);
        byte g = (byte)Math.Clamp(8 + 200 * Math.Pow(t, 2.5) + (t > 0.6 ? (t - 0.6) * 120 : 0), 0, 255);
        byte b = (byte)Math.Clamp(135 * (1 - t) + 240 * Math.Sin(t * Math.PI), 0, 255);
        return Color.FromRgb(r, g, b);
    }

    // CoolWarm: Cyan/Blue -> White/Gray -> Red/Orange
    private static Color SampleCoolWarm(double t)
    {
        byte r = (byte)Math.Clamp(59 + 196 * t, 0, 255);
        byte g = (byte)Math.Clamp(76 + 150 * (1 - Math.Abs(t - 0.5) * 2), 0, 255);
        byte b = (byte)Math.Clamp(192 + 63 * (1 - t), 0, 255);
        return Color.FromRgb(r, g, b);
    }

    // Turbo: Smooth Google Turbo rainbow
    private static Color SampleTurbo(double t)
    {
        byte r = (byte)Math.Clamp(34 + 221 * Math.Sin(Math.Max(0, t - 0.2) * Math.PI * 1.2), 0, 255);
        byte g = (byte)Math.Clamp(230 * Math.Sin(t * Math.PI), 0, 255);
        byte b = (byte)Math.Clamp(150 * (1 - t) + 100 * Math.Cos(t * Math.PI * 0.5), 0, 255);
        return Color.FromRgb(r, g, b);
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
