using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Renderers;

public abstract class VisualizerRendererBase : IVisualizerRenderer
{
    private static readonly Dictionary<string, IBrush> BrushCache = new();
    private static readonly Dictionary<string, IPen> PenCache = new();

    protected static readonly Typeface DefaultTypeface = new(FontFamily.Default, FontStyle.Normal, FontWeight.Normal);
    protected static readonly Typeface BoldTypeface = new(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);

    protected static readonly IBrush WhiteBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
    protected static readonly IBrush MutedTextBrush = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255));
    protected static readonly IBrush DarkTextBrush = new SolidColorBrush(Color.FromRgb(24, 24, 27));

    // Lime is unused by every node/cell state colour, so "changed this step" never reads as a state.
    private static readonly IPen ChangedPen = new Pen(new SolidColorBrush(Color.FromRgb(163, 230, 53)), 2.0);

    /// <summary>The previous step's snapshot, when both it and the current step carry a <typeparamref name="T"/>.</summary>
    protected static T? PreviousSnapshot<T>(VisualizerOptions options) where T : class
    {
        var sequence = options.Sequence;
        if (sequence?.CurrentStep?.Snapshot is not T || sequence.CurrentIndex <= 0) return null;
        return sequence.Steps[sequence.CurrentIndex - 1].Snapshot as T;
    }

    protected static void DrawChangedOutline(DrawingContext context, Rect rect, double cornerRadius)
    {
        double inset = Math.Min(2.5, Math.Min(rect.Width, rect.Height) / 4.0);
        context.DrawRectangle(null, ChangedPen, new RoundedRect(rect.Deflate(inset), Math.Max(0, cornerRadius - inset)));
    }

    protected static void DrawChangedRing(DrawingContext context, Point center, double radius)
    {
        double ring = Math.Max(2, radius - 3);
        context.DrawEllipse(null, ChangedPen, center, ring, ring);
    }

    public abstract void Render(DrawingContext context, Rect bounds, VisualizerOptions options);
    public abstract VisualizerHitTestResult? HitTest(Point pointerPosition, Rect bounds, VisualizerOptions options);

    protected static IBrush GetBrush(string hexColor)
    {
        if (BrushCache.TryGetValue(hexColor, out var cached)) return cached;
        if (Color.TryParse(hexColor, out var color))
        {
            var brush = new SolidColorBrush(color);
            BrushCache[hexColor] = brush;
            return brush;
        }
        return WhiteBrush;
    }

    protected static IPen GetPen(string hexColor, double thickness = 1.0)
    {
        string key = $"{hexColor}_{thickness}";
        if (PenCache.TryGetValue(key, out var cached)) return cached;
        if (Color.TryParse(hexColor, out var color))
        {
            var pen = new Pen(new SolidColorBrush(color), thickness);
            PenCache[key] = pen;
            return pen;
        }
        var defPen = new Pen(WhiteBrush, thickness);
        PenCache[key] = defPen;
        return defPen;
    }

    // Shaping text is the slowest part of drawing a big tree, and the same labels come back on every redraw (each step,
    // each hover) and throughout a fit search, so shaped text is reused. Like the brush and pen caches, UI thread only.
    private const int TextCacheLimit = 2048;
    private static readonly Dictionary<(string Text, double Size, IBrush Brush, bool Bold), FormattedText> TextCache = new();

    protected FormattedText CreateFormattedText(string text, double fontSize, IBrush brush, FontWeight? weight = null)
    {
        bool bold = weight == FontWeight.Bold;
        var key = (text, fontSize, brush, bold);
        if (TextCache.TryGetValue(key, out var cached)) return cached;

        if (TextCache.Count >= TextCacheLimit) TextCache.Clear();
        var formatted = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            bold ? BoldTypeface : DefaultTypeface,
            fontSize,
            brush);
        TextCache[key] = formatted;
        return formatted;
    }

    protected void DrawActiveHalo(DrawingContext context, Rect cellRect)
    {
        // Outer soft glow
        var outerRect = cellRect.Inflate(4);
        var glowBrush = new SolidColorBrush(Color.FromArgb(60, 251, 191, 36)); // Amber glow
        context.DrawRectangle(glowBrush, null, new RoundedRect(outerRect, 6));

        // High contrast active border
        var activePen = GetPen("#fbbf24", 2.0);
        context.DrawRectangle(null, activePen, new RoundedRect(cellRect.Inflate(1), 5));
    }

    protected void DrawActiveCircleHalo(DrawingContext context, Point center, double radius)
    {
        var glowBrush = new SolidColorBrush(Color.FromArgb(60, 251, 191, 36));
        context.DrawEllipse(glowBrush, null, center, radius + 5, radius + 5);

        var activePen = GetPen("#fbbf24", 2.2);
        context.DrawEllipse(null, activePen, center, radius + 1, radius + 1);
    }
}
