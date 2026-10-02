using System;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Rendering;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls;

public class DebugLineRenderer : IBackgroundRenderer
{
    public static readonly Color PausedLineColor = Color.FromRgb(234, 179, 8);
    public static readonly Color VisualizerStepColor = Color.FromRgb(56, 189, 248);

    private readonly IBrush _fillBrush;
    private readonly IPen _borderPen;

    public DebugLineRenderer() : this(PausedLineColor)
    {
    }

    public DebugLineRenderer(Color accent)
    {
        _fillBrush = new SolidColorBrush(Color.FromArgb(45, accent.R, accent.G, accent.B));
        _borderPen = new Pen(new SolidColorBrush(Color.FromArgb(140, accent.R, accent.G, accent.B)), 1.0);
    }

    public KnownLayer Layer => KnownLayer.Background;

    public int HighlightedLine { get; set; } = -1;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (HighlightedLine <= 0 || !textView.VisualLinesValid) return;

        var vl = textView.GetVisualLine(HighlightedLine);
        if (vl == null) return;

        var y = vl.VisualTop - textView.VerticalOffset;
        var h = vl.Height;
        var w = Math.Max(textView.Bounds.Width, 2000);

        drawingContext.DrawRectangle(_fillBrush, _borderPen, new Rect(0, y, w, h));
    }
}
