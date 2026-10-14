using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Rendering;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Kinds;

/// <summary>
/// Linked-list visualizers: a <see cref="LinkedListState"/> as a <see cref="LinkedListData"/>, and back. Nodes are drawn
/// in the order they are listed (so a reversal animates in place) and the studio finds the cycle from the next pointers.
/// </summary>
internal sealed class LinkedListKind : IVisualizerKind
{
    public static readonly LinkedListKind Instance = new();

    public object? Build(VisualizerState state, VisualizerSpec spec, ICollection<string> notices)
    {
        var source = state.LinkedList!;
        var nodes = source.Nodes.Take(VisualLimits.MaxListNodes).ToList();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < nodes.Count; i++) index.TryAdd(nodes[i].Id, i);

        var list = new LinkedListData { StackChains = source.StackChains ?? false };
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            var next = node.Next != null && index.TryGetValue(node.Next, out var n) ? n : (int?)null;
            list.Nodes.Add(new LinkedListNodeData(i, node.Value.ToString(), next)
            {
                Id = node.Id,
                RawValue = ModelValues.ToObject(node.Value),
                Color = node.Color ?? StateColor(node.State),
                IsActive = node.State == ElementState.Current,
                IsCycleTarget = node.State == ElementState.Cycle,

                // A next beyond the nodes drawn continues off the drawing.
                ContinuesBeyondView = node.Truncated == true || node.Next != null && next == null
            });
        }

        foreach (var chain in source.Chains ?? [])
        {
            if (index.TryGetValue(chain, out var start) && start > 0) list.ChainStarts.Add(start);
        }

        var head = source.Head != null && index.TryGetValue(source.Head, out var h) ? h : 0;
        MarkCycle(list, head, markTarget: source.MarkCycle ?? true);
        if (source.Nodes.Count > VisualLimits.MaxListNodes) notices.Add(Notices.ShowingFirst(VisualLimits.MaxListNodes, source.Nodes.Count, "nodes"));
        return list;
    }

    // Done and visited nodes take the colour the list tracker gives finished nodes.
    private static string? StateColor(ElementState? state) =>
        state is ElementState.Done or ElementState.Visited ? LinkedListTracker.DoneColor : null;

    // Follows next from the head: the first node reached twice closes a cycle (and is marked, unless the spec says not to).
    private static void MarkCycle(LinkedListData list, int head, bool markTarget)
    {
        if (list.Nodes.Count == 0) return;
        var seen = new HashSet<int>();
        int? previous = null;
        int? current = head;
        while (current is { } i && i < list.Nodes.Count)
        {
            if (!seen.Add(i))
            {
                list.HasCycle = true;
                list.CycleTargetIndex = i;
                list.CycleSourceIndex = previous;
                if (markTarget) list.Nodes[i].IsCycleTarget = true;
                return;
            }

            previous = i;
            current = list.Nodes[i].NextIndex;
        }
    }

    public object? Clone(object? model) => ModelValues.As<LinkedListData>(model).Clone();

    public void Apply(object? model, VisualizerChangesSpec changes)
    {
        var list = ModelValues.As<LinkedListData>(model);
        foreach (var change in changes.Nodes ?? [])
        {
            if (!TryIndex(change.Id, list, out var i)) continue;
            var node = list.Nodes[i];
            if (change.Value is { } value)
            {
                node.DisplayValue = value.ToString();
                node.RawValue = ModelValues.ToObject(value);
            }

            if (change.State is { } state)
            {
                node.IsActive = state == ElementState.Current;
                node.IsCycleTarget = state == ElementState.Cycle;
                if (change.Color == null) node.Color = StateColor(state);
            }

            if (change.Color != null) node.Color = change.Color;
        }
    }

    // The step's node list names nodes by position, which only the model knows: a list is decorated on the model alone.
    public void DecorateStep(VisualizerStep step, IReadOnlyList<ElementRef> highlight, IReadOnlyList<PointerSpec> pointers) { }

    public object? DecorateModel(object? model, IReadOnlyList<ElementRef> highlight, IReadOnlyList<PointerSpec> pointers, bool onStep)
    {
        if (highlight.Count == 0 && pointers.Count == 0) return model;
        var list = ModelValues.As<LinkedListData>(model).Clone();
        foreach (var id in highlight.Where(h => h.Kind == ElementRefKind.Node).Select(h => h.Id!))
        {
            if (TryIndex(id, list, out var i)) list.Nodes[i].IsActive = true;
        }

        // A list keeps its pointers in the drawing; index -1 is a variable that is null.
        list.Pointers = pointers.Select(p => new PointerMarkerData(p.Name, p.At is { Kind: ElementRefKind.Node } at && TryIndex(at.Id!, list, out var i) ? i : -1, ModelValues.PointerColor(p))).ToList();
        return list;
    }

    // A node by its spec id; a list the C# side built has none, and is named "n0", "n1"… as it is listed.
    private static bool TryIndex(string id, LinkedListData list, out int index)
    {
        index = list.Nodes.FindIndex(n => SpecId(n) == id);
        return index >= 0;
    }

    private static string SpecId(LinkedListNodeData node) => node.Id ?? $"n{node.Index}";

    public void Assign(VisualizerOptions options, object? model) => options.LinkedListData = ModelValues.As<LinkedListData>(model);

    public object? InitialModel(VisualizerOptions options) => options.LinkedListData;

    public VisualizerState ToState(object? model)
    {
        var list = ModelValues.As<LinkedListData>(model);
        return new VisualizerState
        {
            LinkedList = new LinkedListState
            {
                Nodes = list.Nodes.Select(n => new ListNodeSpec
                {
                    Id = SpecId(n),
                    Value = ModelValues.ToScalar(n.RawValue, n.DisplayValue),
                    Next = n.NextIndex is { } next && next >= 0 && next < list.Nodes.Count ? SpecId(list.Nodes[next]) : null,
                    Color = ModelValues.NullIfEmpty(n.Color),
                    State = n.IsCycleTarget ? ElementState.Cycle : null,
                    Truncated = n.ContinuesBeyondView ? true : null
                }).ToList(),

                // The C# side decided which node is marked; the marks are written out node by node.
                MarkCycle = false,
                Chains = list.ChainStarts.Count > 0 ? list.ChainStarts.Where(i => i >= 0 && i < list.Nodes.Count).Select(i => SpecId(list.Nodes[i])).ToList() : null,
                StackChains = list.StackChains ? true : null
            }
        };
    }

    public VisualizerChangesSpec? Diff(VisualizerState previous, VisualizerState next)
    {
        if (previous.LinkedList is not { } a || next.LinkedList is not { } b) return null;
        if (a.Nodes.Count != b.Nodes.Count || a.Head != b.Head || a.StackChains != b.StackChains || a.MarkCycle != b.MarkCycle || !StateDiff.SameList(a.Chains, b.Chains)) return null;

        var expressible = true;
        var changes = new List<NodeChangeSpec>();
        for (var i = 0; i < b.Nodes.Count; i++)
        {
            var (x, y) = (a.Nodes[i], b.Nodes[i]);
            if (x.Id != y.Id || x.Next != y.Next || x.Truncated != y.Truncated) return null;
            if (StateDiff.Node(y.Id, x.Value, y.Value, x.State, y.State, x.Color, y.Color, null, null, null, null, ref expressible) is { } change)
            {
                changes.Add(change);
            }
        }

        return expressible ? new VisualizerChangesSpec { Nodes = changes.Count > 0 ? changes : null } : null;
    }

    public void ReadExtras(VisualizerStep? step, object? model, List<ElementRef> highlight, List<PointerSpec> pointers)
    {
        var list = ModelValues.As<LinkedListData>(model);
        var seen = new HashSet<int>();
        foreach (var id in step?.ActiveNodeIds ?? [])
        {
            if (id.StartsWith("node_", StringComparison.Ordinal) && int.TryParse(id.AsSpan(5), out var i) && i >= 0 && i < list.Nodes.Count && seen.Add(i))
            {
                highlight.Add(ElementRef.Node(SpecId(list.Nodes[i])));
            }
        }

        for (var i = 0; i < list.Nodes.Count; i++)
        {
            if (list.Nodes[i].IsActive && seen.Add(i)) highlight.Add(ElementRef.Node(SpecId(list.Nodes[i])));
        }

        foreach (var pointer in list.Pointers)
        {
            pointers.Add(new PointerSpec
            {
                Name = pointer.Name,
                At = pointer.Index >= 0 && pointer.Index < list.Nodes.Count ? ElementRef.Node(SpecId(list.Nodes[pointer.Index])) : (ElementRef?)null,
                Color = pointer.Color
            });
        }
    }
}
