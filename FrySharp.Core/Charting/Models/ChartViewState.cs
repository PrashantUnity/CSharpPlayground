using System;
using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Models;

/// <summary>
/// Per-view interaction state for a 2D chart. Decouples inline and fullscreen views so they
/// do not mutate the underlying <see cref="ChartOptions"/> or fight over type/grid/zoom/pan.
/// </summary>
public sealed class ChartViewState
{
    public ChartType? OverrideType { get; set; }
    public bool? OverrideShowGrid { get; set; }

    /// <summary>The viewer's choice to stack or unstack the series (the stack toggle); null keeps what the chart says.</summary>
    public ChartStack? OverrideStack { get; set; }
    public double Zoom { get; set; } = 1.0;
    public double PanOffsetX { get; set; } = 0.0;
    public double PanOffsetY { get; set; } = 0.0;

    /// <summary>Series the viewer hid by clicking them in the legend (by index); the chart's spec is not touched.</summary>
    public HashSet<int> HiddenSeries { get; } = new();

    public ChartViewState() { }

    public ChartViewState(ChartOptions options)
    {
        OverrideType = options.Type;
        OverrideShowGrid = options.ShowGrid;
    }

    /// <summary>A copy for another view of the same chart: the type and grid the user chose, fitted (no zoom or pan).</summary>
    public ChartViewState(ChartViewState other)
    {
        OverrideType = other.OverrideType;
        OverrideShowGrid = other.OverrideShowGrid;
        OverrideStack = other.OverrideStack;
        HiddenSeries = new HashSet<int>(other.HiddenSeries);
    }

    public ChartType EffectiveType(ChartOptions options) => OverrideType ?? options.Type;

    public bool EffectiveShowGrid(ChartOptions options) => OverrideShowGrid ?? options.ShowGrid;

    public ChartStack EffectiveStack(ChartOptions options) => OverrideStack ?? options.Stack;

    public void Reset()
    {
        Zoom = 1.0;
        PanOffsetX = 0.0;
        PanOffsetY = 0.0;
        OverrideType = null;
        OverrideShowGrid = null;
        OverrideStack = null;
        HiddenSeries.Clear();
    }
}
