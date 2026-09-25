using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Renderers;

public class BarVisualizerRenderer : VisualizerRendererBase
{
    private const double Gap = 4.0;
    private const double MinBarWidth = 1.5;

    private static readonly IBrush ShadeBrush = new SolidColorBrush(Color.FromArgb(70, 56, 189, 248));
    private static readonly IPen ShadeSurfacePen = new Pen(new SolidColorBrush(Color.FromRgb(125, 211, 252)), 1.5);
    private static readonly IPen ShadeFloorPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 125, 211, 252)), 1.2) { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) };
    private static readonly IBrush ShadeLabelBrush = new SolidColorBrush(Color.FromRgb(186, 230, 253));
    private static readonly IBrush ShadeLabelBackground = new SolidColorBrush(Color.FromArgb(230, 12, 30, 48));
    private const double LabelAreaHeight = 36.0;
    private const double TopPadding = 24.0;

    private readonly record struct BarLayout(double BarWidth, double Spacing, double StartX, double BaselineY, double AvailHeight);

    // Bars share the width: a long array gets thinner bars and gaps instead of running off the right edge.
    // Zoom only stretches the heights.
    private static BarLayout ComputeLayout(Rect bounds, VisualizerOptions options, int count)
    {
        double availWidth = Math.Max(50, bounds.Width - 40);
        double availHeight = Math.Max(50, bounds.Height - LabelAreaHeight - TopPadding);

        double slot = availWidth / count;
        double gap = Math.Min(Gap, slot * 0.25);
        double barWidth = Math.Clamp(slot - gap, MinBarWidth, 60.0);
        double totalWidth = count * barWidth + (count - 1) * gap;

        double startX = Math.Max(16, (bounds.Width - totalWidth) / 2.0) + options.PanOffsetX;
        double baselineY = bounds.Height - LabelAreaHeight + options.PanOffsetY;
        return new BarLayout(barWidth, gap, startX, baselineY, availHeight);
    }

    public override void Render(DrawingContext context, Rect bounds, VisualizerOptions options)
    {
        var data = GetEffectiveData(options);
        if (data == null || data.Items.Count == 0) return;

        int count = data.Items.Count;
        var (barWidth, gap, startX, baselineY, availHeight) = ComputeLayout(bounds, options, count);

        double maxVal = data.MaxValue <= 0 ? 1.0 : data.MaxValue;
        var changed = StepChanges.Bars(PreviousSnapshot<BarChartVisualizerData>(options), data);

        // Bars first, then the shade over them (the lines in "container" problems have no width, so the water covers
        // them), then every label on top so nothing is hidden.
        for (int i = 0; i < count; i++)
        {
            var item = data.Items[i];
            var barRect = BarRect(i, item, startX, barWidth, gap, baselineY, availHeight, maxVal, options);

            var (fillBrush, borderPen) = ResolveBarStyling(item);
            context.DrawRectangle(fillBrush, borderPen, new RoundedRect(barRect, 3));

            if (item.IsActive)
            {
                DrawActiveHalo(context, barRect);
            }

            if (changed.Contains(i))
            {
                DrawChangedOutline(context, barRect, 3);
            }
        }

        if (data.Shade is { } shade)
        {
            DrawShade(context, shade, count, startX, barWidth, gap, baselineY, availHeight, maxVal, options);
        }

        for (int i = 0; i < count; i++)
        {
            var item = data.Items[i];
            var barRect = BarRect(i, item, startX, barWidth, gap, baselineY, availHeight, maxVal, options);
            double x = barRect.X;

            // Value label above bar
            if (data.ShowValues && barWidth >= 16)
            {
                double fontSize = Math.Clamp(barWidth * 0.45, 9.0, 13.0);
                var valText = CreateFormattedText(item.DisplayValue, fontSize, WhiteBrush, FontWeight.Bold);
                context.DrawText(valText, new Point(x + (barWidth - valText.Width) / 2.0, barRect.Y - valText.Height - 3));
            }

            // Index below bar
            if (data.ShowIndices && barWidth >= 14)
            {
                double fontSize = Math.Clamp(barWidth * 0.4, 8.0, 11.0);
                var idxText = CreateFormattedText(i.ToString(), fontSize, MutedTextBrush);
                context.DrawText(idxText, new Point(x + (barWidth - idxText.Width) / 2.0, baselineY + 4));
            }

            // Pointer label (e.g. "i", "j", "mid")
            if (!string.IsNullOrEmpty(item.PointerLabel))
            {
                var ptrText = CreateFormattedText(item.PointerLabel, 11.0, GetBrush("#38bdf8"), FontWeight.Bold);
                context.DrawText(ptrText, new Point(x + (barWidth - ptrText.Width) / 2.0, baselineY + 18));
            }
        }

        if (data.Shade is { Label: { Length: > 0 } } labelled)
        {
            DrawShadeLabel(context, labelled, count, startX, barWidth, gap, baselineY, availHeight, maxVal, options);
        }
    }

    private static Rect BarRect(int i, BarVisualizerItem item, double startX, double barWidth, double gap, double baselineY, double availHeight, double maxVal, VisualizerOptions options)
    {
        double ratio = Math.Clamp(item.Value / maxVal, 0.05, 1.0);
        double barHeight = ratio * availHeight * Math.Max(0.2, options.Zoom);
        return new Rect(startX + i * (barWidth + gap), baselineY - barHeight, barWidth, barHeight);
    }

    private static Rect ShadeRect(BarShade shade, int count, double startX, double barWidth, double gap, double baselineY, double availHeight, double maxVal, VisualizerOptions options)
    {
        int from = Math.Clamp(Math.Min(shade.From, shade.To), 0, count - 1);
        int to = Math.Clamp(Math.Max(shade.From, shade.To), 0, count - 1);
        double scale = availHeight * Math.Max(0.2, options.Zoom);
        double top = Math.Clamp(Math.Max(shade.Level, shade.Floor) / maxVal, 0.0, 1.0) * scale;
        double bottom = Math.Clamp(Math.Min(shade.Level, shade.Floor) / maxVal, 0.0, 1.0) * scale;
        return new Rect(startX + from * (barWidth + gap), baselineY - top, (to - from + 1) * (barWidth + gap) - gap, top - bottom);
    }

    private static void DrawShade(DrawingContext context, BarShade shade, int count, double startX, double barWidth, double gap, double baselineY, double availHeight, double maxVal, VisualizerOptions options)
    {
        var rect = ShadeRect(shade, count, startX, barWidth, gap, baselineY, availHeight, maxVal, options);
        context.DrawRectangle(ShadeBrush, null, rect);
        context.DrawLine(ShadeSurfacePen, rect.TopLeft, rect.TopRight);
        if (shade.Floor > 0)
        {
            context.DrawLine(ShadeFloorPen, rect.BottomLeft, rect.BottomRight);
        }
    }

    private void DrawShadeLabel(DrawingContext context, BarShade shade, int count, double startX, double barWidth, double gap, double baselineY, double availHeight, double maxVal, VisualizerOptions options)
    {
        var rect = ShadeRect(shade, count, startX, barWidth, gap, baselineY, availHeight, maxVal, options);
        var text = CreateFormattedText(shade.Label!, 11.0, ShadeLabelBrush, FontWeight.Bold);
        var pill = new Rect(0, 0, text.Width + 12, text.Height + 4);

        // Inside the block when it is tall enough, otherwise just above its surface.
        double y = rect.Height >= pill.Height + 8 ? rect.Top + 4 : rect.Top - pill.Height - 4;
        pill = pill.WithX(rect.Center.X - pill.Width / 2.0).WithY(y);
        context.DrawRectangle(ShadeLabelBackground, ShadeSurfacePen, new RoundedRect(pill, pill.Height / 2.0));
        context.DrawText(text, new Point(pill.X + 6, pill.Y + 2));
    }

    public override VisualizerHitTestResult? HitTest(Point pointerPosition, Rect bounds, VisualizerOptions options)
    {
        var data = GetEffectiveData(options);
        if (data == null || data.Items.Count == 0) return null;

        int count = data.Items.Count;
        var (barWidth, gap, startX, baselineY, availHeight) = ComputeLayout(bounds, options, count);
        double maxVal = data.MaxValue <= 0 ? 1.0 : data.MaxValue;

        for (int i = 0; i < count; i++)
        {
            var item = data.Items[i];
            double x = startX + i * (barWidth + gap);
            double ratio = Math.Clamp(item.Value / maxVal, 0.05, 1.0);
            double barHeight = ratio * availHeight * Math.Max(0.2, options.Zoom);
            double y = baselineY - barHeight;

            var hitRect = new Rect(x - gap / 2.0, y - 10, barWidth + gap, barHeight + LabelAreaHeight + 10);
            if (hitRect.Contains(pointerPosition))
            {
                string status = item.IsSorted ? "Sorted" : (item.IsPivot ? "Pivot" : (item.IsActive ? "Active" : "Regular"));
                return new VisualizerHitTestResult
                {
                    Title = $"Bar [{i}]",
                    Details = $"Value: {item.Value} • Status: {status}{(string.IsNullOrEmpty(item.PointerLabel) ? "" : $" • Pointer: {item.PointerLabel}")}",
                    CanvasPoint = pointerPosition,
                    ColorHex = item.ColorHex
                };
            }
        }

        return null;
    }

    private static BarChartVisualizerData? GetEffectiveData(VisualizerOptions options)
    {
        if (options.Sequence?.CurrentStep?.Snapshot is BarChartVisualizerData snapshot)
        {
            return snapshot;
        }
        return options.BarData;
    }

    private static (IBrush Fill, IPen Border) ResolveBarStyling(BarVisualizerItem item)
    {
        if (!string.IsNullOrEmpty(item.ColorHex))
        {
            return (GetBrush(item.ColorHex), GetPen(item.ColorHex, 1.2));
        }
        if (item.IsPivot)
        {
            return (GetBrush("#ec4899"), GetPen("#f472b6", 1.5));
        }
        if (item.IsActive)
        {
            return (GetBrush("#eab308"), GetPen("#fde047", 1.5));
        }
        if (item.IsSorted)
        {
            return (GetBrush("#22c55e"), GetPen("#4ade80", 1.2));
        }
        return (GetBrush("#3b82f6"), GetPen("#60a5fa", 1.0));
    }
}
