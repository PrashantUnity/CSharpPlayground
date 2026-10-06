using System;
using System.Collections.Generic;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.ColorMath;

/// <summary>
/// Greedy perceptual contrast solver testing candidate scale stops against target surfaces
/// to guarantee WCAG 2.1 AA (4.5:1) or AAA (7.0:1) accessibility compliance.
/// </summary>
public static class ContrastSolver
{
    public readonly record struct PickResult(int Stop, ColorRgb Color, float Ratio, bool IsSatisfied);

    /// <summary>
    /// Evaluates candidate stops sequentially from the given scale against target background.
    /// Returns the first stop meeting minRatio, or the stop with highest contrast if none meet it.
    /// </summary>
    public static PickResult PickStop(
        IReadOnlyDictionary<int, ColorRgb> scale,
        ReadOnlySpan<int> candidateStops,
        ColorRgb targetBackground,
        float minRatio = 4.5f)
    {
        if (candidateStops.IsEmpty)
        {
            return new PickResult(500, scale.TryGetValue(500, out var fallback) ? fallback : ColorRgb.White, 1f, false);
        }

        int bestStop = candidateStops[0];
        float bestRatio = 0f;

        foreach (int stop in candidateStops)
        {
            if (!scale.TryGetValue(stop, out var color)) continue;
            float ratio = color.ContrastRatio(targetBackground);

            if (ratio >= minRatio)
            {
                return new PickResult(stop, color, ratio, true);
            }

            if (ratio > bestRatio)
            {
                bestRatio = ratio;
                bestStop = stop;
            }
        }

        return new PickResult(bestStop, scale[bestStop], bestRatio, false);
    }
}
