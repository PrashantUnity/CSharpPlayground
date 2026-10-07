using System;
using System.Collections.Generic;
using System.Globalization;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Layout;

/// <summary>Ticks for a time axis: x holds Unix milliseconds (UTC), and ticks fall on calendar units (seconds to years).</summary>
internal static class TimeTicks
{
    private enum Unit { Fixed, Month, Year }

    private readonly record struct Step(Unit Unit, double Milliseconds, int Count, string Format);

    private const double Second = 1000;
    private const double Minute = 60 * Second;
    private const double Hour = 60 * Minute;
    private const double Day = 24 * Hour;

    // From finest to coarsest; the first whose step gives few enough ticks is the one used.
    private static readonly Step[] Steps =
    [
        new(Unit.Fixed, Second, 1, "HH:mm:ss"), new(Unit.Fixed, Second * 5, 1, "HH:mm:ss"), new(Unit.Fixed, Second * 15, 1, "HH:mm:ss"), new(Unit.Fixed, Second * 30, 1, "HH:mm:ss"),
        new(Unit.Fixed, Minute, 1, "HH:mm"), new(Unit.Fixed, Minute * 5, 1, "HH:mm"), new(Unit.Fixed, Minute * 15, 1, "HH:mm"), new(Unit.Fixed, Minute * 30, 1, "HH:mm"),
        new(Unit.Fixed, Hour, 1, "HH:mm"), new(Unit.Fixed, Hour * 3, 1, "HH:mm"), new(Unit.Fixed, Hour * 6, 1, "HH:mm"), new(Unit.Fixed, Hour * 12, 1, "d MMM HH:mm"),
        new(Unit.Fixed, Day, 1, "d MMM"), new(Unit.Fixed, Day * 2, 1, "d MMM"), new(Unit.Fixed, Day * 7, 1, "d MMM"),
        new(Unit.Month, 0, 1, "MMM yyyy"), new(Unit.Month, 0, 3, "MMM yyyy"), new(Unit.Month, 0, 6, "MMM yyyy"),
        new(Unit.Year, 0, 1, "yyyy"), new(Unit.Year, 0, 2, "yyyy"), new(Unit.Year, 0, 5, "yyyy"), new(Unit.Year, 0, 10, "yyyy"), new(Unit.Year, 0, 25, "yyyy"), new(Unit.Year, 0, 50, "yyyy"), new(Unit.Year, 0, 100, "yyyy")
    ];

    private static readonly DateTime Epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // The range DateTime can hold, in Unix milliseconds.
    private static readonly double MinMs = (DateTime.MinValue.ToUniversalTime() - Epoch).TotalMilliseconds + 1;
    private static readonly double MaxMs = (DateTime.MaxValue - Epoch).TotalMilliseconds - 1;

    public static DateTime ToDate(double ms) => Epoch.AddMilliseconds(Math.Clamp(ms, MinMs, MaxMs));

    /// <summary>The ticks from <paramref name="min"/> to <paramref name="max"/> (Unix ms), at most about <paramref name="maxTicks"/> of them.</summary>
    public static IEnumerable<(double Value, string Label)> Between(double min, double max, int maxTicks)
    {
        if (!(max > min) || !double.IsFinite(min) || !double.IsFinite(max)) yield break;
        maxTicks = Math.Max(2, maxTicks);
        var span = max - min;
        var step = Steps[^1];
        foreach (var candidate in Steps)
        {
            var width = candidate.Unit switch
            {
                Unit.Fixed => candidate.Milliseconds,
                Unit.Month => candidate.Count * 30.44 * Day,
                _ => candidate.Count * 365.25 * Day
            };
            if (span / width <= maxTicks)
            {
                step = candidate;
                break;
            }
        }

        // Labels carry the year when the axis spans more than one and the step is finer than a year.
        var format = step.Format;
        if (step.Unit == Unit.Fixed && span > 300 * Day && step.Milliseconds >= Day) format = "d MMM yyyy";

        if (step.Unit == Unit.Fixed)
        {
            var first = Math.Ceiling(min / step.Milliseconds - 1e-9) * step.Milliseconds;
            for (var t = first; t <= max + 1e-6; t += step.Milliseconds)
            {
                yield return (t, ToDate(t).ToString(format, CultureInfo.InvariantCulture));
            }

            yield break;
        }

        var start = ToDate(min);
        var year = start.Year;
        var month = step.Unit == Unit.Year ? 1 : start.Month;
        if (step.Unit == Unit.Year) year = (int)(Math.Floor(year / (double)step.Count) * step.Count);
        else month = (month - 1) / step.Count * step.Count + 1;

        for (var guard = 0; guard < 10_000; guard++)
        {
            if (year < 1 || year > 9998) yield break;
            var date = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var ms = (date - Epoch).TotalMilliseconds;
            if (ms > max + 1e-6) yield break;
            if (ms >= min - 1e-6) yield return (ms, date.ToString(format, CultureInfo.InvariantCulture));

            if (step.Unit == Unit.Year) year += step.Count;
            else
            {
                month += step.Count;
                while (month > 12)
                {
                    month -= 12;
                    year++;
                }
            }
        }
    }
}
