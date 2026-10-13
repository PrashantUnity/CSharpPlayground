using System.Globalization;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Rendering;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Kinds;

/// <summary>
/// Bar visualizers (sorting and the like): a <see cref="BarsState"/> as a <see cref="BarChartVisualizerData"/>, and back.
/// A bar draws the current, pivot and done (sorted) states.
/// </summary>
internal sealed class BarsKind : IVisualizerKind
{
    public static readonly BarsKind Instance = new();

    public object? Build(VisualizerState state, VisualizerSpec spec, ICollection<string> notices)
    {
        var source = state.Bars!;
        var count = Math.Min(source.Values.Count, VisualLimits.MaxArrayItems);
        var bars = new BarChartVisualizerData
        {
            ShowValues = spec.ShowValues ?? true,
            ShowIndices = source.ShowIndices ?? true,
            Description = source.Description,
            Shade = source.Shade is { } shade ? new BarShade(shade.From, shade.To, shade.Level, shade.Label, shade.Floor) : null
        };

        for (var i = 0; i < count; i++)
        {
            bars.Items.Add(new BarVisualizerItem { Index = i, Value = source.Values[i] ?? 0, DisplayValue = Display(source.Values[i]) });
        }

        // The value axis: as given, or from zero (or the lowest value) to the highest, as the bars have always drawn it.
        var values = bars.Items.Select(b => b.Value).ToList();
        bars.MaxValue = source.Max ?? Math.Max(1.0, values.Count > 0 ? values.Max() : 1.0);
        bars.MinValue = source.Min ?? Math.Min(0.0, values.Count > 0 ? values.Min() : 0.0);

        foreach (var item in source.Items ?? []) ApplyItem(bars, item);
        if (source.Values.Count > count) notices.Add(Notices.ShowingFirst(count, source.Values.Count, "bars"));
        return bars;
    }

    private static string Display(double? value) => value is { } v ? v.ToString("0.##", CultureInfo.InvariantCulture) : string.Empty;

    public object? Clone(object? model) => ModelValues.As<BarChartVisualizerData>(model).Clone();

    public void Apply(object? model, VisualizerChangesSpec changes)
    {
        foreach (var item in changes.Items ?? []) ApplyItem(ModelValues.As<BarChartVisualizerData>(model), item);
    }

    private static void ApplyItem(BarChartVisualizerData bars, ItemSpec spec)
    {
        if (spec.Index < 0 || spec.Index >= bars.Items.Count) return;
        var bar = bars.Items[spec.Index];
        if (spec.Value is { } value)
        {
            // A number is the bar's new height; text only changes what the bar says.
            if (value.AsNumber() is { } number)
            {
                bar.Value = number;
                bar.DisplayValue = Display(number);
            }
            else
            {
                bar.DisplayValue = value.IsNull ? string.Empty : value.ToString();
            }
        }

        if (spec.State is { } state)
        {
            bar.IsActive = state == ElementState.Current;
            bar.IsPivot = state == ElementState.Pivot;
            bar.IsSorted = state == ElementState.Done;
        }

        if (spec.Color != null) bar.ColorHex = spec.Color;
        if (spec.Label != null) bar.Label = spec.Label;
        if (spec.Pointer != null) bar.PointerLabel = spec.Pointer;
    }

    // Bars draw highlights and pointers from the bars only.
    public void DecorateStep(VisualizerStep step, IReadOnlyList<ElementRef> highlight, IReadOnlyList<PointerSpec> pointers) { }

    public object? DecorateModel(object? model, IReadOnlyList<ElementRef> highlight, IReadOnlyList<PointerSpec> pointers, bool onStep)
    {
        if (highlight.Count == 0 && pointers.Count == 0) return model;
        var bars = ModelValues.As<BarChartVisualizerData>(model).Clone();
        foreach (var index in highlight.Where(h => h.Kind == ElementRefKind.Item).Select(h => h.Index))
        {
            if (index >= 0 && index < bars.Items.Count) bars.Items[index].IsActive = true;
        }

