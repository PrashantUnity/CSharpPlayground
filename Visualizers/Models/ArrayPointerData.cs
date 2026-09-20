using System.Collections.Generic;

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
}
