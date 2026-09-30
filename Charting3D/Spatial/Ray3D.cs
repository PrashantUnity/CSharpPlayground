using System;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting3D.Spatial;

public readonly struct Ray3D
{
    public Vector3D Origin { get; }
    public Vector3D Direction { get; }

    public Ray3D(in Vector3D origin, in Vector3D direction)
    {
        Origin = origin;
        Direction = direction.Normalized;
    }

    public Vector3D PointAt(double t) => Origin + Direction * t;

    public double DistanceToPoint(in Vector3D point)
    {
        var v = point - Origin;
        double t = Vector3D.Dot(v, Direction);
        if (t < 0)
        {
            return v.Length;
        }
        var proj = PointAt(t);
        return (point - proj).Length;
    }
}
