using System;
using System.Collections.Generic;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

public abstract class SceneShape
{
    public string? Id { get; set; }
    public string? Fill { get; set; }
    public string? Stroke { get; set; }
    public double StrokeThickness { get; set; } = 1.5;
    public double Opacity { get; set; } = 1.0;
    public string? Tooltip { get; set; }
    public abstract SceneShape Clone();
}

public class SceneRect : SceneShape
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double CornerRadius { get; set; } = 4.0;
    public string? Label { get; set; }
    public string? TextColor { get; set; }
    public double FontSize { get; set; } = 12.0;

    public override SceneShape Clone() => new SceneRect
    {
        Id = Id,
        Fill = Fill,
        Stroke = Stroke,
        StrokeThickness = StrokeThickness,
        Opacity = Opacity,
        Tooltip = Tooltip,
        X = X,
        Y = Y,
        Width = Width,
        Height = Height,
        CornerRadius = CornerRadius,
        Label = Label,
        TextColor = TextColor,
        FontSize = FontSize
    };
}

public class SceneCircle : SceneShape
{
    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public double Radius { get; set; } = 20.0;
    public string? Label { get; set; }
    public string? TextColor { get; set; }
    public double FontSize { get; set; } = 12.0;

    public override SceneShape Clone() => new SceneCircle
    {
        Id = Id,
        Fill = Fill,
        Stroke = Stroke,
        StrokeThickness = StrokeThickness,
        Opacity = Opacity,
        Tooltip = Tooltip,
        CenterX = CenterX,
        CenterY = CenterY,
        Radius = Radius,
        Label = Label,
        TextColor = TextColor,
        FontSize = FontSize
    };
}

public class SceneLine : SceneShape
{
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
    public bool IsDashed { get; set; }

    public override SceneShape Clone() => new SceneLine
    {
        Id = Id,
        Fill = Fill,
        Stroke = Stroke,
        StrokeThickness = StrokeThickness,
        Opacity = Opacity,
        Tooltip = Tooltip,
        X1 = X1,
        Y1 = Y1,
        X2 = X2,
        Y2 = Y2,
        IsDashed = IsDashed
    };
}

public class SceneArrow : SceneShape
{
    public double StartX { get; set; }
    public double StartY { get; set; }
    public double EndX { get; set; }
    public double EndY { get; set; }
    public double ArrowHeadSize { get; set; } = 8.0;
    public string? Label { get; set; }

    public override SceneShape Clone() => new SceneArrow
    {
        Id = Id,
        Fill = Fill,
        Stroke = Stroke,
        StrokeThickness = StrokeThickness,
        Opacity = Opacity,
        Tooltip = Tooltip,
        StartX = StartX,
        StartY = StartY,
        EndX = EndX,
        EndY = EndY,
        ArrowHeadSize = ArrowHeadSize,
        Label = Label
    };
}

public class SceneText : SceneShape
{
    public double X { get; set; }
    public double Y { get; set; }
    public string Text { get; set; } = string.Empty;
    public double FontSize { get; set; } = 12.0;
    public bool IsBold { get; set; }
    public bool IsCentered { get; set; }

    public override SceneShape Clone() => new SceneText
    {
        Id = Id,
        Fill = Fill,
        Stroke = Stroke,
        StrokeThickness = StrokeThickness,
        Opacity = Opacity,
        Tooltip = Tooltip,
        X = X,
        Y = Y,
        Text = Text,
        FontSize = FontSize,
        IsBold = IsBold,
        IsCentered = IsCentered
    };
}

public class VisualizerScene
{
    public double Width { get; set; } = 600.0;
    public double Height { get; set; } = 300.0;
    public string? BackgroundColor { get; set; }
    public List<SceneShape> Shapes { get; set; } = new();

    public VisualizerScene() { }

    public VisualizerScene(double width, double height)
    {
        Width = Math.Max(50, width);
        Height = Math.Max(50, height);
    }

    public SceneRect AddRect(double x, double y, double w, double h, string? label = null, string? fill = "#1e293b", string? stroke = "#475569", double cornerRadius = 4.0)
    {
        var shape = new SceneRect
        {
            X = x,
            Y = y,
            Width = w,
            Height = h,
            Label = label,
            Fill = fill,
            Stroke = stroke,
            CornerRadius = cornerRadius
        };
        Shapes.Add(shape);
        return shape;
    }

    public SceneCircle AddCircle(double cx, double cy, double radius = 20.0, string? label = null, string? fill = "#1e293b", string? stroke = "#475569")
    {
        var shape = new SceneCircle
        {
            CenterX = cx,
            CenterY = cy,
            Radius = radius,
            Label = label,
            Fill = fill,
            Stroke = stroke
        };
        Shapes.Add(shape);
        return shape;
    }

    public SceneLine AddLine(double x1, double y1, double x2, double y2, string? stroke = "#64748b", double thickness = 1.5, bool isDashed = false)
    {
        var shape = new SceneLine
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Stroke = stroke,
            StrokeThickness = thickness,
            IsDashed = isDashed
        };
        Shapes.Add(shape);
        return shape;
    }

    public SceneArrow AddArrow(double x1, double y1, double x2, double y2, string? label = null, string? stroke = "#3b82f6", double headSize = 8.0)
    {
        var shape = new SceneArrow
        {
            StartX = x1,
            StartY = y1,
            EndX = x2,
            EndY = y2,
            Label = label,
            Stroke = stroke,
            ArrowHeadSize = headSize
        };
        Shapes.Add(shape);
        return shape;
    }

    public SceneText AddText(double x, double y, string text, double fontSize = 12.0, string? color = "#e2e8f0", bool isBold = false, bool isCentered = false)
    {
        var shape = new SceneText
        {
            X = x,
            Y = y,
            Text = text,
            FontSize = fontSize,
            Fill = color,
            IsBold = isBold,
            IsCentered = isCentered
        };
        Shapes.Add(shape);
        return shape;
    }

    public VisualizerScene Clone()
    {
        var clone = new VisualizerScene(Width, Height)
        {
            BackgroundColor = BackgroundColor
        };
        foreach (var s in Shapes)
        {
            clone.Shapes.Add(s.Clone());
        }
        return clone;
    }
}
