using System;
using System.Collections.Generic;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.ColorMath;

/// <summary>The colour models palettes can be built in.</summary>
public enum ColorEngineKind
{
    /// <summary>OKLCH (CSS Color 4; Tailwind 4 and Radix palettes): perceptually even lightness and hue.</summary>
    Oklch,

    /// <summary>HCT (Material Design 3): CAM16 hue and chroma with L* tone, so tone differences predict contrast.</summary>
    Hct,
}

/// <summary>A colour as an engine sees it: hue in degrees, chroma in the engine's units, tone 0 (black) .. 100 (white).</summary>
public readonly record struct PerceptualColor(double Hue, double Chroma, double Tone);

/// <summary>
/// A perceptual colour model: takes colours apart into hue, chroma and tone and puts them back together, always
/// returning a displayable sRGB colour (out-of-gamut requests lose chroma, never hue or tone).
/// </summary>
public interface IColorEngine
{
    ColorEngineKind Kind { get; }

    string DisplayName { get; }

    /// <summary>The chroma of a typical vivid brand colour, in this engine's units (OKLCH about 0.17, HCT about 48).</summary>
    double VividChroma { get; }

    PerceptualColor Decompose(ColorRgb color);

    ColorRgb Compose(double hue, double chroma, double tone);

    /// <summary>The tone (0..100) of a scale stop (50 lightest .. 950 darkest).</summary>
    double ToneOfStop(int stop);

    /// <summary>The 11 stops 50..950 of one hue and chroma, lightest first.</summary>
    IReadOnlyDictionary<int, ColorRgb> TonalScale(double hue, double chroma);

    /// <summary>The hue turned by <paramref name="degrees"/> in this engine's hue circle, 0..360.</summary>
    double RotateHue(double hue, double degrees);

    /// <summary>
    /// This engine's tone for a CIE L* lightness (exact for greys), so surface and text targets can be given once in L*
    /// and mean the same lightness in every engine.
    /// </summary>
    double ToneFromLstar(double lstar);
}

/// <summary>The engines, by kind.</summary>
public static class ColorEngines
{
    public static IColorEngine Oklch { get; } = new OklchEngine();

    public static IColorEngine Hct { get; } = new HctEngine();

    public static IColorEngine Get(ColorEngineKind kind) => kind == ColorEngineKind.Hct ? Hct : Oklch;

    internal static double SanitizeHue(double hue)
    {
        hue %= 360;
        return hue < 0 ? hue + 360 : hue;
    }
}

/// <summary>
/// OKLCH. Tone is OKLab lightness × 100. Scales follow a lightness curve tuned for even perceived steps, with chroma
/// highest in the middle stops (very light and very dark colours can't hold much chroma, and look muddy if forced).
/// </summary>
public sealed class OklchEngine : IColorEngine
{
    private static readonly int[] Stops = [50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 950];

    private static readonly Dictionary<int, (double Lightness, double ChromaShare)> Curve = new()
    {
        [50] = (0.975, 0.14),
        [100] = (0.936, 0.28),
        [200] = (0.885, 0.48),
        [300] = (0.810, 0.72),
        [400] = (0.715, 0.92),
        [500] = (0.635, 1.00),
        [600] = (0.555, 0.98),
        [700] = (0.480, 0.88),
        [800] = (0.410, 0.74),
        [900] = (0.345, 0.60),
        [950] = (0.265, 0.46),
    };

    public ColorEngineKind Kind => ColorEngineKind.Oklch;

    public string DisplayName => "OKLCH";

    public double VividChroma => 0.17;

    public PerceptualColor Decompose(ColorRgb color)
    {
        var lch = ColorMath.Oklch.FromRgb(color);
        return new PerceptualColor(lch.H, lch.C, lch.L * 100);
    }

    public ColorRgb Compose(double hue, double chroma, double tone) =>
        new Oklch(Math.Clamp(tone, 0, 100) / 100, Math.Max(0, chroma), ColorEngines.SanitizeHue(hue)).ToRgb();

    public double ToneOfStop(int stop) => (Curve.TryGetValue(stop, out var point) ? point.Lightness : 0.635) * 100;

    public IReadOnlyDictionary<int, ColorRgb> TonalScale(double hue, double chroma)
    {
        var scale = new Dictionary<int, ColorRgb>(Stops.Length);
        foreach (var stop in Stops)
        {
            var (lightness, share) = Curve[stop];
            scale[stop] = Compose(hue, chroma * share, lightness * 100);
        }

        return scale;
    }

    public double RotateHue(double hue, double degrees) => ColorEngines.SanitizeHue(hue + degrees);

    // For a grey, OKLab L is the cube root of luminance, and L* = 116·∛Y − 16.
    public double ToneFromLstar(double lstar)
    {
        var y = MaterialColorUtils.YFromLstar(Math.Clamp(lstar, 0, 100)) / 100.0;
        return Math.Cbrt(y) * 100;
    }
}

/// <summary>
/// Material Design 3 HCT. Tone is CIE L*; scale stops map to M3 tones (50 → 95 … 950 → 5) at constant chroma, which the
/// solver lowers where sRGB can't show it.
/// </summary>
public sealed class HctEngine : IColorEngine
{
    private static readonly int[] Stops = [50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 950];

    private static readonly Dictionary<int, double> Tones = new()
    {
        [50] = 95, [100] = 90, [200] = 80, [300] = 70, [400] = 60, [500] = 50,
        [600] = 40, [700] = 30, [800] = 20, [900] = 10, [950] = 5,
    };

    public ColorEngineKind Kind => ColorEngineKind.Hct;

    public string DisplayName => "Material HCT";

    public double VividChroma => 48;

    public PerceptualColor Decompose(ColorRgb color)
    {
        var hct = ColorMath.Hct.FromRgb(color);
        return new PerceptualColor(hct.Hue, hct.Chroma, hct.Tone);
    }

    public ColorRgb Compose(double hue, double chroma, double tone) =>
        new Hct(ColorEngines.SanitizeHue(hue), Math.Max(0, chroma), Math.Clamp(tone, 0, 100)).ToRgb();

    public double ToneOfStop(int stop) => Tones.TryGetValue(stop, out var tone) ? tone : 50;

    public IReadOnlyDictionary<int, ColorRgb> TonalScale(double hue, double chroma)
    {
        var scale = new Dictionary<int, ColorRgb>(Stops.Length);
        foreach (var stop in Stops) scale[stop] = Compose(hue, chroma, Tones[stop]);
        return scale;
    }

    public double RotateHue(double hue, double degrees) => ColorEngines.SanitizeHue(hue + degrees);

    public double ToneFromLstar(double lstar) => Math.Clamp(lstar, 0, 100);
}
