using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Renderers;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting3D.Controls;

public class Plot3DCanvasControl : Control
{
    public static readonly StyledProperty<Plot3DOptions?> OptionsProperty =
        AvaloniaProperty.Register<Plot3DCanvasControl, Plot3DOptions?>(nameof(Options));

    private static readonly Typeface TooltipTypeface = new(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);
    private static readonly IBrush TooltipTextBrush = new SolidColorBrush(Color.FromArgb(240, 255, 255, 255));
    private static readonly IBrush EmptyTextBrush = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255));

    private Point? _lastPointerPos;
    private bool _isOrbiting;
    private bool _isPanning;
    private Plot3DHitTestResult? _hoveredHit;

    public Plot3DOptions? Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    static Plot3DCanvasControl()
    {
        AffectsRender<Plot3DCanvasControl>(OptionsProperty);
    }

    public Plot3DCanvasControl()
    {
        ClipToBounds = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Options == null) return;

        var prop = e.GetCurrentPoint(this).Properties;
        _lastPointerPos = e.GetPosition(this);
        _hoveredHit = null;

        if (prop.IsRightButtonPressed || (prop.IsLeftButtonPressed && (e.KeyModifiers.HasFlag(KeyModifiers.Shift) || e.KeyModifiers.HasFlag(KeyModifiers.Alt))))
        {
            _isPanning = true;
            e.Pointer.Capture(this);
            e.Handled = true;
        }
        else if (prop.IsLeftButtonPressed)
        {
            _isOrbiting = true;
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (Options == null) return;
        if (Bounds.Width < 10 || Bounds.Height < 10) return;

        var curPos = e.GetPosition(this);

        if (_lastPointerPos.HasValue)
        {
            double dx = curPos.X - _lastPointerPos.Value.X;
            double dy = curPos.Y - _lastPointerPos.Value.Y;
            _lastPointerPos = curPos;

            if (_isOrbiting)
            {
                // Dragging right rotates azimuth (Yaw), dragging up rotates elevation (Pitch)
                Options.Camera.Orbit(-dx * 0.45, -dy * 0.45);
                InvalidateVisual();
                e.Handled = true;
                return;
            }
            else if (_isPanning)
            {
                Options.Camera.Pan(dx, dy, Bounds.Width, Bounds.Height);
                InvalidateVisual();
                e.Handled = true;
                return;
            }
        }

        // Only hit-test when not in active drag
        if (!_isOrbiting && !_isPanning)
        {
            var renderer = Plot3DRendererFactory.GetRenderer(Options.Type);
            var hit = renderer.HitTest(curPos, Bounds, Options);

            if (hit?.DisplayText != _hoveredHit?.DisplayText)
            {
                _hoveredHit = hit;
                InvalidateVisual();
            }
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _isOrbiting = false;
        _isPanning = false;
        _lastPointerPos = null;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hoveredHit != null && !_isOrbiting && !_isPanning)
        {
            _hoveredHit = null;
            InvalidateVisual();
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (Options == null) return;
        if (Bounds.Width < 10 || Bounds.Height < 10) return;

        double factor = e.Delta.Y > 0 ? 0.88 : 1.14;
        Options.Camera.Zoom(factor);
        InvalidateVisual();
        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Bounds.Width < 10 || Bounds.Height < 10) return;

        if (Options == null)
        {
            var emptyFt = new FormattedText(
                "No 3D visualization data available",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default),
                12,
                EmptyTextBrush);
            context.DrawText(emptyFt, new Point((Bounds.Width - emptyFt.Width) / 2, (Bounds.Height - emptyFt.Height) / 2));
            return;
        }

        var renderer = Plot3DRendererFactory.GetRenderer(Options.Type);
        renderer.Render(context, Bounds, Options);

        // Render hover tooltip and halo
        if (_hoveredHit != null && !_isOrbiting && !_isPanning)
        {
            RenderHoverTooltip(context, _hoveredHit);
        }
    }

    private void RenderHoverTooltip(DrawingContext context, Plot3DHitTestResult hit)
    {
        var pos = hit.CanvasPosition;
        var color = Color.FromRgb(78, 201, 176);

        var haloBrush = new SolidColorBrush(Color.FromArgb(60, color.R, color.G, color.B));
        var dotBrush = new SolidColorBrush(color);
        var dotPen = new Pen(new SolidColorBrush(Colors.White), 1.5);

        context.DrawEllipse(haloBrush, null, pos, 9, 9);
        context.DrawEllipse(dotBrush, dotPen, pos, 4.5, 4.5);

        // Tooltip text
        var text = hit.DisplayText;
        var ft = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            TooltipTypeface,
            11,
            TooltipTextBrush);

        double tipWidth = ft.Width + 16;
        double tipHeight = ft.Height + 10;

        double tipX = pos.X - (tipWidth / 2);
        double tipY = pos.Y - tipHeight - 12;

        // Clamp inside bounds
        if (tipX < 5) tipX = 5;
        if (tipX + tipWidth > Bounds.Width - 5) tipX = Bounds.Width - tipWidth - 5;
        if (tipY < 5) tipY = pos.Y + 14;

        var tipRect = new Rect(tipX, tipY, tipWidth, tipHeight);
        var tipBg = new SolidColorBrush(Color.FromArgb(238, 16, 22, 30));
        var tipBorderPen = new Pen(new SolidColorBrush(Color.FromArgb(170, color.R, color.G, color.B)), 1);

        context.DrawRectangle(tipBg, tipBorderPen, new RoundedRect(tipRect, 4, 4, 4, 4));
        context.DrawText(ft, new Point(tipX + 8, tipY + 5));
    }
}
