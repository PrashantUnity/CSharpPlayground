using System;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.ColorMath;

/// <summary>
/// Advanced Perceptual Contrast Algorithm (APCA) implementation following the W3C Silver / WCAG 3.0 draft specification.
/// Computes perceptual lightness contrast (Lc) between foreground and background colors,
/// accounting for human photoreceptor response, display gamma, and spatial frequency.
/// </summary>
public static class ApcaEngine
{
    // APCA 0.0.98G standard constants
    private const float MainTrc = 2.4f;       // Display transfer function (gamma)
    private const float SRgbRed = 0.2126729f; // sRGB coefficients
    private const float SRgbGreen = 0.7151522f;
    private const float SRgbBlue = 0.0721750f;

    private const float NormBg = 0.56f;
    private const float NormTxt = 0.57f;
    private const float RevBg = 0.65f;
    private const float RevTxt = 0.62f;

    private const float BlkThrs = 0.022f;     // Soft clamp threshold for deep blacks
    private const float BlkClmp = 1.414f;
    private const float ScaleBoW = 1.14f;
    private const float ScaleWoB = 1.14f;
    private const float LoBoWOffset = 0.027f;
    private const float LoWoBOffset = 0.027f;
    private const float DeltaYMin = 0.0005f;

    /// <summary>
    /// Calculates relative luminance Y for APCA with black point soft clamping.
    /// </summary>
    public static float SrgbToApcaY(ColorRgb color)
    {
        float r = MathF.Pow(color.R / 255f, MainTrc);
        float g = MathF.Pow(color.G / 255f, MainTrc);
        float b = MathF.Pow(color.B / 255f, MainTrc);

        float y = SRgbRed * r + SRgbGreen * g + SRgbBlue * b;
        return y > BlkThrs ? y : y + MathF.Pow(BlkThrs - y, BlkClmp);
    }

    /// <summary>
    /// Calculates APCA Lightness Contrast (Lc) score between text and background.
    /// Positive values indicate dark text on light background; negative values indicate light text on dark background.
    /// Output range is approximately -108 to +106.
    /// </summary>
    public static float ContrastLc(ColorRgb text, ColorRgb background)
    {
        float yTxt = SrgbToApcaY(text);
        float yBg = SrgbToApcaY(background);

        if (MathF.Abs(yBg - yTxt) < DeltaYMin)
            return 0f;

        float sapc;
        if (yBg > yTxt) // Dark text on light background
        {
            sapc = (MathF.Pow(yBg, NormBg) - MathF.Pow(yTxt, NormTxt)) * ScaleBoW;
            return sapc < 0.1f ? 0f : (sapc - LoBoWOffset) * 100f;
        }
        else // Light text on dark background
        {
            sapc = (MathF.Pow(yBg, RevBg) - MathF.Pow(yTxt, RevTxt)) * ScaleWoB;
            return sapc > -0.1f ? 0f : (sapc + LoWoBOffset) * 100f;
        }
    }

    /// <summary>
    /// Evaluates readability rating based on absolute Lc score according to APCA conformance levels.
    /// </summary>
    public static string GetRating(float lc)
    {
        float abs = MathF.Abs(lc);
        return abs switch
        {
            >= 90f => "Preferred Body (14px+)",
            >= 75f => "Standard Body (16px+)",
            >= 60f => "Large Text (24px / 18px Bold)",
            >= 45f => "Sub-Headings & Non-Text UI",
            >= 30f => "Placeholder / Decorative",
            _ => "Fail (Insufficient Contrast)"
        };
    }

    /// <summary>
    /// Determines whether the contrast meets the recommended minimum for regular body text (|Lc| &gt;= 75).
    /// </summary>
    public static bool IsBodyTextCompliant(float lc) => MathF.Abs(lc) >= 75f;

    /// <summary>
    /// Determines whether the contrast meets the recommended minimum for large or bold text (|Lc| &gt;= 60).
    /// </summary>
    public static bool IsLargeTextCompliant(float lc) => MathF.Abs(lc) >= 60f;
}
