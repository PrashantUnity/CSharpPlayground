using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Kinds;

/// <summary>
/// One kind of visualizer's model, both ways: built from a spec state (and a step's changes, highlights and pointers)
/// for the renderer, and described as a spec state when the C# trackers built the model.
/// </summary>
internal interface IVisualizerKind
{
    /// <summary>The model a state draws (a <see cref="GridMatrixData"/>, a <see cref="TreeNodeData"/>, …), within the limits; null for an empty tree.</summary>
    object? Build(VisualizerState state, VisualizerSpec spec, ICollection<string> notices);

    object? Clone(object? model);

    /// <summary>A step's changes, applied in place to a copy of the previous step's model.</summary>
    void Apply(object? model, VisualizerChangesSpec changes);

    /// <summary>A step's highlights and pointers, where the renderer reads them from the step (its active cells, node ids, pointer list).</summary>
    void DecorateStep(VisualizerStep step, IReadOnlyList<ElementRef> highlight, IReadOnlyList<PointerSpec> pointers);

    /// <summary>
    /// A copy of the model with the highlights and pointers the renderer reads from the model (active flags, pointer tags),
    /// or the model itself when there is nothing to add. <paramref name="onStep"/> says a step's own lists
    /// (<see cref="DecorateStep"/>) already carry what they can. The model passed in may be shared and is never changed.
    /// </summary>
    object? DecorateModel(object? model, IReadOnlyList<ElementRef> highlight, IReadOnlyList<PointerSpec> pointers, bool onStep);

    /// <summary>Makes the model the visualizer's data before any step.</summary>
    void Assign(VisualizerOptions options, object? model);

    /// <summary>The data before any step of a visualizer the C# side built.</summary>
    object? InitialModel(VisualizerOptions options);

    /// <summary>A model as a spec state.</summary>
    VisualizerState ToState(object? model);

    /// <summary>What changed from one state to the next, when changes can say it; null when it takes a whole state.</summary>
    VisualizerChangesSpec? Diff(VisualizerState previous, VisualizerState next);

    /// <summary>
    /// The highlights and pointers of a C# step (or of the data before any step, with no step), in spec form: from the
    /// step's lists and from the model's own flags (an active node, a list's pointers).
    /// </summary>
    void ReadExtras(VisualizerStep? step, object? model, List<ElementRef> highlight, List<PointerSpec> pointers);
}

internal static class VisualizerKinds
{
    public static IVisualizerKind For(VisualizerKind kind) => kind switch
    {
        VisualizerKind.Matrix or VisualizerKind.Islands => GridKind.Instance,
        VisualizerKind.Board => BoardKind.Instance,
        VisualizerKind.Tree => TreeKind.Instance,
        VisualizerKind.Graph => GraphKind.Instance,
        VisualizerKind.LinkedList => LinkedListKind.Instance,
        VisualizerKind.ArrayPointers => ArrayKind.Instance,
        VisualizerKind.Bars => BarsKind.Instance,
        _ => CanvasKind.Instance
    };
}

/// <summary>Values between the spec and the models, read exactly as the models show them.</summary>
internal static class ModelValues
{
    /// <summary>A kind's own model, which only a mix-up would make anything else.</summary>
    public static T As<T>(object? model) where T : class =>
        model as T ?? throw new ArgumentException($"Expected a {typeof(T).Name}, got {model?.GetType().Name ?? "null"}.", nameof(model));

    /// <summary>A model's value as a scalar that reads as the model displays it (its own text when the two differ).</summary>
    public static ScalarValue ToScalar(object? raw, string display)
    {
        var scalar = ScalarValue.From(raw, VisualizerValueFormatter.Format);
        return scalar.ReadsAs(display) ? scalar : ScalarValue.FromText(display);
    }

    /// <summary>The scalar as the .NET value the models and their rules expect (an int where it fits).</summary>
    public static object? ToObject(ScalarValue value) => value.Kind switch
    {
        ScalarKind.Null => null,
        ScalarKind.Boolean => value.Boolean,
        ScalarKind.Integer when value.Integer is >= int.MinValue and <= int.MaxValue => (int)value.Integer,
        ScalarKind.Integer => value.Integer,
        ScalarKind.Real => value.Real,
        _ => value.Text
    };

    public static Dictionary<string, object?> ToMetadata(Dictionary<string, string>? notes) =>
        notes == null ? new() : notes.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);

    public static Dictionary<string, string>? ToNotes(Dictionary<string, object?> metadata) =>
        metadata.Count == 0 ? null : metadata.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? "null");

    public static string? NullIfEmpty(string? text) => string.IsNullOrEmpty(text) ? null : text;

    /// <summary>Pointer names sharing an element, as one tag.</summary>
    public static string? JoinNames(IEnumerable<PointerSpec> pointers)
    {
        var names = string.Join(", ", pointers.Select(p => p.Name));
        return names.Length == 0 ? null : names;
    }

    /// <summary>The colour a pointer gets when its spec doesn't give one: the same for a name every time.</summary>
    public static string PointerColor(PointerSpec pointer) =>
        pointer.Color ?? VisualizerPaletteService.GetPointerColor(pointer.Name);
}
