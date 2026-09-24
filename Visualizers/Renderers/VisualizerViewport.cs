using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Renderers;

/// <summary>The zoom and pan that show a whole visualizer drawing.</summary>
public readonly record struct VisualizerViewFit(double Zoom, double PanX, double PanY);

/// <summary>Zoom limits and "fit to view" for the visualizer canvas.</summary>
public static class VisualizerViewport
{
    public const double MinZoom = 0.25;
    public const double MaxZoom = 4.0;

    /// <summary>Fitting never enlarges past this, so a three-node tree doesn't balloon to fill a full-screen canvas.</summary>
    public const double MaxFitZoom = 2.0;

    /// <summary>Clear space kept between a fitted drawing and the canvas edge.</summary>
    public const double FitMargin = 16.0;

    private const int FitIterations = 8;

    public static double ClampZoom(double zoom) => Math.Clamp(zoom, MinZoom, MaxZoom);

    /// <summary>
    /// The zoom and pan that show the whole drawing, or null when there is nothing to fit. With a playback sequence the
    /// first and last steps count too, so a structure that grows while it plays (a recursion tree) stays in view throughout.
    /// </summary>
    public static VisualizerViewFit? ComputeFit(VisualizerOptions options, Size canvasSize)
    {
        var renderer = VisualizerRendererFactory.GetRenderer(options.Kind);
        return ComputeFit((zoom, step) => MeasureContent(renderer, options, canvasSize, zoom, step), canvasSize, StepsToFit(options.Sequence));
    }

    /// <summary>The fit search over any measurement: <paramref name="measure"/> gives the drawn extent for a zoom and step, with no pan.</summary>
    internal static VisualizerViewFit? ComputeFit(Func<double, int?, Rect?> measure, Size canvasSize, IReadOnlyList<int?> steps)
    {
        var available = new Size(canvasSize.Width - FitMargin * 2, canvasSize.Height - FitMargin * 2);
        if (available.Width <= 0 || available.Height <= 0) return null;

        // The search and the final centring often ask for the same zoom and step again.
        var measured = new Dictionary<(double Zoom, int? Step), Rect?>();
        Rect? Measure(double atZoom, int? atStep)
        {
            if (!measured.TryGetValue((atZoom, atStep), out var cached))
            {
                cached = measure(atZoom, atStep);
                measured[(atZoom, atStep)] = cached;
            }
            return cached;
        }

        double zoom = MaxFitZoom;
        foreach (var step in steps)
        {
            zoom = FitZoom(z => Measure(z, step), available, zoom);
        }

        Rect? drawn = null;
        foreach (var step in steps)
        {
            if (Measure(zoom, step) is { } rect) drawn = drawn?.Union(rect) ?? rect;
        }
        if (drawn is not { } content) return null;

        // Centre what fits; an axis that still overflows at the smallest zoom starts at the margin, where reading begins.
        double panX = content.Width <= available.Width ? canvasSize.Width / 2.0 - content.Center.X : FitMargin - content.Left;
        double panY = content.Height <= available.Height ? canvasSize.Height / 2.0 - content.Center.Y : FitMargin - content.Top;
        return new VisualizerViewFit(zoom, panX, panY);
    }

    /// <summary>The extent of what the renderer draws at <paramref name="zoom"/> with no pan, for one step (default: the current one).</summary>
    public static Rect? MeasureContent(IVisualizerRenderer renderer, VisualizerOptions options, Size canvasSize, double zoom, int? stepIndex = null)
    {
        var probe = options.CloneView();
        probe.Zoom = zoom;
        probe.PanOffsetX = 0;
        probe.PanOffsetY = 0;
        if (stepIndex is int index && options.Sequence is { HasSteps: true } sequence)
        {
            // A private sequence over the same steps, so measuring never moves the real playback.
            probe.Sequence = new VisualizerSequence { Steps = sequence.Steps, CurrentIndex = Math.Clamp(index, 0, sequence.TotalSteps - 1) };
        }

        var drawing = new DrawingGroup();
        using (var context = drawing.Open())
        {
            renderer.Render(context, new Rect(canvasSize), probe);
        }

        var bounds = drawing.GetBounds();
        return bounds.Width > 0 || bounds.Height > 0 ? bounds : null;
    }

    // The step on screen plus the first and last, where structures that grow or shrink while playing are biggest.
    private static IReadOnlyList<int?> StepsToFit(VisualizerSequence? sequence)
    {
        if (sequence is not { HasSteps: true }) return new int?[] { null };
        return new int?[] { sequence.CurrentIndex, 0, sequence.TotalSteps - 1 }.Distinct().ToList();
    }

    // Largest zoom up to `upper` whose drawing fits. An axis the zoom barely changes, like bar widths that always share
    // the canvas, can't be fixed by zooming, so it is left out rather than shrinking everything for nothing.
    private static double FitZoom(Func<double, Rect?> measure, Size available, double upper)
    {
        if (upper <= MinZoom || measure(upper) is not { } atUpper) return upper;
        if (atUpper.Width <= available.Width && atUpper.Height <= available.Height) return upper;
        if (measure(MinZoom) is not { } smallest) return upper;

        bool widthScales = atUpper.Width > smallest.Width * 1.05;
        bool heightScales = atUpper.Height > smallest.Height * 1.05;
        bool Fits(Rect rect) =>
            (!widthScales || rect.Width <= available.Width) &&
            (!heightScales || rect.Height <= available.Height);

        if (Fits(atUpper)) return upper;
        if (!Fits(smallest)) return MinZoom;

        // Drawings grow roughly in proportion to the zoom, so each guess interpolates between the largest zoom known to
        // fit and the smallest known not to. Measuring means drawing everything once, so a few guesses beat bisecting.
        double low = MinZoom, high = upper;
        Rect lowRect = smallest, highRect = atUpper;
        for (int i = 0; i < FitIterations && high / low > 1.01 && Fill(lowRect) < 0.99; i++)
        {
            double guess = Interpolate(low, lowRect, high, highRect);
            if (!(guess > low && guess < high)) guess = Math.Sqrt(low * high);

            var rect = measure(guess);
            if (rect is not { } drawn || Fits(drawn))
            {
                low = guess;
                if (rect is { } fitted) lowRect = fitted;
            }
            else
            {
                high = guess;
                highRect = drawn;
            }
        }
        return low;

        // How much of the tighter, zoomable axis a fitting drawing already fills.
        double Fill(Rect rect) => Math.Max(
            widthScales ? rect.Width / available.Width : 0,
            heightScales ? rect.Height / available.Height : 0);

        // Where each overflowing axis would meet the available space, assuming straight-line growth; a hair under it,
        // so the guess tends to land on the side that fits.
        double Interpolate(double zoomLow, Rect atLow, double zoomHigh, Rect atHigh)
        {
            double guess = zoomHigh;
            if (widthScales && atHigh.Width > available.Width && atHigh.Width > atLow.Width)
            {
                guess = Math.Min(guess, zoomLow + (available.Width - atLow.Width) * (zoomHigh - zoomLow) / (atHigh.Width - atLow.Width));
            }
            if (heightScales && atHigh.Height > available.Height && atHigh.Height > atLow.Height)
            {
                guess = Math.Min(guess, zoomLow + (available.Height - atLow.Height) * (zoomHigh - zoomLow) / (atHigh.Height - atLow.Height));
            }
            return guess * 0.998;
        }
    }
}
