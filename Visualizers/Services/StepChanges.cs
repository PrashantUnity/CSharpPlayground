using System;
using System.Collections.Generic;
using System.Linq;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

/// <summary>What a step changed since the previous one: values, labels, structure; not traversal state, which has its own colours.</summary>
public static class StepChanges
{
    public static HashSet<int> ArrayItems(ArrayPointerData? previous, ArrayPointerData current)
    {
        var changed = new HashSet<int>();
        if (previous == null) return changed;

        for (int i = 0; i < current.Items.Count; i++)
        {
            if (i >= previous.Items.Count || previous.Items[i].DisplayValue != current.Items[i].DisplayValue)
            {
                changed.Add(i);
            }
        }
        return changed;
    }

    public static HashSet<int> Bars(BarChartVisualizerData? previous, BarChartVisualizerData current)
    {
        var changed = new HashSet<int>();
        if (previous == null) return changed;

        for (int i = 0; i < current.Items.Count; i++)
        {
            if (i >= previous.Items.Count || previous.Items[i].Value != current.Items[i].Value)
            {
                changed.Add(i);
            }
        }
        return changed;
    }

    public static HashSet<(int Row, int Col)> MatrixCells(GridMatrixData? previous, GridMatrixData current)
    {
        var changed = new HashSet<(int Row, int Col)>();
        if (previous == null || previous.Rows != current.Rows || previous.Columns != current.Columns) return changed;

        for (int r = 0; r < current.Rows; r++)
        {
            for (int c = 0; c < current.Columns; c++)
            {
                var before = previous[r, c];
                var now = current[r, c];
                if (before.DisplayValue != now.DisplayValue || before.SubLabel != now.SubLabel)
                {
                    changed.Add((r, c));
                }
            }
        }
        return changed;
    }

    public static HashSet<(int Row, int Col)> BoardCells(BoardVisualizerData? previous, BoardVisualizerData current)
    {
        var changed = new HashSet<(int Row, int Col)>();
        if (previous == null || previous.Rows != current.Rows || previous.Columns != current.Columns) return changed;

        for (int r = 0; r < current.Rows; r++)
        {
            for (int c = 0; c < current.Columns; c++)
            {
                var before = previous[r, c];
                var now = current[r, c];
                if (before.Value != now.Value || before.SubLabel != now.SubLabel)
                {
                    changed.Add((r, c));
                }
            }
        }
        return changed;
    }

    /// <summary>Nodes that are new, hold a different value, or whose next pointer was just rewired.</summary>
    public static HashSet<int> LinkedListNodes(LinkedListData? previous, LinkedListData current)
    {
        var changed = new HashSet<int>();
        if (previous == null) return changed;

        // Row layouts redraw nodes in link order, so a node is matched by identity rather than by position.
        if (current.StackChains && previous.StackChains)
        {
            var before = new Dictionary<object, (string Value, object? Next)>(ReferenceEqualityComparer.Instance);
            foreach (var node in previous.Nodes)
            {
                if (node.RawValue != null) before[node.RawValue] = (node.DisplayValue, NextOf(previous, node));
            }
            for (int i = 0; i < current.Nodes.Count; i++)
            {
                var node = current.Nodes[i];
                if (node.RawValue == null || !before.TryGetValue(node.RawValue, out var was) ||
                    was.Value != node.DisplayValue || !ReferenceEquals(was.Next, NextOf(current, node)))
                {
                    changed.Add(i);
                }
            }
            return changed;
        }

        for (int i = 0; i < current.Nodes.Count; i++)
        {
            if (i >= previous.Nodes.Count ||
                previous.Nodes[i].DisplayValue != current.Nodes[i].DisplayValue ||
                previous.Nodes[i].NextIndex != current.Nodes[i].NextIndex)
            {
                changed.Add(i);
            }
        }
        return changed;
    }

    private static object? NextOf(LinkedListData data, LinkedListNodeData node) =>
        node.NextIndex is int next && next >= 0 && next < data.Nodes.Count ? data.Nodes[next].RawValue : null;

    /// <summary>Ids of nodes that are new, hold a different value or label, or moved to another parent or side.</summary>
    public static HashSet<string> TreeNodes(TreeNodeData? previous, TreeNodeData current)
    {
        var changed = new HashSet<string>();
        if (previous == null) return changed;

        var before = new Dictionary<string, (string, string?, string?, char)>();
        Walk(previous, node => before[node.Id] = Signature(node));
        Walk(current, node =>
        {
            if (!before.TryGetValue(node.Id, out var old) || old != Signature(node))
            {
                changed.Add(node.Id);
            }
        });
        return changed;
    }

    public static HashSet<string> GraphNodes(GraphData? previous, GraphData current)
    {
        var changed = new HashSet<string>();
        if (previous == null) return changed;

        var before = previous.Nodes.ToDictionary(n => n.Id, n => (n.Label, n.SubLabel));
        foreach (var node in current.Nodes)
        {
            if (!before.TryGetValue(node.Id, out var old) || old != (node.Label, node.SubLabel))
            {
                changed.Add(node.Id);
            }
        }
        return changed;
    }

    /// <summary>Marks items with no counterpart in the previous snapshot: what was just enqueued, pushed or inserted.</summary>
    public static bool[] AddedWatchItems(StepWatch? previous, StepWatch current)
    {
        var added = new bool[current.Items.Count];
        if (previous == null) return added;

        var remaining = previous.Items.GroupBy(item => item).ToDictionary(g => g.Key, g => g.Count());
        for (int i = 0; i < current.Items.Count; i++)
        {
            if (remaining.TryGetValue(current.Items[i], out int left) && left > 0)
            {
                remaining[current.Items[i]] = left - 1;
            }
            else
            {
                added[i] = true;
            }
        }
        return added;
    }

    private static (string, string?, string?, char) Signature(TreeNodeData node)
    {
        char side = node.Parent == null ? 'R'
            : ReferenceEquals(node.Parent.Left, node) ? 'L'
            : ReferenceEquals(node.Parent.Right, node) ? 'r'
            : 'C';
        return (node.DisplayValue, node.SubLabel, node.Parent?.Id, side);
    }

    private static void Walk(TreeNodeData node, Action<TreeNodeData> visit)
    {
        visit(node);
        foreach (var child in node.Children)
        {
            Walk(child, visit);
        }
    }
}
