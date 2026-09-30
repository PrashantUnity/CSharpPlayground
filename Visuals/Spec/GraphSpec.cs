using System.Text.Json.Serialization;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Spec;

/// <summary>
/// Nodes and the edges between them, for a graph visualizer and a 3D graph alike. Positions are optional: the studio
/// lays out any node without one.
/// </summary>
public sealed class GraphSpec
{
    /// <summary>Whether edges have a direction (default: yes); an edge can say otherwise for itself.</summary>
    public bool? Directed { get; set; }

    public List<GraphNodeSpec> Nodes { get; set; } = [];
    public List<GraphEdgeSpec> Edges { get; set; } = [];
}

public sealed class GraphNodeSpec
{
    /// <summary>Unique within the graph; edges, highlights and events name the node by it.</summary>
    [JsonRequired]
    public string Id { get; set; } = string.Empty;

    /// <summary>The text on the node (default: its id).</summary>
    public string? Label { get; set; }

    public double? X { get; set; }
    public double? Y { get; set; }

    /// <summary>Height in a 3D graph.</summary>
    public double? Z { get; set; }

    public string? Color { get; set; }

    /// <summary>The node's radius in pixels (default: the studio's).</summary>
    public double? Size { get; set; }

    public ElementState? State { get; set; }

    /// <summary>A small tag drawn next to the node, like a pointer's name.</summary>
    public string? Pointer { get; set; }

    /// <summary>A second, smaller line of text on the node, like a distance.</summary>
    public string? Note { get; set; }

    /// <summary>More facts shown when the pointer rests on the node.</summary>
    public Dictionary<string, string>? Notes { get; set; }
}

public sealed class GraphEdgeSpec
{
    [JsonRequired]
    public string From { get; set; } = string.Empty;
    [JsonRequired]
    public string To { get; set; } = string.Empty;
    public double? Weight { get; set; }
    public string? Label { get; set; }
    public string? Color { get; set; }

    /// <summary>Overrides <see cref="GraphSpec.Directed"/> for this edge.</summary>
    public bool? Directed { get; set; }

    public ElementState? State { get; set; }
}
