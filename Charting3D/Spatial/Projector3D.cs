using System;
using Avalonia;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting3D.Spatial;

public readonly struct ProjectedPoint3D
{
    public Point ScreenPoint { get; }
    public double Depth { get; }
    public bool IsVisible { get; }

    public ProjectedPoint3D(Point screenPoint, double depth, bool isVisible)
    {
        ScreenPoint = screenPoint;
        Depth = depth;
        IsVisible = isVisible;
    }
}

public class Projector3D
{
    public const double WorldBoxSize = 8.0; // [-4, +4]

    public Matrix4x4D ViewMatrix { get; }
    public Matrix4x4D ProjectionMatrix { get; }
    public Matrix4x4D ViewProjectionMatrix { get; }
    public Rect Viewport { get; }
    public Plot3DOptions Options { get; }

    public Projector3D(Rect viewport, Plot3DOptions options)
    {
        Viewport = viewport;
        Options = options;

        double aspect = viewport.Width / System.Math.Max(1.0, viewport.Height);
        ViewMatrix = options.Camera.GetViewMatrix();
        ProjectionMatrix = options.Camera.GetProjectionMatrix(aspect);
        ViewProjectionMatrix = ViewMatrix * ProjectionMatrix;
    }

    /// <summary>
    /// Where a data point is in the scene. Data z is up, as in matplotlib and Plotly, for every kind of plot: the scene
    /// is Y-up (its camera and floor are), so data (x, y, z) is placed at scene (x, z, -y), which keeps the data
    /// right-handed (x across the front, y going back, z up from the default camera).
    /// </summary>
    public Vector3D MapDataToWorld(double x, double y, double z)
    {
        double rangeX = System.Math.Max(1e-9, Options.MaxX - Options.MinX);
        double rangeY = System.Math.Max(1e-9, Options.MaxY - Options.MinY);
        double rangeZ = System.Math.Max(1e-9, Options.MaxZ - Options.MinZ);

        double normX = ((x - Options.MinX) / rangeX - 0.5) * WorldBoxSize;
        double normY = ((y - Options.MinY) / rangeY - 0.5) * WorldBoxSize;
        double normZ = ((z - Options.MinZ) / rangeZ - 0.5) * WorldBoxSize;

        return new Vector3D(normX, normZ, -normY);
    }

    public ProjectedPoint3D ProjectWorld(in Vector3D worldPos)
    {
        var clip = ViewProjectionMatrix.Transform(worldPos);

        // Check if behind camera
        bool isVisible = clip.Z > -1.0 && clip.Z < 1.0;

        double sx = Viewport.X + (clip.X + 1.0) * 0.5 * Viewport.Width;
        double sy = Viewport.Y + (1.0 - clip.Y) * 0.5 * Viewport.Height;

        return new ProjectedPoint3D(new Point(sx, sy), clip.Z, isVisible);
    }

    public ProjectedPoint3D ProjectData(double x, double y, double z)
    {
        var world = MapDataToWorld(x, y, z);
        return ProjectWorld(world);
    }

    public Ray3D CreateRay(Point screenPos)
    {
        var eye = Options.Camera.GetEyePosition();

        double aspect = Viewport.Width / System.Math.Max(1.0, Viewport.Height);
        double fovRad = Options.Camera.FieldOfView * System.Math.PI / 180.0;
        double tanFov = System.Math.Tan(fovRad * 0.5);

        // Normalized Device Coordinates (-1 to 1)
        double ndcX = (screenPos.X - Viewport.X) / Viewport.Width * 2.0 - 1.0;
        double ndcY = 1.0 - (screenPos.Y - Viewport.Y) / Viewport.Height * 2.0;

        var forward = (Options.Camera.Target - eye).Normalized;
        var right = Vector3D.Cross(Vector3D.UnitY, forward).Normalized;
        var up = Vector3D.Cross(forward, right);

        Vector3D rayDir;
        if (Options.Camera.IsOrthographic)
        {
            double halfH = Options.Camera.OrthographicSize * 0.5;
            double halfW = halfH * aspect;
            var origin = eye + right * (ndcX * halfW) + up * (ndcY * halfH);
            return new Ray3D(origin, forward);
        }
        else
        {
            rayDir = (forward + right * (ndcX * tanFov * aspect) + up * (ndcY * tanFov)).Normalized;
            return new Ray3D(eye, rayDir);
        }
    }
}
