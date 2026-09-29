using System;
using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Spatial;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;

public class Plot3DOptions
{
    public string Title { get; set; } = "3D Visualization";
    public Plot3DType Type { get; set; } = Plot3DType.Scatter;
    public Camera3D Camera { get; set; } = new();
    public List<Series3D> Series { get; set; } = new();
    public Surface3DData? Surface { get; set; }
    public Graph3DData? Graph { get; set; }
    public ColorMapPreset ColorMap { get; set; } = ColorMapPreset.Viridis;
    public string PrimaryColor { get; set; } = "#4ec9b0";

    public double Width { get; set; } = 640;
    public double Height { get; set; } = 360;

    public bool ShowAxes { get; set; } = true;
    public bool ShowFloorGrid { get; set; } = true;
    public bool ShowBoundingBox { get; set; } = true;
    public bool ShowLabels { get; set; } = true;
    public bool Wireframe { get; set; }
    public bool AutoRotate { get; set; }
    public double AutoRotateSpeed { get; set; } = 0.6; // degrees per tick

    public double MinX { get; set; } = -5;
    public double MaxX { get; set; } = 5;
    public double MinY { get; set; } = -5;
    public double MaxY { get; set; } = 5;
    public double MinZ { get; set; } = -5;
    public double MaxZ { get; set; } = 5;

    public void RecalculateBounds()
    {
        if (Surface != null)
        {
            MinX = Surface.MinX; MaxX = Surface.MaxX;
            MinY = Surface.MinZ; MaxY = Surface.MaxZ; // in 3D surface, Z is height (Y in scene)
            MinZ = Surface.MinY; MaxZ = Surface.MaxY;
            EnsureValidBounds();
            return;
        }

        if (Graph != null)
        {
            Graph.RecalculateBounds();
            MinX = Graph.MinX; MaxX = Graph.MaxX;
            MinY = Graph.MinY; MaxY = Graph.MaxY;
            MinZ = Graph.MinZ; MaxZ = Graph.MaxZ;
            EnsureValidBounds();
            return;
        }

        if (Series.Count > 0)
        {
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;
            double minZ = double.MaxValue, maxZ = double.MinValue;

            bool hasPoints = false;
            foreach (var s in Series)
            {
                s.RecalculateBounds();
                if (s.Points.Count == 0) continue;
                hasPoints = true;
                if (s.MinX < minX) minX = s.MinX;
                if (s.MaxX > maxX) maxX = s.MaxX;
                if (s.MinY < minY) minY = s.MinY;
                if (s.MaxY > maxY) maxY = s.MaxY;
                if (s.MinZ < minZ) minZ = s.MinZ;
                if (s.MaxZ > maxZ) maxZ = s.MaxZ;
            }

            if (hasPoints)
            {
                MinX = minX; MaxX = maxX;
                MinY = minY; MaxY = maxY;
                MinZ = minZ; MaxZ = maxZ;
            }
        }

        EnsureValidBounds();
    }

    private void EnsureValidBounds()
    {
        if (Math.Abs(MaxX - MinX) < 1e-6) { MinX -= 1; MaxX += 1; }
        if (Math.Abs(MaxY - MinY) < 1e-6) { MinY -= 1; MaxY += 1; }
        if (Math.Abs(MaxZ - MinZ) < 1e-6) { MinZ -= 1; MaxZ += 1; }
    }
}
