using System;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.ColorMath;

/// <summary>
/// Immutable, allocation-free HSL color coordinates (Hue: 0..360, Saturation: 0..1, Lightness: 0..1).
/// </summary>
public readonly record struct ColorHsl(float H, float S, float L)
{
    public ColorHsl Clamp() => new(
        (H % 360f + 360f) % 360f,
        Math.Clamp(S, 0f, 1f),
        Math.Clamp(L, 0f, 1f)
    );

    public ColorHsl WithHue(float h) => new(h, S, L);
    public ColorHsl WithSaturation(float s) => new(H, s, L);
    public ColorHsl WithLightness(float l) => new(H, S, l);

    /// <summary>
    /// Converts HSL to sRGB using piecewise trigonometric transfer.
    /// </summary>
    public ColorRgb ToRgb()
    {
        float h = (H % 360f + 360f) % 360f;
        float s = Math.Clamp(S, 0f, 1f);
        float l = Math.Clamp(L, 0f, 1f);

        float k(float n) => (n + h / 30f) % 12f;
        float a = s * Math.Min(l, 1f - l);
        float f(float n) => l - a * Math.Max(-1f, Math.Min(k(n) - 3f, Math.Min(9f - k(n), 1f)));

        byte r = (byte)Math.Clamp((int)Math.Round(255f * f(0f)), 0, 255);
        byte g = (byte)Math.Clamp((int)Math.Round(255f * f(8f)), 0, 255);
        byte b = (byte)Math.Clamp((int)Math.Round(255f * f(4f)), 0, 255);

        return new ColorRgb(r, g, b);
    }

    public string ToHex() => ToRgb().ToHex();

    public static ColorHsl FromRgb(ColorRgb rgb) => rgb.ToHsl();

    public static ColorHsl FromHex(string hex) => ColorRgb.FromHex(hex).ToHsl();
}
