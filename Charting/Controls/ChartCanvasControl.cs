using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Renderers;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Services;
using PdfEditorApp.Plugins.CSharpEditor.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Controls.Common;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Common;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Controls;

public class ChartCanvasControl : Control
{
    public static readonly StyledProperty<ChartOptions?> OptionsProperty =
        AvaloniaProperty.Register<ChartCanvasControl, ChartOptions?>(nameof(Options));

    private static readonly Typeface TooltipTypeface = new(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);
    private static readonly IBrush TooltipTextBrush = new SolidColorBrush(Color.FromArgb(240, 255, 255, 255));
    private static readonly IBrush DarkEmptyTextBrush = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255));
    private static readonly IBrush LightEmptyTextBrush = new SolidColorBrush(Color.FromArgb(140, 100, 116, 139));
    private static IBrush EmptyTextBrush => ThemeService.IsDark ? DarkEmptyTextBrush : LightEmptyTextBrush;

    public ChartViewState ViewState { get; set; } = new();

    private ChartHitTestResult? _hoveredHit;
    private readonly ClickGesture _click = new();
    private bool _isPanning;
    private Point _panStart;
    private double _startPanX, _startPanY;

    /// <summary>A value was clicked (a point, bar or slice).</summary>
    public event EventHandler<ElementClickedEventArgs<ChartHitTestResult>>? ValueClicked;

    public ChartOptions? Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    static ChartCanvasControl()
    {
        AffectsRender<ChartCanvasControl>(OptionsProperty);
    }

    public ChartCanvasControl()
    {
        ClipToBounds = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _click.Pressed(e, this);
        var prop = e.GetCurrentPoint(this).Properties;
        if (prop.IsLeftButtonPressed || prop.IsMiddleButtonPressed || prop.IsRightButtonPressed)
        {
            _isPanning = true;
            _panStart = e.GetPosition(this);
            _startPanX = ViewState.PanOffsetX;
            _startPanY = ViewState.PanOffsetY;
            e.Pointer.Capture(this);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _isPanning = false;
        e.Pointer.Capture(null);
        if (_click.Released(e, this) is not { } at || Options == null || Options.Series.Count == 0) return;
        var hitPos = TransformToModel(at);
        var eff = GetEffectiveOptions();
        if (ChartRendererFactory.GetRenderer(eff.Type).HitTest(hitPos, new Rect(Bounds.Size), eff) is { } hit)
        {
            ValueClicked?.Invoke(this, new ElementClickedEventArgs<ChartHitTestResult>(hit, e.KeyModifiers));
        }
    }

    protected override void OnDoubleTapped(TappedEventArgs e)
    {
        base.OnDoubleTapped(e);
        ViewState.Reset();
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (Options == null || !WheelZoomGate.ShouldZoom(this, e.KeyModifiers)) return;
        double factor = e.Delta.Y > 0 ? 1.15 : 0.85;
        ViewState.Zoom = Math.Clamp(ViewState.Zoom * factor, 0.2, 20.0);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (Options == null || Options.Series.Count == 0) return;

        var pos = e.GetPosition(this);
        if (_isPanning)
        {
            double dx = pos.X - _panStart.X;
            double dy = pos.Y - _panStart.Y;
            if (Math.Abs(dx) > 3 || Math.Abs(dy) > 3)
            {
                ViewState.PanOffsetX = _startPanX + dx;
                ViewState.PanOffsetY = _startPanY + dy;
                InvalidateVisual();
                return;
            }
        }

        var hitPos = TransformToModel(pos);
        var eff = GetEffectiveOptions();
        var renderer = ChartRendererFactory.GetRenderer(eff.Type);
        var hit = renderer.HitTest(hitPos, new Rect(Bounds.Size), eff);

        if (hit?.Point != _hoveredHit?.Point)
        {
            _hoveredHit = hit;
            InvalidateVisual();
        }
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

        if (Options == null || Options.Series.Count == 0)
        {
            var emptyFt = new FormattedText(
                "No chart data available",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default),
                12,
                EmptyTextBrush);
            context.DrawText(emptyFt, new Point((Bounds.Width - emptyFt.Width) / 2, (Bounds.Height - emptyFt.Height) / 2));
            return;
        }

        var eff = GetEffectiveOptions();
        var renderer = ChartRendererFactory.GetRenderer(eff.Type);
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var transform = Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(ViewState.Zoom, ViewState.Zoom) * Matrix.CreateTranslation(center.X + ViewState.PanOffsetX, center.Y + ViewState.PanOffsetY);
        using (context.PushTransform(transform))
        {
            renderer.Render(context, new Rect(Bounds.Size), eff);
            if (_hoveredHit != null)
            {
                RenderHoverTooltip(context, _hoveredHit);
            }
        }
    }

    public ChartOptions GetEffectiveOptions()
    {
        if (Options == null) return new ChartOptions();
        return new ChartOptions
        {
            Title = Options.Title,
            Subtitle = Options.Subtitle,
            Type = ViewState.EffectiveType(Options),
            PrimaryColor = Options.PrimaryColor,
            ShowGrid = ViewState.EffectiveShowGrid(Options),
            ShowPoints = Options.ShowPoints,
            ShowStats = Options.ShowStats,
            ShowLegend = Options.ShowLegend,
            Width = Options.Width,
            Height = Options.Height,
            Series = Options.Series,
            XAxisTitle = Options.XAxisTitle,
            YAxisTitle = Options.YAxisTitle,
            XMin = Options.XMin,
            XMax = Options.XMax,
            YMin = Options.YMin,
            YMax = Options.YMax,
            Notice = Options.Notice
        };
    }

    private Point TransformToModel(Point pos)
    {
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var transform = Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(ViewState.Zoom, ViewState.Zoom) * Matrix.CreateTranslation(center.X + ViewState.PanOffsetX, center.Y + ViewState.PanOffsetY);
        return transform.TryInvert(out var inv) ? inv.Transform(pos) : pos;
    }

    private void RenderHoverTooltip(DrawingContext context, ChartHitTestResult hit)
    {
        var pos = hit.CanvasPosition;
        var color = ChartPaletteService.ParseColor(
            !string.IsNullOrEmpty(hit.Point.CustomColor)
                ? hit.Point.CustomColor
                : (!string.IsNullOrEmpty(hit.Series.Color) ? hit.Series.Color : Options?.PrimaryColor ?? "#4ec9b0"));

        var haloBrush = new SolidColorBrush(Color.FromArgb(50, color.R, color.G, color.B));
        var dotBrush = new SolidColorBrush(color);
        var dotPen = new Pen(new SolidColorBrush(Colors.White), 1.5);

        context.DrawEllipse(haloBrush, null, pos, 8, 8);
        context.DrawEllipse(dotBrush, dotPen, pos, 4, 4);

        var text = hit.DisplayText;
        var ft = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            TooltipTypeface,
            11,
            TooltipTextBrush);

        double tipWidth = ft.Width + 14;
        double tipHeight = ft.Height + 10;
        double tipX = Math.Clamp(pos.X - (tipWidth / 2), 5, Math.Max(5, Bounds.Width - tipWidth - 5));
        double tipY = pos.Y - tipHeight - 10 < 5 ? pos.Y + 12 : pos.Y - tipHeight - 10;

        var tipRect = new Rect(tipX, tipY, tipWidth, tipHeight);
        var tipBg = new SolidColorBrush(Color.FromArgb(235, 18, 24, 34));
        var tipBorderPen = new Pen(new SolidColorBrush(Color.FromArgb(160, color.R, color.G, color.B)), 1);

        context.DrawRectangle(tipBg, tipBorderPen, new RoundedRect(tipRect, 4, 4, 4, 4));
        context.DrawText(ft, new Point(tipX + 7, tipY + 5));
    }
}
