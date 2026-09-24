using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Renderers;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class VisualizerViewportTests
{
    // 632×332 leaves 600×300 inside the fit margin.
    private static readonly Size Canvas = new(600 + 2 * VisualizerViewport.FitMargin, 300 + 2 * VisualizerViewport.FitMargin);
    private static readonly int?[] CurrentStepOnly = { null };

    // Like most renderers: drawn from a fixed corner, growing with the zoom.
    private static Func<double, int?, Rect?> Drawing(double width, double height, double left = 20, double top = 20) =>
        (zoom, _) => new Rect(left, top, width * zoom, height * zoom);

    private static void AssertCentred(VisualizerViewFit fit, Rect drawn)
    {
        Assert.Equal(Canvas.Width / 2, drawn.Center.X + fit.PanX, 3);
        Assert.Equal(Canvas.Height / 2, drawn.Center.Y + fit.PanY, 3);
    }

    [Fact]
    public void ComputeFit_ShrinksAnOverflowingDrawingAndCentresIt()
    {
        var measure = Drawing(1000, 400);

        var fit = VisualizerViewport.ComputeFit(measure, Canvas, CurrentStepOnly)!.Value;

        // Width is the tighter axis: 600 / 1000.
        Assert.InRange(fit.Zoom, 0.59, 0.6);
        AssertCentred(fit, measure(fit.Zoom, null)!.Value);
    }

    [Fact]
    public void ComputeFit_SettlesInAFewMeasurements()
    {
        // Every measurement draws the whole structure once, so a fit must not bisect its way through a dozen of them.
        int calls = 0;
        var drawing = Drawing(1000, 400);

        var fit = VisualizerViewport.ComputeFit((zoom, step) => { calls++; return drawing(zoom, step); }, Canvas, CurrentStepOnly)!.Value;

        Assert.InRange(fit.Zoom, 0.59, 0.6);
        Assert.True(calls <= 4, $"{calls} measurements");
    }

    [Fact]
    public void ComputeFit_EnlargesASmallDrawingOnlyUpToTheCap()
    {
        var fit = VisualizerViewport.ComputeFit(Drawing(60, 40), Canvas, CurrentStepOnly)!.Value;

        Assert.Equal(VisualizerViewport.MaxFitZoom, fit.Zoom);
    }

    [Fact]
    public void ComputeFit_IgnoresAnAxisZoomCannotChange()
    {
        // Bars: the width is shared out across the canvas whatever the zoom; only the heights grow.
        Rect? Bars(double zoom, int? step) => new Rect(16, 20, 900, 200 * zoom);

        var fit = VisualizerViewport.ComputeFit(Bars, Canvas, CurrentStepOnly)!.Value;

        Assert.InRange(fit.Zoom, 1.48, 1.5);

        // Still too wide, so it starts at the margin where reading begins instead of being centred off both edges.
        Assert.Equal(VisualizerViewport.FitMargin, 16 + fit.PanX, 3);
    }

    [Fact]
    public void ComputeFit_UsesTheSmallestZoomAndShowsTheStart_WhenNothingFits()
    {
        var measure = Drawing(10_000, 100);

        var fit = VisualizerViewport.ComputeFit(measure, Canvas, CurrentStepOnly)!.Value;

        Assert.Equal(VisualizerViewport.MinZoom, fit.Zoom);
        Assert.Equal(VisualizerViewport.FitMargin, 20 + fit.PanX, 3);

        // The height does fit, so that axis is still centred.
        Assert.Equal(Canvas.Height / 2, measure(fit.Zoom, null)!.Value.Center.Y + fit.PanY, 3);
    }

    [Fact]
    public void ComputeFit_KeepsTheBiggestStepInView()
    {
        // A recursion tree: small at the step on screen, full size at the last step.
        Rect? Growing(double zoom, int? step) => new Rect(20, 20, 100 * (step + 1 ?? 1) * zoom, 50 * (step + 1 ?? 1) * zoom);

        var fit = VisualizerViewport.ComputeFit(Growing, Canvas, new int?[] { 1, 0, 9 })!.Value;

        // Step 9 is 1000×500 at 100%, so it needs the zoom at or below 0.6.
        Assert.InRange(fit.Zoom, 0.59, 0.6);
        var biggest = Growing(fit.Zoom, 9)!.Value;
        Assert.True(biggest.Width <= 600 && biggest.Height <= 300);
    }

    [Fact]
    public void ComputeFit_ReturnsNull_WhenThereIsNothingToFit()
    {
        Assert.Null(VisualizerViewport.ComputeFit((_, _) => null, Canvas, CurrentStepOnly));
        Assert.Null(VisualizerViewport.ComputeFit(Drawing(100, 100), new Size(20, 400), CurrentStepOnly));
    }

    [Fact]
    public void ClampZoom_KeepsZoomWithinItsLimits()
    {
        Assert.Equal(VisualizerViewport.MinZoom, VisualizerViewport.ClampZoom(0.01));
        Assert.Equal(VisualizerViewport.MaxZoom, VisualizerViewport.ClampZoom(50));
        Assert.Equal(1.3, VisualizerViewport.ClampZoom(1.3));
    }

    [Fact]
    public void CloneView_SharesDataAndPlaybackButNotTheViewport()
    {
        var recorder = VisualizerRecorder.CreateArray(ArrayPointerDataParser.Parse(new[] { 3, 1, 2 }), "Sort");
        recorder.Step("swap 0,1");
        recorder.Step("swap 1,2");
        var inline = recorder.Options;

        var fullScreen = inline.CloneView();
        fullScreen.Zoom = 2.5;
        fullScreen.PanOffsetX = 40;
        fullScreen.Sequence!.SeekStep(2);

        Assert.Equal(1.0, inline.Zoom);
        Assert.Equal(0, inline.PanOffsetX);
        Assert.Same(inline.ArrayData, fullScreen.ArrayData);
        Assert.Same(inline.Sequence, fullScreen.Sequence);
        Assert.Equal(2, inline.Sequence!.CurrentIndex);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(2.0)]
    [InlineData(4.0)]
    public void TreeLayout_KeepsLevelsAndSiblingsApartAtEveryZoom(double zoom)
    {
        var root = TreeDataParser.ParseLeetCodeString("[1,2,3,4,5,6,7]")!;

        var (radius, _, _) = TreeRenderer.Layout(root, zoom);

        var nodes = Flatten(root).ToList();
        foreach (var node in nodes)
        {
            foreach (var child in node.Children)
            {
                // A 20px band under each node stays free for its sub-label.
                Assert.True(child.Y - node.Y >= 2 * radius + 20 - 1e-9, $"zoom {zoom}: level gap {child.Y - node.Y} for radius {radius}");
            }
        }

        foreach (var level in nodes.GroupBy(n => n.Depth))
        {
            var xs = level.Select(n => n.X).OrderBy(x => x).ToList();
            for (int i = 1; i < xs.Count; i++) Assert.True(xs[i] - xs[i - 1] >= 2 * radius, $"zoom {zoom}: siblings overlap");
        }
    }

    [Fact]
    public void TreeLayout_AtOneHundredPercent_IsUnchanged()
    {
        var root = TreeDataParser.ParseLeetCodeString("[1,2,3]")!;

        TreeRenderer.Layout(root, 1.0);

        // The long-standing spacing: 64px between levels, and each in-order slot one 22px node plus a 24px gap wide.
        Assert.Equal(64, root.Children[0].Y - root.Y, 6);
        Assert.Equal(2 * 22 + 24, root.X - root.Children[0].X, 6);
    }

    private static IEnumerable<TreeNodeData> Flatten(TreeNodeData node) =>
        new[] { node }.Concat(node.Children.SelectMany(Flatten));
}
