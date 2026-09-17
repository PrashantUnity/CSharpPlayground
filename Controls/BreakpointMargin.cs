using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls;

public class BreakpointMargin : AbstractMargin, ICustomHitTest
{
    private int _hoveredLine = -1;
    private int _currentPausedLine = -1;
    private readonly HashSet<int> _breakpoints = new();

    public event Action<int>? BreakpointToggled;

    public BreakpointMargin()
    {
        Width = 24;
        MinWidth = 24;
        ClipToBounds = true;
        try
        {
            Cursor = new Cursor(StandardCursorType.Hand);
        }
        catch
        {
            // Defensive fallback when running in headless test runner without ICursorFactory
        }
    }

    public bool HitTest(Point point)
    {
        return new Rect(Bounds.Size).Contains(point);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        try
        {
            Cursor = new Cursor(StandardCursorType.Hand);
        }
        catch
        {
        }
    }

    public int CurrentPausedLine
    {
        get => _currentPausedLine;
        set
        {
            if (_currentPausedLine != value)
            {
                _currentPausedLine = value;
                InvalidateVisual();
            }
        }
    }

    public void SetBreakpoints(IEnumerable<int> lines)
    {
        _breakpoints.Clear();
        foreach (var l in lines)
        {
            _breakpoints.Add(l);
        }
        InvalidateVisual();
    }

    public void AddBreakpoint(int line)
    {
        if (_breakpoints.Add(line))
        {
            InvalidateVisual();
        }
    }

    public void RemoveBreakpoint(int line)
    {
        if (_breakpoints.Remove(line))
        {
            InvalidateVisual();
        }
    }

    public bool HasBreakpoint(int line) => _breakpoints.Contains(line);

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(24, 0);
    }

    public override void Render(DrawingContext drawingContext)
    {
        drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));

        var textView = TextView;
        if (textView == null || !textView.VisualLinesValid) return;

        var bpBrush = new SolidColorBrush(Color.Parse("#EF4444"));
        var bpPen = new Pen(new SolidColorBrush(Color.Parse("#B91C1C")), 1.2);

        var pausedBrush = new SolidColorBrush(Color.Parse("#FBBF24"));
        var pausedPen = new Pen(new SolidColorBrush(Color.Parse("#D97706")), 1.2);

        var hoverBrush = new SolidColorBrush(Color.FromArgb(110, 239, 68, 68));
        var hoverPen = new Pen(new SolidColorBrush(Color.FromArgb(190, 220, 38, 38)), 1.2);
        var highlightRingPen = new Pen(new SolidColorBrush(Color.FromArgb(120, 239, 68, 68)), 1.5);

        var centerX = Bounds.Width > 0 ? Bounds.Width / 2.0 : 12.0;

        foreach (var vl in textView.VisualLines)
        {
            var lineNum = vl.FirstDocumentLine.LineNumber;
            var y = vl.VisualTop - textView.VerticalOffset;
            var h = vl.Height;
            var centerY = y + h / 2.0;

            var isPausedLine = lineNum == _currentPausedLine;
            var hasBp = _breakpoints.Contains(lineNum);
            var isHovered = lineNum == _hoveredLine;

            if (hasBp)
            {
                if (isHovered)
                {
                    drawingContext.DrawEllipse(null, highlightRingPen, new Point(centerX, centerY), 7.5, 7.5);
                }

                drawingContext.DrawEllipse(bpBrush, bpPen, new Point(centerX, centerY), 5.5, 5.5);

                if (isPausedLine)
                {
                    var arrowGeom = new StreamGeometry();
                    using (var ctx = arrowGeom.Open())
                    {
                        ctx.BeginFigure(new Point(centerX - 2.5, centerY - 3.5), true);
                        ctx.LineTo(new Point(centerX - 2.5, centerY + 3.5));
                        ctx.LineTo(new Point(centerX + 3.5, centerY));
                        ctx.EndFigure(true);
                    }
                    drawingContext.DrawGeometry(Brushes.White, null, arrowGeom);
                }
            }
            else if (isPausedLine)
            {
                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    ctx.BeginFigure(new Point(centerX - 4.5, centerY - 5.0), true);
                    ctx.LineTo(new Point(centerX - 4.5, centerY + 5.0));
                    ctx.LineTo(new Point(centerX + 5.0, centerY));
                    ctx.EndFigure(true);
                }

                drawingContext.DrawGeometry(pausedBrush, pausedPen, geometry);
            }
            else if (isHovered)
            {
                drawingContext.DrawEllipse(hoverBrush, hoverPen, new Point(centerX, centerY), 5.5, 5.5);
            }
        }
    }

    private int GetLineFromPointer(PointerEventArgs e)
    {
        var textView = TextView;
        if (textView == null || !textView.VisualLinesValid) return -1;

        var pos = e.GetPosition(this);

        foreach (var vl in textView.VisualLines)
        {
            var top = vl.VisualTop - textView.VerticalOffset;
            var bottom = top + vl.Height;
            if (pos.Y >= top && pos.Y < bottom)
            {
                return vl.FirstDocumentLine.LineNumber;
            }
        }

        if (textView.VisualLines.Count > 0)
        {
            var lastVl = textView.VisualLines[^1];
            var lastBottom = lastVl.VisualTop - textView.VerticalOffset + lastVl.Height;
            if (pos.Y >= lastBottom)
            {
                return -1;
            }
        }

        var visualY = pos.Y + textView.VerticalOffset;
        var fallbackVl = textView.GetVisualLineFromVisualTop(visualY);
        return fallbackVl?.FirstDocumentLine.LineNumber ?? -1;
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        UpdateHoveredLine(e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        UpdateHoveredLine(e);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hoveredLine != -1)
        {
            _hoveredLine = -1;
            InvalidateVisual();
        }
    }

    private void UpdateHoveredLine(PointerEventArgs e)
    {
        var line = GetLineFromPointer(e);
        if (_hoveredLine != line)
        {
            _hoveredLine = line;
            InvalidateVisual();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var line = GetLineFromPointer(e);
        if (line > 0)
        {
            if (_breakpoints.Contains(line))
            {
                _breakpoints.Remove(line);
            }
            else
            {
                _breakpoints.Add(line);
            }
            InvalidateVisual();
            BreakpointToggled?.Invoke(line);
            TextArea?.Focus();
            e.Handled = true;
        }
    }
}
