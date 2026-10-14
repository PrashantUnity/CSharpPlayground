using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Rendering;

namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Kinds;

/// <summary>
/// Tree visualizers: a flat <see cref="TreeState"/> as linked <see cref="TreeNodeData"/> nodes, and back. Every walk here is
/// iterative, so a deep (skewed) tree can't run out of stack.
/// </summary>
internal sealed class TreeKind : IVisualizerKind
{
    public static readonly TreeKind Instance = new();

    public object? Build(VisualizerState state, VisualizerSpec spec, ICollection<string> notices)
    {
        var source = state.Tree!;
        if (source.Nodes.Count == 0) return null;

        var byId = new Dictionary<string, TreeNodeSpec>(StringComparer.Ordinal);
        foreach (var node in source.Nodes) byId.TryAdd(node.Id, node);
        var rootId = source.Root ?? source.Nodes[0].Id;
        if (!byId.ContainsKey(rootId)) return null;

        // Breadth first from the root, so a tree over the limit loses its deepest nodes, not a whole side.
        var models = new Dictionary<string, TreeNodeData>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(rootId);
        models[rootId] = ToModel(byId[rootId]);
        while (queue.Count > 0)
        {
            var spec0 = byId[queue.Dequeue()];
            foreach (var child in ChildIds(spec0))
            {
                if (models.Count >= VisualLimits.MaxTreeNodes) break;
                if (!byId.TryGetValue(child, out var childSpec) || models.ContainsKey(child)) continue;
                models[child] = ToModel(childSpec);
                queue.Enqueue(child);
            }
        }

        var reachable = CountReachable(byId, rootId);
        if (reachable > models.Count) notices.Add(Notices.ShowingFirst(models.Count, reachable, "nodes"));

        foreach (var (id, model) in models)
        {
            var node = byId[id];
            model.Left = node.Left != null && models.TryGetValue(node.Left, out var left) ? left : null;
            model.Right = node.Right != null && models.TryGetValue(node.Right, out var right) ? right : null;
            if (model.Left != null) model.Children.Add(model.Left);
            if (model.Right != null) model.Children.Add(model.Right);
            foreach (var child in node.Children ?? [])
            {
                if (models.TryGetValue(child, out var childModel) && childModel != model.Left && childModel != model.Right) model.Children.Add(childModel);
            }

            foreach (var child in model.Children) child.Parent = model;
        }

        var root = models[rootId];
        SetDepths(root);
        return root;
    }

    private static TreeNodeData ToModel(TreeNodeSpec spec) => new(spec.Value.ToString(), spec.Id)
    {
        RawValue = ModelValues.ToObject(spec.Value),
        State = ElementStates.ToTree(spec.State ?? ElementState.Default),
        CustomColor = spec.Color,
        PointerLabel = spec.Pointer,
        SubLabel = spec.Note,
        Metadata = ModelValues.ToMetadata(spec.Notes)
    };

    private static IEnumerable<string> ChildIds(TreeNodeSpec node)
    {
        if (node.Left != null) yield return node.Left;
        if (node.Right != null) yield return node.Right;
        foreach (var child in node.Children ?? []) yield return child;
    }

