using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Renderers;

public class CanvasSceneRenderer : VisualizerRendererBase
{
    public override void Render(DrawingContext context, Rect bounds, VisualizerOptions options)
    {
        var scene = GetEffectiveScene(options);
        if (scene == null) return;

        double zoom = Math.Max(0.2, options.Zoom);
        double sceneW = scene.Width * zoom;
        double sceneH = scene.Height * zoom;

        double ox = Math.Max(12, (bounds.Width - sceneW) / 2.0) + options.PanOffsetX;
        double oy = Math.Max(12, (bounds.Height - sceneH) / 2.0) + options.PanOffsetY;

        // Background
        if (!string.IsNullOrEmpty(scene.BackgroundColor))
        {
            var bgBrush = GetBrush(scene.BackgroundColor);
            context.DrawRectangle(bgBrush, null, new Rect(ox, oy, sceneW, sceneH));
        }

        foreach (var shape in scene.Shapes)
        {
            RenderShape(context, shape, ox, oy, zoom);
        }
    }

    private void RenderShape(DrawingContext context, SceneShape shape, double ox, double oy, double zoom)
    {
        if (shape.Opacity < 1.0)
        {
            using (context.PushOpacity(Math.Max(0.0, shape.Opacity)))
            {
                RenderShapeCore(context, shape, ox, oy, zoom);
            }
            return;
        }

        RenderShapeCore(context, shape, ox, oy, zoom);
    }

    private void RenderShapeCore(DrawingContext context, SceneShape shape, double ox, double oy, double zoom)
    {
        switch (shape)
        {
            case SceneRect rect:
                RenderRect(context, rect, ox, oy, zoom);
                break;
            case SceneCircle circle:
                RenderCircle(context, circle, ox, oy, zoom);
                break;
            case SceneLine line:
                RenderLine(context, line, ox, oy, zoom);
                break;
            case SceneArrow arrow:
                RenderArrow(context, arrow, ox, oy, zoom);
                break;
            case SceneText text:
                RenderText(context, text, ox, oy, zoom);
                break;
        }
    }

    private void RenderRect(DrawingContext context, SceneRect r, double ox, double oy, double zoom)
    {
        double x = ox + r.X * zoom;
        double y = oy + r.Y * zoom;
        double w = r.Width * zoom;
        double h = r.Height * zoom;
        var rectBounds = new Rect(x, y, w, h);

        var fillBrush = !string.IsNullOrEmpty(r.Fill) ? GetBrush(r.Fill) : null;
        var borderPen = !string.IsNullOrEmpty(r.Stroke) ? GetPen(r.Stroke, r.StrokeThickness * zoom) : null;

        context.DrawRectangle(fillBrush, borderPen, new RoundedRect(rectBounds, r.CornerRadius * zoom));

        if (!string.IsNullOrEmpty(r.Label))
        {
            var textBrush = !string.IsNullOrEmpty(r.TextColor) ? GetBrush(r.TextColor) : WhiteBrush;
            var ft = CreateFormattedText(r.Label, Math.Max(8, r.FontSize * zoom), textBrush, FontWeight.Bold);
            context.DrawText(ft, new Point(x + (w - ft.Width) / 2.0, y + (h - ft.Height) / 2.0));
        }
    }

    private void RenderCircle(DrawingContext context, SceneCircle c, double ox, double oy, double zoom)
    {
        var center = new Point(ox + c.CenterX * zoom, oy + c.CenterY * zoom);
        double rad = c.Radius * zoom;

        var fillBrush = !string.IsNullOrEmpty(c.Fill) ? GetBrush(c.Fill) : null;
        var borderPen = !string.IsNullOrEmpty(c.Stroke) ? GetPen(c.Stroke, c.StrokeThickness * zoom) : null;

        context.DrawEllipse(fillBrush, borderPen, center, rad, rad);

        if (!string.IsNullOrEmpty(c.Label))
        {
            var textBrush = !string.IsNullOrEmpty(c.TextColor) ? GetBrush(c.TextColor) : WhiteBrush;
            var ft = CreateFormattedText(c.Label, Math.Max(8, c.FontSize * zoom), textBrush, FontWeight.Bold);
            context.DrawText(ft, new Point(center.X - ft.Width / 2.0, center.Y - ft.Height / 2.0));
        }
    }

    private void RenderLine(DrawingContext context, SceneLine l, double ox, double oy, double zoom)
    {
        var p1 = new Point(ox + l.X1 * zoom, oy + l.Y1 * zoom);
        var p2 = new Point(ox + l.X2 * zoom, oy + l.Y2 * zoom);
        var pen = l.IsDashed
            ? new Pen(GetBrush(l.Stroke ?? "#64748b"), l.StrokeThickness * zoom, DashStyle.Dash)
            : GetPen(l.Stroke ?? "#64748b", l.StrokeThickness * zoom);
        context.DrawLine(pen, p1, p2);
    }

