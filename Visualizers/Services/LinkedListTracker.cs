using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Kinds;
using System.ComponentModel;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

/// <summary>
/// Records your own list nodes. By default each node keeps its first-seen slot, so rewiring shows as arrows flipping,
/// not nodes moving. With <c>rows: true</c> every step is redrawn in link order, one row per separate chain, which
/// reads best when lists are merged, split or woven together.
/// </summary>
public sealed class LinkedListTracker : IVisualSource
{
    public const int MaxNodes = 40;

    /// <summary>The fill for a finished node: a node marked done or visited, or painted without a colour of its own.</summary>
    public const string DoneColor = "#14532d";

    private readonly List<object> _nodes = new();
    private readonly Dictionary<object, int> _indexOf = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, string> _colors = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, ElementState> _states = new(ReferenceEqualityComparer.Instance);
    private readonly List<int> _chainStarts = new();
    private readonly WatchList _watches = new();
    private readonly bool _rows;

    public VisualizerOptions Options { get; }
    public VisualizerSequence Sequence { get; }

    private LinkedListTracker(string title, bool rows)
    {
        _rows = rows;
        Sequence = new VisualizerSequence();
        Options = new VisualizerOptions
        {
            Title = title,
            Kind = VisualizerKind.LinkedList,
            LinkedListData = new LinkedListData(),
            Sequence = Sequence
        };
    }

    /// <param name="rows">Redraw each step in link order with one row per separate chain (merging, splitting, weaving).</param>
    /// <param name="headName">The first step's pointer label; defaults to the argument, e.g. Create(dummy) shows "dummy".</param>
    public static LinkedListTracker Create(
        object? head,
        string? title = null,
        bool rows = false,
        [CallerArgumentExpression(nameof(head))] string headName = "",
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var tracker = new LinkedListTracker(title ?? "Linked List", rows);
        headName = headName.Trim();
        bool readable = headName.Length is > 0 and <= 16 && headName.All(c => char.IsLetterOrDigit(c) || c is '_' or '.' or '[' or ']');
        tracker.Step("Initial list", head, readable ? headName : "head", sourceLine, sourceFile);
        return tracker;
    }

    /// <summary>Shows a live queue, stack, set, map or list beneath the chain at every step recorded after this call.</summary>
    public LinkedListTracker Watch(object collection, [CallerArgumentExpression(nameof(collection))] string name = "")
    {
        _watches.Add(collection, name);
        return this;
    }

    /// <summary>Fills a node with a colour from the next recorded step on, e.g. the finished part of a merged list; null clears it.</summary>
    public LinkedListTracker Paint(object node, string? color)
    {
        if (color == null) _colors.Remove(node);
        else _colors[node] = color;
        return this;
    }

    /// <summary>
    /// Sets a node's state from the next recorded step on (the one state vocabulary every visualizer shares): current is
    /// highlighted, done or visited filled as finished.
    /// </summary>
    public LinkedListTracker Mark(object node, ElementState state)
    {
        if (state == ElementState.Default) _states.Remove(node);
        else _states[node] = state;
        return this;
    }

    /// <summary>Fills a node as finished (or with <paramref name="color"/>); the name before <see cref="Paint"/>.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public LinkedListTracker Mark(object node, string color = DoneColor) => Paint(node, color);

    /// <summary>Clears what <see cref="Mark(object, ElementState)"/> and <see cref="Paint"/> set on a node.</summary>
    public LinkedListTracker Unmark(object node)
    {
        _colors.Remove(node);
        _states.Remove(node);
        return this;
    }

    /// <summary>The visualizer as a spec, as every language describes one.</summary>
    public VisualSpec ToVisualSpec() => VisualizerOptionsConverter.ToSpec(Options);


    /// <summary>Records the list as it is now; pass pointers as <c>new { prev, curr, next }</c> (other values become badges).</summary>
    public LinkedListTracker Step(
        string description,
        object? pointers = null,
        [CallerArgumentExpression(nameof(pointers))] string pointersExpression = "",
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var named = ReadPointers(pointers, pointersExpression);

        // Nodes linked in after existing ones join the drawing first; nodes only a variable knows about start a new chain.
        for (int i = 0; i < _nodes.Count; i++)
        {
            Discover(NextOf(_nodes[i]), startsChain: false);
        }
        foreach (var (_, value) in named)
        {
            if (IsNode(value)) Discover(value, startsChain: true);
        }

        var (data, displayIndex) = Snapshot();
        var step = new VisualizerStep(Sequence.TotalSteps, description, VisualizerKind.LinkedList)
        {
            Snapshot = data,
            SourceLine = sourceLine,
            SourceFile = sourceFile,
            Watches = _watches.Capture()
        };

        foreach (var (name, value) in named)
        {
            string color = VisualizerPaletteService.GetPointerColor(name);
            if (value == null)
            {
                data.Pointers.Add(new PointerMarkerData(name, -1, color));
            }
            else if (_indexOf.TryGetValue(value, out int slot))
            {
                data.Pointers.Add(new PointerMarkerData(name, displayIndex[slot], color));
            }
            else if (!IsNode(value))
            {
                step.AuxiliaryInfo[name] = value.ToString() ?? string.Empty;
            }
        }

        Sequence.AddStep(step);
        Options.LinkedListData = data;
        return this;
    }

