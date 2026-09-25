using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

public class TreeTracker
{
    private readonly WatchList _watches = new();
    private TreeNodeData? _currentActiveNode;
    private object? _source;
    private int _syncedIds;

    public TreeNodeData Root { get; private set; }
    public VisualizerOptions Options { get; }
    public VisualizerSequence Sequence { get; }

    public TreeTracker(TreeNodeData root, VisualizerOptions options)
    {
        Root = root;
        Options = options;
        Sequence = options.Sequence ??= new VisualizerSequence();
    }

    public static TreeTracker Create(
        object treeSource,
        string? title = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var root = TreeDataParser.Parse(treeSource) ?? new TreeNodeData("root", "node_1");
        var options = new VisualizerOptions
        {
            Title = title ?? "Tree Visualizer",
            Kind = VisualizerKind.Tree,
            TreeData = root
        };

        var tracker = new TreeTracker(root, options) { _source = treeSource };
        tracker.Snapshot("Initial Tree State", sourceLine, sourceFile);
        return tracker;
    }

    /// <summary>
    /// Re-reads your tree after the algorithm added, removed or rewired nodes (building a tree, inverting it). Every node
    /// keeps its state, labels and pointer. Pass <paramref name="root"/> when the root itself changed; a
    /// <paramref name="note"/> also records a step.
    /// </summary>
    public void Sync(
        object? root = null,
        string? note = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        if (root != null) _source = root;
        if (TreeDataParser.Parse(_source) is not { } fresh || ReferenceEquals(fresh, Root)) return;

        var before = new Dictionary<object, TreeNodeData>(ReferenceEqualityComparer.Instance);
        Walk(Root, node =>
        {
            if (node.RawValue != null) before[node.RawValue] = node;
        });

        // Known nodes keep their id (so the change outline and the playback focus follow them); new ones get fresh ids.
        Walk(fresh, node =>
        {
            if (node.RawValue != null && before.TryGetValue(node.RawValue, out var old))
            {
                node.Id = old.Id;
                node.State = old.State;
                node.IsActive = old.IsActive;
                node.IsVisited = old.IsVisited;
                node.PointerLabel = old.PointerLabel;
                node.SubLabel = old.SubLabel;
                node.CustomColor = old.CustomColor;
                node.Metadata = new Dictionary<string, object?>(old.Metadata);
            }
            else
            {
                node.Id = $"synced_{++_syncedIds}";
            }
        });

        Root = fresh;
        Options.TreeData = fresh;
        _currentActiveNode = _currentActiveNode?.RawValue is { } raw ? FindByRawValue(fresh, raw) : null;

        if (!string.IsNullOrEmpty(note))
        {
            Snapshot(note, sourceLine, sourceFile);
        }
    }

    private static void Walk(TreeNodeData node, Action<TreeNodeData> visit)
    {
        visit(node);
        foreach (var child in node.Children)
        {
            Walk(child, visit);
        }
    }

    /// <summary>Shows a live queue, stack, set, map or list beneath the tree at every step recorded after this call.</summary>
    public void Watch(object collection, [CallerArgumentExpression(nameof(collection))] string name = "") =>
        _watches.Add(collection, name);

    public TreeNodeData? ResolveNode(object? target)
    {
        if (target == null) return null;

        if (target is TreeNodeData directNode)
        {
            return Root.FindNode(directNode.Id) ?? directNode;
        }

        if (target is string str)
        {
            var byId = Root.FindNode(str);
            if (byId != null) return byId;
            var byVal = Root.FindByValue(str);
            if (byVal != null) return byVal;
        }

        if (target is int or long or double or float)
        {
            var byVal = Root.FindByValue(target.ToString()!);
            if (byVal != null) return byVal;
        }

        // Search by RawValue matching user POCO
        var foundByRaw = FindByRawValue(Root, target);
        if (foundByRaw != null) return foundByRaw;

        // Fallback: extract display value from POCO
        string disp = VisualizerReflectionHelper.ExtractDisplayValue(target, "val", "Val", "Value", "value", "Data", "data", "Key", "key");
        if (!string.IsNullOrEmpty(disp))
        {
            return Root.FindByValue(disp);
        }

        return null;
    }

    private static TreeNodeData? FindByRawValue(TreeNodeData current, object raw)
    {
        if (ReferenceEquals(current.RawValue, raw)) return current;
        foreach (var child in current.Children)
        {
            var res = FindByRawValue(child, raw);
            if (res != null) return res;
        }
        return null;
    }

    public void Visit(
        object? nodeOrTarget,
        string? note = null,
        string? subLabel = null,
        string? pointer = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var node = ResolveNode(nodeOrTarget);
        if (node == null) return;

        // The previous node stops glowing; it only turns "visited" if nothing else (swapped, matched...) was marked on it.
        if (_currentActiveNode != null && _currentActiveNode != node)
        {
            _currentActiveNode.IsActive = false;
            if (_currentActiveNode.State == TreeNodeState.Current) _currentActiveNode.State = TreeNodeState.Visited;
        }

        node.State = TreeNodeState.Current;
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

        string desc = note ?? $"Visit Node [{node.DisplayValue}] (Depth {node.Depth})";
        Snapshot(desc, sourceLine, sourceFile);
    }

