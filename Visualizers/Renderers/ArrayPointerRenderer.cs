using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Renderers;

public class ArrayPointerRenderer : VisualizerRendererBase
{
    private const double BoxWidth = 44.0;
    private const double BoxHeight = 44.0;
    private const double Gap = 6.0;

    public override void Render(DrawingContext context, Rect bounds, VisualizerOptions options)
    {
        var data = GetEffectiveArray(options);
        if (data == null || data.Items.Count == 0) return;

        double scale = Math.Max(0.2, options.Zoom);
        double w = BoxWidth * scale;
        double h = BoxHeight * scale;
        double gap = Gap * scale;

        double totalArrayWidth = data.Items.Count * (w + gap);
        double startX = Math.Max(20, bounds.Left + (bounds.Width - totalArrayWidth) / 2.0 + options.PanOffsetX);
        double startY = Math.Max(30, bounds.Top + (bounds.Height - h) / 2.0 + options.PanOffsetY);

        var pointers = GetEffectivePointers(options);
        var pointerMap = new Dictionary<int, List<PointerMarkerData>>();
        foreach (var p in pointers)
        {
            if (!pointerMap.TryGetValue(p.Index, out var list))
            {
                list = new List<PointerMarkerData>();
                pointerMap[p.Index] = list;
            }
            list.Add(p);
        }

        var activeCells = GetActiveIndices(options, pointers);
        var changed = StepChanges.ArrayItems(PreviousSnapshot<ArrayPointerData>(options), data);

        for (int i = 0; i < data.Items.Count; i++)
        {
            var item = data.Items[i];
            double x = startX + i * (w + gap);
            double y = startY;
            var cellRect = new Rect(x, y, w, h);

            bool isActive = activeCells.Contains(i) || item.IsActive;

            // 1. Draw index above box
            var ftIdx = CreateFormattedText($"[{i}]", Math.Max(7, 9.5 * scale), MutedTextBrush);
            context.DrawText(ftIdx, new Point(x + (w - ftIdx.Width) / 2.0, y - ftIdx.Height - 4));

            // 2. Draw cell background & halo
            if (isActive)
            {
                DrawActiveHalo(context, cellRect);
            }

            var fillBrush = !string.IsNullOrEmpty(item.Color)
                ? GetBrush(item.Color)
                : (isActive ? GetBrush("#1e3a8a") : GetBrush("#1e293b"));

            var borderPen = isActive ? GetPen("#60a5fa", 1.8) : GetPen("#475569", 1.0);
            context.DrawRectangle(fillBrush, borderPen, new RoundedRect(cellRect, 5));
            if (changed.Contains(i))
            {
                DrawChangedOutline(context, cellRect, 5);
            }

            // 3. Draw value
            if (!string.IsNullOrEmpty(item.DisplayValue))
            {
                var ftVal = CreateFormattedText(item.DisplayValue, Math.Max(8, 13 * scale), WhiteBrush, FontWeight.Bold);
                context.DrawText(ftVal, new Point(x + (w - ftVal.Width) / 2.0, y + (h - ftVal.Height) / 2.0));
            }

            // 4. Draw pointer badges below box
            if (pointerMap.TryGetValue(i, out var pList))
            {
                double pOffsetY = y + h + 6;
                foreach (var ptr in pList)
                {
                    var pBrush = GetBrush(ptr.Color);
                    var ftArrow = CreateFormattedText("▲", Math.Max(7, 9 * scale), pBrush);
                    context.DrawText(ftArrow, new Point(x + (w - ftArrow.Width) / 2.0, pOffsetY));

                    pOffsetY += ftArrow.Height;
                    var ftName = CreateFormattedText(ptr.Name, Math.Max(7, 10 * scale), pBrush, FontWeight.Bold);
                    context.DrawText(ftName, new Point(x + (w - ftName.Width) / 2.0, pOffsetY));

                    pOffsetY += ftName.Height + 2;
                }
            }
        }
    }

    public override VisualizerHitTestResult? HitTest(Point pointerPosition, Rect bounds, VisualizerOptions options)
    {
        var data = GetEffectiveArray(options);
        if (data == null) return null;

        double scale = Math.Max(0.2, options.Zoom);
        double w = BoxWidth * scale;
        double h = BoxHeight * scale;
        double gap = Gap * scale;

        double totalArrayWidth = data.Items.Count * (w + gap);
        double startX = Math.Max(20, bounds.Left + (bounds.Width - totalArrayWidth) / 2.0 + options.PanOffsetX);
        double startY = Math.Max(30, bounds.Top + (bounds.Height - h) / 2.0 + options.PanOffsetY);

        for (int i = 0; i < data.Items.Count; i++)
        {
            double x = startX + i * (w + gap);
            var rect = new Rect(x, startY, w, h);

            if (rect.Contains(pointerPosition))
            {
                var item = data.Items[i];
                return new VisualizerHitTestResult
                {
                    Title = $"Array Item [{i}]",
                    Details = $"Value: '{item.DisplayValue}'",
                    Col = i,
                    CanvasPoint = pointerPosition
                };
            }
        }

        return null;
    }

    private static ArrayPointerData? GetEffectiveArray(VisualizerOptions options) =>
        options.Sequence?.CurrentStep?.Snapshot as ArrayPointerData ?? options.ArrayData;

    private static List<PointerMarkerData> GetEffectivePointers(VisualizerOptions options)
    {
        if (options.Sequence?.CurrentStep?.CustomData is List<PointerMarkerData> stepPointers)
        {
            return stepPointers;
        }
        return GetEffectiveArray(options)?.Pointers ?? new List<PointerMarkerData>();
    }

    private static HashSet<int> GetActiveIndices(VisualizerOptions options, List<PointerMarkerData> pointers)
    {
        var set = new HashSet<int>();
        var currentStep = options.Sequence?.CurrentStep;
        if (currentStep != null)
        {
            foreach (var cell in currentStep.ActiveCells)
            {
                set.Add(cell.Col);
            }
        }
        foreach (var p in pointers)
        {
            set.Add(p.Index);
        }
        return set;
    }
}
