using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Renderers;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Controls;

public class ChartCanvasControl : Control
{
    public static readonly StyledProperty<ChartOptions?> OptionsProperty =
        AvaloniaProperty.Register<ChartCanvasControl, ChartOptions?>(nameof(Options));

    private static readonly Typeface TooltipTypeface = new(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);
    private static readonly IBrush TooltipTextBrush = new SolidColorBrush(Color.FromArgb(240, 255, 255, 255));
    private static readonly IBrush EmptyTextBrush = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255));

    private ChartHitTestResult? _hoveredHit;

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

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (Options == null || Options.Series.Count == 0) return;

        var pos = e.GetPosition(this);
        var renderer = ChartRendererFactory.GetRenderer(Options.Type);
        var hit = renderer.HitTest(pos, Bounds, Options);

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

        var renderer = ChartRendererFactory.GetRenderer(Options.Type);
        renderer.Render(context, Bounds, Options);

        // Render hover tooltip and halo
        if (_hoveredHit != null)
        {
            RenderHoverTooltip(context, _hoveredHit);
        }
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

        // Tooltip text
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

        double tipX = pos.X - (tipWidth / 2);
        double tipY = pos.Y - tipHeight - 10;

        // Clamp inside bounds
        if (tipX < 5) tipX = 5;
        if (tipX + tipWidth > Bounds.Width - 5) tipX = Bounds.Width - tipWidth - 5;
        if (tipY < 5) tipY = pos.Y + 12;

        var tipRect = new Rect(tipX, tipY, tipWidth, tipHeight);
        var tipBg = new SolidColorBrush(Color.FromArgb(235, 18, 24, 34));
        var tipBorderPen = new Pen(new SolidColorBrush(Color.FromArgb(160, color.R, color.G, color.B)), 1);

        context.DrawRectangle(tipBg, tipBorderPen, new RoundedRect(tipRect, 4, 4, 4, 4));
        context.DrawText(ft, new Point(tipX + 7, tipY + 5));
    }
}
