using System;
using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Spatial;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting3D.Layouts;

public static class ForceDirected3DLayout
{
    public static void ComputeLayout(Graph3DData graph, int iterations = 60, double spaceRadius = 15.0)
    {
        if (graph.Nodes.Count <= 1) return;

        var nodes = graph.Nodes;
        int n = nodes.Count;

        // Initialize positions randomly on a sphere if all are at 0
        var rng = new Random(42);
        bool allZero = true;
        foreach (var node in nodes)
        {
            if (Math.Abs(node.X) > 1e-4 || Math.Abs(node.Y) > 1e-4 || Math.Abs(node.Z) > 1e-4)
            {
                allZero = false;
                break;
            }
        }

        if (allZero)
        {
            for (int i = 0; i < n; i++)
            {
                double theta = rng.NextDouble() * 2 * Math.PI;
                double phi = Math.Acos(2 * rng.NextDouble() - 1);
                double r = spaceRadius * (0.3 + 0.7 * rng.NextDouble());

                nodes[i].X = r * Math.Sin(phi) * Math.Cos(theta);
                nodes[i].Y = r * Math.Sin(phi) * Math.Sin(theta);
                nodes[i].Z = r * Math.Cos(phi);
            }
        }

        // Map for fast index lookup
        var idToIndex = new Dictionary<string, int>();
        for (int i = 0; i < n; i++) idToIndex[nodes[i].Id] = i;

        double optimalDist = spaceRadius * Math.Pow(1.0 / Math.Max(1, n), 1.0 / 3.0) * 1.5;
        double kRepulsion = optimalDist * optimalDist;
        double temp = spaceRadius * 0.1;
        double cooling = temp / (iterations + 1);

        var disp = new Vector3D[n];

        for (int iter = 0; iter < iterations; iter++)
        {
            Array.Clear(disp, 0, n);

            // 1. Repulsive forces between all pairs
            for (int i = 0; i < n; i++)
            {
                var pi = new Vector3D(nodes[i].X, nodes[i].Y, nodes[i].Z);
                for (int j = i + 1; j < n; j++)
                {
                    var pj = new Vector3D(nodes[j].X, nodes[j].Y, nodes[j].Z);
                    var delta = pi - pj;
                    double dist = delta.Length;
                    if (dist < 1e-3)
                    {
                        delta = new Vector3D(rng.NextDouble() - 0.5, rng.NextDouble() - 0.5, rng.NextDouble() - 0.5).Normalized;
                        dist = 1e-3;
                    }

                    double force = kRepulsion / (dist * dist);
                    var fVec = delta.Normalized * force;
                    disp[i] += fVec;
                    disp[j] -= fVec;
                }
            }

            // 2. Attractive forces along edges
            foreach (var edge in graph.Edges)
            {
                if (!idToIndex.TryGetValue(edge.FromId, out int i) ||
                    !idToIndex.TryGetValue(edge.ToId, out int j))
                    continue;

                var pi = new Vector3D(nodes[i].X, nodes[i].Y, nodes[i].Z);
                var pj = new Vector3D(nodes[j].X, nodes[j].Y, nodes[j].Z);
                var delta = pi - pj;
                double dist = delta.Length;
                if (dist < 1e-3) continue;

                double force = (dist * dist) / Math.Max(0.1, optimalDist);
                if (edge.Weight.HasValue && edge.Weight.Value > 0)
                {
                    force *= Math.Min(3.0, Math.Sqrt(edge.Weight.Value));
                }

                var fVec = delta.Normalized * force;
                disp[i] -= fVec;
                disp[j] += fVec;
            }

            // 3. Weak centering gravity towards (0, 0, 0)
            for (int i = 0; i < n; i++)
            {
                var pos = new Vector3D(nodes[i].X, nodes[i].Y, nodes[i].Z);
                disp[i] -= pos * 0.05;
            }

            // 4. Displace with temperature limit
            for (int i = 0; i < n; i++)
            {
                double dLen = disp[i].Length;
                if (dLen > 1e-6)
                {
                    var step = disp[i].Normalized * Math.Min(dLen, temp);
                    nodes[i].X += step.X;
                    nodes[i].Y += step.Y;
                    nodes[i].Z += step.Z;
                }
            }

            temp = Math.Max(0.01, temp - cooling);
        }

        graph.RecalculateBounds();
    }
}
