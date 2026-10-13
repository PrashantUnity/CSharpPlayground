using System;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;

public class Surface3DData
{
    public double[,] ZValues { get; set; } = new double[0, 0];
    public double MinX { get; set; } = -5;
    public double MaxX { get; set; } = 5;
    public double MinY { get; set; } = -5;
    public double MaxY { get; set; } = 5;
    public double MinZ { get; set; }
    public double MaxZ { get; set; }
    public int ResolutionX => ZValues.GetLength(0);
    public int ResolutionY => ZValues.GetLength(1);

    public static Surface3DData FromFunction(
        Func<double, double, double> func,
        double minX = -5, double maxX = 5,
        double minY = -5, double maxY = 5,
        int resX = 30, int resY = 30)
    {
        resX = Math.Max(2, resX);
        resY = Math.Max(2, resY);

        var data = new Surface3DData
        {
            MinX = minX,
            MaxX = maxX,
            MinY = minY,
            MaxY = maxY,
            ZValues = new double[resX, resY]
        };

        double minZ = double.MaxValue;
        double maxZ = double.MinValue;

        double stepX = (maxX - minX) / (resX - 1);
        double stepY = (maxY - minY) / (resY - 1);

        for (int i = 0; i < resX; i++)
        {
            double x = minX + i * stepX;
            for (int j = 0; j < resY; j++)
            {
                double y = minY + j * stepY;
                double z = func(x, y);

                if (double.IsNaN(z) || double.IsInfinity(z)) z = 0;

                data.ZValues[i, j] = z;
                if (z < minZ) minZ = z;
                if (z > maxZ) maxZ = z;
            }
        }

        data.MinZ = minZ;
        data.MaxZ = maxZ;
        return data;
    }

    public static Surface3DData FromGrid(
        double[,] grid,
        double minX = 0, double maxX = 10,
        double minY = 0, double maxY = 10)
    {
        int rows = grid.GetLength(0);
        int cols = grid.GetLength(1);

        var data = new Surface3DData
        {
            MinX = minX,
            MaxX = maxX,
            MinY = minY,
            MaxY = maxY,
            ZValues = grid
        };

        double minZ = double.MaxValue;
        double maxZ = double.MinValue;

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                double z = grid[i, j];
                if (double.IsNaN(z) || double.IsInfinity(z)) z = 0;
                if (z < minZ) minZ = z;
                if (z > maxZ) maxZ = z;
            }
        }

        data.MinZ = minZ;
        data.MaxZ = maxZ;
        return data;
    }
}
