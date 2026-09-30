using System;
using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Layouts;

public static class GraphLayoutEngine
{
    public static (double Width, double Height) ComputeLayout(
        GraphData graph,
        double canvasWidth = 500,
        double canvasHeight = 350,
        double padding = 40.0)
    {
        if (graph.Nodes.Count == 0) return (canvasWidth, canvasHeight);
        if (graph.Nodes.All(n => n.PinnedX.HasValue && n.PinnedY.HasValue))
        {
            FitPinned(graph, canvasWidth, canvasHeight, padding);
            return (canvasWidth, canvasHeight);
        }

        int count = graph.Nodes.Count;

        if (count == 1)
        {
            graph.Nodes[0].X = canvasWidth / 2.0;
            graph.Nodes[0].Y = canvasHeight / 2.0;
            return (canvasWidth, canvasHeight);
        }

        // Circular layout with radius fitting canvas
        double radiusX = (canvasWidth - padding * 2) / 2.0;
        double radiusY = (canvasHeight - padding * 2) / 2.0;
        double centerX = canvasWidth / 2.0;
        double centerY = canvasHeight / 2.0;

        double angleStep = 2.0 * Math.PI / count;

        for (int i = 0; i < count; i++)
        {
            double angle = -Math.PI / 2.0 + i * angleStep;
            graph.Nodes[i].X = centerX + radiusX * Math.Cos(angle);
            graph.Nodes[i].Y = centerY + radiusY * Math.Sin(angle);
        }

        return (canvasWidth, canvasHeight);
    }

    // Positions the program gave, in any units: scaled evenly into the canvas and centred, so the drawing keeps its shape.
    private static void FitPinned(GraphData graph, double canvasWidth, double canvasHeight, double padding)
    {
        double minX = graph.Nodes.Min(n => n.PinnedX!.Value), maxX = graph.Nodes.Max(n => n.PinnedX!.Value);
        double minY = graph.Nodes.Min(n => n.PinnedY!.Value), maxY = graph.Nodes.Max(n => n.PinnedY!.Value);
        double spanX = Math.Max(1e-9, maxX - minX), spanY = Math.Max(1e-9, maxY - minY);
        double usableW = Math.Max(1, canvasWidth - padding * 2), usableH = Math.Max(1, canvasHeight - padding * 2);
        double scale = Math.Min(usableW / spanX, usableH / spanY);
        if (graph.Nodes.Count == 1 || (maxX - minX < 1e-9 && maxY - minY < 1e-9)) scale = 0;
        double offsetX = (canvasWidth - (maxX - minX) * scale) / 2.0;
        double offsetY = (canvasHeight - (maxY - minY) * scale) / 2.0;
        foreach (var node in graph.Nodes)
        {
            node.X = offsetX + (node.PinnedX!.Value - minX) * scale;
            node.Y = offsetY + (node.PinnedY!.Value - minY) * scale;
        }
    }
}
