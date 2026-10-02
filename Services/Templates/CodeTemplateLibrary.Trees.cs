using System.Collections.Generic;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services;

public static partial class CodeTemplateLibrary
{
    private static IEnumerable<CodeTemplate> GetTreeTemplates() => new List<CodeTemplate>
    {
        new()
        {
            Id = "leetcode_226_invert_binary_tree",
            Title = "226. Invert Binary Tree (TreeTracker DFS)",
            Category = "Algorithms",
            Kind = WorkspaceItemKind.Notebook,
            Description = "Step-by-step subtree swapping on a binary tree with pointer badges and time-travel animation.",
            IconKind = MaterialIconKind.SwapHorizontal,
            AccentColor = "#06b6d4",
            AccentBackground = "#082f49",
            AccentBorder = "#0284c7",
            CategoryBadge = "LeetCode 226 • Easy",
            Tags = new List<string> { "Trees", "Recursion", "Invert", "DFS", "TreeTracker" },
            Notes = @"# 226. Invert Binary Tree

Given the root of a binary tree, invert the tree, and return its root.
- Uses `TreeTracker` to record DFS recursion and subtree swaps.
- Drag the scrubber to observe left and right subtrees swapping in real time.",
            InitialCode = @"// Create visualizer from LeetCode serialized tree string
var tracker = TreeTracker.Create(""[4, 2, 7, 1, 3, 6, 9]"", ""226. Invert Binary Tree"");

void InvertTree(TreeNodeData? node)
{
    if (node == null) return;

    tracker.Visit(node, $""Visiting Node [{node.DisplayValue}] to invert its subtrees"", pointer: ""curr"");

    // Swap left and right subtrees in-place
    tracker.SwapChildren(node, $""Swapped children of Node [{node.DisplayValue}]"");

    InvertTree(node.Left);
    InvertTree(node.Right);

    tracker.Highlight(node, TreeNodeState.Matched, $""Subtree at [{node.DisplayValue}] inverted"");
}

InvertTree(tracker.Root);
tracker.ClearPointers();
tracker.Snapshot(""Tree Inversion Complete!"");

Display.Visualizer(tracker);
Console.WriteLine(""Invert Binary Tree complete! Use the scrubber to watch subtrees swap."");"
        },
        new()
        {
            Id = "leetcode_98_validate_bst",
            Title = "98. Validate Binary Search Tree (Range Bounds)",
            Category = "Algorithms",
            Kind = WorkspaceItemKind.Notebook,
            Description = "Tracks valid min/max BST value ranges (min, max) down recursive DFS branches, catching BST violations.",
            IconKind = MaterialIconKind.CheckboxMarkedCircleOutline,
            AccentColor = "#10b981",
            AccentBackground = "#064e3b",
            AccentBorder = "#059669",
            CategoryBadge = "LeetCode 98 • Medium",
            Tags = new List<string> { "Trees", "BST", "Validation", "DFS", "RangeBounds" },
            Notes = @"# 98. Validate Binary Search Tree

Given the root of a binary tree, determine if it is a valid binary search tree (BST).
- Shows dynamic range (min, max) annotations beneath each node.
- Demonstrates invalid node detection with red target highlighting.",
            InitialCode = @"// Tree with BST violation: node 3 in right subtree is < root (5)
var tracker = TreeTracker.Create(""[5, 1, 4, null, null, 3, 6]"", ""98. Validate BST"");

bool Validate(TreeNodeData? node, long min, long max)
{
    if (node == null) return true;

    long val = long.Parse(node.DisplayValue);
    string rangeStr = $""({(min == long.MinValue ? ""-inf"" : min.ToString())}, {(max == long.MaxValue ? ""+inf"" : max.ToString())})"";

    tracker.Visit(node, $""Inspecting [{node.DisplayValue}] • Valid Range: {rangeStr}"", subLabel: rangeStr, pointer: ""curr"");

    if (val <= min || val >= max)
    {
        tracker.Highlight(node, TreeNodeState.Target, $""Violation! Node [{node.DisplayValue}] violates range {rangeStr}"");
        return false;
    }

    tracker.Highlight(node, TreeNodeState.Matched, $""Node [{node.DisplayValue}] satisfies range {rangeStr}"");

    bool leftValid = Validate(node.Left, min, val);
    if (!leftValid) return false;

    bool rightValid = Validate(node.Right, val, max);
    return rightValid;
}

bool isValid = Validate(tracker.Root, long.MinValue, long.MaxValue);
tracker.ClearPointers();
tracker.Snapshot(isValid ? ""Valid BST!"" : ""Invalid BST detected!"");

Display.Visualizer(tracker);
Console.WriteLine($""Result: Is Valid BST = {isValid}"");"
        },
        new()
        {
            Id = "leetcode_236_lowest_common_ancestor",
            Title = "236. Lowest Common Ancestor (LCA)",
            Category = "Algorithms",
            Kind = WorkspaceItemKind.Notebook,
            Description = "Traces search paths for target nodes p and q, highlighting the LCA intersection node.",
            IconKind = MaterialIconKind.RayEndArrow,
            AccentColor = "#f59e0b",
            AccentBackground = "#451a03",
            AccentBorder = "#b45309",
            CategoryBadge = "LeetCode 236 • Medium",
            Tags = new List<string> { "Trees", "LCA", "Recursion", "Ancestry", "TreeTracker" },
            Notes = @"# 236. Lowest Common Ancestor of a Binary Tree

Given a binary tree, find the lowest common ancestor (LCA) of two given nodes `p` and `q`.
- Highlights `p` and `q` with distinct target colors.
- Backtracks through subtrees to identify the common ancestor node.",
            InitialCode = @"var tracker = TreeTracker.Create(""[3, 5, 1, 6, 2, 0, 8, null, null, 7, 4]"", ""236. Lowest Common Ancestor"");

// Target values
string pVal = ""5"";
string qVal = ""1"";

var pNode = tracker.ResolveNode(pVal);
var qNode = tracker.ResolveNode(qVal);
if (pNode != null) tracker.SetPointer(pNode, ""p"");
if (qNode != null) tracker.SetPointer(qNode, ""q"");
tracker.Snapshot($""Targets: p = {pVal}, q = {qVal}"");

TreeNodeData? FindLCA(TreeNodeData? node)
{
    if (node == null) return null;

    tracker.Visit(node, $""Checking Node [{node.DisplayValue}]"");

    if (node.DisplayValue == pVal || node.DisplayValue == qVal)
    {
        tracker.Highlight(node, TreeNodeState.Matched, $""Found target node [{node.DisplayValue}]"");
        return node;
    }

    var left = FindLCA(node.Left);
    var right = FindLCA(node.Right);

    if (left != null && right != null)
    {
        tracker.Highlight(node, TreeNodeState.Target, $""LCA Identified: Node [{node.DisplayValue}] branches to both targets!"");
        tracker.SetPointer(node, ""LCA"");
        return node;
    }

    return left ?? right;
}

var lca = FindLCA(tracker.Root);
tracker.Snapshot($""Search Complete! Lowest Common Ancestor is [{lca?.DisplayValue}]."");

Display.Visualizer(tracker);
Console.WriteLine($""Lowest Common Ancestor: {lca?.DisplayValue}"");"
        }
    };
}
