using System.Collections;
using System.Linq;
using System.Runtime.CompilerServices;
using PdfEditorApp.Plugins.CSharpEditor.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

/// <summary>How a value reads inside a cell or bar: invariant numbers, lowercase booleans, ∞ for the usual "infinity" sentinels.</summary>
public static class VisualizerValueFormatter
{
    private const int MaxNestedItems = 6;

    public static string Format(object? value) => value switch
    {
        null => "null",
        int.MaxValue or long.MaxValue => "∞",
        int.MinValue or long.MinValue => "-∞",
        _ when ObjectInspectorBuilder.IsScalarType(value.GetType()) => ObjectInspectorBuilder.FormatScalarValue(value),

        // A bucket or pair inside a cell: [1,2] rather than the collection's type name.
        IEnumerable items => FormatItems(items.Cast<object?>()),
        ITuple tuple => FormatItems(Enumerable.Range(0, tuple.Length).Select(i => tuple[i])),
        _ => value.ToString() ?? string.Empty
    };

    private static string FormatItems(System.Collections.Generic.IEnumerable<object?> items)
    {
        var shown = items.Take(MaxNestedItems + 1).Select(Format).ToList();
        return shown.Count > MaxNestedItems
            ? $"[{string.Join(",", shown.Take(MaxNestedItems))},…]"
            : $"[{string.Join(",", shown)}]";
    }
}
