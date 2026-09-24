using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

public class ArrayItemData
{
    public int Index { get; set; }
    public string DisplayValue { get; set; } = string.Empty;
    public object? RawValue { get; set; }
    public bool IsActive { get; set; }
    public string? Color { get; set; }

    public ArrayItemData() { }

    public ArrayItemData(int index, string displayValue)
    {
        Index = index;
        DisplayValue = displayValue;
    }
}

public class PointerMarkerData
{
    public string Name { get; set; } = string.Empty;
    public int Index { get; set; }
    public string Color { get; set; } = "#38bdf8";

    public PointerMarkerData() { }

    public PointerMarkerData(string name, int index, string color = "#38bdf8")
    {
        Name = name;
        Index = index;
        Color = color;
    }
}

public class ArrayPointerData
{
    public List<ArrayItemData> Items { get; set; } = new();
    public List<PointerMarkerData> Pointers { get; set; } = new();

    /// <summary>Live list this data mirrors; recorder steps re-read it so in-place swaps show up per step.</summary>
    public IList? Source { get; set; }

    public void RefreshFromSource()
    {
        if (Source == null) return;

        for (int i = 0; i < Source.Count; i++)
        {
            var value = Source[i];
            var display = value?.ToString() ?? string.Empty;
            if (i < Items.Count)
            {
                Items[i].RawValue = value;
                Items[i].DisplayValue = display;
            }
            else
            {
                Items.Add(new ArrayItemData(i, display) { RawValue = value });
            }
        }

        if (Items.Count > Source.Count)
        {
            Items.RemoveRange(Source.Count, Items.Count - Source.Count);
        }
    }

    public ArrayPointerData Clone() => new()
    {
        Items = Items.Select(item => new ArrayItemData(item.Index, item.DisplayValue)
        {
            RawValue = item.RawValue,
            IsActive = item.IsActive,
            Color = item.Color
        }).ToList(),
        Pointers = Pointers.Select(p => new PointerMarkerData(p.Name, p.Index, p.Color)).ToList()
    };
}
