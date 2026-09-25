using System.Collections.Generic;
using System.Linq;

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

    /// <summary>Next points at a node past the drawing limit, so the chain continues off-screen.</summary>
    public bool ContinuesBeyondView { get; set; }

    public LinkedListNodeData() { }

    public LinkedListNodeData(int index, string displayValue, int? nextIndex = null)
    {
        Index = index;
        DisplayValue = displayValue;
        NextIndex = nextIndex;
    }

    public LinkedListNodeData Clone() => new(Index, DisplayValue, NextIndex)
    {
        RawValue = RawValue,
        IsCycleTarget = IsCycleTarget,
        IsActive = IsActive,
        Color = Color,
        ContinuesBeyondView = ContinuesBeyondView
    };
}

public class LinkedListData
{
    public List<LinkedListNodeData> Nodes { get; set; } = new();
    public bool HasCycle { get; set; }
    public int? CycleTargetIndex { get; set; }

    /// <summary>The node whose next pointer closes the cycle (defaults to the last node).</summary>
    public int? CycleSourceIndex { get; set; }

    /// <summary>Each chain start begins a new row instead of continuing the same row after a gap.</summary>
    public bool StackChains { get; set; }

    /// <summary>Named variables pointing into the list (prev, curr, slow…); Index -1 means the variable is null.</summary>
    public List<PointerMarkerData> Pointers { get; set; } = new();

    /// <summary>Node indices that begin a separate chain (a second list, a detached part), drawn after a gap.</summary>
    public List<int> ChainStarts { get; set; } = new();

    public LinkedListData Clone() => new()
    {
        Nodes = Nodes.Select(n => n.Clone()).ToList(),
        HasCycle = HasCycle,
        CycleTargetIndex = CycleTargetIndex,
        CycleSourceIndex = CycleSourceIndex,
        StackChains = StackChains,
        Pointers = Pointers.Select(p => new PointerMarkerData(p.Name, p.Index, p.Color)).ToList(),
        ChainStarts = new List<int>(ChainStarts)
    };
}
