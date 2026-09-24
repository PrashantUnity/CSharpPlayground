using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PdfEditorApp.Plugins.CSharpEditor.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

public enum DataStructureShape
{
    None,
    Tree,
    LinkedList,
    Grid
}

/// <summary>Recognizes values worth drawing instead of dumping: tree and list nodes, and rectangular 2D grids.</summary>
public static class DataStructureDetector
{
    private const int MaxGridCells = 2500;
    private const int MaxTreeNodes = 1023;
    private const BindingFlags MemberFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

    public static DataStructureShape Detect(object? value)
    {
        if (value == null) return DataStructureShape.None;
        if (IsGrid(value)) return DataStructureShape.Grid;

        var type = value.GetType();
        if (ObjectInspectorBuilder.IsScalarType(type) || value is IEnumerable) return DataStructureShape.None;

        if (IsSelfTyped(MemberType(type, "Left"), type) && IsSelfTyped(MemberType(type, "Right"), type))
        {
            return IsSmallAcyclicTree(value, BinaryChildren) ? DataStructureShape.Tree : DataStructureShape.None;
        }

        if (IsSelfTyped(ElementType(MemberType(type, "Children")), type))
        {
            return IsSmallAcyclicTree(value, NaryChildren) ? DataStructureShape.Tree : DataStructureShape.None;
        }

        return IsSelfTyped(MemberType(type, "Next"), type) ? DataStructureShape.LinkedList : DataStructureShape.None;
    }

    private static bool IsGrid(object value)
    {
        if (value is Array { Rank: 2 } grid)
        {
            int cells = grid.GetLength(0) * grid.GetLength(1);
            return cells is > 0 and <= MaxGridCells && ObjectInspectorBuilder.IsScalarType(grid.GetType().GetElementType()!);
        }

        // Jagged boards like char[][]; ragged results (e.g. tree levels) are lists, not grids.
        if (value is Array { Rank: 1 } rows &&
            rows.GetType().GetElementType() is { IsArray: true } rowType &&
            rowType.GetArrayRank() == 1 &&
            ObjectInspectorBuilder.IsScalarType(rowType.GetElementType()!))
        {
            int columns = -1;
            foreach (var row in rows)
            {
                if (row is not Array cells || (columns >= 0 && cells.Length != columns)) return false;
                columns = cells.Length;
            }
            return rows.Length >= 2 && columns >= 2 && rows.Length * columns <= MaxGridCells;
        }

        return false;
    }

    /// <summary>True when the named member links to the value's own type, as ListNode.next or TreeNode.left do.</summary>
    internal static bool HasSelfTypedMember(object value, string name) =>
        IsSelfTyped(MemberType(value.GetType(), name), value.GetType());

    // A node type refers to itself (or a base type); `object`-typed members prove nothing.
    private static bool IsSelfTyped(Type? memberType, Type nodeType) =>
        memberType != null && memberType != typeof(object) && memberType.IsAssignableFrom(nodeType);

    private static Type? MemberType(Type type, string name)
    {
        try
        {
            return type.GetProperty(name, MemberFlags)?.PropertyType ?? type.GetField(name, MemberFlags)?.FieldType;
        }
        catch (AmbiguousMatchException)
        {
            return null;
        }
    }

    private static Type? ElementType(Type? collectionType) =>
        collectionType?.GetInterfaces()
            .Prepend(collectionType)
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            ?.GetGenericArguments()[0];

    private static IEnumerable<object> BinaryChildren(object node)
    {
        if (VisualizerReflectionHelper.GetMemberValue(node, "Left", "left") is { } left) yield return left;
        if (VisualizerReflectionHelper.GetMemberValue(node, "Right", "right") is { } right) yield return right;
    }

    private static IEnumerable<object> NaryChildren(object node) =>
        VisualizerReflectionHelper.GetMemberValue(node, "Children", "children") is IEnumerable children
            ? children.Cast<object?>().OfType<object>()
            : Enumerable.Empty<object>();

    // Shared or cyclic links (a child pointing back at an ancestor) mean this is not a tree to draw.
    private static bool IsSmallAcyclicTree(object root, Func<object, IEnumerable<object>> children)
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<object>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (!seen.Add(node) || seen.Count > MaxTreeNodes) return false;
            foreach (var child in children(node)) pending.Push(child);
        }

        return true;
    }
}
