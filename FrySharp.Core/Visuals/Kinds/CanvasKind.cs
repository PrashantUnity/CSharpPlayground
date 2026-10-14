using System.Text.Json;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Rendering;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Kinds;

/// <summary>
/// Canvas visualizers: a <see cref="CanvasState"/> as a <see cref="VisualizerScene"/>, and back. A canvas step draws a
/// whole new scene; it has no changes, highlights or pointers.
/// </summary>
internal sealed class CanvasKind : IVisualizerKind
{
    public static readonly CanvasKind Instance = new();

    private const double StrokeThickness = 1.5;
    private const double CornerRadius = 4.0;
    private const double FontSize = 12.0;
    private const double Radius = 20.0;
    private const double HeadSize = 8.0;

    public object? Build(VisualizerState state, VisualizerSpec spec, ICollection<string> notices)
    {
        var source = state.Canvas!;
        var scene = new VisualizerScene(source.Width ?? 600, source.Height ?? 300) { BackgroundColor = source.Background };
        foreach (var shape in source.Shapes.Take(VisualLimits.MaxCanvasShapes)) scene.Shapes.Add(ToModel(shape));
        if (source.Shapes.Count > VisualLimits.MaxCanvasShapes) notices.Add(Notices.ShowingFirst(VisualLimits.MaxCanvasShapes, source.Shapes.Count, "shapes"));
        return scene;
    }

    private static SceneShape ToModel(ShapeSpec spec)
    {
        SceneShape shape = spec switch
        {
            RectShapeSpec r => new SceneRect
            {
                X = r.X, Y = r.Y, Width = r.Width, Height = r.Height, CornerRadius = r.CornerRadius ?? CornerRadius,
                Label = r.Label, TextColor = r.TextColor, FontSize = r.FontSize ?? FontSize
            },
            CircleShapeSpec c => new SceneCircle
            {
                CenterX = c.X, CenterY = c.Y, Radius = c.Radius ?? Radius, Label = c.Label, TextColor = c.TextColor, FontSize = c.FontSize ?? FontSize
            },
            LineShapeSpec l => new SceneLine { X1 = l.X1, Y1 = l.Y1, X2 = l.X2, Y2 = l.Y2, IsDashed = l.Dashed ?? false },
            ArrowShapeSpec a => new SceneArrow { StartX = a.X1, StartY = a.Y1, EndX = a.X2, EndY = a.Y2, ArrowHeadSize = a.HeadSize ?? HeadSize, Label = a.Label },
            TextShapeSpec t => new SceneText { X = t.X, Y = t.Y, Text = t.Text, FontSize = t.FontSize ?? FontSize, IsBold = t.Bold ?? false, IsCentered = t.Centered ?? false },
            _ => throw new ArgumentOutOfRangeException(nameof(spec), spec.GetType().Name, "not a canvas shape")
        };

        shape.Id = spec.Id;
        shape.Fill = spec.Fill;
        shape.Stroke = spec.Stroke;
        shape.StrokeThickness = spec.StrokeThickness ?? StrokeThickness;
        shape.Opacity = spec.Opacity ?? 1.0;
        shape.Tooltip = spec.Tooltip;
        return shape;
    }

    public object? Clone(object? model) => ModelValues.As<VisualizerScene>(model).Clone();

    public void Apply(object? model, VisualizerChangesSpec changes) { }

    public void DecorateStep(VisualizerStep step, IReadOnlyList<ElementRef> highlight, IReadOnlyList<PointerSpec> pointers) { }

    public object? DecorateModel(object? model, IReadOnlyList<ElementRef> highlight, IReadOnlyList<PointerSpec> pointers, bool onStep) => model;

    public void Assign(VisualizerOptions options, object? model) => options.SceneData = ModelValues.As<VisualizerScene>(model);

    public object? InitialModel(VisualizerOptions options) => options.SceneData;

    public VisualizerState ToState(object? model)
    {
        var scene = ModelValues.As<VisualizerScene>(model);
        return new VisualizerState
        {
            Canvas = new CanvasState
            {
                Width = scene.Width,
                Height = scene.Height,
                Background = scene.BackgroundColor,
                Shapes = scene.Shapes.Select(ToSpec).ToList()
            }
        };
    }

    private static ShapeSpec ToSpec(SceneShape shape)
    {
        ShapeSpec spec = shape switch
        {
            SceneRect r => new RectShapeSpec
            {
                X = r.X, Y = r.Y, Width = r.Width, Height = r.Height, CornerRadius = Unless(r.CornerRadius, CornerRadius),
                Label = r.Label, TextColor = r.TextColor, FontSize = Unless(r.FontSize, FontSize)
            },
            SceneCircle c => new CircleShapeSpec
            {
                X = c.CenterX, Y = c.CenterY, Radius = Unless(c.Radius, Radius), Label = c.Label, TextColor = c.TextColor, FontSize = Unless(c.FontSize, FontSize)
            },
            SceneLine l => new LineShapeSpec { X1 = l.X1, Y1 = l.Y1, X2 = l.X2, Y2 = l.Y2, Dashed = l.IsDashed ? true : null },
            SceneArrow a => new ArrowShapeSpec { X1 = a.StartX, Y1 = a.StartY, X2 = a.EndX, Y2 = a.EndY, HeadSize = Unless(a.ArrowHeadSize, HeadSize), Label = a.Label },
            SceneText t => new TextShapeSpec
            {
                X = t.X, Y = t.Y, Text = t.Text, FontSize = Unless(t.FontSize, FontSize), Bold = t.IsBold ? true : null, Centered = t.IsCentered ? true : null
            },
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape.GetType().Name, "not a canvas shape")
        };

        spec.Id = shape.Id;
        spec.Fill = shape.Fill;
        spec.Stroke = shape.Stroke;
        spec.StrokeThickness = Unless(shape.StrokeThickness, StrokeThickness);
        spec.Opacity = Unless(shape.Opacity, 1.0);
        spec.Tooltip = shape.Tooltip;
        return spec;
    }

    private static double? Unless(double value, double @default) => value == @default ? null : value;

    // A scene is replaced whole; the same scene again is no change at all.
    public VisualizerChangesSpec? Diff(VisualizerState previous, VisualizerState next)
    {
        if (previous.Canvas is not { } a || next.Canvas is not { } b) return null;
        var typeInfo = VisualJson.Options.GetTypeInfo(typeof(CanvasState));
        return JsonSerializer.Serialize(a, typeInfo) == JsonSerializer.Serialize(b, typeInfo) ? new VisualizerChangesSpec() : null;
    }

    public void ReadExtras(VisualizerStep? step, object? model, List<ElementRef> highlight, List<PointerSpec> pointers) { }
}
