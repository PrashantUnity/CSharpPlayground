using System;
using System.Collections.Generic;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.ColorMath;

/// <summary>
/// Procedural 11-stop tonal scale synthesizer utilizing non-linear luminance distribution
/// and parabolic saturation envelope matching modern design system curves (50..950).
/// </summary>
public static class TonalScaleEngine
{
    public static readonly int[] StandardStops = [50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 950];

    // Non-linear Lightness curve (0.0 to 1.0)
    public static readonly IReadOnlyDictionary<int, float> LightnessCurve = new Dictionary<int, float>
    {
        [50] = 0.97f,
        [100] = 0.94f,
        [200] = 0.86f,
        [300] = 0.77f,
        [400] = 0.66f,
        [500] = 0.55f,
        [600] = 0.45f,
        [700] = 0.35f,
        [800] = 0.25f,
        [900] = 0.15f,
        [950] = 0.09f
    };

    // Parabolic Saturation envelope multiplier (0.0 to 1.0)
    public static readonly IReadOnlyDictionary<int, float> SaturationEnvelope = new Dictionary<int, float>
    {
        [50] = 0.60f,
        [100] = 0.72f,
        [200] = 0.86f,
        [300] = 0.95f,
        [400] = 1.00f,
        [500] = 1.00f,
        [600] = 1.00f,
        [700] = 0.96f,
        [800] = 0.90f,
        [900] = 0.82f,
        [950] = 0.72f
    };

    /// <summary>
    /// Generates an 11-stop tonal scale for the specified hue and saturation.
    /// </summary>
    public static Dictionary<int, ColorRgb> GenerateScale(float hue, float saturation, bool isFlatSaturation = false)
    {
        var scale = new Dictionary<int, ColorRgb>(11);
        float h = (hue % 360f + 360f) % 360f;
        float baseSat = Math.Clamp(saturation, 0f, 1f);

        foreach (int stop in StandardStops)
        {
            float s = Math.Clamp(baseSat * (isFlatSaturation ? 1.0f : SaturationEnvelope[stop]), 0f, 1f);
            float l = LightnessCurve[stop];
            scale[stop] = new ColorHsl(h, s, l).ToRgb();
        }

        return scale;
    }
}
