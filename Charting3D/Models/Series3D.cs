using System;
using System.Collections.Generic;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;

public class Series3D
{
    public string Name { get; set; } = "Series 1";
    public string? Color { get; set; }
    public List<Point3D> Points { get; set; } = new();

    public double MinX { get; private set; }
    public double MaxX { get; private set; }
    public double MinY { get; private set; }
    public double MaxY { get; private set; }
    public double MinZ { get; private set; }
    public double MaxZ { get; private set; }

    public void RecalculateBounds()
    {
        if (Points.Count == 0)
        {
            MinX = MaxX = MinY = MaxY = MinZ = MaxZ = 0;
            return;
        }

        double minX = double.MaxValue, maxX = double.MinValue;
        double minY = double.MaxValue, maxY = double.MinValue;
        double minZ = double.MaxValue, maxZ = double.MinValue;

        foreach (var pt in Points)
        {
            if (pt.X < minX) minX = pt.X;
            if (pt.X > maxX) maxX = pt.X;
            if (pt.Y < minY) minY = pt.Y;
            if (pt.Y > maxY) maxY = pt.Y;
            if (pt.Z < minZ) minZ = pt.Z;
            if (pt.Z > maxZ) maxZ = pt.Z;
        }

        MinX = minX; MaxX = maxX;
        MinY = minY; MaxY = maxY;
        MinZ = minZ; MaxZ = maxZ;
    }
}
