using System;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.ColorMath;

public enum VisionDeficiency
{
    Normal,
    Protanopia,
    Deuteranopia,
    Tritanopia,
    Achromatopsia
}

/// <summary>
/// Color-blindness simulation engine implementing Machado (2009) linear RGB matrices
/// and CIELAB Delta E distinction analysis to detect confusable status badges and UI elements.
/// </summary>
public static class ColorBlindnessEngine
{
    // Machado 2009 linear RGB transform matrices
    private static readonly float[,] ProtanopiaMatrix =
    {
        { 0.152286f, 1.052583f, -0.204868f },
        { 0.114503f, 0.786281f,  0.099216f },
        { -0.003882f, -0.048116f, 1.051998f }
    };

    private static readonly float[,] DeuteranopiaMatrix =
    {
        { 0.367322f, 0.860646f, -0.227968f },
        { 0.280085f, 0.672501f,  0.047413f },
        { -0.011820f, 0.042940f,  0.968881f }
    };

    private static readonly float[,] TritanopiaMatrix =
    {
        { 1.255528f, -0.076749f, -0.178779f },
        { -0.078411f, 0.930809f,  0.147602f },
        { 0.004733f,  0.691367f,  0.303900f }
    };

    private static float Lin(byte c)
    {
        float x = c / 255f;
        return x <= 0.04045f ? x / 12.92f : MathF.Pow((x + 0.055f) / 1.055f, 2.4f);
    }

    private static byte Unlin(float v)
    {
        float clamped = Math.Clamp(v, 0f, 1f);
        float srgb = clamped <= 0.0031308f ? 12.92f * clamped : 1.055f * MathF.Pow(clamped, 1f / 2.4f) - 0.055f;
        return (byte)Math.Clamp((int)Math.Round(srgb * 255f), 0, 255);
    }

    /// <summary>
    /// Simulates how an sRGB color appears to a person with the specified visual deficiency.
    /// </summary>
    public static ColorRgb Simulate(ColorRgb rgb, VisionDeficiency deficiency)
    {
        if (deficiency == VisionDeficiency.Normal) return rgb;

        if (deficiency == VisionDeficiency.Achromatopsia)
        {
            byte gray = (byte)Math.Clamp((int)Math.Round(255f * MathF.Pow(rgb.RelativeLuminance, 1f / 2.2f)), 0, 255);
            return new ColorRgb(gray, gray, gray);
        }

        float[,] m = deficiency switch
        {
            VisionDeficiency.Protanopia => ProtanopiaMatrix,
            VisionDeficiency.Deuteranopia => DeuteranopiaMatrix,
            VisionDeficiency.Tritanopia => TritanopiaMatrix,
            _ => ProtanopiaMatrix
        };

        float rLin = Lin(rgb.R);
        float gLin = Lin(rgb.G);
        float bLin = Lin(rgb.B);

        float outR = m[0, 0] * rLin + m[0, 1] * gLin + m[0, 2] * bLin;
        float outG = m[1, 0] * rLin + m[1, 1] * gLin + m[1, 2] * bLin;
        float outB = m[2, 0] * rLin + m[2, 1] * gLin + m[2, 2] * bLin;

        return new ColorRgb(Unlin(outR), Unlin(outG), Unlin(outB));
    }

    /// <summary>
    /// Converts sRGB to CIELAB (L*, a*, b*) under D65 standard illuminant.
    /// </summary>
    public static (float L, float A, float B) RgbToLab(ColorRgb rgb)
    {
        float r = Lin(rgb.R);
        float g = Lin(rgb.G);
        float b = Lin(rgb.B);

        float x = (r * 0.4124f + g * 0.3576f + b * 0.1805f) / 0.95047f;
        float y = (r * 0.2126f + g * 0.7152f + b * 0.0722f) / 1.00000f;
        float z = (r * 0.0193f + g * 0.1192f + b * 0.9505f) / 1.08883f;

        static float F(float t) => t > 0.008856f ? MathF.Cbrt(t) : 7.787f * t + 16f / 116f;

        float fx = F(x);
        float fy = F(y);
        float fz = F(z);

        float l = 116f * fy - 16f;
        float a = 500f * (fx - fy);
        float bStar = 200f * (fy - fz);

        return (l, a, bStar);
    }

    /// <summary>
    /// Computes CIE76 Delta E perceptual color distance between two colors.
    /// Delta E &lt; 12 indicates high probability of visual confusion for color-deficient users.
    /// </summary>
    public static float DeltaE(ColorRgb c1, ColorRgb c2)
    {
        var (l1, a1, b1) = RgbToLab(c1);
        var (l2, a2, b2) = RgbToLab(c2);

        float dl = l1 - l2;
        float da = a1 - a2;
        float db = b1 - b2;

        return MathF.Sqrt(dl * dl + da * da + db * db);
    }

    /// <summary>
    /// Returns true if two colors are perceptually distinct under the specified vision condition (Delta E &gt;= 12).
    /// </summary>
    public static bool IsDistinct(ColorRgb c1, ColorRgb c2, VisionDeficiency deficiency, float threshold = 12f)
    {
        var sim1 = Simulate(c1, deficiency);
        var sim2 = Simulate(c2, deficiency);
        return DeltaE(sim1, sim2) >= threshold;
    }
}
