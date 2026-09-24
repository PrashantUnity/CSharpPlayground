using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
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

    // Set by Fit to View; the drawing is refitted as the canvas resizes until the learner zooms or pans.
    private bool _stayFitted;
    private bool _refitQueued;

    /// <summary>Raised whenever zoom or pan changes, whether from the wheel, a drag, the toolbar or a fit.</summary>
    public event EventHandler? ViewportChanged;

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

        // Click the drawing, then step through it with the keyboard.
        Focusable = true;
    }

    public void ZoomBy(double factor)
    {
        if (Options == null) return;
        _stayFitted = false;
        Options.Zoom = VisualizerViewport.ClampZoom(Options.Zoom * factor);
        OnViewportChanged();
    }

    public void ResetView()
    {
        if (Options == null) return;
        _stayFitted = false;
        Options.Zoom = 1.0;
        Options.PanOffsetX = 0;
        Options.PanOffsetY = 0;
        OnViewportChanged();
    }

    /// <summary>Zooms and centres so the whole drawing shows, and keeps it fitted as the canvas resizes.</summary>
    public void FitToView()
    {
        _stayFitted = true;
        ApplyFit();
    }

    private void ApplyFit()
    {
        if (Options == null || VisualizerViewport.ComputeFit(Options, Bounds.Size) is not { } fit) return;
        Options.Zoom = fit.Zoom;
        Options.PanOffsetX = fit.PanX;
        Options.PanOffsetY = fit.PanY;
        OnViewportChanged();
    }

    private void OnViewportChanged()
    {
        InvalidateVisual();
        ViewportChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (!_stayFitted) return;

        // The first real size is fitted before it is ever drawn; later resizes (a grip drag, a window resize) arrive in
        // bursts, so they refit once the burst settles instead of measuring a big tree on every pointer move.
        if (e.PreviousSize.Width <= 0 || e.PreviousSize.Height <= 0)
        {
            ApplyFit();
        }
        else if (!_refitQueued)
        {
            _refitQueued = true;
            Dispatcher.UIThread.Post(() =>
            {
                _refitQueued = false;
                if (_stayFitted) ApplyFit();
            }, DispatcherPriority.Background);
        }
    }

    private void OnOptionsChanged(VisualizerOptions? oldOpt, VisualizerOptions? newOpt)
    {
        _stayFitted = false;
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
            Focus(NavigationMethod.Pointer);
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
            if (dx == 0 && dy == 0) return;
            _stayFitted = false;
            Options.PanOffsetX = _initialPanX + dx;
            Options.PanOffsetY = _initialPanY + dy;
            OnViewportChanged();
            return;
        }

        var renderer = VisualizerRendererFactory.GetRenderer(Options.Kind);
        var hit = renderer.HitTest(pos, new Rect(Bounds.Size), Options);

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

        ZoomBy(e.Delta.Y > 0 ? 1.15 : 0.87);
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

        // Hit testing only sees what is drawn, so without this, dragging from empty space and double-clicks fell through.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));

        if (Options == null)
        {
            DrawPlaceholder(context, "No visualizer data available");
            return;
        }

        // Renderers draw in the canvas's own coordinates, the same space pointer positions and fit measurements use.
        var renderer = VisualizerRendererFactory.GetRenderer(Options.Kind);
        renderer.Render(context, new Rect(Bounds.Size), Options);

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
