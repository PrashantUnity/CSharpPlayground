using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

public class GraphTracker
{
    private readonly WatchList _watches = new();
    private GraphNodeData? _currentActiveNode;

    public GraphData Graph { get; }
    public VisualizerOptions Options { get; }
    public VisualizerSequence Sequence { get; }

    public GraphTracker(GraphData graph, VisualizerOptions options)
    {
        Graph = graph;
        Options = options;
        Sequence = options.Sequence ??= new VisualizerSequence();
    }

    public static GraphTracker Create(
        object graphSource,
        string? title = null,
        bool isDirected = true,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var graph = GraphDataParser.Parse(graphSource, isDirected);
        var options = new VisualizerOptions
        {
            Title = title ?? (isDirected ? "Directed Graph Visualizer" : "Undirected Graph Visualizer"),
            Kind = VisualizerKind.Graph,
            GraphData = graph
        };

        var tracker = new GraphTracker(graph, options);
        tracker.Snapshot("Initial Graph State", sourceLine, sourceFile);
        return tracker;
    }

    /// <summary>Shows a live queue, stack, set, map or list beneath the graph at every step recorded after this call.</summary>
    public void Watch(object collection, [CallerArgumentExpression(nameof(collection))] string name = "") =>
        _watches.Add(collection, name);

    public GraphNodeData? ResolveNode(object? target)
    {
        if (target == null) return null;
        if (target is GraphNodeData directNode) return Graph.FindNode(directNode.Id) ?? directNode;

        string str = target.ToString() ?? string.Empty;
        return Graph.FindNode(str);
    }

    public GraphEdgeData? ResolveEdge(object? from, object? to)
    {
        if (from == null || to == null) return null;
        string u = from.ToString() ?? string.Empty;
        string v = to.ToString() ?? string.Empty;
        return Graph.FindEdge(u, v);
    }

    public void Visit(
        object? nodeOrId,
        string? note = null,
        string? subLabel = null,
        string? pointer = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var node = ResolveNode(nodeOrId);
        if (node == null) return;

        // The previous node stops glowing; it only turns "visited" if nothing else was marked on it meanwhile.
        if (_currentActiveNode != null && _currentActiveNode != node)
        {
            _currentActiveNode.IsActive = false;
            if (_currentActiveNode.State == GraphNodeState.Current) _currentActiveNode.State = GraphNodeState.Visited;
        }

        node.State = GraphNodeState.Current;
        node.IsActive = true;
        node.IsVisited = true;
        _currentActiveNode = node;

        if (!string.IsNullOrEmpty(subLabel))
        {
            node.SubLabel = subLabel;
        }

        if (!string.IsNullOrEmpty(pointer))
        {
            node.PointerLabel = pointer;
        }

        string desc = note ?? $"Visiting Vertex [{node.Label}]";
        Snapshot(desc, sourceLine, sourceFile);
    }

    public void Enqueue(
        object? nodeOrId,
        string? note = null,
        string? subLabel = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var node = ResolveNode(nodeOrId);
        if (node == null) return;

        if (node.State != GraphNodeState.Current && node.State != GraphNodeState.Visited)
        {
            node.State = GraphNodeState.Frontier;
        }

        if (!string.IsNullOrEmpty(subLabel))
        {
            node.SubLabel = subLabel;
        }

        string desc = note ?? $"Enqueued Vertex [{node.Label}]";
        Snapshot(desc, sourceLine, sourceFile);
    }

    public void RelaxEdge(
        object? from,
        object? to,
        double? weight = null,
        double? newDistance = null,
        string? note = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var edge = ResolveEdge(from, to);
        if (edge != null)
        {
            edge.State = GraphEdgeState.Relaxed;
            edge.IsActive = true;
        }

        var toNode = ResolveNode(to);
        if (toNode != null)
        {
            toNode.State = GraphNodeState.Relaxed;
            if (newDistance.HasValue)
            {
                toNode.SubLabel = $"d={newDistance.Value}";
            }
        }

        string uStr = from?.ToString() ?? "";
        string vStr = to?.ToString() ?? "";
        string desc = note ?? $"Relaxed edge ({uStr} -> {vStr})" +
            (newDistance.HasValue ? $" • New distance = {newDistance.Value}" : "");
        Snapshot(desc, sourceLine, sourceFile);
    }

    public void HighlightNode(
        object? nodeOrId,
        GraphNodeState state,
        string? note = null,
        string? subLabel = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var node = ResolveNode(nodeOrId);
        if (node == null) return;

        node.State = state;
        if (!string.IsNullOrEmpty(subLabel))
        {
            node.SubLabel = subLabel;
        }

        string desc = note ?? $"{state} Vertex [{node.Label}]";
        Snapshot(desc, sourceLine, sourceFile);
    }

