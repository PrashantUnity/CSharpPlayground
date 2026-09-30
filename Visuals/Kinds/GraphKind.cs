using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Rendering;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Kinds;

/// <summary>Graph visualizers: a <see cref="GraphSpec"/> as a <see cref="GraphData"/>, and back.</summary>
internal sealed class GraphKind : IVisualizerKind
{
    public static readonly GraphKind Instance = new();

    public object? Build(VisualizerState state, VisualizerSpec spec, ICollection<string> notices)
    {
        var source = state.Graph!;
        var graph = new GraphData { IsDirected = source.Directed ?? true };
        var nodes = source.Nodes.Take(VisualLimits.MaxGraphNodes).ToList();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            if (!ids.Add(node.Id)) continue;
            graph.Nodes.Add(new GraphNodeData(node.Id, node.Label ?? node.Id)
            {
                // Positions the spec gives are kept (the layout scales them to fit); without them the studio lays the graph out.
                PinnedX = node.X,
                PinnedY = node.Y,
                State = ElementStates.ToGraphNode(node.State ?? ElementState.Default),
                PointerLabel = node.Pointer,
                SubLabel = node.Note,
                Color = node.Color,
                Metadata = ModelValues.ToMetadata(node.Notes)
            });
        }

        foreach (var edge in source.Edges.Where(e => ids.Contains(e.From) && ids.Contains(e.To)).Take(VisualLimits.MaxGraphEdges))
        {
            graph.Edges.Add(new GraphEdgeData(edge.From, edge.To, edge.Weight, edge.Directed ?? source.Directed ?? true)
            {
                Label = edge.Label,
                State = ElementStates.ToGraphEdge(edge.State ?? ElementState.Default),
                Color = edge.Color
            });
        }

