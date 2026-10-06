using System;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.ColorMath;

/// <summary>
/// A colour in OKLab (Björn Ottosson, 2020; CSS Color 4): <see cref="L"/> is perceived lightness 0..1, <see cref="A"/>
/// green–red and <see cref="B"/> blue–yellow. Equal distances look like equal differences, which HSL does not give:
/// that is why palettes built here look even across hues.
/// </summary>
public readonly record struct Oklab(double L, double A, double B)
{
    public static Oklab FromRgb(ColorRgb color) =>
        FromLinearRgb(SrgbTransfer.ToLinear(color.R / 255.0), SrgbTransfer.ToLinear(color.G / 255.0), SrgbTransfer.ToLinear(color.B / 255.0));

    /// <summary>From linear-light sRGB channels (0..1, may be outside it for colours outside sRGB).</summary>
    public static Oklab FromLinearRgb(double r, double g, double b)
    {
        var l = Math.Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
        var m = Math.Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
        var s = Math.Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);
        return new Oklab(
            0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
            1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
            0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s);
    }

    /// <summary>Linear-light sRGB channels; outside 0..1 when the colour is outside sRGB.</summary>
    public (double R, double G, double B) ToLinearRgb()
    {
        var l = L + 0.3963377774 * A + 0.2158037573 * B;
        var m = L - 0.1055613458 * A - 0.0638541728 * B;
        var s = L - 0.0894841775 * A - 1.2914855480 * B;
        l = l * l * l;
        m = m * m * m;
        s = s * s * s;
        return (
            4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
            -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
            -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s);
    }

    /// <summary>Whether the colour is displayable in sRGB (within a rounding tolerance).</summary>
    public bool IsInSrgbGamut
    {
        get
        {
            const double tolerance = 1e-6;
            var (r, g, b) = ToLinearRgb();
            return r >= -tolerance && r <= 1 + tolerance && g >= -tolerance && g <= 1 + tolerance && b >= -tolerance && b <= 1 + tolerance;
        }
    }

    /// <summary>The sRGB colour with every channel clamped (what a screen does with an out-of-gamut colour).</summary>
    public ColorRgb ToRgbClipped()
    {
        var (r, g, b) = ToLinearRgb();
        return new ColorRgb(SrgbTransfer.ToByte(r), SrgbTransfer.ToByte(g), SrgbTransfer.ToByte(b));
    }

    public Oklch ToOklch()
    {
        var c = Math.Sqrt(A * A + B * B);
        var h = c < 1e-9 ? 0 : Math.Atan2(B, A) * 180 / Math.PI;
        return new Oklch(L, c, h < 0 ? h + 360 : h);
    }

    /// <summary>ΔEOK: the distance between two colours (0.02 is about the smallest difference people notice).</summary>
    public double DeltaE(Oklab other)
    {
        var dl = L - other.L;
        var da = A - other.A;
        var db = B - other.B;
        return Math.Sqrt(dl * dl + da * da + db * db);
    }
}

/// <summary>
/// OKLab in polar form: lightness <see cref="L"/> 0..1, chroma <see cref="C"/> (0 grey, about 0.37 at most in sRGB) and
/// hue <see cref="H"/> in degrees.
/// </summary>
public readonly record struct Oklch(double L, double C, double H)
{
    /// <summary>The smallest colour difference people notice (CSS Color 4 gamut mapping).</summary>
    public const double JustNoticeableDifference = 0.02;

    public static Oklch FromRgb(ColorRgb color) => Oklab.FromRgb(color).ToOklch();

    public Oklab ToOklab()
    {
        var radians = H * Math.PI / 180;
        return new Oklab(L, C * Math.Cos(radians), C * Math.Sin(radians));
    }

    public bool IsInSrgbGamut => ToOklab().IsInSrgbGamut;

    /// <summary>
    /// The displayable colour closest in look: keeps lightness and hue and lowers chroma until clipping no longer shows
    /// (the CSS Color 4 gamut-mapping algorithm), so a too-vivid request never turns into a different hue.
    /// </summary>
    public ColorRgb ToRgb() => GamutMapped().ToOklab().ToRgbClipped();

    /// <summary>This colour brought into sRGB by the CSS Color 4 algorithm (binary search on chroma, ΔEOK below 0.02).</summary>
    public Oklch GamutMapped()
    {
        if (L >= 1) return new Oklch(1, 0, H);
        if (L <= 0) return new Oklch(0, 0, H);
        if (IsInSrgbGamut) return this;

        const double epsilon = 0.0001;
        var clipped = Clip(this);
        if (clipped.DeltaE(ToOklab()) < JustNoticeableDifference) return clipped.ToOklch();

        double min = 0, max = C;
        var minInGamut = true;
        var current = this;
        while (max - min > epsilon)
        {
            var chroma = (min + max) / 2;
            current = this with { C = chroma };
            if (minInGamut && current.IsInSrgbGamut)
            {
                min = chroma;
                continue;
            }

            clipped = Clip(current);
            var e = clipped.DeltaE(current.ToOklab());
            if (e < JustNoticeableDifference)
            {
                if (JustNoticeableDifference - e < epsilon) return clipped.ToOklch();
                minInGamut = false;
                min = chroma;
            }
            else
            {
                max = chroma;
            }
        }

        return Clip(current).ToOklch();
    }

    // Clamps the linear channels into sRGB.
    private static Oklab Clip(Oklch color)
    {
        var (r, g, b) = color.ToOklab().ToLinearRgb();
        return Oklab.FromLinearRgb(Math.Clamp(r, 0, 1), Math.Clamp(g, 0, 1), Math.Clamp(b, 0, 1));
    }
}

/// <summary>The sRGB transfer function (IEC 61966-2-1), in double precision.</summary>
public static class SrgbTransfer
{
    /// <summary>Encoded channel 0..1 to linear light.</summary>
    public static double ToLinear(double encoded) =>
        encoded <= 0.04045 ? encoded / 12.92 : Math.Pow((encoded + 0.055) / 1.055, 2.4);

    /// <summary>Linear light to encoded channel 0..1.</summary>
    public static double ToEncoded(double linear) =>
        linear <= 0.0031308 ? linear * 12.92 : 1.055 * Math.Pow(linear, 1 / 2.4) - 0.055;

    /// <summary>Linear light to an 8-bit channel, clamped.</summary>
    public static byte ToByte(double linear) => (byte)Math.Clamp((int)Math.Round(ToEncoded(Math.Clamp(linear, 0, 1)) * 255), 0, 255);
}
