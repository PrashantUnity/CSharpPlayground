using System;
using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Layouts;

public static class TreeLayoutEngine
{
    public static (double Width, double Height) ComputeLayout(
        TreeNodeData? root,
        double nodeRadius = 20.0,
        double horizontalSpacing = 24.0,
        double verticalSpacing = 64.0,
        double padding = 32.0)
    {
        if (root == null) return (100, 100);

        // Step 1: Assign depth
        AssignDepth(root, 0);

        // Step 2: In-order traversal to determine horizontal order for binary trees,
        // or leaf-level spacing for general trees
        double step = nodeRadius * 2 + horizontalSpacing;
        double currentX = padding + nodeRadius;
        AssignInitialX(root, ref currentX, step);

        // Step 3: Compute Y and center parents over children
        CenterParents(root, step);

        // Step 4: Calculate bounding box
        double minX = double.MaxValue, maxX = double.MinValue;
        double minY = double.MaxValue, maxY = double.MinValue;

        Traverse(root, node =>
        {
            node.Y = padding + nodeRadius + node.Depth * verticalSpacing;
            minX = Math.Min(minX, node.X - nodeRadius);
            maxX = Math.Max(maxX, node.X + nodeRadius);
            minY = Math.Min(minY, node.Y - nodeRadius);
            maxY = Math.Max(maxY, node.Y + nodeRadius);
        });

        // Normalize if shifted
        if (minX < padding)
        {
            double shift = padding - minX;
            Traverse(root, n => n.X += shift);
            maxX += shift;
        }

        double width = Math.Max(200, maxX + padding);
        double height = Math.Max(160, maxY + padding);
        return (width, height);
    }

    private static void AssignDepth(TreeNodeData node, int depth)
    {
        node.Depth = depth;
        foreach (var child in node.Children)
        {
            AssignDepth(child, depth + 1);
        }
    }

    private static void AssignInitialX(TreeNodeData node, ref double currentX, double step)
    {
        if (node.Left != null)
        {
            AssignInitialX(node.Left, ref currentX, step);
        }

        if (node.Children.Count > 0 && node.Left == null && node.Right == null)
        {
            // N-ary tree
            for (int i = 0; i < node.Children.Count; i++)
            {
                if (i == node.Children.Count / 2)
                {
                    node.X = currentX;
                    currentX += step;
                }
                AssignInitialX(node.Children[i], ref currentX, step);
            }
            if (node.X == 0)
            {
                node.X = currentX;
                currentX += step;
            }
            return;
        }

        node.X = currentX;
        currentX += step;

        if (node.Right != null)
        {
            AssignInitialX(node.Right, ref currentX, step);
        }
    }

    private static void CenterParents(TreeNodeData node, double step)
    {
        foreach (var child in node.Children)
        {
            CenterParents(child, step);
        }

        if (node.Left != null && node.Right != null)
        {
            node.X = (node.Left.X + node.Right.X) / 2.0;
        }
        else if (node.Left != null && node.Right == null)
        {
            // Left child must remain strictly to the left of parent
            if (node.X <= node.Left.X)
            {
                node.X = node.Left.X + step / 2.0;
            }
        }
        else if (node.Left == null && node.Right != null)
        {
            // Right child must remain strictly to the right of parent
            if (node.X >= node.Right.X)
            {
                node.X = node.Right.X - step / 2.0;
            }
        }
        else if (node.Children.Count > 1)
        {
            double firstChildX = node.Children[0].X;
            double lastChildX = node.Children[^1].X;
            node.X = (firstChildX + lastChildX) / 2.0;
        }
        else if (node.Children.Count == 1)
        {
            node.X = node.Children[0].X;
        }
    }

    private static void Traverse(TreeNodeData node, Action<TreeNodeData> action)
    {
        action(node);
        foreach (var child in node.Children)
        {
            Traverse(child, action);
        }
    }
}