        foreach (var group in pointers.Where(p => p.At is { Kind: ElementRefKind.Item }).GroupBy(p => p.At!.Value.Index))
        {
            if (group.Key < 0 || group.Key >= bars.Items.Count) continue;
            var bar = bars.Items[group.Key];
            var names = ModelValues.JoinNames(group);
            bar.PointerLabel = string.IsNullOrEmpty(bar.PointerLabel) ? names : $"{bar.PointerLabel}, {names}";
        }

        return bars;
    }

    public void Assign(VisualizerOptions options, object? model) => options.BarData = ModelValues.As<BarChartVisualizerData>(model);

    public object? InitialModel(VisualizerOptions options) => options.BarData;

    public VisualizerState ToState(object? model)
    {
        var bars = ModelValues.As<BarChartVisualizerData>(model);
        var items = new List<ItemSpec>();
        foreach (var bar in bars.Items)
        {
            var item = new ItemSpec
            {
                Index = bar.Index,

                // A bar's text is its value, unless the C# side wrote something else there.
                Value = bar.DisplayValue == Display(bar.Value) ? null : ScalarValue.FromText(bar.DisplayValue),
                State = bar.IsSorted ? ElementState.Done : bar.IsPivot ? ElementState.Pivot : null,
                Color = ModelValues.NullIfEmpty(bar.ColorHex),
                Label = ModelValues.NullIfEmpty(bar.Label),
                Pointer = ModelValues.NullIfEmpty(bar.PointerLabel)
            };

            if (item.Value != null || item.State != null || item.Color != null || item.Label != null || item.Pointer != null) items.Add(item);
        }

        return new VisualizerState
        {
            Bars = new BarsState
            {
                Values = bars.Items.Select(b => (double?)b.Value).ToList(),
                Items = items.Count > 0 ? items : null,
                Min = bars.MinValue,
                Max = bars.MaxValue,
                ShowIndices = bars.ShowIndices ? null : false,
                Description = bars.Description,
                Shade = bars.Shade is { } shade ? new ShadeSpec { From = shade.From, To = shade.To, Level = shade.Level, Floor = shade.Floor, Label = shade.Label } : null
            }
        };
    }

    public VisualizerChangesSpec? Diff(VisualizerState previous, VisualizerState next)
    {
        if (previous.Bars is not { } a || next.Bars is not { } b) return null;
        if (a.Min != b.Min || a.Max != b.Max || a.ShowIndices != b.ShowIndices || a.Description != b.Description || !SameShade(a.Shade, b.Shade)) return null;

        // A bar's own text (not its value) is only in a whole state.
        var textA = (a.Items ?? []).Where(i => i.Value != null).ToDictionary(i => i.Index, i => i.Value);
        var textB = (b.Items ?? []).Where(i => i.Value != null).ToDictionary(i => i.Index, i => i.Value);
        if (textA.Count != textB.Count || textA.Any(kv => !textB.TryGetValue(kv.Key, out var t) || t != kv.Value)) return null;
        if (textB.Keys.Any(i => i < a.Values.Count && i < b.Values.Count && a.Values[i] != b.Values[i])) return null;

        return StateDiff.Items(Scalars(a.Values), Scalars(b.Values), a.Items, b.Items);
    }

    private static List<ScalarValue> Scalars(List<double?> values) => values.Select(v => v is { } d ? ScalarValue.FromReal(d) : ScalarValue.Null).ToList();

    private static bool SameShade(ShadeSpec? a, ShadeSpec? b) =>
        a == null && b == null || a != null && b != null && a.From == b.From && a.To == b.To && a.Level == b.Level && a.Floor == b.Floor && a.Label == b.Label;

    public void ReadExtras(VisualizerStep? step, object? model, List<ElementRef> highlight, List<PointerSpec> pointers)
    {
        var bars = ModelValues.As<BarChartVisualizerData>(model);
        for (var i = 0; i < bars.Items.Count; i++)
        {
            if (bars.Items[i].IsActive) highlight.Add(ElementRef.Item(i));
        }
    }
}
