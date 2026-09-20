using System;
using System.Collections.Generic;
using System.Linq;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

public class GraphTracker
{
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

    public static GraphTracker Create(object graphSource, string? title = null, bool isDirected = true)
    {
        var graph = GraphDataParser.Parse(graphSource, isDirected);
        var options = new VisualizerOptions
        {
            Title = title ?? (isDirected ? "Directed Graph Visualizer" : "Undirected Graph Visualizer"),
            Kind = VisualizerKind.Graph,
            GraphData = graph
        };

        var tracker = new GraphTracker(graph, options);
        tracker.Snapshot("Initial Graph State");
        return tracker;
    }

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

    public void Visit(object? nodeOrId, string? note = null, string? subLabel = null, string? pointer = null)
    {
        var node = ResolveNode(nodeOrId);
        if (node == null) return;

        if (_currentActiveNode != null && _currentActiveNode != node && _currentActiveNode.State == GraphNodeState.Current)
        {
            _currentActiveNode.State = GraphNodeState.Visited;
            _currentActiveNode.IsActive = false;
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
        Snapshot(desc);
    }

    public void Enqueue(object? nodeOrId, string? note = null, string? subLabel = null)
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
        Snapshot(desc);
    }

    public void RelaxEdge(object? from, object? to, double? weight = null, double? newDistance = null, string? note = null)
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
        Snapshot(desc);
    }

    public void HighlightNode(object? nodeOrId, GraphNodeState state, string? note = null, string? subLabel = null)
    {
        var node = ResolveNode(nodeOrId);
        if (node == null) return;

        node.State = state;
        if (!string.IsNullOrEmpty(subLabel))
        {
            node.SubLabel = subLabel;
        }

        string desc = note ?? $"{state} Vertex [{node.Label}]";
        Snapshot(desc);
    }

    public void HighlightEdge(object? from, object? to, GraphEdgeState state, string? note = null)
    {
        var edge = ResolveEdge(from, to);
        if (edge == null) return;

        edge.State = state;
        string desc = note ?? $"{state} Edge ({from} -> {to})";
        Snapshot(desc);
    }

    public void SetPointer(object? nodeOrId, string pointerLabel, string? note = null)
    {
        var node = ResolveNode(nodeOrId);
        if (node == null) return;

        node.PointerLabel = pointerLabel;
        if (!string.IsNullOrEmpty(note))
        {
            Snapshot(note);
        }
    }

    public void ClearPointers()
    {
        foreach (var node in Graph.Nodes)
        {
            node.PointerLabel = null;
        }
    }

    public void Annotate(object? nodeOrId, string subLabel, string? note = null)
    {
        var node = ResolveNode(nodeOrId);
        if (node == null) return;

        node.SubLabel = subLabel;
        if (!string.IsNullOrEmpty(note))
        {
            Snapshot(note);
        }
    }

    public void MarkPath(IEnumerable<object> nodePath, string? note = null)
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
        Snapshot(desc);
    }

    public void Snapshot(string description)
    {
        var step = new VisualizerStep(Sequence.TotalSteps, description, VisualizerKind.Graph)
        {
            Snapshot = Graph.Clone()
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
