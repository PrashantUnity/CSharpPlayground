using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Debugging.Visualizers;

/// <summary>
/// Type visualizer for arrays, dictionaries, sets, and enumerables.
/// Renders friendly count summaries and indexed child elements.
/// </summary>
public sealed class CollectionTypeVisualizer : IDebugTypeVisualizer
{
    private const int MaxInspectItems = 50;

    public int Priority => 50;

    public bool CanVisualize(object? value, Type type) =>
        value is IEnumerable && value is not string;

    public string FormatSummary(object value, Type type)
    {
        if (value is Array arr && arr.Rank > 1)
        {
            var dims = string.Join(", ", Enumerable.Range(0, arr.Rank).Select(d => arr.GetLength(d)));
            var name = type.Name.Split('`')[0];
            return $"{name}[{dims}]";
        }

        if (value is ICollection col)
        {
            return $"Count = {col.Count}";
        }

        return type.Name;
    }

    public IEnumerable<(string Name, object? Value)> GetChildren(object value, Type type)
    {
        if (value is Array multiArr && multiArr.Rank > 1)
        {
            int rows = multiArr.GetLength(0);
            int cols = multiArr.GetLength(1);
            int count = 0;
            for (int r = 0; r < rows && count < MaxInspectItems; r++)
            {
                for (int c = 0; c < cols && count < MaxInspectItems; c++)
                {
                    yield return ($"[{r}, {c}]", multiArr.GetValue(r, c));
                    count++;
                }
            }
            yield break;
        }

        if (value is IDictionary dict)
        {
            int count = 0;
            foreach (DictionaryEntry entry in dict)
            {
                if (count++ >= MaxInspectItems) break;
                yield return ($"[{entry.Key}]", entry.Value);
            }
            yield break;
        }

        if (value is IEnumerable enumerable)
        {
            int idx = 0;
            foreach (var item in enumerable)
            {
                if (idx >= MaxInspectItems) break;
                yield return ($"[{idx}]", item);
                idx++;
            }
        }
    }
}