    public void HighlightEdge(
        object? from,
        object? to,
        GraphEdgeState state,
        string? note = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var edge = ResolveEdge(from, to);
        if (edge == null) return;

        edge.State = state;
        string desc = note ?? $"{state} Edge ({from} -> {to})";
        Snapshot(desc, sourceLine, sourceFile);
    }

    /// <summary>
    /// Adds an edge, and any endpoint that isn't drawn yet, for graphs discovered while the algorithm runs. Nothing is
    /// recorded until the next step; adding an edge that already exists does nothing.
    /// </summary>
    public void AddEdge(object from, object to, double? weight = null)
    {
        string u = from.ToString() ?? string.Empty;
        string v = to.ToString() ?? string.Empty;
        foreach (var id in new[] { u, v })
        {
            if (Graph.FindNode(id) == null) Graph.Nodes.Add(new GraphNodeData(id));
        }
        if (Graph.FindEdge(u, v) == null)
        {
            Graph.Edges.Add(new GraphEdgeData(u, v, weight, Graph.IsDirected));
        }
    }

    /// <summary>Sets a node's state without recording a step, so several nodes can change in one step.</summary>
    public void Mark(object? nodeOrId, GraphNodeState state)
    {
        var node = ResolveNode(nodeOrId);
        if (node == null) return;

        node.State = state;
        node.IsActive = state == GraphNodeState.Current;
        if (state == GraphNodeState.Current) _currentActiveNode = node;
    }

    /// <summary>Fills a node with a colour from the next recorded step on (e.g. one colour per component); null clears it.</summary>
    public void Paint(object? nodeOrId, string? color)
    {
        var node = ResolveNode(nodeOrId);
        if (node != null) node.Color = color;
    }

    /// <summary>Sets an edge's state without recording a step.</summary>
    public void MarkEdge(object? from, object? to, GraphEdgeState state)
    {
        var edge = ResolveEdge(from, to);
        if (edge == null) return;

        edge.State = state;
        edge.IsActive = state is GraphEdgeState.Active or GraphEdgeState.Path;
    }

    /// <summary>Ends the current node's glow (it turns visited), e.g. before a closing summary step.</summary>
    public void ClearCurrent()
    {
        foreach (var node in Graph.Nodes)
        {
            if (node.State == GraphNodeState.Current) node.State = GraphNodeState.Visited;
            node.IsActive = false;
        }
        _currentActiveNode = null;
    }

    public void SetPointer(
        object? nodeOrId,
        string pointerLabel,
        string? note = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var node = ResolveNode(nodeOrId);
        if (node == null) return;

        node.PointerLabel = pointerLabel;
        if (!string.IsNullOrEmpty(note))
        {
            Snapshot(note, sourceLine, sourceFile);
        }
    }

    public void ClearPointers()
    {
        foreach (var node in Graph.Nodes)
        {
            node.PointerLabel = null;
        }
    }

    public void Annotate(
        object? nodeOrId,
        string subLabel,
        string? note = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var node = ResolveNode(nodeOrId);
        if (node == null) return;

        node.SubLabel = subLabel;
        if (!string.IsNullOrEmpty(note))
        {
            Snapshot(note, sourceLine, sourceFile);
        }
    }

    public void MarkPath(
        IEnumerable<object> nodePath,
        string? note = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var nodeList = nodePath.Select(ResolveNode).Where(n => n != null).Cast<GraphNodeData>().ToList();
        if (nodeList.Count == 0) return;

        foreach (var node in nodeList)
        {
            node.State = GraphNodeState.Path;
        }

        for (int i = 0; i < nodeList.Count - 1; i++)
        {
            var edge = Graph.FindEdge(nodeList[i].Id, nodeList[i + 1].Id);
            if (edge != null)
            {
                edge.State = GraphEdgeState.Path;
                edge.IsActive = true;
            }
        }

        string pathStr = string.Join(" -> ", nodeList.Select(n => n.Label));
        string desc = note ?? $"Shortest Path: [{pathStr}]";
        Snapshot(desc, sourceLine, sourceFile);
    }

    public void Snapshot(
        string description,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var step = new VisualizerStep(Sequence.TotalSteps, description, VisualizerKind.Graph)
        {
            Snapshot = Graph.Clone(),
            SourceLine = sourceLine,
            SourceFile = sourceFile,
            Watches = _watches.Capture()
        };

        foreach (var node in Graph.Nodes)
        {
            if (node.IsActive || node.State == GraphNodeState.Current || node.State == GraphNodeState.Target || node.State == GraphNodeState.Path)
            {
                step.ActiveNodeIds.Add(node.Id);
            }
        }

        Sequence.AddStep(step);
    }
}
