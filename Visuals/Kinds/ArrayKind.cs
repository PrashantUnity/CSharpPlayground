using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Rendering;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Kinds;

/// <summary>Array-and-pointers visualizers: an <see cref="ArrayState"/> as an <see cref="ArrayPointerData"/>, and back.</summary>
internal sealed class ArrayKind : IVisualizerKind
{
    public static readonly ArrayKind Instance = new();

    public object? Build(VisualizerState state, VisualizerSpec spec, ICollection<string> notices)
    {
        var source = state.Array!;
        var array = new ArrayPointerData();
        var count = Math.Min(source.Values.Count, VisualLimits.MaxArrayItems);
        for (var i = 0; i < count; i++)
        {
            array.Items.Add(new ArrayItemData(i, source.Values[i].ToString()) { RawValue = ModelValues.ToObject(source.Values[i]) });
        }

        foreach (var item in source.Items ?? []) ApplyItem(array, item);
        if (source.Values.Count > count) notices.Add(Notices.ShowingFirst(count, source.Values.Count, "items"));
        return array;
    }

    public object? Clone(object? model) => ModelValues.As<ArrayPointerData>(model).Clone();

    public void Apply(object? model, VisualizerChangesSpec changes)
    {
        foreach (var item in changes.Items ?? []) ApplyItem(ModelValues.As<ArrayPointerData>(model), item);
    }

    private static void ApplyItem(ArrayPointerData array, ItemSpec spec)
    {
        if (spec.Index < 0 || spec.Index >= array.Items.Count) return;
        var item = array.Items[spec.Index];
        if (spec.Value is { } value)
        {
            item.DisplayValue = value.ToString();
            item.RawValue = ModelValues.ToObject(value);
        }

        if (spec.State is { } state) item.IsActive = state == ElementState.Current;
        if (spec.Color != null) item.Color = spec.Color;
    }

    public void DecorateStep(VisualizerStep step, IReadOnlyList<ElementRef> highlight, IReadOnlyList<PointerSpec> pointers)
    {
        foreach (var item in highlight.Where(h => h.Kind == ElementRefKind.Item)) step.ActiveCells.Add((0, item.Index));
        if (pointers.Count > 0) step.CustomData = Markers(pointers);
    }

    public object? DecorateModel(object? model, IReadOnlyList<ElementRef> highlight, IReadOnlyList<PointerSpec> pointers, bool onStep)
    {
        if (onStep || highlight.Count == 0 && pointers.Count == 0) return model;
        var array = ModelValues.As<ArrayPointerData>(model).Clone();
        foreach (var index in highlight.Where(h => h.Kind == ElementRefKind.Item).Select(h => h.Index))
        {
            if (index >= 0 && index < array.Items.Count) array.Items[index].IsActive = true;
        }

        array.Pointers = Markers(pointers);
        return array;
    }

    // An array's pointers sit under its items; one past either end stays where the loop left it, a null one nowhere.
    private static List<PointerMarkerData> Markers(IReadOnlyList<PointerSpec> pointers) =>
        pointers.Select(p => new PointerMarkerData(p.Name, p.At is { Kind: ElementRefKind.Item } at ? at.Index : -1, ModelValues.PointerColor(p))).ToList();

    public void Assign(VisualizerOptions options, object? model) => options.ArrayData = ModelValues.As<ArrayPointerData>(model);

    public object? InitialModel(VisualizerOptions options) => options.ArrayData;

    public VisualizerState ToState(object? model)
    {
        var array = ModelValues.As<ArrayPointerData>(model);
        var items = array.Items
            .Where(i => !string.IsNullOrEmpty(i.Color))
            .Select(i => new ItemSpec { Index = i.Index, Color = i.Color })
            .ToList();

        return new VisualizerState
        {
            Array = new ArrayState
            {
                Values = array.Items.Select(i => ModelValues.ToScalar(i.RawValue, i.DisplayValue)).ToList(),
                Items = items.Count > 0 ? items : null
            }
        };
    }

    public VisualizerChangesSpec? Diff(VisualizerState previous, VisualizerState next) =>
        previous.Array is { } a && next.Array is { } b ? StateDiff.Items(a.Values, b.Values, a.Items, b.Items) : null;

    public void ReadExtras(VisualizerStep? step, object? model, List<ElementRef> highlight, List<PointerSpec> pointers)
    {
        var array = ModelValues.As<ArrayPointerData>(model);
        var seen = new HashSet<int>();
        foreach (var (_, col) in step?.ActiveCells ?? [])
        {
            if (col >= 0 && col < array.Items.Count && seen.Add(col)) highlight.Add(ElementRef.Item(col));
        }

        for (var i = 0; i < array.Items.Count; i++)
        {
            if (array.Items[i].IsActive && seen.Add(i)) highlight.Add(ElementRef.Item(i));
        }

        // A step keeps its pointers in its custom data, or else shows the ones its snapshot of the array carries.
        var markers = step?.CustomData as List<PointerMarkerData> ?? array.Pointers;
        foreach (var marker in markers)
        {
            pointers.Add(new PointerSpec { Name = marker.Name, At = ElementRef.Item(Math.Clamp(marker.Index, -1, array.Items.Count)), Color = marker.Color });
        }
    }
}
