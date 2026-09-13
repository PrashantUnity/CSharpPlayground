using System;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Rendering;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls;

public class DebugLineRenderer : IBackgroundRenderer
{
    private int _currentPausedLine = -1;

    public KnownLayer Layer => KnownLayer.Background;

    public int CurrentPausedLine
    {
        get => _currentPausedLine;
        set => _currentPausedLine = value;
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_currentPausedLine <= 0 || !textView.VisualLinesValid) return;

        var vl = textView.GetVisualLine(_currentPausedLine);
        if (vl == null) return;

        var y = vl.VisualTop - textView.VerticalOffset;
        var h = vl.Height;
        var w = Math.Max(textView.Bounds.Width, 2000);

        var rect = new Rect(0, y, w, h);
        var fillBrush = new SolidColorBrush(Color.FromArgb(45, 234, 179, 8)); // Translucent amber gold
        var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(140, 234, 179, 8)), 1.0);

        drawingContext.DrawRectangle(fillBrush, borderPen, rect);
    }
}
