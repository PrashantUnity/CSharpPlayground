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

        for (int i = 0; i < count; i++)
        {
            var item = data.Items[i];
            double x = startX + i * (barWidth + gap);
            double ratio = Math.Clamp(item.Value / maxVal, 0.05, 1.0);
            double barHeight = ratio * availHeight * Math.Max(0.2, options.Zoom);
            double y = baselineY - barHeight;

            var barRect = new Rect(x, y, barWidth, barHeight);

            // Fill & border
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

            // Value label above bar
            if (data.ShowValues && barWidth >= 16)
            {
                double fontSize = Math.Clamp(barWidth * 0.45, 9.0, 13.0);
                var valText = CreateFormattedText(item.DisplayValue, fontSize, WhiteBrush, FontWeight.Bold);
                context.DrawText(valText, new Point(x + (barWidth - valText.Width) / 2.0, y - valText.Height - 3));
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
