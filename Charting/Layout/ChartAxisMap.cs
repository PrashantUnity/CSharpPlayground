using System;
using System.Collections.Generic;
using System.Linq;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Renderers;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Layout;

internal readonly record struct AxisTick(double Value, string Label);

/// <summary>
/// One axis of a cartesian chart: where a value falls along it (0 at its start, 1 at its end) and where its ticks go. The
/// kinds differ only in that mapping; a reversed axis runs the other way.
/// </summary>
internal abstract class AxisMap
{
    protected AxisMap(double min, double max, bool reverse)
    {
        Min = min;
        Max = max;
        Reverse = reverse;
    }

    public double Min { get; }
    public double Max { get; }
    public bool Reverse { get; }

    /// <summary>True when each value has a slot of its own, named by its label (a bar chart's x axis).</summary>
    public virtual bool IsCategory => false;

    /// <summary>The share of the axis a value is along, before the axis is reversed.</summary>
    protected abstract double Along(double value);

    /// <summary>Where a value falls, from 0 (the axis' start, left or bottom) to 1.</summary>
    public double Unit(double value)
    {
        var u = Along(value);
        return Reverse ? 1 - u : u;
    }

    /// <summary>The ticks to label, at most about <paramref name="maxTicks"/>.</summary>
    public abstract IEnumerable<AxisTick> Ticks(int maxTicks);
}

/// <summary>Evenly spaced numbers, with ticks a round step apart.</summary>
internal sealed class LinearAxis(double min, double max, double step, bool reverse) : AxisMap(min, max, reverse)
{
    public double Step { get; } = step;

    protected override double Along(double value) => (value - Min) / (Max - Min);

    public override IEnumerable<AxisTick> Ticks(int maxTicks)
    {
        foreach (var tick in ChartTicks.Between(Min, Max, Step)) yield return new AxisTick(tick, ChartFormat.Tick(tick));
    }
}

/// <summary>A power of ten for each step; <see cref="AxisMap.Min"/> and <see cref="AxisMap.Max"/> are powers of ten.</summary>
internal sealed class LogAxis(double min, double max, bool reverse) : AxisMap(min, max, reverse)
{
    protected override double Along(double value) =>
        value > 0 ? (Math.Log10(value) - Math.Log10(Min)) / (Math.Log10(Max) - Math.Log10(Min)) : double.NaN;

    public override IEnumerable<AxisTick> Ticks(int maxTicks)
    {
        var first = (int)Math.Round(Math.Log10(Min));
        var last = (int)Math.Round(Math.Log10(Max));
        var every = Math.Max(1, (int)Math.Ceiling((last - first + 1) / (double)Math.Max(2, maxTicks)));
        for (var power = first; power <= last; power += every)
        {
            var tick = Math.Pow(10, power);
            yield return new AxisTick(tick, ChartFormat.Tick(tick));
        }
    }

    /// <summary>The powers of ten around a range of positive values.</summary>
    public static (double Min, double Max) Decades(double dataMin, double dataMax)
    {
        if (!(dataMin > 0) || !(dataMax > 0) || dataMin > dataMax) return (1, 10);
        var low = Math.Floor(Math.Log10(dataMin) + 1e-9);
        var high = Math.Ceiling(Math.Log10(dataMax) - 1e-9);
        if (high <= low) high = low + 1;
        return (Math.Pow(10, low), Math.Pow(10, high));
    }
}

/// <summary>Dates and times: a linear axis of Unix milliseconds whose ticks fall on calendar units.</summary>
internal sealed class TimeAxis(double min, double max, bool reverse) : AxisMap(min, max, reverse)
{
    protected override double Along(double value) => (value - Min) / (Max - Min);

    public override IEnumerable<AxisTick> Ticks(int maxTicks) =>
        TimeTicks.Between(Min, Max, maxTicks).Select(t => new AxisTick(t.Value, t.Label));
}

/// <summary>One slot for each value (<c>count</c> of them), named by label; value i sits in the middle of slot i.</summary>
internal sealed class CategoryAxis(int count, IReadOnlyList<string> labels, bool reverse) : AxisMap(-0.5, count - 0.5, reverse)
{
    public int Count { get; } = count;
    public IReadOnlyList<string> Labels { get; } = labels;

    public override bool IsCategory => true;

    protected override double Along(double value) => (value + 0.5) / Count;

    public override IEnumerable<AxisTick> Ticks(int maxTicks)
    {
        var step = Math.Max(1, (int)Math.Ceiling(Count / (double)Math.Max(2, maxTicks)));
        for (var i = 0; i < Count; i += step)
        {
            var label = i < Labels.Count ? Labels[i] : string.Empty;
            if (label.Length > 12) label = label[..10] + "..";
            yield return new AxisTick(i, label);
        }
    }
}
