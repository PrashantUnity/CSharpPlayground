using System;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting3D.Spatial;

public readonly struct Vector3D : IEquatable<Vector3D>
{
    public double X { get; }
    public double Y { get; }
    public double Z { get; }

    public static readonly Vector3D Zero = new(0, 0, 0);
    public static readonly Vector3D UnitX = new(1, 0, 0);
    public static readonly Vector3D UnitY = new(0, 1, 0);
    public static readonly Vector3D UnitZ = new(0, 0, 1);
    public static readonly Vector3D One = new(1, 1, 1);

    public Vector3D(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public double Length => System.Math.Sqrt(X * X + Y * Y + Z * Z);
    public double LengthSquared => X * X + Y * Y + Z * Z;

    public Vector3D Normalized
    {
        get
        {
            double len = Length;
            return len > 1e-12 ? new Vector3D(X / len, Y / len, Z / len) : Zero;
        }
    }

    public static double Dot(in Vector3D a, in Vector3D b) =>
        a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    public static Vector3D Cross(in Vector3D a, in Vector3D b) =>
        new(
            a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X
        );

    public static double Distance(in Vector3D a, in Vector3D b) =>
        (a - b).Length;

    public static Vector3D Lerp(in Vector3D a, in Vector3D b, double t) =>
        new(
            a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t,
            a.Z + (b.Z - a.Z) * t
        );

    public static Vector3D operator +(in Vector3D a, in Vector3D b) =>
        new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Vector3D operator -(in Vector3D a, in Vector3D b) =>
        new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static Vector3D operator -(in Vector3D a) =>
        new(-a.X, -a.Y, -a.Z);

    public static Vector3D operator *(in Vector3D a, double scalar) =>
        new(a.X * scalar, a.Y * scalar, a.Z * scalar);

    public static Vector3D operator *(double scalar, in Vector3D a) =>
        new(a.X * scalar, a.Y * scalar, a.Z * scalar);

    public static Vector3D operator /(in Vector3D a, double scalar) =>
        new(a.X / scalar, a.Y / scalar, a.Z / scalar);

    public static bool operator ==(in Vector3D a, in Vector3D b) =>
        System.Math.Abs(a.X - b.X) < 1e-9 &&
        System.Math.Abs(a.Y - b.Y) < 1e-9 &&
        System.Math.Abs(a.Z - b.Z) < 1e-9;

    public static bool operator !=(in Vector3D a, in Vector3D b) => !(a == b);

    public bool Equals(Vector3D other) => this == other;

    public override bool Equals(object? obj) => obj is Vector3D other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    public override string ToString() => $"({X:0.##}, {Y:0.##}, {Z:0.##})";
}
