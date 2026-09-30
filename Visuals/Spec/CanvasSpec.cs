using System.Text.Json.Serialization;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Spec;

/// <summary>Free drawing: shapes placed in pixels on a canvas of <see cref="Width"/> × <see cref="Height"/>.</summary>
public sealed class CanvasState
{
    public double? Width { get; set; }
    public double? Height { get; set; }
    public string? Background { get; set; }
    public List<ShapeSpec> Shapes { get; set; } = [];
}

/// <summary>A shape on a canvas. Its <c>type</c> says which: rect, circle, line, arrow or text.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(RectShapeSpec), "rect")]
[JsonDerivedType(typeof(CircleShapeSpec), "circle")]
[JsonDerivedType(typeof(LineShapeSpec), "line")]
[JsonDerivedType(typeof(ArrowShapeSpec), "arrow")]
[JsonDerivedType(typeof(TextShapeSpec), "text")]
public abstract class ShapeSpec
{
    /// <summary>Names the shape in click events.</summary>
    public string? Id { get; set; }

    public string? Fill { get; set; }
    public string? Stroke { get; set; }
    public double? StrokeThickness { get; set; }

    /// <summary>0 (invisible) to 1 (default).</summary>
    public double? Opacity { get; set; }

    /// <summary>Shown when the pointer rests on the shape.</summary>
    public string? Tooltip { get; set; }
}

public sealed class RectShapeSpec : ShapeSpec
{
    [JsonRequired]
    public double X { get; set; }
    [JsonRequired]
    public double Y { get; set; }
    [JsonRequired]
    public double Width { get; set; }
    [JsonRequired]
    public double Height { get; set; }
    public double? CornerRadius { get; set; }
    public string? Label { get; set; }
    public string? TextColor { get; set; }
    public double? FontSize { get; set; }
}

/// <summary>A circle around (<see cref="X"/>, <see cref="Y"/>).</summary>
public sealed class CircleShapeSpec : ShapeSpec
{
    [JsonRequired]
    public double X { get; set; }
    [JsonRequired]
    public double Y { get; set; }
    public double? Radius { get; set; }
    public string? Label { get; set; }
    public string? TextColor { get; set; }
    public double? FontSize { get; set; }
}

public sealed class LineShapeSpec : ShapeSpec
{
    [JsonRequired]
    public double X1 { get; set; }
    [JsonRequired]
    public double Y1 { get; set; }
    [JsonRequired]
    public double X2 { get; set; }
    [JsonRequired]
    public double Y2 { get; set; }
    public bool? Dashed { get; set; }
}

public sealed class ArrowShapeSpec : ShapeSpec
{
    [JsonRequired]
    public double X1 { get; set; }
    [JsonRequired]
    public double Y1 { get; set; }
    [JsonRequired]
    public double X2 { get; set; }
    [JsonRequired]
    public double Y2 { get; set; }
    public double? HeadSize { get; set; }
    public string? Label { get; set; }
}

/// <summary>Text starting at (<see cref="X"/>, <see cref="Y"/>), or centred on it; its colour is <see cref="ShapeSpec.Fill"/>.</summary>
public sealed class TextShapeSpec : ShapeSpec
{
    [JsonRequired]
    public double X { get; set; }
    [JsonRequired]
    public double Y { get; set; }
    [JsonRequired]
    public string Text { get; set; } = string.Empty;
    public double? FontSize { get; set; }
    public bool? Bold { get; set; }
    public bool? Centered { get; set; }
}
