using System;
using System.Collections.Generic;
using System.Linq;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Layout;

/// <summary>
/// Piles series up. Series share a pile when they are on the same value axis, are bars or lines (areas are lines), and
/// name the same pile (a series without a name joins its kind's own). Values go up from the baseline, negative ones down
/// from it; a missing value adds nothing. A percent stack scales every position to 100. Done once when the chart is laid
/// out, never as it is drawn.
/// </summary>
internal static class ChartStacking
{
    public static void Apply(ChartStack mode, IReadOnlyList<SeriesLayout> series)
    {
        if (mode == ChartStack.None) return;

        var piles = series
            .Where(s => s.IsBar || s.IsLineLike)
            .GroupBy(s => (s.Side, Name: s.Series.StackGroup ?? (s.IsBar ? "\u0001bar" : "\u0001line"), IsBar: s.IsBar));

        foreach (var pile in piles)
        {
            var members = pile.ToList();
            var length = members.Max(m => m.Series.Points.Count);
            var positive = new double[length];
            var negative = new double[length];
            var totals = new double[length];
            if (mode == ChartStack.Percent)
            {
                foreach (var member in members)
                {
                    for (var i = 0; i < member.Series.Points.Count; i++)
                    {
                        if (double.IsFinite(member.Series.Points[i].Y)) totals[i] += Math.Abs(member.Series.Points[i].Y);
                    }
                }
            }

            foreach (var member in members)
            {
                var from = new double[length];
                var to = new double[length];
                for (var i = 0; i < length; i++)
                {
                    var value = i < member.Series.Points.Count && double.IsFinite(member.Series.Points[i].Y) ? member.Series.Points[i].Y : 0;
                    var start = value >= 0 ? positive[i] : negative[i];
                    var end = start + value;
                    if (value >= 0) positive[i] = end;
                    else negative[i] = end;

                    if (mode == ChartStack.Percent)
                    {
                        var scale = totals[i] > 0 ? 100 / totals[i] : 0;
                        start *= scale;
                        end *= scale;
                    }

                    from[i] = start;
                    to[i] = end;
                }

                member.Base = from;
                member.Top = to;
            }
        }
    }
}
