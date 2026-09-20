using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

public static class ArrayPointerDataParser
{
    public static ArrayPointerData Parse(object? array, object? pointers = null)
    {
        var data = new ArrayPointerData();
        if (array == null) return data;

        if (array is ArrayPointerData alreadyData)
        {
            if (pointers != null) ApplyPointers(alreadyData, pointers);
            return alreadyData;
        }

        if (array is IEnumerable enumerable)
        {
            int idx = 0;
            foreach (var item in enumerable)
            {
                data.Items.Add(new ArrayItemData(idx, item?.ToString() ?? string.Empty)
                {
                    RawValue = item
                });
                idx++;
            }
        }

        if (pointers != null)
        {
            ApplyPointers(data, pointers);
        }

        return data;
    }

    public static void ApplyPointers(ArrayPointerData data, object pointers)
    {
        data.Pointers.Clear();

        if (pointers is IDictionary dict)
        {
            foreach (DictionaryEntry entry in dict)
            {
                string name = entry.Key?.ToString() ?? string.Empty;
                if (int.TryParse(entry.Value?.ToString(), out int pIdx))
                {
                    string color = VisualizerPaletteService.GetPointerColor(name);
                    data.Pointers.Add(new PointerMarkerData(name, pIdx, color));
                }
            }
            return;
        }

        var props = pointers.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in props)
        {
            var val = prop.GetValue(pointers);
            if (val is int pIdx)
            {
                string name = prop.Name;
                string color = VisualizerPaletteService.GetPointerColor(name);
                data.Pointers.Add(new PointerMarkerData(name, pIdx, color));
            }
        }
    }
}