        if (source.Nodes.Count > VisualLimits.MaxGraphNodes) notices.Add(Notices.ShowingFirst(VisualLimits.MaxGraphNodes, source.Nodes.Count, "nodes"));
        else if (source.Edges.Count > VisualLimits.MaxGraphEdges) notices.Add(Notices.ShowingFirst(VisualLimits.MaxGraphEdges, source.Edges.Count, "edges"));
        return graph;
    }

    public object? Clone(object? model) => ModelValues.As<GraphData>(model).Clone();

    public void Apply(object? model, VisualizerChangesSpec changes)
    {
        var graph = ModelValues.As<GraphData>(model);
        foreach (var change in changes.Nodes ?? [])
        {
            if (graph.FindNode(change.Id) is not { } node) continue;
            if (change.Value is { } value) node.Label = value.ToString();
            if (change.State is { } state)
            {
                node.State = ElementStates.ToGraphNode(state);
                node.IsVisited = false;
            }

            if (change.Color != null) node.Color = change.Color;
            if (change.Pointer != null) node.PointerLabel = change.Pointer;
            if (change.Note != null) node.SubLabel = change.Note;
        }

        foreach (var change in changes.Edges ?? [])
        {
            if (graph.FindEdge(change.From, change.To) is not { } edge) continue;
            if (change.Weight is { } weight) edge.Weight = weight;
            if (change.Label != null) edge.Label = change.Label;
            if (change.State is { } state) edge.State = ElementStates.ToGraphEdge(state);
            if (change.Color != null) edge.Color = change.Color;
        }
    }

    public void DecorateStep(VisualizerStep step, IReadOnlyList<ElementRef> highlight, IReadOnlyList<PointerSpec> pointers)
    {
        foreach (var node in highlight.Where(h => h.Kind == ElementRefKind.Node)) step.ActiveNodeIds.Add(node.Id!);
    }

    // Edges have no list on the step: a highlighted edge is always marked on the model.
    public object? DecorateModel(object? model, IReadOnlyList<ElementRef> highlight, IReadOnlyList<PointerSpec> pointers, bool onStep)
    {
        var flagged = highlight.Where(h => h.Kind == ElementRefKind.Edge || !onStep && h.Kind == ElementRefKind.Node).ToList();
        if (flagged.Count == 0 && pointers.Count == 0) return model;
        var graph = ModelValues.As<GraphData>(model).Clone();
        foreach (var element in flagged)
        {
            if (element.Kind == ElementRefKind.Node && graph.FindNode(element.Id!) is { } node) node.IsActive = true;
            else if (element.Kind == ElementRefKind.Edge && graph.FindEdge(element.Id!, element.To!) is { } edge) edge.IsActive = true;
        }

        foreach (var group in pointers.Where(p => p.At is { Kind: ElementRefKind.Node }).GroupBy(p => p.At!.Value.Id!))
        {
            if (graph.FindNode(group.Key) is not { } node) continue;
            var names = ModelValues.JoinNames(group);
            node.PointerLabel = string.IsNullOrEmpty(node.PointerLabel) ? names : $"{node.PointerLabel}, {names}";
        }

        return graph;
    }

    public void Assign(VisualizerOptions options, object? model) => options.GraphData = ModelValues.As<GraphData>(model);

    public object? InitialModel(VisualizerOptions options) => options.GraphData;

    public VisualizerState ToState(object? model)
    {
        var graph = ModelValues.As<GraphData>(model);
        return new VisualizerState
        {
            Graph = new GraphSpec
            {
                Directed = graph.IsDirected,
                Nodes = graph.Nodes.Select(n => new GraphNodeSpec
                {
                    Id = n.Id,
                    Label = n.Label == n.Id ? null : n.Label,

                    // The drawn X and Y are the layout's work, redone on every draw; only a position the program pinned is data.
                    X = n.PinnedX,
                    Y = n.PinnedY,
                    State = ElementStates.OrNull(n.State == GraphNodeState.Default && n.IsVisited ? ElementState.Visited : ElementStates.FromGraphNode(n.State)),
                    Pointer = ModelValues.NullIfEmpty(n.PointerLabel),
                    Note = ModelValues.NullIfEmpty(n.SubLabel),
                    Color = ModelValues.NullIfEmpty(n.Color),
                    Notes = ModelValues.ToNotes(n.Metadata)
                }).ToList(),
                Edges = graph.Edges.Select(e => new GraphEdgeSpec
                {
                    From = e.FromId,
                    To = e.ToId,
                    Weight = e.Weight,
                    Label = ModelValues.NullIfEmpty(e.Label),
                    Color = ModelValues.NullIfEmpty(e.Color),
                    Directed = e.IsDirected == graph.IsDirected ? null : e.IsDirected,
                    State = ElementStates.OrNull(ElementStates.FromGraphEdge(e.State))
                }).ToList()
            }
        };
    }

    public VisualizerChangesSpec? Diff(VisualizerState previous, VisualizerState next)
    {
        if (previous.Graph is not { } a || next.Graph is not { } b) return null;
        if (a.Directed != b.Directed || a.Nodes.Count != b.Nodes.Count || a.Edges.Count != b.Edges.Count) return null;

        var expressible = true;
        var nodes = new List<NodeChangeSpec>();
        for (var i = 0; i < b.Nodes.Count; i++)
        {
            var (x, y) = (a.Nodes[i], b.Nodes[i]);
            if (x.Id != y.Id || x.X != y.X || x.Y != y.Y || x.Z != y.Z || x.Size != y.Size || !StateDiff.SameNotes(x.Notes, y.Notes)) return null;
            var label = (Before: ScalarValue.FromText(x.Label ?? x.Id), After: ScalarValue.FromText(y.Label ?? y.Id));
            if (StateDiff.Node(y.Id, label.Before, label.After, x.State, y.State, x.Color, y.Color, x.Pointer, y.Pointer, x.Note, y.Note, ref expressible) is { } change)
            {
                nodes.Add(change);
            }
        }

        var edges = new List<EdgeChangeSpec>();
        for (var i = 0; i < b.Edges.Count; i++)
        {
            var (x, y) = (a.Edges[i], b.Edges[i]);
            if (x.From != y.From || x.To != y.To || x.Directed != y.Directed) return null;
            if (StateDiff.Edge(x, y, ref expressible) is { } change) edges.Add(change);
        }

        if (!expressible) return null;
        return new VisualizerChangesSpec { Nodes = nodes.Count > 0 ? nodes : null, Edges = edges.Count > 0 ? edges : null };
    }

    public void ReadExtras(VisualizerStep? step, object? model, List<ElementRef> highlight, List<PointerSpec> pointers)
    {
        var graph = ModelValues.As<GraphData>(model);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in (step?.ActiveNodeIds ?? []).Concat(graph.Nodes.Where(n => n.IsActive).Select(n => n.Id)))
        {
            if (graph.FindNode(id) != null && seen.Add(id)) highlight.Add(ElementRef.Node(id));
        }

        foreach (var edge in graph.Edges.Where(e => e.IsActive)) highlight.Add(ElementRef.Edge(edge.FromId, edge.ToId));
    }
}
