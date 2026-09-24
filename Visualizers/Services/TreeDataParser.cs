using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

public static class TreeDataParser
{
    public static TreeNodeData? Parse(object? root)
    {
        if (root == null) return null;
        if (root is TreeNodeData alreadyNode) return alreadyNode;

        if (root is string str)
        {
            return ParseLeetCodeString(str);
        }

        if (root is IEnumerable enumerable && !(root is string))
        {
            return ParseLevelOrderEnumerable(enumerable);
        }

        int idCounter = 0;
        return ParseRecursive(root, ref idCounter, 0, new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    public static TreeNodeData? ParseLeetCodeString(string leetCodeStr)
    {
        if (string.IsNullOrWhiteSpace(leetCodeStr)) return null;

        string trimmed = leetCodeStr.Trim();
        if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
        {
            trimmed = trimmed.Substring(1, trimmed.Length - 2).Trim();
        }

        if (string.IsNullOrWhiteSpace(trimmed)) return null;

        var tokens = trimmed.Split(',', StringSplitOptions.TrimEntries);
        var values = new List<string?>();

        foreach (var tok in tokens)
        {
            string t = tok.Trim().Trim('"', '\'');
            if (string.Equals(t, "null", StringComparison.OrdinalIgnoreCase) || t == "#" || string.IsNullOrEmpty(t))
            {
                values.Add(null);
            }
            else
            {
                values.Add(t);
            }
        }

        return ConstructFromLevelOrder(values);
    }

    public static TreeNodeData? ParseLevelOrderEnumerable(IEnumerable enumerable)
    {
        var values = new List<string?>();
        foreach (var item in enumerable)
        {
            if (item == null)
            {
                values.Add(null);
            }
            else
            {
                string s = item.ToString()?.Trim() ?? "";
                if (string.Equals(s, "null", StringComparison.OrdinalIgnoreCase) || s == "#" || string.IsNullOrEmpty(s))
                    values.Add(null);
                else
                    values.Add(s);
            }
        }
        return ConstructFromLevelOrder(values);
    }

    private static TreeNodeData? ConstructFromLevelOrder(List<string?> values)
    {
        if (values.Count == 0 || values[0] == null) return null;

        int idCounter = 0;
        var root = new TreeNodeData(values[0]!, $"node_{++idCounter}")
        {
            Depth = 0
        };

        var queue = new Queue<TreeNodeData>();
        queue.Enqueue(root);

        int i = 1;
        while (queue.Count > 0 && i < values.Count)
        {
            var curr = queue.Dequeue();

            // Left child
            if (i < values.Count)
            {
                if (values[i] != null)
                {
                    var leftNode = new TreeNodeData(values[i]!, $"node_{++idCounter}")
                    {
                        Depth = curr.Depth + 1,
                        Parent = curr
                    };
                    curr.Left = leftNode;
                    curr.Children.Add(leftNode);
                    queue.Enqueue(leftNode);
                }
                i++;
            }

            // Right child
            if (i < values.Count)
            {
                if (values[i] != null)
                {
                    var rightNode = new TreeNodeData(values[i]!, $"node_{++idCounter}")
                    {
                        Depth = curr.Depth + 1,
                        Parent = curr
                    };
                    curr.Right = rightNode;
                    curr.Children.Add(rightNode);
                    queue.Enqueue(rightNode);
                }
                i++;
            }
        }

        return root;
    }

    public static string ToLeetCodeString(TreeNodeData? root)
    {
        if (root == null) return "[]";

        var list = new List<string>();
        var queue = new Queue<TreeNodeData?>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (node == null)
            {
                list.Add("null");
            }
            else
            {
                list.Add(node.DisplayValue);
                queue.Enqueue(node.Left);
                queue.Enqueue(node.Right);
            }
        }

        // Trim trailing "null"s
        int lastNonNull = list.FindLastIndex(s => s != "null");
        if (lastNonNull >= 0)
        {
            list = list.Take(lastNonNull + 1).ToList();
        }
        else
        {
            return "[]";
        }

        return "[" + string.Join(", ", list) + "]";
    }

    private static TreeNodeData? ParseRecursive(object? nodeObj, ref int idCounter, int depth, HashSet<object> ancestors)
    {
        // A child that points back at an ancestor (a common insertion bug) would otherwise recurse forever
        if (nodeObj == null || !ancestors.Add(nodeObj)) return null;

        try
        {
            string displayVal = VisualizerReflectionHelper.ExtractDisplayValue(nodeObj, "val", "Val", "Value", "value", "Data", "data", "Key", "key");
            string id = $"node_{++idCounter}";

            var node = new TreeNodeData(displayVal, id)
            {
                RawValue = nodeObj,
                Depth = depth
            };

            // Check for Left / Right (both properties and fields)
            var leftObj = VisualizerReflectionHelper.GetMemberValue(nodeObj, "Left", "left");
            var rightObj = VisualizerReflectionHelper.GetMemberValue(nodeObj, "Right", "right");

            if (leftObj != null || rightObj != null)
            {
                if (leftObj != null)
                {
                    node.Left = ParseRecursive(leftObj, ref idCounter, depth + 1, ancestors);
                    if (node.Left != null)
                    {
                        node.Left.Parent = node;
                        node.Children.Add(node.Left);
                    }
                }
                if (rightObj != null)
                {
                    node.Right = ParseRecursive(rightObj, ref idCounter, depth + 1, ancestors);
                    if (node.Right != null)
                    {
                        node.Right.Parent = node;
                        node.Children.Add(node.Right);
                    }
                }
            }
            else
            {
                // Check for Children / children / nodes
                var childrenObj = VisualizerReflectionHelper.GetMemberValue(nodeObj, "Children", "children", "Nodes", "nodes");
                if (childrenObj is IEnumerable childrenEnum)
                {
                    foreach (var childObj in childrenEnum)
                    {
                        var childNode = ParseRecursive(childObj, ref idCounter, depth + 1, ancestors);
                        if (childNode != null)
                        {
                            childNode.Parent = node;
                            node.Children.Add(childNode);
                        }
                    }
                }
            }

            return node;
        }
        finally
        {
            ancestors.Remove(nodeObj);
        }
    }

    public static VisualizerSequence GenerateTraversalSteps(TreeNodeData root, string traversalType)
    {
        // Traverse a private copy so the caller's tree keeps its original node states.
        var tree = root.Clone();
        var sequence = new VisualizerSequence();
        var initialStep = new VisualizerStep(0, $"Tree initialized • Ready for {traversalType} Traversal", VisualizerKind.Tree)
        {
            Snapshot = tree.Clone()
        };
        sequence.AddStep(initialStep);

        var visited = new List<TreeNodeData>();
        string normalized = traversalType.ToLowerInvariant().Replace(" ", "").Replace("-", "");

        if (normalized.Contains("inorder"))
        {
            InOrder(tree, tree, visited, sequence);
        }
        else if (normalized.Contains("postorder"))
        {
            PostOrder(tree, tree, visited, sequence);
        }
        else if (normalized.Contains("levelorder") || normalized.Contains("bfs"))
        {
            LevelOrder(tree, visited, sequence);
        }
        else // default PreOrder
        {
            PreOrder(tree, tree, visited, sequence);
        }

        foreach (var v in visited)
        {
            v.State = TreeNodeState.Visited;
            v.IsActive = false;
        }

        var finalStep = new VisualizerStep(sequence.TotalSteps, $"Traversal complete: {visited.Count} nodes visited", VisualizerKind.Tree)
        {
            Snapshot = tree.Clone()
        };
        foreach (var v in visited) finalStep.ActiveNodeIds.Add(v.Id);
        sequence.AddStep(finalStep);

        return sequence;
    }

    private static void PreOrder(TreeNodeData tree, TreeNodeData? node, List<TreeNodeData> visited, VisualizerSequence seq)
    {
        if (node == null) return;
        visited.Add(node);
        AddStep(seq, tree, node, visited, "PreOrder Visit");
        if (node.Left != null) PreOrder(tree, node.Left, visited, seq);
        if (node.Right != null) PreOrder(tree, node.Right, visited, seq);
    }

    private static void InOrder(TreeNodeData tree, TreeNodeData? node, List<TreeNodeData> visited, VisualizerSequence seq)
    {
        if (node == null) return;
        if (node.Left != null) InOrder(tree, node.Left, visited, seq);
        visited.Add(node);
        AddStep(seq, tree, node, visited, "InOrder Visit");
        if (node.Right != null) InOrder(tree, node.Right, visited, seq);
    }

    private static void PostOrder(TreeNodeData tree, TreeNodeData? node, List<TreeNodeData> visited, VisualizerSequence seq)
    {
        if (node == null) return;
        if (node.Left != null) PostOrder(tree, node.Left, visited, seq);
        if (node.Right != null) PostOrder(tree, node.Right, visited, seq);
        visited.Add(node);
        AddStep(seq, tree, node, visited, "PostOrder Visit");
    }

    private static void LevelOrder(TreeNodeData tree, List<TreeNodeData> visited, VisualizerSequence seq)
    {
        var queue = new Queue<TreeNodeData>();
        queue.Enqueue(tree);

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            visited.Add(node);
            AddStep(seq, tree, node, visited, "BFS / LevelOrder Visit");

            if (node.Left != null) queue.Enqueue(node.Left);
            if (node.Right != null) queue.Enqueue(node.Right);
            foreach (var child in node.Children)
            {
                if (child != node.Left && child != node.Right) queue.Enqueue(child);
            }
        }
    }

    private static void AddStep(VisualizerSequence seq, TreeNodeData tree, TreeNodeData node, List<TreeNodeData> visited, string action)
    {
        if (visited.Count > 1)
        {
            var previous = visited[^2];
            previous.State = TreeNodeState.Visited;
            previous.IsActive = false;
        }

        node.State = TreeNodeState.Current;
        node.IsActive = true;
        node.IsVisited = true;

        var step = new VisualizerStep(
            seq.TotalSteps,
            $"{action}: Node [{node.DisplayValue}] (Depth {node.Depth}) • Visited: {visited.Count}",
            VisualizerKind.Tree)
        {
            Snapshot = tree.Clone()
        };

        step.ActiveNodeIds.Add(node.Id);
        step.AuxiliaryInfo["Current Node"] = node.DisplayValue;
        step.AuxiliaryInfo["Depth"] = node.Depth.ToString();
        step.AuxiliaryInfo["Visited Count"] = visited.Count.ToString();
        seq.AddStep(step);
    }
}