    private void RenderArrow(DrawingContext context, SceneArrow a, double ox, double oy, double zoom)
    {
        var p1 = new Point(ox + a.StartX * zoom, oy + a.StartY * zoom);
        var p2 = new Point(ox + a.EndX * zoom, oy + a.EndY * zoom);
        var pen = GetPen(a.Stroke ?? "#3b82f6", a.StrokeThickness * zoom);

        context.DrawLine(pen, p1, p2);

        // Arrowhead
        double angle = Math.Atan2(p2.Y - p1.Y, p2.X - p1.X);
        double headLen = a.ArrowHeadSize * zoom;
        var pa1 = new Point(p2.X - headLen * Math.Cos(angle - Math.PI / 6), p2.Y - headLen * Math.Sin(angle - Math.PI / 6));
        var pa2 = new Point(p2.X - headLen * Math.Cos(angle + Math.PI / 6), p2.Y - headLen * Math.Sin(angle + Math.PI / 6));
        context.DrawLine(pen, p2, pa1);
        context.DrawLine(pen, p2, pa2);

        if (!string.IsNullOrEmpty(a.Label))
        {
            var ft = CreateFormattedText(a.Label, Math.Max(8, 10 * zoom), GetBrush(a.Stroke ?? "#93c5fd"), FontWeight.Bold);
            double midX = (p1.X + p2.X) / 2.0;
            double midY = (p1.Y + p2.Y) / 2.0 - ft.Height - 2;
            context.DrawText(ft, new Point(midX - ft.Width / 2.0, midY));
        }
    }

    private void RenderText(DrawingContext context, SceneText t, double ox, double oy, double zoom)
    {
        var textBrush = !string.IsNullOrEmpty(t.Fill) ? GetBrush(t.Fill) : WhiteBrush;
        var weight = t.IsBold ? FontWeight.Bold : FontWeight.Normal;
        var ft = CreateFormattedText(t.Text, Math.Max(8, t.FontSize * zoom), textBrush, weight);

        double x = ox + t.X * zoom;
        double y = oy + t.Y * zoom;
        if (t.IsCentered)
        {
            x -= ft.Width / 2.0;
        }

        context.DrawText(ft, new Point(x, y));
    }

    public override VisualizerHitTestResult? HitTest(Point pointerPosition, Rect bounds, VisualizerOptions options)
    {
        var scene = GetEffectiveScene(options);
        if (scene == null) return null;

        double zoom = Math.Max(0.2, options.Zoom);
        double sceneW = scene.Width * zoom;
        double sceneH = scene.Height * zoom;
        double ox = Math.Max(12, (bounds.Width - sceneW) / 2.0) + options.PanOffsetX;
        double oy = Math.Max(12, (bounds.Height - sceneH) / 2.0) + options.PanOffsetY;

        // Iterate in reverse for topmost hit
        for (int i = scene.Shapes.Count - 1; i >= 0; i--)
        {
            var shape = scene.Shapes[i];
            if (shape is SceneRect rect)
            {
                var r = new Rect(ox + rect.X * zoom, oy + rect.Y * zoom, rect.Width * zoom, rect.Height * zoom);
                if (r.Contains(pointerPosition))
                {
                    return new VisualizerHitTestResult
                    {
                        Title = rect.Label ?? "Rectangle",
                        Details = rect.Tooltip ?? $"Position: ({rect.X}, {rect.Y}) • Size: {rect.Width}×{rect.Height}",
                        CanvasPoint = pointerPosition,
                        ColorHex = rect.Fill
                    };
                }
            }
            else if (shape is SceneCircle circle)
            {
                double cx = ox + circle.CenterX * zoom;
                double cy = oy + circle.CenterY * zoom;
                double rad = circle.Radius * zoom;
                double dx = pointerPosition.X - cx;
                double dy = pointerPosition.Y - cy;
                if (dx * dx + dy * dy <= rad * rad)
                {
                    return new VisualizerHitTestResult
                    {
                        Title = circle.Label ?? "Circle",
                        Details = circle.Tooltip ?? $"Center: ({circle.CenterX}, {circle.CenterY}) • Radius: {circle.Radius}",
                        CanvasPoint = pointerPosition,
                        ColorHex = circle.Fill
                    };
                }
            }
        }

        return null;
    }

    private static VisualizerScene? GetEffectiveScene(VisualizerOptions options)
    {
        if (options.Sequence?.CurrentStep?.Snapshot is VisualizerScene snapshot)
        {
            return snapshot;
        }
        return options.SceneData;
    }
}
