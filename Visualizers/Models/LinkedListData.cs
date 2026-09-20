using System.Collections.Generic;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

public class LinkedListNodeData
{
    public int Index { get; set; }
    public string DisplayValue { get; set; } = string.Empty;
    public object? RawValue { get; set; }
    public int? NextIndex { get; set; }
    public bool IsCycleTarget { get; set; }
    public bool IsActive { get; set; }
    public string? Color { get; set; }

    public LinkedListNodeData() { }

    public LinkedListNodeData(int index, string displayValue, int? nextIndex = null)
    {
        Index = index;
        DisplayValue = displayValue;
        NextIndex = nextIndex;
    }
}

public class LinkedListData
{
    public List<LinkedListNodeData> Nodes { get; set; } = new();
    public bool HasCycle { get; set; }
    public int? CycleTargetIndex { get; set; }
}
