using System;
using System.Collections.Generic;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.ColorMath;

/// <summary>
/// Finds colours that meet a contrast target against a surface: <see cref="SolveTone"/> searches the tone of a hue and
/// chroma (WCAG 2.2 ratio and, optionally, APCA Lc together); <see cref="PickStop"/> picks from a fixed scale.
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

    public readonly record struct ToneResult(double Tone, ColorRgb Color, float Ratio, float ApcaLc, bool IsSatisfied);

    /// <summary>Whether <paramref name="foreground"/> on <paramref name="background"/> meets both targets.</summary>
    public static bool Meets(ColorRgb foreground, ColorRgb background, float minRatio, float minApcaLc = 0f) =>
        foreground.ContrastRatio(background) >= minRatio && MathF.Abs(ApcaEngine.ContrastLc(foreground, background)) >= minApcaLc;

    /// <summary>
    /// The colour of <paramref name="hue"/> and <paramref name="chroma"/> whose tone is closest to
    /// <paramref name="desiredTone"/> while reaching <paramref name="minRatio"/> (WCAG) and <paramref name="minApcaLc"/>
    /// (APCA, 0 to ignore) on <paramref name="background"/>. It moves away from the background (lighter on a dark one,
    /// darker on a light one); when that side can't reach the target it tries the other side, and when neither can it
    /// returns the strongest it found with <see cref="ToneResult.IsSatisfied"/> false.
    /// </summary>
    public static ToneResult SolveTone(IColorEngine engine, double hue, double chroma, double desiredTone, ColorRgb background, float minRatio = 4.5f, float minApcaLc = 0f)
    {
        ArgumentNullException.ThrowIfNull(engine);
        desiredTone = Math.Clamp(desiredTone, 0, 100);
        var desired = engine.Compose(hue, chroma, desiredTone);
        if (Meets(desired, background, minRatio, minApcaLc)) return Result(desiredTone, desired, true);

        var backgroundTone = engine.Decompose(background).Tone;
        var lighterFirst = desiredTone >= backgroundTone;
        var preferred = Search(lighterFirst ? 100 : 0, desiredTone);
        if (preferred.IsSatisfied) return preferred;

        var other = Search(lighterFirst ? 0 : 100, backgroundTone);
        if (other.IsSatisfied) return other;
        return preferred.Ratio >= other.Ratio ? preferred : other;

        // The tone nearest to `from` (moving towards `extreme`) that meets the targets; contrast grows towards the extreme.
        ToneResult Search(double extreme, double from)
        {
            var extremeColor = engine.Compose(hue, chroma, extreme);
            if (!Meets(extremeColor, background, minRatio, minApcaLc)) return Result(extreme, extremeColor, false);
            double near = from, far = extreme;
            for (var i = 0; i < 18; i++)
            {
                var mid = (near + far) / 2;
                if (Meets(engine.Compose(hue, chroma, mid), background, minRatio, minApcaLc)) far = mid;
                else near = mid;
            }

            return Result(far, engine.Compose(hue, chroma, far), true);
        }

        ToneResult Result(double tone, ColorRgb color, bool satisfied) =>
            new(tone, color, color.ContrastRatio(background), ApcaEngine.ContrastLc(color, background), satisfied);
    }
}
