using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.ColorMath;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Settings;

/// <summary>
/// Interactive 2D radial color harmony wheel control.
/// Renders an anti-aliased circular spectrum, geometric chord lines, and draggable node handles.
/// </summary>
public class ColorHarmonyWheelControl : Control
{
    public static readonly StyledProperty<float> HueDegreesProperty =
        AvaloniaProperty.Register<ColorHarmonyWheelControl, float>(
            nameof(HueDegrees), 210f, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<ColorHarmonyMode> HarmonyModeProperty =
        AvaloniaProperty.Register<ColorHarmonyWheelControl, ColorHarmonyMode>(
            nameof(HarmonyMode), ColorHarmonyMode.Complementary);

    public static readonly StyledProperty<double> WheelDiameterProperty =
        AvaloniaProperty.Register<ColorHarmonyWheelControl, double>(
            nameof(WheelDiameter), 160.0);

    static ColorHarmonyWheelControl()
    {
        AffectsRender<ColorHarmonyWheelControl>(HueDegreesProperty, HarmonyModeProperty, WheelDiameterProperty);
    }

    public float HueDegrees
    {
        get => GetValue(HueDegreesProperty);
        set => SetValue(HueDegreesProperty, value);
    }

    public ColorHarmonyMode HarmonyMode
    {
        get => GetValue(HarmonyModeProperty);
        set => SetValue(HarmonyModeProperty, value);
    }

    public double WheelDiameter
    {
        get => GetValue(WheelDiameterProperty);
        set => SetValue(WheelDiameterProperty, value);
    }

    private bool _isDragging;

    protected override Size MeasureOverride(Size availableSize)
    {
        double d = WheelDiameter;
        return new Size(d, d);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pt = e.GetCurrentPoint(this);
        if (pt.Properties.IsLeftButtonPressed)
        {
            _isDragging = true;
            e.Pointer.Capture(this);
            UpdateHueFromPoint(pt.Position);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_isDragging)
        {
            var pt = e.GetCurrentPoint(this);
            UpdateHueFromPoint(pt.Position);
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isDragging)
        {
            _isDragging = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    private void UpdateHueFromPoint(Point pos)
    {
        double cx = Bounds.Width / 2.0;
        double cy = Bounds.Height / 2.0;
        double dx = pos.X - cx;
        double dy = pos.Y - cy;

        double rad = Math.Atan2(dy, dx);
        double deg = rad * (180.0 / Math.PI);
        if (deg < 0.0) deg += 360.0;

        HueDegrees = (float)deg;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double width = Bounds.Width;
        double height = Bounds.Height;
        if (width <= 20 || height <= 20) return;

        Point center = new(width / 2.0, height / 2.0);
        double outerRadius = Math.Min(width, height) / 2.0 - 10.0;
        double ringThickness = 14.0;
        double innerRadius = outerRadius - ringThickness;
        if (innerRadius <= 5.0) return;

        double midRadius = (outerRadius + innerRadius) / 2.0;

        // 1. Draw 48 anti-aliased rainbow arc segments
        const int segments = 48;
        const double stepDeg = 360.0 / segments;
        for (int i = 0; i < segments; i++)
        {
            float hue = (float)(i * stepDeg);
            var rgb = new ColorHsl(hue, 0.90f, 0.52f).ToRgb();
            var brush = new SolidColorBrush(Color.FromRgb(rgb.R, rgb.G, rgb.B));
            var pen = new Pen(brush, ringThickness, lineCap: PenLineCap.Round);

            double a1 = (i * stepDeg - 90.0) * (Math.PI / 180.0);
            double a2 = ((i + 1) * stepDeg - 90.0) * (Math.PI / 180.0);

            Point p1 = new(center.X + midRadius * Math.Cos(a1), center.Y + midRadius * Math.Sin(a1));
            Point p2 = new(center.X + midRadius * Math.Cos(a2), center.Y + midRadius * Math.Sin(a2));

            context.DrawLine(pen, p1, p2);
        }

        // 2. Draw central hub surface
        var hubBg = new SolidColorBrush(Color.FromArgb(230, 22, 27, 34));
        var hubBorder = new Pen(new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), 1.0);
        context.DrawEllipse(hubBg, hubBorder, center, innerRadius - 2.0, innerRadius - 2.0);

        // 3. Compute active harmony node angles
        float baseAngle = HueDegrees;
        float[] harmonicOffsets = HarmonyMode switch
        {
            ColorHarmonyMode.Complementary => [180f],
            ColorHarmonyMode.Analogous => [-30f, 30f],
            ColorHarmonyMode.Triadic => [120f, 240f],
            ColorHarmonyMode.Tetradic => [90f, 180f, 270f],
            ColorHarmonyMode.SplitComplementary => [150f, 210f],
            ColorHarmonyMode.Monochromatic => [],
            _ => [180f]
        };

        // 4. Draw geometric chord connector lines
        Point basePt = GetPointOnCircle(center, midRadius, baseAngle);
        var chordPen = new Pen(new SolidColorBrush(Color.FromArgb(160, 56, 189, 248)), 1.5, dashStyle: DashStyle.Dash);

        foreach (float offset in harmonicOffsets)
        {
            Point nodePt = GetPointOnCircle(center, midRadius, baseAngle + offset);
            context.DrawLine(chordPen, basePt, nodePt);
        }

        // Connect adjacent secondary nodes for Triadic / Tetradic chords
        if (harmonicOffsets.Length > 1)
        {
            for (int i = 0; i < harmonicOffsets.Length - 1; i++)
            {
                Point ptA = GetPointOnCircle(center, midRadius, baseAngle + harmonicOffsets[i]);
                Point ptB = GetPointOnCircle(center, midRadius, baseAngle + harmonicOffsets[i + 1]);
                context.DrawLine(chordPen, ptA, ptB);
            }
        }

        // 5. Draw secondary node markers
        foreach (float offset in harmonicOffsets)
        {
            float nodeAngle = (baseAngle + offset % 360f + 360f) % 360f;
            Point nodePt = GetPointOnCircle(center, midRadius, nodeAngle);
            var rgb = new ColorHsl(nodeAngle, 0.90f, 0.55f).ToRgb();
            var nodeFill = new SolidColorBrush(Color.FromRgb(rgb.R, rgb.G, rgb.B));
            var nodeBorder = new Pen(new SolidColorBrush(Colors.White), 1.5);
            context.DrawEllipse(nodeFill, nodeBorder, nodePt, 5.0, 5.0);
        }

        // 6. Draw primary base hue thumb handle (larger, with glowing accent)
        var baseRgb = new ColorHsl((baseAngle % 360f + 360f) % 360f, 0.95f, 0.55f).ToRgb();
        var baseFill = new SolidColorBrush(Color.FromRgb(baseRgb.R, baseRgb.G, baseRgb.B));
        var glowPen = new Pen(new SolidColorBrush(Color.FromArgb(120, baseRgb.R, baseRgb.G, baseRgb.B)), 4.0);
        var baseBorder = new Pen(new SolidColorBrush(Colors.White), 2.0);

        context.DrawEllipse(null, glowPen, basePt, 8.5, 8.5);
        context.DrawEllipse(baseFill, baseBorder, basePt, 7.0, 7.0);
    }

    private static Point GetPointOnCircle(Point center, double radius, float degrees)
    {
        double rad = (degrees - 90.0) * (Math.PI / 180.0);
        return new Point(center.X + radius * Math.Cos(rad), center.Y + radius * Math.Sin(rad));
    }
}