    private static int CountReachable(Dictionary<string, TreeNodeSpec> byId, string rootId)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal) { rootId };
        var pending = new Stack<string>();
        pending.Push(rootId);
        while (pending.Count > 0)
        {
            foreach (var child in ChildIds(byId[pending.Pop()]))
            {
                if (byId.ContainsKey(child) && seen.Add(child)) pending.Push(child);
            }
        }

        return seen.Count;
    }

    private static void SetDepths(TreeNodeData root)
    {
        root.Depth = 0;
        foreach (var node in Walk(root).Skip(1)) node.Depth = node.Parent!.Depth + 1;
    }

    /// <summary>Every node, parents before their children (preorder), without recursion.</summary>
    internal static IEnumerable<TreeNodeData> Walk(TreeNodeData root)
    {
        var pending = new Stack<TreeNodeData>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            yield return node;
            for (var i = node.Children.Count - 1; i >= 0; i--) pending.Push(node.Children[i]);
        }
    }

    public object? Clone(object? model)
    {
        if (model == null) return null;
        var root = ModelValues.As<TreeNodeData>(model);
        var copies = new Dictionary<TreeNodeData, TreeNodeData>(ReferenceEqualityComparer.Instance);
        foreach (var node in Walk(root))
        {
            copies[node] = new TreeNodeData(node.DisplayValue, node.Id)
            {
                RawValue = node.RawValue,
                X = node.X,
                Y = node.Y,
                Depth = node.Depth,
                IsActive = node.IsActive,
                IsVisited = node.IsVisited,
                State = node.State,
                PointerLabel = node.PointerLabel,
                SubLabel = node.SubLabel,
                CustomColor = node.CustomColor,
                Metadata = new Dictionary<string, object?>(node.Metadata)
            };
        }

        foreach (var (original, copy) in copies)
        {
            copy.Left = original.Left != null ? copies[original.Left] : null;
            copy.Right = original.Right != null ? copies[original.Right] : null;
            copy.Children.AddRange(original.Children.Select(c => copies[c]));
            copy.Parent = original.Parent != null && copies.TryGetValue(original.Parent, out var parent) ? parent : null;
        }

        return copies[root];
    }

    public void Apply(object? model, VisualizerChangesSpec changes)
    {
        if (model == null || changes.Nodes == null) return;
        var byId = Index(ModelValues.As<TreeNodeData>(model));
        foreach (var change in changes.Nodes)
        {
            if (!byId.TryGetValue(change.Id, out var node)) continue;
            if (change.Value is { } value)
            {
                node.DisplayValue = value.ToString();
                node.RawValue = ModelValues.ToObject(value);
            }

            if (change.State is { } state)
            {
                node.State = ElementStates.ToTree(state);
                node.IsVisited = false;
            }

            if (change.Color != null) node.CustomColor = change.Color;
            if (change.Pointer != null) node.PointerLabel = change.Pointer;
            if (change.Note != null) node.SubLabel = change.Note;
        }
    }

    // Ids are unique in a spec; a C# tree can repeat one, and then the first (in preorder) is the one it names.
    private static Dictionary<string, TreeNodeData> Index(TreeNodeData root)
    {
        var byId = new Dictionary<string, TreeNodeData>(StringComparer.Ordinal);
        foreach (var node in Walk(root)) byId.TryAdd(node.Id, node);
        return byId;
    }

    public void DecorateStep(VisualizerStep step, IReadOnlyList<ElementRef> highlight, IReadOnlyList<PointerSpec> pointers)
    {
        foreach (var node in highlight.Where(h => h.Kind == ElementRefKind.Node)) step.ActiveNodeIds.Add(node.Id!);
    }

    public object? DecorateModel(object? model, IReadOnlyList<ElementRef> highlight, IReadOnlyList<PointerSpec> pointers, bool onStep)
    {
        var flagged = onStep ? [] : highlight.Where(h => h.Kind == ElementRefKind.Node).Select(h => h.Id!).ToList();
        if (model == null || flagged.Count == 0 && pointers.Count == 0) return model;
        var root = ModelValues.As<TreeNodeData>(Clone(model));
        var byId = Index(root);
        foreach (var id in flagged)
        {
            if (byId.TryGetValue(id, out var node)) node.IsActive = true;
        }

        // A step's pointers join the node's own tag (a pointer the tree keeps, like "root").
        foreach (var group in pointers.Where(p => p.At is { Kind: ElementRefKind.Node }).GroupBy(p => p.At!.Value.Id!))
        {
            if (!byId.TryGetValue(group.Key, out var node)) continue;
            var names = ModelValues.JoinNames(group);
            node.PointerLabel = string.IsNullOrEmpty(node.PointerLabel) ? names : $"{node.PointerLabel}, {names}";
        }

        return root;
    }

    public void Assign(VisualizerOptions options, object? model) => options.TreeData = model == null ? null : ModelValues.As<TreeNodeData>(model);

    public object? InitialModel(VisualizerOptions options) => options.TreeData;

    public VisualizerState ToState(object? model)
    {
        var state = new TreeState();
        if (model == null) return new VisualizerState { Tree = state };

        var root = ModelValues.As<TreeNodeData>(model);
        var ids = SpecIds(root);
        state.Root = ids[root];
        foreach (var node in Walk(root))
        {
            var binary = node.Left != null || node.Right != null;
            state.Nodes.Add(new TreeNodeSpec
            {
                Id = ids[node],
                Value = ModelValues.ToScalar(node.RawValue, node.DisplayValue),
                Left = node.Left != null ? ids[node.Left] : null,
                Right = node.Right != null ? ids[node.Right] : null,
                Children = binary || node.Children.Count == 0 ? null : node.Children.Select(c => ids[c]).ToList(),
                State = ElementStates.OrNull(node.State == TreeNodeState.Default && node.IsVisited ? ElementState.Visited : ElementStates.FromTree(node.State)),
                Color = ModelValues.NullIfEmpty(node.CustomColor),
                Pointer = ModelValues.NullIfEmpty(node.PointerLabel),
                Note = ModelValues.NullIfEmpty(node.SubLabel),
                Notes = ModelValues.ToNotes(node.Metadata)
            });
        }

        return new VisualizerState { Tree = state };
    }

    // Each node's id in the spec: its own, or with "#2", "#3"… when a C# tree repeats it.
    private static Dictionary<TreeNodeData, string> SpecIds(TreeNodeData root)
    {
        var ids = new Dictionary<TreeNodeData, string>(ReferenceEqualityComparer.Instance);
        var used = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var node in Walk(root))
        {
            var id = string.IsNullOrEmpty(node.Id) ? "node" : node.Id;
            used[id] = used.TryGetValue(id, out var count) ? count + 1 : 1;
            ids[node] = used[id] == 1 ? id : $"{id}#{used[id]}";
        }

        return ids;
    }

    public VisualizerChangesSpec? Diff(VisualizerState previous, VisualizerState next)
    {
        if (previous.Tree is not { } a || next.Tree is not { } b) return null;
        if (a.Root != b.Root || a.Nodes.Count != b.Nodes.Count) return null;

        var expressible = true;
        var changes = new List<NodeChangeSpec>();
        for (var i = 0; i < b.Nodes.Count; i++)
        {
            var (x, y) = (a.Nodes[i], b.Nodes[i]);
            if (x.Id != y.Id || x.Left != y.Left || x.Right != y.Right || !StateDiff.SameList(x.Children, y.Children) || !StateDiff.SameNotes(x.Notes, y.Notes))
            {
                return null;
            }

            if (StateDiff.Node(y.Id, x.Value, y.Value, x.State, y.State, x.Color, y.Color, x.Pointer, y.Pointer, x.Note, y.Note, ref expressible) is { } change)
            {
                changes.Add(change);
            }

            if (!expressible) return null;
        }

        return new VisualizerChangesSpec { Nodes = changes.Count > 0 ? changes : null };
    }

    public void ReadExtras(VisualizerStep? step, object? model, List<ElementRef> highlight, List<PointerSpec> pointers)
    {
        if (model == null) return;
        var root = ModelValues.As<TreeNodeData>(model);
        var ids = SpecIds(root);
        var firstById = new Dictionary<string, TreeNodeData>(StringComparer.Ordinal);
        foreach (var node in Walk(root)) firstById.TryAdd(node.Id, node);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in step?.ActiveNodeIds ?? [])
        {
            if (firstById.TryGetValue(id, out var node) && seen.Add(ids[node])) highlight.Add(ElementRef.Node(ids[node]));
        }

        foreach (var node in Walk(root).Where(n => n.IsActive))
        {
            if (seen.Add(ids[node])) highlight.Add(ElementRef.Node(ids[node]));
        }
    }
}
