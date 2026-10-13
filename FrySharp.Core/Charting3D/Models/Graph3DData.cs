using System;
using System.Collections.Generic;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;

public class Graph3DNode
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public string? Color { get; set; }
    public double Radius { get; set; } = 8.0;
    public object? Tag { get; set; }

    public Graph3DNode() { }

    public Graph3DNode(string id, string? label = null, double x = 0, double y = 0, double z = 0, string? color = null)
    {
        Id = id;
        Label = label ?? id;
        X = x;
        Y = y;
        Z = z;
        Color = color;
    }
}

public class Graph3DEdge
{
    public string FromId { get; set; } = string.Empty;
    public string ToId { get; set; } = string.Empty;
    public double? Weight { get; set; }
    public string? Color { get; set; }
    public bool IsDirected { get; set; }

    public Graph3DEdge() { }

    public Graph3DEdge(string fromId, string toId, double? weight = null, string? color = null, bool isDirected = false)
    {
        FromId = fromId;
        ToId = toId;
        Weight = weight;
        Color = color;
        IsDirected = isDirected;
    }
}

public class Graph3DData
{
    public List<Graph3DNode> Nodes { get; set; } = new();
    public List<Graph3DEdge> Edges { get; set; } = new();

    public double MinX { get; private set; }
    public double MaxX { get; private set; }
    public double MinY { get; private set; }
    public double MaxY { get; private set; }
    public double MinZ { get; private set; }
    public double MaxZ { get; private set; }

    public Graph3DNode AddNode(string id, string? label = null, double x = 0, double y = 0, double z = 0, string? color = null, double radius = 8.0)
    {
        var node = new Graph3DNode(id, label, x, y, z, color) { Radius = radius };
        Nodes.Add(node);
        return node;
    }

    public Graph3DEdge AddEdge(string fromId, string toId, double? weight = null, string? color = null, bool isDirected = false)
    {
        var edge = new Graph3DEdge(fromId, toId, weight, color, isDirected);
        Edges.Add(edge);
        return edge;
    }

    public void RecalculateBounds()
    {
        if (Nodes.Count == 0)
        {
            MinX = MaxX = MinY = MaxY = MinZ = MaxZ = 0;
            return;
        }

        double minX = double.MaxValue, maxX = double.MinValue;
        double minY = double.MaxValue, maxY = double.MinValue;
        double minZ = double.MaxValue, maxZ = double.MinValue;

        foreach (var n in Nodes)
        {
            if (n.X < minX) minX = n.X;
            if (n.X > maxX) maxX = n.X;
            if (n.Y < minY) minY = n.Y;
            if (n.Y > maxY) maxY = n.Y;
            if (n.Z < minZ) minZ = n.Z;
            if (n.Z > maxZ) maxZ = n.Z;
        }

        MinX = minX; MaxX = maxX;
        MinY = minY; MaxY = maxY;
        MinZ = minZ; MaxZ = maxZ;
    }
}
