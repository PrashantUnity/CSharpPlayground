using System.Collections.Generic;
using System.Linq;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

public enum GraphNodeState
{
    Default,
    Current,
    Visited,
    Target,
    Path,
    Frontier,
    Relaxed,
    Cycle,
    Unreachable
}

public enum GraphEdgeState
{
    Default,
    Active,
    Visited,
    Relaxed,
    Path,
    Rejected,
    CrossEdge
}

public class GraphNodeData
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public bool IsActive { get; set; }
    public bool IsVisited { get; set; }
    public GraphNodeState State { get; set; } = GraphNodeState.Default;
    public string? PointerLabel { get; set; }
    public string? SubLabel { get; set; }
    public string? Color { get; set; }
    public Dictionary<string, object?> Metadata { get; set; } = new();

    public GraphNodeData() { }

    public GraphNodeData(string id, string? label = null)
    {
        Id = id;
        Label = label ?? id;
    }

    public GraphNodeData Clone() => new(Id, Label)
    {
        X = X,
        Y = Y,
        IsActive = IsActive,
        IsVisited = IsVisited,
        State = State,
        PointerLabel = PointerLabel,
        SubLabel = SubLabel,
        Color = Color,
        Metadata = new Dictionary<string, object?>(Metadata)
    };
}

public class GraphEdgeData
{
    public string FromId { get; set; } = string.Empty;
    public string ToId { get; set; } = string.Empty;
    public double? Weight { get; set; }
    public string? Label { get; set; }
    public bool IsDirected { get; set; } = true;
    public bool IsActive { get; set; }
    public GraphEdgeState State { get; set; } = GraphEdgeState.Default;
    public string? Color { get; set; }

    public GraphEdgeData() { }

    public GraphEdgeData(string fromId, string toId, double? weight = null, bool isDirected = true)
    {
        FromId = fromId;
        ToId = toId;
        Weight = weight;
        IsDirected = isDirected;
        Label = weight?.ToString();
    }

    public GraphEdgeData Clone() => new(FromId, ToId, Weight, IsDirected)
    {
        Label = Label,
        IsActive = IsActive,
        State = State,
        Color = Color
    };
}

public class GraphData
{
    public bool IsDirected { get; set; } = true;
    public List<GraphNodeData> Nodes { get; set; } = new();
    public List<GraphEdgeData> Edges { get; set; } = new();

    public GraphNodeData? FindNode(string id) =>
        Nodes.FirstOrDefault(n => n.Id == id || n.Label == id);

    public GraphEdgeData? FindEdge(string fromId, string toId)
    {
        if (IsDirected)
        {
            return Edges.FirstOrDefault(e =>
                (e.FromId == fromId && e.ToId == toId));
        }

        return Edges.FirstOrDefault(e =>
            (e.FromId == fromId && e.ToId == toId) ||
            (e.FromId == toId && e.ToId == fromId));
    }

    public GraphData Clone()
    {
        var copy = new GraphData
        {
            IsDirected = IsDirected,
            Nodes = Nodes.Select(n => n.Clone()).ToList(),
            Edges = Edges.Select(e => e.Clone()).ToList()
        };
        return copy;
    }
}
