using System;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.ColorMath;

/// <summary>
/// Immutable, allocation-free sRGB color representation with WCAG 2.1 relative luminance and contrast calculations.
/// </summary>
public readonly record struct ColorRgb(byte R, byte G, byte B)
{
    public static readonly ColorRgb Black = new(0, 0, 0);
    public static readonly ColorRgb White = new(255, 255, 255);

    /// <summary>
    /// Formats as standard #RRGGBB hex string.
    /// </summary>
    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

    /// <summary>
    /// Formats as #AARRGGBB hex string (standard Avalonia/WPF alpha format).
    /// </summary>
    public string ToHexWithAlpha(float alpha)
    {
        byte a = (byte)Math.Clamp((int)Math.Round(alpha * 255f), 0, 255);
        return $"#{a:X2}{R:X2}{G:X2}{B:X2}";
    }

    /// <summary>
    /// Linearized channel value for photometric / colorimetric calculations.
    /// </summary>
    private static float Linearize(byte c)
    {
        float v = c / 255f;
        return v <= 0.04045f ? v / 12.92f : MathF.Pow((v + 0.055f) / 1.055f, 2.4f);
    }

    /// <summary>
    /// WCAG 2.1 relative luminance (0.0 to 1.0).
    /// </summary>
    public float RelativeLuminance => 0.2126f * Linearize(R) + 0.7152f * Linearize(G) + 0.0722f * Linearize(B);

    /// <summary>
    /// Computes WCAG 2.1 contrast ratio between this color and another (1.0:1 to 21.0:1).
    /// </summary>
    public float ContrastRatio(ColorRgb other)
    {
        float l1 = RelativeLuminance;
        float l2 = other.RelativeLuminance;
        float lighter = Math.Max(l1, l2);
        float darker = Math.Min(l1, l2);
        return (lighter + 0.05f) / (darker + 0.05f);
    }

    /// <summary>
    /// Converts RGB to HSL coordinates.
    /// </summary>
    public ColorHsl ToHsl()
    {
        float r = R / 255f;
        float g = G / 255f;
        float b = B / 255f;

        float max = Math.Max(r, Math.Max(g, b));
        float min = Math.Min(r, Math.Min(g, b));
        float delta = max - min;

        float l = (max + min) / 2f;
        float h = 0f;
        float s = 0f;

        if (delta > 0.00001f)
        {
            s = l > 0.5f ? delta / (2f - max - min) : delta / (max + min);

            if (Math.Abs(max - r) < 0.00001f)
            {
                h = (g - b) / delta + (g < b ? 6f : 0f);
            }
            else if (Math.Abs(max - g) < 0.00001f)
            {
                h = (b - r) / delta + 2f;
            }
            else
            {
                h = (r - g) / delta + 4f;
            }

            h *= 60f;
        }

        return new ColorHsl(h, s, l);
    }

    /// <summary>
    /// Parses a hex color string (#RGB, #RRGGBB, or #AARRGGBB).
    /// </summary>
    public static ColorRgb FromHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return Black;

        ReadOnlySpan<char> span = hex.Trim().AsSpan();
        if (span.StartsWith("#")) span = span[1..];

        if (span.Length == 3)
        {
            byte r = Convert.ToByte(new string(span[0], 2), 16);
            byte g = Convert.ToByte(new string(span[1], 2), 16);
            byte b = Convert.ToByte(new string(span[2], 2), 16);
            return new ColorRgb(r, g, b);
        }

        if (span.Length == 6)
        {
            byte r = Convert.ToByte(span[..2].ToString(), 16);
            byte g = Convert.ToByte(span.Slice(2, 2).ToString(), 16);
            byte b = Convert.ToByte(span.Slice(4, 2).ToString(), 16);
            return new ColorRgb(r, g, b);
        }

        if (span.Length == 8)
        {
            // Avalonia/WPF ARGB format: skip Alpha, read R, G, B
            byte r = Convert.ToByte(span.Slice(2, 2).ToString(), 16);
            byte g = Convert.ToByte(span.Slice(4, 2).ToString(), 16);
            byte b = Convert.ToByte(span.Slice(6, 2).ToString(), 16);
            return new ColorRgb(r, g, b);
        }

        return Black;
    }
}
