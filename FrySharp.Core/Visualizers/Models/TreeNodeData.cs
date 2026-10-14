using System.Collections.Generic;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

public enum TreeNodeState
{
    Default,
    Current,
    Visited,
    Target,
    Path,
    Matched,
    Candidate,
    Backtracked,
    Pruned,
    Swapped
}

public class TreeNodeData
{
    public string Id { get; set; } = string.Empty;
    public string DisplayValue { get; set; } = string.Empty;
    public object? RawValue { get; set; }
    public TreeNodeData? Left { get; set; }
    public TreeNodeData? Right { get; set; }
    public List<TreeNodeData> Children { get; set; } = new();
    public TreeNodeData? Parent { get; set; }

    public double X { get; set; }
    public double Y { get; set; }
    public int Depth { get; set; }
    public bool IsActive { get; set; }
    public bool IsVisited { get; set; }
    public TreeNodeState State { get; set; } = TreeNodeState.Default;
    public string? PointerLabel { get; set; }
    public string? SubLabel { get; set; }
    public string? CustomColor { get; set; }
    public Dictionary<string, object?> Metadata { get; set; } = new();

    public TreeNodeData() { }

    public TreeNodeData(string displayValue, string? id = null)
    {
        DisplayValue = displayValue;
        Id = id ?? displayValue;
    }

    public TreeNodeData Clone()
    {
        var copy = new TreeNodeData(DisplayValue, Id)
        {
            RawValue = RawValue,
            X = X,
            Y = Y,
            Depth = Depth,
            IsActive = IsActive,
            IsVisited = IsVisited,
            State = State,
            PointerLabel = PointerLabel,
            SubLabel = SubLabel,
            CustomColor = CustomColor,
            Metadata = new Dictionary<string, object?>(Metadata)
        };

        if (Left != null)
        {
            copy.Left = Left.Clone();
            copy.Left.Parent = copy;
            copy.Children.Add(copy.Left);
        }

        if (Right != null)
        {
            copy.Right = Right.Clone();
            copy.Right.Parent = copy;
            copy.Children.Add(copy.Right);
        }

        foreach (var child in Children)
        {
            if (child != Left && child != Right)
            {
                var childCopy = child.Clone();
                childCopy.Parent = copy;
                copy.Children.Add(childCopy);
            }
        }

        return copy;
    }

    public void SwapChildren()
    {
        var temp = Left;
        Left = Right;
        Right = temp;

        Children.Clear();
        if (Left != null) Children.Add(Left);
        if (Right != null) Children.Add(Right);
    }

    public TreeNodeData? FindNode(string id)
    {
        if (Id == id) return this;
        foreach (var child in Children)
        {
            var found = child.FindNode(id);
            if (found != null) return found;
        }
        return null;
    }

    public TreeNodeData? FindByValue(string val)
    {
        if (DisplayValue == val) return this;
        foreach (var child in Children)
        {
            var found = child.FindByValue(val);
            if (found != null) return found;
        }
        return null;
    }
}