    // Slot i is the i-th node ever seen; the drawing lists nodes in slot order, or in link order for rows.
    private (LinkedListData Data, int[] DisplayIndex) Snapshot()
    {
        int count = _nodes.Count;
        var nextSlot = new int?[count];
        for (int i = 0; i < count; i++)
        {
            var next = NextOf(_nodes[i]);
            nextSlot[i] = next != null && _indexOf.TryGetValue(next, out int target) ? target : null;
        }

        var order = new List<int>(count);
        var chainStarts = new List<int>();
        if (_rows)
        {
            // A row starts at a node nothing links to (in first-seen order) and follows next; a pure loop gets its own.
            var linkedTo = new bool[count];
            foreach (var next in nextSlot)
            {
                if (next is int target) linkedTo[target] = true;
            }

            var placed = new bool[count];
            void Walk(int start)
            {
                if (order.Count > 0) chainStarts.Add(order.Count);
                for (int? slot = start; slot is int s && !placed[s]; slot = nextSlot[s])
                {
                    placed[s] = true;
                    order.Add(s);
                }
            }
            for (int i = 0; i < count; i++) if (!linkedTo[i]) Walk(i);
            for (int i = 0; i < count; i++) if (!placed[i]) Walk(i);
        }
        else
        {
            for (int i = 0; i < count; i++) order.Add(i);
            chainStarts.AddRange(_chainStarts);
        }

        var displayIndex = new int[count];
        for (int i = 0; i < order.Count; i++) displayIndex[order[i]] = i;

        var data = new LinkedListData { ChainStarts = chainStarts, StackChains = _rows };
        foreach (int slot in order)
        {
            var node = _nodes[slot];
            data.Nodes.Add(new LinkedListNodeData(data.Nodes.Count, ValueOf(node), nextSlot[slot] is int target ? displayIndex[target] : null)
            {
                RawValue = node,
                Color = _colors.TryGetValue(node, out var color) ? color
                    : _states.TryGetValue(node, out var done) && done is ElementState.Done or ElementState.Visited ? DoneColor : null,
                IsActive = _states.TryGetValue(node, out var state) && state == ElementState.Current,
                ContinuesBeyondView = NextOf(node) != null && nextSlot[slot] == null
            });
        }

        if (FindCycle(nextSlot) is var (from, to))
        {
            data.HasCycle = true;
            data.CycleSourceIndex = displayIndex[from];
            data.CycleTargetIndex = displayIndex[to];
        }
        return (data, displayIndex);
    }

    // The first link that closes a loop, found by following next from every slot.
    private static (int From, int To)? FindCycle(int?[] nextSlot)
    {
        var state = new byte[nextSlot.Length];   // 0 unseen, 1 on the current walk, 2 finished
        for (int start = 0; start < nextSlot.Length; start++)
        {
            if (state[start] != 0) continue;
            var path = new List<int>();
            int? slot = start;
            while (slot is int s && state[s] == 0)
            {
                state[s] = 1;
                path.Add(s);
                slot = nextSlot[s];
            }
            if (slot is int hit && state[hit] == 1) return (path[^1], hit);
            foreach (int s in path) state[s] = 2;
        }
        return null;
    }

    private void Discover(object? node, bool startsChain)
    {
        bool first = true;
        while (node != null && !_indexOf.ContainsKey(node) && _nodes.Count < MaxNodes)
        {
            if (first && startsChain && _nodes.Count > 0)
            {
                _chainStarts.Add(_nodes.Count);
            }
            first = false;
            _indexOf[node] = _nodes.Count;
            _nodes.Add(node);
            node = NextOf(node);
        }
    }

    private static List<(string Name, object? Value)> ReadPointers(object? pointers, string expression)
    {
        var named = new List<(string Name, object? Value)>();
        if (pointers == null) return named;

        // A single node passed directly (tracker.Step("…", curr)) is named after the argument.
        if (IsNode(pointers))
        {
            named.Add((string.IsNullOrWhiteSpace(expression) ? "node" : expression.Trim(), pointers));
            return named;
        }

        foreach (var property in pointers.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length == 0)
            {
                named.Add((property.Name, property.GetValue(pointers)));
            }
        }
        return named;
    }

    // The next member must link to the node's own type: new { prev, curr, next } also has a "next", but it is not a node.
    private static bool IsNode(object? value) =>
        value != null &&
        !ObjectInspectorBuilder.IsScalarType(value.GetType()) &&
        DataStructureDetector.HasSelfTypedMember(value, "Next");

    private static object? NextOf(object node) => VisualizerReflectionHelper.GetMemberValue(node, "next", "Next");

    private static string ValueOf(object node) =>
        VisualizerReflectionHelper.ExtractDisplayValue(node, "val", "Val", "Value", "value", "Data", "data");
}
