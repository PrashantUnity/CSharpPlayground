using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

/// <summary>Records your own list nodes; each keeps its first-seen slot, so rewiring shows as arrows flipping, not nodes moving.</summary>
public sealed class LinkedListTracker
{
    public const int MaxNodes = 40;

    private readonly List<object> _nodes = new();
    private readonly Dictionary<object, int> _indexOf = new(ReferenceEqualityComparer.Instance);
    private readonly List<int> _chainStarts = new();
    private readonly WatchList _watches = new();

    public VisualizerOptions Options { get; }
    public VisualizerSequence Sequence { get; }

    private LinkedListTracker(string title)
    {
        Sequence = new VisualizerSequence();
        Options = new VisualizerOptions
        {
            Title = title,
            Kind = VisualizerKind.LinkedList,
            LinkedListData = new LinkedListData(),
            Sequence = Sequence
        };
    }

    public static LinkedListTracker Create(
        object? head,
        string? title = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var tracker = new LinkedListTracker(title ?? "Linked List");
        tracker.Step("Initial list", new { head }, sourceLine: sourceLine, sourceFile: sourceFile);
        return tracker;
    }

    /// <summary>Shows a live queue, stack, set, map or list beneath the chain at every step recorded after this call.</summary>
    public void Watch(object collection, [CallerArgumentExpression(nameof(collection))] string name = "") =>
        _watches.Add(collection, name);

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

        var data = Snapshot();
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
            else if (_indexOf.TryGetValue(value, out int index))
            {
                data.Pointers.Add(new PointerMarkerData(name, index, color));
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

    private LinkedListData Snapshot()
    {
        var data = new LinkedListData { ChainStarts = new List<int>(_chainStarts) };
        for (int i = 0; i < _nodes.Count; i++)
        {
            var next = NextOf(_nodes[i]);
            int? nextIndex = next != null && _indexOf.TryGetValue(next, out int target) ? target : null;
            data.Nodes.Add(new LinkedListNodeData(i, ValueOf(_nodes[i]), nextIndex)
            {
                RawValue = _nodes[i],
                ContinuesBeyondView = next != null && nextIndex == null
            });
        }
        return data;
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
