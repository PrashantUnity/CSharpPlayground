using PdfEditorApp.Plugins.CSharpEditor.Charting.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>The expand button of a 2D chart opens it over the window (it was wired to nothing, unlike 3D plots and visualizers).</summary>
public class ChartFullScreenTests
{
    [Fact]
    public void TheFullScreenCopy_KeepsTheChosenTypeAndGrid_ButOpensFitted()
    {
        var inline = new ChartViewState { OverrideType = ChartType.Bar, OverrideShowGrid = false, Zoom = 3, PanOffsetX = 40, PanOffsetY = -12 };

        var copy = new ChartViewState(inline);

        Assert.Equal(ChartType.Bar, copy.OverrideType);
        Assert.False(copy.OverrideShowGrid);
        Assert.Equal((1.0, 0.0, 0.0), (copy.Zoom, copy.PanOffsetX, copy.PanOffsetY));
        Assert.Equal(3, inline.Zoom); // the inline chart keeps its own view
    }

    [Fact]
    public void AChartOutsideAWindow_HasNothingToOpenOver()
    {
        Assert.Null(ChartFullScreenOverlay.Open(new InteractiveChartControl()));
        Assert.Null(ChartFullScreenOverlay.Open(new InteractiveChartControl(new ChartOptions { Title = "t" })));
    }

    [Fact]
    public void WhatTheLegendHidesAndTheStackToggle_AreTheViewsOwn_NeverTheCharts()
    {
        var options = new ChartOptions
        {
            Stack = ChartStack.None,
            Series = { new ChartSeries { Points = { new ChartDataPoint(0, 1) } }, new ChartSeries { Points = { new ChartDataPoint(0, 2) } } }
        };
        var view = new ChartViewState();
        view.HiddenSeries.Add(1);
        view.OverrideStack = ChartStack.Stacked;

        var drawn = options.WithView(view.EffectiveType(options), view.EffectiveShowGrid(options), view.HiddenSeries, view.EffectiveStack(options));
        var copy = new ChartViewState(view);

        Assert.Equal(ChartStack.None, options.Stack);   // the chart itself is untouched
        Assert.Null(options.HiddenSeries);
        Assert.Equal(ChartStack.Stacked, drawn.Stack);
        Assert.Equal([1], drawn.HiddenSeries!);
        Assert.Same(options.Series, drawn.Series);       // the series are shared, not copied
        Assert.Equal([1], copy.HiddenSeries);            // a full-screen copy starts with the same choices...
        copy.HiddenSeries.Clear();
        Assert.Equal([1], view.HiddenSeries);            // ...and changes them on its own

        view.Reset();
        Assert.Empty(view.HiddenSeries);
        Assert.Null(view.OverrideStack);
    }
}
