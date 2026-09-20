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
}
