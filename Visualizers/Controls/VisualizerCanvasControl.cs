using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Renderers;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Controls;

public class VisualizerCanvasControl : Control
{
    public static readonly StyledProperty<VisualizerOptions?> OptionsProperty =
        AvaloniaProperty.Register<VisualizerCanvasControl, VisualizerOptions?>(nameof(Options));

    private static readonly Typeface TooltipTypeface = new(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);
    private static readonly IBrush TooltipTextBrush = new SolidColorBrush(Color.FromArgb(245, 255, 255, 255));
    private static readonly IBrush TooltipMutedBrush = new SolidColorBrush(Color.FromArgb(170, 255, 255, 255));
    private static readonly IBrush TooltipBgBrush = new SolidColorBrush(Color.FromArgb(235, 15, 23, 42)); // Deep slate
    private static readonly IPen TooltipBorderPen = new Pen(new SolidColorBrush(Color.FromArgb(90, 56, 189, 248)), 1);

    private VisualizerHitTestResult? _hoveredHit;
    private bool _isPanning;
    private Point _panStartPoint;
    private double _initialPanX;
    private double _initialPanY;

    public VisualizerOptions? Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    static VisualizerCanvasControl()
    {
        AffectsRender<VisualizerCanvasControl>(OptionsProperty);
        OptionsProperty.Changed.AddClassHandler<VisualizerCanvasControl>((control, e) =>
        {
            control.OnOptionsChanged(e.OldValue as VisualizerOptions, e.NewValue as VisualizerOptions);
        });
    }

    public VisualizerCanvasControl()
    {
        ClipToBounds = true;
    }

    private void OnOptionsChanged(VisualizerOptions? oldOpt, VisualizerOptions? newOpt)
    {
        if (oldOpt?.Sequence != null)
        {
            oldOpt.Sequence.StepChanged -= OnSequenceStepChanged;
        }

        if (newOpt?.Sequence != null)
        {
            newOpt.Sequence.StepChanged += OnSequenceStepChanged;
        }

        InvalidateVisual();
    }

    private void OnSequenceStepChanged(object? sender, int stepIndex)
    {
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isPanning = true;
            _panStartPoint = e.GetPosition(this);
            if (Options != null)
            {
                _initialPanX = Options.PanOffsetX;
                _initialPanY = Options.PanOffsetY;
            }
            e.Pointer.Capture(this);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isPanning)
        {
            _isPanning = false;
            e.Pointer.Capture(null);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (Options == null) return;

        var pos = e.GetPosition(this);

        if (_isPanning)
        {
            double dx = pos.X - _panStartPoint.X;
            double dy = pos.Y - _panStartPoint.Y;
            Options.PanOffsetX = _initialPanX + dx;
            Options.PanOffsetY = _initialPanY + dy;
            InvalidateVisual();
            return;
        }

        var renderer = VisualizerRendererFactory.GetRenderer(Options.Kind);
        var hit = renderer.HitTest(pos, Bounds, Options);

        if (hit?.Details != _hoveredHit?.Details || hit?.Row != _hoveredHit?.Row || hit?.Col != _hoveredHit?.Col)
        {
            _hoveredHit = hit;
            InvalidateVisual();
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (Options == null) return;

        double delta = e.Delta.Y;
        double factor = delta > 0 ? 1.15 : 0.87;
        Options.Zoom = Math.Clamp(Options.Zoom * factor, 0.25, 4.0);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hoveredHit != null)
        {
            _hoveredHit = null;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Options == null)
        {
            DrawPlaceholder(context, "No visualizer data available");
            return;
        }

        var renderer = VisualizerRendererFactory.GetRenderer(Options.Kind);
        renderer.Render(context, Bounds, Options);

        if (_hoveredHit != null)
        {
            RenderTooltip(context, _hoveredHit);
        }
    }

    private void RenderTooltip(DrawingContext context, VisualizerHitTestResult hit)
    {
        var titleFt = new FormattedText(
            hit.Title,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            TooltipTypeface,
            11.5,
            TooltipTextBrush);

        var detailsFt = new FormattedText(
            hit.Details,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default),
            10.0,
            TooltipMutedBrush);

        double width = Math.Max(titleFt.Width, detailsFt.Width) + 18;
        double height = titleFt.Height + detailsFt.Height + 14;

        double tx = hit.CanvasPoint.X + 14;
        double ty = hit.CanvasPoint.Y - height - 8;

        // Keep inside bounds
        if (tx + width > Bounds.Width - 10) tx = hit.CanvasPoint.X - width - 14;
        if (ty < 10) ty = hit.CanvasPoint.Y + 14;

        var tipRect = new Rect(tx, ty, width, height);
        context.DrawRectangle(TooltipBgBrush, TooltipBorderPen, new RoundedRect(tipRect, 6));

        context.DrawText(titleFt, new Point(tx + 9, ty + 6));
        context.DrawText(detailsFt, new Point(tx + 9, ty + 8 + titleFt.Height));
    }

    private void DrawPlaceholder(DrawingContext context, string message)
    {
        var ft = new FormattedText(
            message,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default),
            12,
            TooltipMutedBrush);
        context.DrawText(ft, new Point((Bounds.Width - ft.Width) / 2.0, (Bounds.Height - ft.Height) / 2.0));
    }
}