    public void Highlight(
        object? nodeOrTarget,
        TreeNodeState state,
        string? note = null,
        string? subLabel = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var node = ResolveNode(nodeOrTarget);
        if (node == null) return;

        node.State = state;
        if (!string.IsNullOrEmpty(subLabel))
        {
            node.SubLabel = subLabel;
        }

        string desc = note ?? $"{state} Node [{node.DisplayValue}]";
        Snapshot(desc, sourceLine, sourceFile);
    }

    /// <summary>
    /// Colours a node without recording a step, so several nodes can change at once (comparing two trees in lockstep);
    /// the next recorded step shows them.
    /// </summary>
    public void Mark(object? nodeOrTarget, TreeNodeState state)
    {
        var node = ResolveNode(nodeOrTarget);
        if (node == null) return;

        node.State = state;
        node.IsActive = state == TreeNodeState.Current;
        if (state == TreeNodeState.Current) _currentActiveNode = node;
    }

    /// <summary>Ends the current node's glow (it turns visited), e.g. before a closing summary step.</summary>
    public void ClearCurrent()
    {
        Walk(Root, node =>
        {
            if (node.State == TreeNodeState.Current) node.State = TreeNodeState.Visited;
            node.IsActive = false;
        });
        _currentActiveNode = null;
    }

    /// <summary>Returns every node to its default colour (labels and pointers stay), e.g. before trying the next candidate.</summary>
    public void ClearMarks()
    {
        Walk(Root, node =>
        {
            node.State = TreeNodeState.Default;
            node.IsActive = false;
        });
        _currentActiveNode = null;
    }

    public void SetPointer(
        object? nodeOrTarget,
        string pointerLabel,
        string? note = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var node = ResolveNode(nodeOrTarget);
        if (node == null) return;

        node.PointerLabel = pointerLabel;
        if (!string.IsNullOrEmpty(note))
        {
            Snapshot(note, sourceLine, sourceFile);
        }
    }

    public void ClearPointers()
    {
        ClearPointersRecursive(Root);
    }

    private static void ClearPointersRecursive(TreeNodeData node)
    {
        node.PointerLabel = null;
        foreach (var child in node.Children)
        {
            ClearPointersRecursive(child);
        }
    }

    public void Annotate(
        object? nodeOrTarget,
        string subLabel,
        string? note = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var node = ResolveNode(nodeOrTarget);
        if (node == null) return;

        node.SubLabel = subLabel;
        if (!string.IsNullOrEmpty(note))
        {
            Snapshot(note, sourceLine, sourceFile);
        }
    }

    public void MarkPath(
        IEnumerable<object?> pathNodes,
        string? note = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var resolved = new List<TreeNodeData>();
        foreach (var item in pathNodes)
        {
            var node = ResolveNode(item);
            if (node != null)
            {
                node.State = TreeNodeState.Path;
                resolved.Add(node);
            }
        }

        string desc = note ?? $"Mark Path ({resolved.Count} nodes)";
        Snapshot(desc, sourceLine, sourceFile);
    }

    public void SwapChildren(
        object? nodeOrTarget,
        string? note = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var node = ResolveNode(nodeOrTarget);
        if (node == null) return;

        node.SwapChildren();
        node.State = TreeNodeState.Swapped;
        node.IsActive = true;
        _currentActiveNode = node;

        string desc = note ?? $"Swapped left & right subtrees of Node [{node.DisplayValue}]";
        Snapshot(desc, sourceLine, sourceFile);
    }

    public void Backtrack(
        object? nodeOrTarget,
        string? note = null,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var node = ResolveNode(nodeOrTarget);
        if (node == null) return;

        node.State = TreeNodeState.Backtracked;
        node.IsActive = false;

        string desc = note ?? $"Backtrack from Node [{node.DisplayValue}]";
        Snapshot(desc, sourceLine, sourceFile);
    }

    public void Snapshot(
        string description,
        [CallerLineNumber] int sourceLine = 0,
        [CallerFilePath] string sourceFile = "")
    {
        var step = new VisualizerStep(Sequence.TotalSteps, description, VisualizerKind.Tree)
        {
            Snapshot = Root.Clone(),
            SourceLine = sourceLine,
            SourceFile = sourceFile,
            Watches = _watches.Capture()
        };

        CollectActiveIds(Root, step.ActiveNodeIds);
        Sequence.AddStep(step);
    }

    private static void CollectActiveIds(TreeNodeData node, List<string> activeIds)
    {
        if (node.IsActive || node.State == TreeNodeState.Current || node.State == TreeNodeState.Target || node.State == TreeNodeState.Path)
        {
            activeIds.Add(node.Id);
        }
        foreach (var child in node.Children)
        {
            CollectActiveIds(child, activeIds);
        }
    }
}
